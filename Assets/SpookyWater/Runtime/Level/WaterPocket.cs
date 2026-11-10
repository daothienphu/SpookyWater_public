using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpookyWater
{
    [Serializable]
    public struct WaterPocketPoint
    {
        [Tooltip("Anchor position, relative to the WaterPocket's position, in world units (X right, Y up).")]
        public Vector2 position;
        [Tooltip("Control handle for the curve arriving at this anchor, relative to the anchor. Zero makes a sharp corner.")]
        public Vector2 inTangent;
        [Tooltip("Control handle for the curve leaving this anchor, relative to the anchor. Zero makes a sharp corner.")]
        public Vector2 outTangent;

        public WaterPocketPoint(Vector2 position, Vector2 inTangent, Vector2 outTangent)
        {
            this.position = position;
            this.inTangent = inTangent;
            this.outTangent = outTangent;
        }
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("SpookyWater/Water Pocket")]
    [HelpURL(SpookyWaterDocs.Pocket)]
    public class WaterPocket : MonoBehaviour
    {
        private const string GeneratedName = "GeneratedPocket";
        private const string MaskShaderName = "SpookyWaterPocketMask";
        private const int MaskSortingOrder = -32000;
        private const float StencilEqual = 3f;
        private const float DisplacementBoundsPadding = 1f;

        private static readonly int RippleScaleId = Shader.PropertyToID("_RippleScale");
        private static readonly int PocketStencilCompId = Shader.PropertyToID("_PocketStencilComp");
        private static readonly int PocketLinesId = Shader.PropertyToID("_WaterPocketLines");
        private static readonly int PocketLineCountId = Shader.PropertyToID("_WaterPocketLineCount");
        private const int MaxPocketLines = 8;
        private static readonly Vector4[] PocketLines = new Vector4[MaxPocketLines];

        [Group("Shape")]
        [Tooltip("The closed bezier outline of the pocket, in the front (XY) plane at world Z = 0, relative to this " +
                 "object's position. Inside the outline and above the Water Level is air: the front water is cut " +
                 "away there whatever is painted on the tilemap. Inside and below the Water Level is the pocket's own " +
                 "water. Edit the anchors and handles in the Scene view.")]
        [SerializeField] private List<WaterPocketPoint> _points = new()
        {
            new WaterPocketPoint(new Vector2(-3f, -1.5f), new Vector2(0f, 0.8f), new Vector2(0.8f, 0f)),
            new WaterPocketPoint(new Vector2(3f, -1.5f), new Vector2(-0.8f, 0f), new Vector2(0f, 0.8f)),
            new WaterPocketPoint(new Vector2(3f, 1.5f), new Vector2(0f, -0.8f), new Vector2(-0.8f, 0f)),
            new WaterPocketPoint(new Vector2(-3f, 1.5f), new Vector2(0.8f, 0f), new Vector2(0f, -0.8f)),
        };
        [Tooltip("Height of the pocket's water surface, relative to this object's position. It must cross the " +
                 "outline: the water spans the outline's width at this height.")]
        [SerializeField] private float _waterLevel = -0.9f;
        [Tooltip("Straight segments each bezier span is split into. Higher gives smoother curves.")]
        [Range(2, 64)] [SerializeField] private int _curveSteps = 16;

        [Group("Surface")]
        [Tooltip("How far the pocket's water surface reaches back from the front edge, in world units (world Z). A " +
                 "pocket is usually a shallow alcove, so this stays much shorter than the main lake.")]
        [Min(0.05f)] [SerializeField] private float _depth = 1.5f;
        [Tooltip("Material for the pocket's surface strip. Use the SpookyWater/Water Surface shader. Empty uses the " +
                 "level builder's Surface material; no strip is built if that is empty too. The pocket has no planar " +
                 "reflection, so the strip mirrors the material's Reflection Sky Color; a copy of the surface material " +
                 "tuned for the pocket usually looks best.")]
        [SerializeField] private Material _surfaceMaterial;
        [Tooltip("How far the interactive ripples move the pocket's water up and down, set per renderer. " +
                 "Below 1 gives the pocket smaller waves than the open lake.")]
        [Min(0f)] [SerializeField] private float _displacementScale = 1f;
        [Tooltip("Mesh segments of the surface strip: X per world unit across, Z over the whole depth.")]
        [SerializeField] private Vector2Int _surfaceSegments = new(16, 12);
        [Tooltip("How far the surface strip widens on each side by the time it reaches its back edge, in world " +
                 "units, like the horizon extension fanning out. 0 is a plain strip as wide as the waterline, which " +
                 "perspective narrows with distance. Raise it until the strip's sides stay hidden behind the front " +
                 "water around the pocket. The ripple simulation widens with it.")]
        [Min(0f)] [SerializeField] private float _surfaceExpand = 1.5f;

        [Group("Clipping")]
        [Tooltip("Sprites clipped by the pocket's outline, e.g. the pocket's back wall, rocks or plants inside it. " +
                 "The pocket drives their Mask Interaction through a Sprite Mask shaped exactly like the outline, and " +
                 "restores it when the pocket is disabled or the sprite is removed from the list.")]
        [SerializeField] private List<SpriteRenderer> _clippedSprites = new();
        [Tooltip("Visible Inside Mask shows the listed sprites only inside the outline; Visible Outside Mask hides " +
                 "them there.")]
        [SerializeField] private SpriteMaskInteraction _clipMode = SpriteMaskInteraction.VisibleInsideMask;
        [Tooltip("Resolution of the Sprite Mask's backing texture, in pixels per world unit. The mask edge itself " +
                 "is filled from the outline at this resolution, so higher gives a more accurate edge at a larger texture (capped at 2048 pixels a side).")]
        [Range(4, 128)] [SerializeField] private int _clipPixelsPerUnit = 32;

        [Group("Waterline")]
        [Tooltip("Material for the pocket's front water (the animated waterline band and the water under it). Use " +
                 "the SpookyWater/Water Front shader. Empty uses the level builder's Line material. A runtime copy is " +
                 "drawn only inside the outline, so the asset is never modified.")]
        [SerializeField] private Material _lineMaterial;
        [Tooltip("Height of the animated band under the Water Level, in world units. Ripples move its top edge and " +
                 "fade out toward its bottom; the foam line sits at its top. The tile waterline is one cell tall.")]
        [Min(0.05f)] [SerializeField] private float _waterlineBand = 1f;
        [Tooltip("Mesh segments per world unit along the waterline. Only the wavy top edge needs them.")]
        [Min(1)] [SerializeField] private int _waterlineSegmentsPerUnit = 12;
        [Tooltip("Mesh segments up the waterline band, so the ripple falloff renders as a curve.")]
        [Min(1)] [SerializeField] private int _waterlineVerticalSegments = 4;

        [Group("Ripples")]
        [Tooltip("Material that runs the pocket's ripple simulation. Use the Hidden/SpookyWater/Ripple Simulation " +
                 "shader. Empty uses the level builder's.")]
        [SerializeField] private Material _rippleSimulationMaterial;
        [Tooltip("The pocket's own ripple simulation, separate from the main lake's: ripples on one never show on " +
                 "the other. The back of the pocket's surface and its sides are shores ripples bounce off; the " +
                 "front edge absorbs them. Most values can be changed live in Play mode.")]
        [SerializeField] private RippleGridSettings _rippleGrid = new() { maxResolution = 512, damping = 1.8f, wakeRadius = 0.4f };
        [Tooltip("Occasional small random ripples on the pocket's water.")]
        [SerializeField] private AmbientRippleSettings _ambientRipples = new() { interval = new Vector2(1f, 3f) };
        [Tooltip("Tiny, quickly fading ripples just behind the pocket's front edge.")]
        [SerializeField] private FrontRippleSettings _frontRipples = new() { enabled = false };
        [Tooltip("Wind over the pocket's water, independent of the main lake's wind: a sheltered pocket can stay calm " +
                 "while the lake is ruffled, or the other way round.")]
        [SerializeField] private WindSettings _wind = new();

        [Group("Interaction")]
        [Tooltip("Height in world units of the invisible trigger band along the pocket's waterline that ripples and " +
                 "splashes the water when a 2D physics body crosses it.")]
        [Min(0.01f)] [SerializeField] private float _contactBandThickness = 0.1f;
        [Tooltip("Physics layers whose bodies ripple and splash the pocket's water when they cross its waterline.")]
        [SerializeField] private LayerMask _contactLayers = ~0;
        [Tooltip("How strong the contact ripples are and when a falling or rising body counts as having splashed.")]
        [SerializeField] private ContactRippleSettings _contactRipple = new();

        private readonly List<Vector2> _outline = new();
        private readonly List<UnityEngine.Object> _owned = new();
        private readonly Dictionary<SpriteRenderer, SpriteMaskInteraction> _clipOriginals = new();
        private GameObject _generated;
        private bool _started;
        private bool _rebuildQueued;

        public IReadOnlyList<WaterPocketPoint> Points => _points;

        public int CurveSteps => Mathf.Max(_curveSteps, 2);

        public float WaterLevelY => transform.position.y + _waterLevel;

        public float Depth => Mathf.Max(_depth, 0.05f);

        public WindSettings Wind => _wind;

        public RippleGridSettings RippleGrid => _rippleGrid;

        public WaterField Field { get; private set; }

        public bool IsBuilt => Field;

        public float WaterlineMinX { get; private set; }

        public float WaterlineMaxX { get; private set; }

        public IReadOnlyList<Vector2> Outline => _outline;

        private void OnEnable()
        {
            Water.RegisterPocket(this);
            if (!Application.isPlaying || _started) Generate();
        }

        private void Start()
        {
            _started = true;
            if (Application.isPlaying) Generate();
        }

        private void OnDisable()
        {
            Water.UnregisterPocket(this);
            Clear();
            PublishPocketLines();
        }

        private static void PublishPocketLines()
        {
            var count = 0;
            var pockets = Water.Pockets;
            for (var i = 0; i < pockets.Count && count < MaxPocketLines; i++)
            {
                var pocket = pockets[i];
                if (!pocket || !pocket.IsBuilt) continue;
                PocketLines[count++] = new Vector4(pocket.WaterlineMinX, pocket.WaterlineMaxX, pocket.WaterLevelY, 0f);
            }
            Shader.SetGlobalVectorArray(PocketLinesId, PocketLines);
            Shader.SetGlobalFloat(PocketLineCountId, count);
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (_rebuildQueued || !isActiveAndEnabled) return;
            _rebuildQueued = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                _rebuildQueued = false;
                if (this && isActiveAndEnabled && (!Application.isPlaying || _started)) Generate();
            };
#endif
        }

        private void Update()
        {
            if (Application.isPlaying || !transform.hasChanged) return;
            transform.hasChanged = false;
            Generate();
        }

        public bool ContainsAir(Vector2 worldPoint)
        {
            return IsBuilt && worldPoint.y > WaterLevelY && PointInPolygon(_outline, worldPoint);
        }

        public bool CoversColumn(float worldX)
        {
            return IsBuilt && worldX >= WaterlineMinX && worldX <= WaterlineMaxX;
        }

        public void SetPoints(IList<WaterPocketPoint> points)
        {
            _points = new List<WaterPocketPoint>(points);
        }

        public void SetWaterLevel(float localY) => _waterLevel = localY;

        public Vector2 Evaluate(int span, float t)
        {
            var count = _points.Count;
            var a = _points[span % count];
            var b = _points[(span + 1) % count];
            Vector2 origin = transform.position;
            var p0 = origin + a.position;
            var p1 = p0 + a.outTangent;
            var p3 = origin + b.position;
            var p2 = p3 + b.inTangent;
            var u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }

        public void BuildOutline(List<Vector2> outline)
        {
            outline.Clear();
            if (_points == null || _points.Count < 2) return;

            var steps = CurveSteps;
            for (var span = 0; span < _points.Count; span++)
            {
                for (var i = 0; i < steps; i++) outline.Add(Evaluate(span, (float)i / steps));
            }
        }

        public bool TryGetWaterline(List<Vector2> outline, out float minX, out float maxX)
        {
            minX = float.MaxValue;
            maxX = float.MinValue;
            var level = WaterLevelY;
            for (var i = 0; i < outline.Count; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Count];
                if ((a.y > level) == (b.y > level)) continue;

                var x = Mathf.Lerp(a.x, b.x, (level - a.y) / (b.y - a.y));
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
            }
            return maxX > minX;
        }

        public void Clear()
        {
            Field = null;
            _outline.Clear();
            RestoreClippedSprites();
            if (_generated) DestroyOwned(_generated);
            _generated = null;
            foreach (var asset in _owned)
            {
                if (asset) DestroyOwned(asset);
            }
            _owned.Clear();
        }

        public void Generate()
        {
            Clear();
            if (!isActiveAndEnabled) return;

            BuildOutline(_outline);
            if (_outline.Count < 3) return;
            if (!TryGetWaterline(_outline, out var minX, out var maxX))
            {
                Debug.LogWarning($"WaterPocket '{name}': the Water Level does not cross the outline, so the pocket has no " +
                                 "water. Move the Water Level handle inside the shape.", this);
                _outline.Clear();
                return;
            }

            if (transform.lossyScale != Vector3.one || transform.rotation != Quaternion.identity)
            {
                Debug.LogWarning($"WaterPocket '{name}' is rotated or scaled; the outline only follows its position. Shape " +
                                 "the pocket with its points instead.", this);
            }

            var level = Water.Level;
            var simMaterial = _rippleSimulationMaterial ? _rippleSimulationMaterial : level ? level.RippleSimulationMaterial : null;
            var lineMaterial = _lineMaterial ? _lineMaterial : level ? level.LineMaterial : null;
            var surfaceMaterial = _surfaceMaterial ? _surfaceMaterial : level ? level.SurfaceMaterial : null;
            if (!simMaterial || !lineMaterial)
            {
                Debug.LogWarning($"WaterPocket '{name}' has no Line or Ripple Simulation material and no water level to borrow " +
                                 "them from. Assign them, or add the pocket to a scene with a Water Tilemap Builder.", this);
                _outline.Clear();
                return;
            }

            WaterlineMinX = minX;
            WaterlineMaxX = maxX;
            var waterY = WaterLevelY;
            var tintSurfaceY = Water.TryGetTileSurfaceY(new Vector2((minX + maxX) * 0.5f, waterY - 0.01f), out var mainY)
                ? Mathf.Max(mainY, waterY)
                : waterY;

            _generated = new GameObject($"{GeneratedName} ({name})");

            var field = _generated.AddComponent<WaterField>();
            field.SetSurfaceHeight(waterY);
            var depth = Depth;
            var spread = SurfaceSpreadPerUnit();
            var fieldMinX = minX - depth * spread;
            var fieldMaxX = maxX + depth * spread;
            field.InitializeLocal(new Vector2(fieldMinX, 0f), new Vector2(fieldMaxX - fieldMinX, depth), simMaterial,
                _rippleGrid, _ambientRipples, _frontRipples);
            field.SetWind(_wind);
            var openEdge = Mathf.Max(_rippleGrid.openEdgeWidth, 1e-3f);
            field.SetWaterMask(xz =>
            {
                if (xz.y < 0f) return (xz.x >= minX && xz.x <= maxX, -xz.y / openEdge);
                var widen = xz.y * spread;
                return (xz.y < depth && xz.x >= minX - widen && xz.x <= maxX + widen, 0f);
            });
            Field = field;

            BuildMask(minX, maxX, waterY);
            BuildFrontWater(field, lineMaterial, waterY, tintSurfaceY);
            if (surfaceMaterial) BuildSurfaceStrip(field, surfaceMaterial, minX, maxX, waterY, spread);
            BuildContactBand(field, minX, maxX, waterY);
            BuildSpriteClip();

            foreach (var t in _generated.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
            _generated.hideFlags = HideFlags.HideAndDontSave;
            field.ApplyLocalProperties();
            PublishPocketLines();
        }

        private void BuildMask(float chordMinX, float chordMaxX, float waterY)
        {
            var shader = Resources.Load<Shader>(MaskShaderName);
            if (!shader)
            {
                Debug.LogWarning($"WaterPocket: the mask shader 'Resources/{MaskShaderName}' is missing, so the front water " +
                                 "is not cut away inside the pocket.", this);
                return;
            }

            var mesh = Own(new Mesh { name = "WaterPocketMask" });
            var vertices = new List<Vector3>(_outline.Count);
            foreach (var p in _outline) vertices.Add(new Vector3(p.x, p.y, 0f));
            var triangles = Triangulate(_outline);

            var bandBottom = waterY - _waterlineBand;
            var start = vertices.Count;
            vertices.Add(new Vector3(chordMinX, bandBottom, 0f));
            vertices.Add(new Vector3(chordMaxX, bandBottom, 0f));
            vertices.Add(new Vector3(chordMinX, waterY, 0f));
            vertices.Add(new Vector3(chordMaxX, waterY, 0f));
            triangles.AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3 });

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            var material = Own(new Material(shader) { name = "WaterPocketMask" });
            CreateRenderer("Mask", mesh, material, SpookyWaterSettings.Instance.FrontSortingLayerId, MaskSortingOrder);
        }

        private void BuildFrontWater(WaterField field, Material lineMaterial, float waterY, float tintSurfaceY)
        {
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minY = float.MaxValue;
            foreach (var p in _outline)
            {
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
            }
            minY = Mathf.Min(minY, waterY - _waterlineBand);

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var info = new List<Vector4>();
            var triangles = new List<int>();

            var bandBottom = waterY - _waterlineBand;
            var columns = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) * Mathf.Max(_waterlineSegmentsPerUnit, 1)));
            var rows = Mathf.Max(_waterlineVerticalSegments, 1);
            var lineInfo = new Vector4(tintSurfaceY, 1f, 1f, 0f);
            for (var r = 0; r <= rows; r++)
            {
                var v = (float)r / rows;
                var y = Mathf.Lerp(bandBottom, waterY, v);
                var uvY = (y - (waterY - _waterlineBand)) / _waterlineBand;
                for (var c = 0; c <= columns; c++)
                {
                    var u = (float)c / columns;
                    vertices.Add(new Vector3(Mathf.Lerp(minX, maxX, u), y, 0f));
                    uvs.Add(new Vector2(u, uvY));
                    info.Add(lineInfo);
                }
            }
            AddGrid(triangles, 0, columns, rows);

            if (bandBottom > minY + 1e-4f)
            {
                var bodyInfo = new Vector4(tintSurfaceY, 0f, 1f, 0f);
                var start = vertices.Count;
                vertices.Add(new Vector3(minX, minY, 0f));
                vertices.Add(new Vector3(maxX, minY, 0f));
                vertices.Add(new Vector3(minX, bandBottom, 0f));
                vertices.Add(new Vector3(maxX, bandBottom, 0f));
                for (var i = 0; i < 4; i++)
                {
                    uvs.Add(new Vector2(i & 1, 0f));
                    info.Add(bodyInfo);
                }
                AddGrid(triangles, start, 1, 1);
            }

            var mesh = Own(new Mesh { name = "WaterPocketFront" });
            if (vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, info);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var bounds = mesh.bounds;
            bounds.Expand(new Vector3(0f, DisplacementBoundsPadding * 2f, 0f));
            mesh.bounds = bounds;

            var material = Own(new Material(lineMaterial) { name = lineMaterial.name + " (Pocket)" });
            material.SetFloat(PocketStencilCompId, StencilEqual);
            var renderer = CreateRenderer("FrontWater", mesh, material, SpookyWaterSettings.Instance.FrontSortingLayerId, 0);
            SetRippleScale(renderer);
            renderer.gameObject.AddComponent<WaterTile>().Initialize(WaterCellKind.Line, field);
            field.AddLocalRenderer(renderer);
        }

        private void BuildSurfaceStrip(WaterField field, Material surfaceMaterial, float minX, float maxX, float waterY, float spread)
        {
            var farWiden = Depth * spread;
            var xSegments = Mathf.Max(1, Mathf.CeilToInt((maxX - minX + farWiden * 2f) * Mathf.Max(_surfaceSegments.x, 1)));
            var zSegments = Mathf.Max(_surfaceSegments.y, 1);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (var z = 0; z <= zSegments; z++)
            {
                for (var x = 0; x <= xSegments; x++)
                {
                    var u = (float)x / xSegments;
                    var v = (float)z / zSegments;
                    var widen = v * farWiden;
                    vertices.Add(new Vector3(Mathf.Lerp(minX - widen, maxX + widen, u), waterY, v * Depth));
                    uvs.Add(new Vector2(u, v));
                }
            }

            var stride = xSegments + 1;
            for (var z = 0; z < zSegments; z++)
            {
                for (var x = 0; x < xSegments; x++)
                {
                    var i = x + z * stride;
                    triangles.Add(i); triangles.Add(i + stride); triangles.Add(i + 1);
                    triangles.Add(i + 1); triangles.Add(i + stride); triangles.Add(i + stride + 1);
                }
            }

            var mesh = Own(new Mesh { name = "WaterPocketSurface" });
            if (vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var bounds = mesh.bounds;
            bounds.Expand(new Vector3(0f, DisplacementBoundsPadding * 2f, 0f));
            mesh.bounds = bounds;

            var renderer = CreateRenderer("Surface", mesh, surfaceMaterial, SpookyWaterSettings.Instance.SurfaceSortingLayerId, 0);
            SetRippleScale(renderer);
            var go = renderer.gameObject;
            go.AddComponent<WaterTile>().Initialize(WaterCellKind.Surface, field);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3((minX + maxX) * 0.5f, waterY, Depth * 0.5f);
            box.size = new Vector3(maxX - minX + farWiden, 0.1f, Depth);
            field.AddLocalRenderer(renderer);
        }

        private void BuildContactBand(WaterField field, float minX, float maxX, float waterY)
        {
            var go = new GameObject("ContactBand");
            go.transform.SetParent(_generated.transform, false);
            go.transform.position = Vector3.zero;
            go.AddComponent<WaterTile>().Initialize(WaterCellKind.Line, field);

            var band = go.AddComponent<BoxCollider2D>();
            band.isTrigger = true;
            band.size = new Vector2(maxX - minX, _contactBandThickness);
            band.offset = new Vector2((minX + maxX) * 0.5f, waterY - _contactBandThickness * 0.5f);
            go.AddComponent<WaterContactRipple>().Initialize(_contactRipple, _contactLayers);
        }

        public float SurfaceExpand => Mathf.Max(_surfaceExpand, 0f);

        private float SurfaceSpreadPerUnit() => SurfaceExpand / Depth;

        private void BuildSpriteClip()
        {
            var any = false;
            foreach (var clipped in _clippedSprites) if (clipped) any = true;
            if (!any) return;

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in _outline)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            var size = max - min;
            var ppu = Mathf.Max(_clipPixelsPerUnit, 1);
            ppu = Mathf.Max(Mathf.Min(ppu, Mathf.FloorToInt(2040f / Mathf.Max(size.x, size.y, 0.01f))), 1);
            var width = Mathf.Clamp(Mathf.CeilToInt(size.x * ppu) + 2, 4, 2048);
            var height = Mathf.Clamp(Mathf.CeilToInt(size.y * ppu) + 2, 4, 2048);

            var origin = min - Vector2.one / ppu;
            var pixels = new Color32[width * height];
            var crossings = new List<float>();
            for (var y = 0; y < height; y++)
            {
                var worldY = origin.y + (y + 0.5f) / ppu;
                crossings.Clear();
                for (var i = 0; i < _outline.Count; i++)
                {
                    var a = _outline[i];
                    var b = _outline[(i + 1) % _outline.Count];
                    if (a.y > worldY == b.y > worldY) continue;
                    crossings.Add(Mathf.Lerp(a.x, b.x, (worldY - a.y) / (b.y - a.y)));
                }
                crossings.Sort();

                for (var c = 0; c + 1 < crossings.Count; c += 2)
                {
                    var from = Mathf.Max(Mathf.CeilToInt((crossings[c] - origin.x) * ppu - 0.5f), 0);
                    var to = Mathf.Min(Mathf.FloorToInt((crossings[c + 1] - origin.x) * ppu - 0.5f), width - 1);
                    for (var x = from; x <= to; x++) pixels[y * width + x] = new Color32(255, 255, 255, 255);
                }
            }

            var texture = Own(new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "WaterPocketClip",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            });
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            var sprite = Own(Sprite.Create(texture, new Rect(0f, 0f, width, height), Vector2.zero, ppu, 0, SpriteMeshType.FullRect));
            sprite.name = "WaterPocketClip";

            var go = new GameObject("SpriteClip");
            go.transform.SetParent(_generated.transform, false);
            go.transform.position = new Vector3(origin.x, origin.y, 0f);
            var mask = go.AddComponent<SpriteMask>();
            mask.sprite = sprite;
            mask.alphaCutoff = 0.5f;

            SpriteRenderer first = null;
            SpriteRenderer last = null;
            foreach (var clipped in _clippedSprites)
            {
                if (!clipped) continue;
                if (!first || SortsBefore(clipped, first)) first = clipped;
                if (!last || SortsBefore(last, clipped)) last = clipped;
            }
            mask.sortingLayerID = first.sortingLayerID;
            mask.sortingOrder = Mathf.Max(first.sortingOrder - 1, short.MinValue);
            mask.isCustomRangeActive = true;
            mask.backSortingLayerID = first.sortingLayerID;
            mask.backSortingOrder = Mathf.Max(first.sortingOrder - 1, short.MinValue);
            mask.frontSortingLayerID = last.sortingLayerID;
            mask.frontSortingOrder = last.sortingOrder;

            foreach (var renderer in _clippedSprites)
            {
                if (!renderer) continue;
                if (!_clipOriginals.ContainsKey(renderer)) _clipOriginals[renderer] = renderer.maskInteraction;
                renderer.maskInteraction = _clipMode;
            }
        }

        private static bool SortsBefore(SpriteRenderer a, SpriteRenderer b)
        {
            var layerA = SortingLayer.GetLayerValueFromID(a.sortingLayerID);
            var layerB = SortingLayer.GetLayerValueFromID(b.sortingLayerID);
            return layerA != layerB ? layerA < layerB : a.sortingOrder < b.sortingOrder;
        }

        private void RestoreClippedSprites()
        {
            foreach (var (renderer, original) in _clipOriginals)
            {
                if (renderer) renderer.maskInteraction = original;
            }
            _clipOriginals.Clear();
        }

        private MeshRenderer CreateRenderer(string objectName, Mesh mesh, Material material, int sortingLayer, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(_generated.transform, false);
            go.transform.position = Vector3.zero;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var group = go.AddComponent<SortingGroup>();
            group.sortingLayerID = sortingLayer;
            group.sortingOrder = sortingOrder;
            return renderer;
        }

        private void SetRippleScale(Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetFloat(RippleScaleId, Mathf.Max(_displacementScale, 0f));
            renderer.SetPropertyBlock(block);
        }

        private T Own<T>(T asset) where T : UnityEngine.Object
        {
            asset.hideFlags = HideFlags.DontSave;
            _owned.Add(asset);
            return asset;
        }

        private static void DestroyOwned(UnityEngine.Object target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private static void AddGrid(List<int> triangles, int baseIndex, int columns, int rows)
        {
            var stride = columns + 1;
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < columns; c++)
                {
                    var i = baseIndex + c + r * stride;
                    triangles.Add(i + 1); triangles.Add(i + stride); triangles.Add(i);
                    triangles.Add(i + 1 + stride); triangles.Add(i + stride); triangles.Add(i + 1);
                }
            }
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static List<int> Triangulate(List<Vector2> polygon)
        {
            var triangles = new List<int>();
            var n = polygon.Count;
            if (n < 3) return triangles;

            var area = 0f;
            for (var i = 0; i < n; i++) area += Cross(polygon[i], polygon[(i + 1) % n]);
            var remaining = new List<int>(n);
            for (var i = 0; i < n; i++) remaining.Add(area > 0f ? i : n - 1 - i);

            while (remaining.Count > 3)
            {
                var clipped = false;
                for (var i = 0; i < remaining.Count && !clipped; i++)
                {
                    var a = remaining[(i + remaining.Count - 1) % remaining.Count];
                    var b = remaining[i];
                    var c = remaining[(i + 1) % remaining.Count];
                    if (Cross(polygon[b] - polygon[a], polygon[c] - polygon[b]) <= 1e-7f) continue;

                    var blocked = false;
                    foreach (var j in remaining)
                    {
                        if (j == a || j == b || j == c || !InTriangle(polygon[j], polygon[a], polygon[b], polygon[c])) continue;
                        blocked = true;
                        break;
                    }
                    if (blocked) continue;

                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    remaining.RemoveAt(i);
                    clipped = true;
                }

                if (clipped) continue;
                var flattest = 0;
                var flattestCross = float.MaxValue;
                for (var i = 0; i < remaining.Count; i++)
                {
                    var a = polygon[remaining[(i + remaining.Count - 1) % remaining.Count]];
                    var b = polygon[remaining[i]];
                    var c = polygon[remaining[(i + 1) % remaining.Count]];
                    var cross = Mathf.Abs(Cross(b - a, c - b));
                    if (cross >= flattestCross) continue;
                    flattestCross = cross;
                    flattest = i;
                }
                remaining.RemoveAt(flattest);
            }

            triangles.Add(remaining[0]); triangles.Add(remaining[1]); triangles.Add(remaining[2]);
            return triangles;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            return Cross(b - a, p - a) >= 0f && Cross(c - b, p - b) >= 0f && Cross(a - c, p - c) >= 0f;
        }

        private static bool PointInPolygon(List<Vector2> polygon, Vector2 p)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        private void OnDrawGizmos()
        {
            var outline = _outline.Count >= 3 ? _outline : null;
            if (outline == null)
            {
                outline = new List<Vector2>();
                BuildOutline(outline);
            }
            if (outline.Count < 2) return;

            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
            for (var i = 0; i < outline.Count; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Count];
                Gizmos.DrawLine(new Vector3(a.x, a.y, 0f), new Vector3(b.x, b.y, 0f));
            }

            if (!IsBuilt) return;
            var y = WaterLevelY;
            var widen = Depth * SurfaceSpreadPerUnit();
            var nearLeft = new Vector3(WaterlineMinX, y, 0f);
            var nearRight = new Vector3(WaterlineMaxX, y, 0f);
            var farLeft = new Vector3(WaterlineMinX - widen, y, Depth);
            var farRight = new Vector3(WaterlineMaxX + widen, y, Depth);
            Gizmos.DrawLine(nearLeft, nearRight);
            Gizmos.DrawLine(nearRight, farRight);
            Gizmos.DrawLine(farRight, farLeft);
            Gizmos.DrawLine(farLeft, nearLeft);
        }
    }
}
