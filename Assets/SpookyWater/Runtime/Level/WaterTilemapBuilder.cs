using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace SpookyWater
{
    [DisallowMultipleComponent]
    [AddComponentMenu("SpookyWater/Water Tilemap Builder")]
    [HelpURL(SpookyWaterDocs.Builder)]
    public partial class WaterTilemapBuilder : MonoBehaviour
    {
        private static readonly int RippleScaleId = Shader.PropertyToID("_RippleScale");
        private static readonly int PlanarReflectionTexId = Shader.PropertyToID("_PlanarReflectionTexture");
        private static readonly int WaterHorizonSeamId = Shader.PropertyToID("_WaterHorizonSeam");
        private static readonly int WaterHorizonHeightScaleId = Shader.PropertyToID("_WaterHorizonHeightScale");
        private static readonly int WaterHorizonEdgeFalloffId = Shader.PropertyToID("_WaterHorizonEdgeFalloff");
        private static readonly int EdgeFalloffWidthId = Shader.PropertyToID("_EdgeFalloffWidth");
        private static readonly int RippleHeightOffsetId = Shader.PropertyToID("_RippleHeightOffset");
        private static readonly int DistanceFalloffPowerId = Shader.PropertyToID("_DistanceFalloffPower");
        private static readonly int DepthFogColorId = Shader.PropertyToID("_DepthFogColor");
        private static readonly int DepthFogStartZId = Shader.PropertyToID("_DepthFogStartZ");
        private static readonly int DepthFogEndZId = Shader.PropertyToID("_DepthFogEndZ");
        private static readonly int DepthFogPowerId = Shader.PropertyToID("_DepthFogPower");
        private static readonly int WaterSurfaceDisplacementId = Shader.PropertyToID("_WaterSurfaceDisplacement");
        private static readonly int WaterSurfaceFogColorId = Shader.PropertyToID("_WaterSurfaceFogColor");
        private static readonly int WaterSurfaceFogParamsId = Shader.PropertyToID("_WaterSurfaceFogParams");
        private static readonly int UnderwaterColorId = Shader.PropertyToID("_UnderwaterColor");
        private static readonly int UnderwaterFogDistanceId = Shader.PropertyToID("_UnderwaterFogDistance");
        private static readonly int UnderwaterHazeColorId = Shader.PropertyToID("_UnderwaterHazeColor");
        private static readonly int WaterUnderwaterColorId = Shader.PropertyToID("_WaterUnderwaterColor");
        private static readonly int WaterUnderwaterFogId = Shader.PropertyToID("_WaterUnderwaterFog");

        private const string GeneratedRootName = "GeneratedWater";
        private const float DisplacementBoundsPadding = 1f;

        [Group("Tilemaps")]
        [Tooltip("The front cross-section tilemap (the XY plane). Paint the Water Body tile on every cell that is " +
                 "water; cells with open air directly above them become the animated waterline automatically. " +
                 "Leave empty to use the Tilemap on this GameObject.")]
        [SerializeField] private Tilemap _tilemap;
        [Tooltip("The optional surface tilemap (the flat XZ top of the water). Paint the Water Surface tile where " +
                 "the water should be reachable from above. Its columns must line up with the front tilemap's " +
                 "columns (same world X); its rows run away from the camera, and the bottom painted row is the " +
                 "near edge at world Z = 0. Leave empty for an underwater-only level.")]
        [SerializeField] private Tilemap _surfaceTilemap;

        [Group("Materials")]
        [Tooltip("Material for the reflective surface tiles. Use the SpookyWater/Water Surface shader. Required " +
                 "when a Surface Tilemap is assigned.")]
        [SerializeField] private Material _surfaceMeshMaterial;
        [Tooltip("Material for the animated waterline strip along the top of the front water. Use the " +
                 "SpookyWater/Water Front shader. Only the ripple height scale is overridden per tile, so the " +
                 "shared material asset is never modified.")]
        [SerializeField] private Material _lineMeshMaterial;
        [Tooltip("Material for the fully submerged front glass. Use the SpookyWater/Water Front shader; it can be " +
                 "the same asset as the Line material.")]
        [SerializeField] private Material _bodyMeshMaterial;
        [Tooltip("Optional material for a cheap extension of the water out to the horizon, so a perspective " +
                 "camera sees water reaching the distance without painting tiles that far. Use the " +
                 "SpookyWater/Water Horizon shader. Leave empty to skip it. Only built when a Surface Tilemap " +
                 "is assigned.")]
        [SerializeField] private Material _horizonMaterial;
        [Tooltip("Material that runs the ripple simulation. Use the Hidden/SpookyWater/Ripple Simulation shader. " +
                 "It is copied at runtime, so the asset is never modified.")]
        [SerializeField] private Material _rippleSimulationMaterial;

        [Group("Horizon")]
        [Tooltip("How far the horizon extension reaches beyond the far edge of the painted surface, in world units.")]
        [Min(0f)] [SerializeField] private float _horizonDistance = 60f;
        [Tooltip("Mesh segments across the water's width at the seam. Kept low on purpose: the mesh is meant to " +
                 "cost almost nothing, and perspective hides its coarseness at range.")]
        [Min(1)] [SerializeField] private int _horizonXSegments = 8;
        [Tooltip("How fast the horizon fans out sideways with distance, relative to the main camera's view angle " +
                 "(measured when the water is generated). 0 is a plain strip as wide as the water, 1 matches " +
                 "the camera's view so the side edges stay off-screen while the camera is over the lake. " +
                 "Below 1 the edges can come into view, which is fine where shore art covers them.")]
        [Range(0f, 2f)] [SerializeField] private float _horizonSpread = 1f;
        [Tooltip("Mesh segments from near to far. It only affects how smoothly the ripple displacement tapers to " +
                 "zero toward the horizon.")]
        [Min(1)] [SerializeField] private int _horizonZSegments = 2;
        [Tooltip("World units before the horizon seam over which the surface tiles bend from their own ripple " +
                 "height onto the coarser horizon mesh, so the two meet with no gap. Longer hides the handoff " +
                 "better but reaches further into the interactive water.")]
        [Min(0.001f)] [SerializeField] private float _horizonSeamBlend = 1f;

        [Group("Ripples")]
        [Tooltip("The ripple simulation: grid resolution, spreading speed, energy loss, wakes and how much " +
                 "energy a splash adds. Most values can be changed live in Play mode.")]
        [SerializeField] private RippleGridSettings _rippleGrid = new();
        [Tooltip("Occasional small random ripples so still water does not look like glass.")]
        [SerializeField] private AmbientRippleSettings _ambientRipples = new();
        [Tooltip("Tiny, quickly fading ripples just behind the water's front edge, for restless water close to the viewer.")]
        [SerializeField] private FrontRippleSettings _frontRipples = new();
        [Tooltip("Optional constant movement of the whole surface, as if wind were blowing over it. Off by " +
                 "default, so the water is a flat mirror until something disturbs it.")]
        [SerializeField] private WindSettings _wind = new();
        [Tooltip("Multiplies the Ripple Scale of the water materials for every generated tile (set per tile, so the " +
                 "material assets are untouched). Ripple height is an absolute world-unit value, so a small " +
                 "cell size needs this turned down or waves poke into neighbouring cells.")]
        [Min(0f)] [SerializeField] private float _displacementScale = 1f;

        [Group("Interaction")]
        [Tooltip("Height in world units of the invisible trigger band along the top of the waterline that spawns a " +
                 "ripple and splash when a 2D physics body crosses it. It is thin so it fires as the body passes " +
                 "through the surface rather than while submerged; a very fast body can still skip over it between " +
                 "physics steps.")]
        [Min(0.01f)] [SerializeField] private float _contactRippleBandThickness = 0.1f;
        [Tooltip("Physics layers whose bodies ripple and splash the water when they cross the waterline. Bodies " +
                 "on other layers pass through silently.")]
        [SerializeField] private LayerMask _contactLayers = ~0;
        [Tooltip("How strong the contact ripples are and when a falling or rising body counts as having " +
                 "splashed in or out.")]
        [SerializeField] private ContactRippleSettings _contactRipple = new();

        [Group("Reflection")]
        [Tooltip("Extra physics layers to reflect (and show through the surface from below) in addition to objects " +
                 "with a WaterReflectable component. Usually Nothing. Do not include the layer of the water tiles " +
                 "themselves, since they sample the reflection.")]
        [SerializeField] private LayerMask _reflectionMask;
        [Tooltip("How far below the flat water plane the see-through view starts when the camera is under the " +
                 "surface, in world units. Set it to at least the deepest wave trough, or sprites crossing the " +
                 "water show a gap between their underwater part and the part seen through the surface.")]
        [Min(0f)] [SerializeField] private float _underwaterClipMargin = 0.25f;
        [Tooltip("Size of the reflection texture relative to the screen. Lower is cheaper and softer.")]
        [Range(0.1f, 1f)] [SerializeField] private float _reflectionResolutionScale = 0.5f;

        [Group("Mesh Detail")]
        [Tooltip("Mesh segments across one cell's width for the waterline and surface tiles. Only the wavy edges " +
                 "need this; the submerged glass is always a flat quad.")]
        [Min(1)] [SerializeField] private int _surfaceSegmentsPerCell = 12;
        [Tooltip("Mesh segments up one waterline cell's height, so the ripple falloff over the cell renders as a " +
                 "curve instead of a straight edge.")]
        [Min(1)] [SerializeField] private int _verticalSegmentsPerCell = 4;
        [Tooltip("How many surface rows nearest the camera use the finer Near Segments below; every row beyond " +
                 "uses Far Segments. Ripple detail only reads close to the viewer, so distant water needs " +
                 "fewer vertices.")]
        [Min(0)] [SerializeField] private int _surfaceDetailedRowCount = 3;
        [Tooltip("Mesh segments (X, Z) per cell for the near surface rows.")]
        [SerializeField] private Vector2Int _surfaceNearSegments = new(20, 20);
        [Tooltip("Mesh segments (X, Z) per cell for surface rows beyond the near rows.")]
        [SerializeField] private Vector2Int _surfaceFarSegments = new(3, 2);

        [Group("Advanced")]
        [Tooltip("Painted cells are merged into square chunks this many cells across, giving one mesh and one " +
                 "culling test per chunk instead of per cell. Smaller culls offscreen water more tightly; " +
                 "larger means fewer objects. 8 to 32 suits most levels.")]
        [Min(1)] [SerializeField] private int _chunkSize = 16;
        [Tooltip("Extra margin in world units around the painted water for the ripple field, so tiles at the very " +
                 "edge of the painted area do not sample the field's border.")]
        [Min(0f)] [SerializeField] private float _fieldMargin = 2f;
        [Tooltip("Rebuild the water from the painted tilemaps every time this component starts. Leave it on: " +
                 "the ripple textures and shader globals are runtime-only state that cannot be saved in the " +
                 "scene, so without a rebuild the baked meshes have no live simulation feeding them. " +
                 "Generate Water in the inspector is for previewing layout changes without entering Play mode.")]
        [SerializeField] private bool _regenerateOnStart = true;

        [Group("Debug")]
        [Tooltip("Log the ripple field's setup and every ripple it receives, and draw recent ripples as gizmos in " +
                 "the Scene view. Takes effect immediately in Play mode.")]
        [SerializeField] private bool _verboseFieldLogging;

        private const float DiscoverInterval = 0.5f;
        private const string ConflictMessage = "WaterTilemapBuilder: another WaterTilemapBuilder is already active. A scene supports a single " +
                                               "water level: the ripple field, reflection and shader globals are shared, so one builder " +
                                               "should paint every pond on its two tilemaps. This builder will generate if the active one is removed.";

        private Transform _generatedRoot;
        private WaterField _field;
        private WaterReflectionRenderer _reflection;
        private float _nextDiscoverTime;
        private bool _started;
        private bool _generatedThisSession;
        private bool _conflictLogged;

        public Tilemap FrontTilemap => _tilemap ? _tilemap : GetComponent<Tilemap>();

        public Tilemap SurfaceTilemap => _surfaceTilemap;

        public Material SurfaceMaterial => _surfaceMeshMaterial;

        public Material LineMaterial => _lineMeshMaterial;

        public Material RippleSimulationMaterial => _rippleSimulationMaterial;

        public WaterField Field
        {
            get
            {
                if (!_field) DiscoverGenerated();
                return _field;
            }
        }

        public WaterReflectionRenderer Reflection
        {
            get
            {
                if (!_reflection) DiscoverGenerated();
                return _reflection;
            }
        }

        public bool IsGenerated => Field;

        private void DiscoverGenerated()
        {
            var now = Time.realtimeSinceStartup;
            if (now < _nextDiscoverTime) return;
            _nextDiscoverTime = now + DiscoverInterval;

            var root = transform.Find(GeneratedRootName);
            if (!root) return;
            if (!_field) _field = root.GetComponentInChildren<WaterField>(true);
            if (!_reflection) _reflection = root.GetComponentInChildren<WaterReflectionRenderer>(true);
        }

        private void OnEnable()
        {
            Water.CameraChanged -= OnWaterCameraChanged;
            Water.CameraChanged += OnWaterCameraChanged;
            if (Water.Register(this))
            {
                _conflictLogged = false;
                return;
            }

            LogConflict();
        }

        private void OnDisable()
        {
            Water.CameraChanged -= OnWaterCameraChanged;
            Water.Unregister(this);
        }

        private void OnDestroy()
        {
            Water.Unregister(this);
        }

        internal void OnPromoted()
        {
            _conflictLogged = false;
            if (_started && _regenerateOnStart && !_generatedThisSession) Generate();
        }

        private void LogConflict()
        {
            if (_conflictLogged) return;
            _conflictLogged = true;
            Debug.LogError(ConflictMessage, this);
        }

        private void OnValidate()
        {
            if (_field) _field.VerboseLogging = _verboseFieldLogging;
            if (!Application.isPlaying)
                WaterField.PublishWind(_wind, Vector4.zero, Water.TryGetWaterlineY(out var waterY) ? waterY : transform.position.y);
        }

        private void Start()
        {
            _started = true;
            if (_regenerateOnStart && Water.Register(this)) Generate();
        }

        public void Generate()
        {
            if (!Water.Register(this))
            {
                LogConflict();
                return;
            }

            if (!_tilemap) _tilemap = GetComponent<Tilemap>();
            if (!_tilemap)
            {
                Debug.LogError("WaterTilemapBuilder needs a front Tilemap (assign one or put this component on the Tilemap GameObject).", this);
                return;
            }

            if (!_lineMeshMaterial || !_bodyMeshMaterial || !_rippleSimulationMaterial)
            {
                Debug.LogError("WaterTilemapBuilder is missing one of its material references (Line / Body / RippleSimulation).", this);
                return;
            }

            var generateSurface = _surfaceTilemap;
            if (generateSurface && !_surfaceMeshMaterial)
            {
                Debug.LogError("WaterTilemapBuilder has a Surface Tilemap assigned but no Surface Mesh material.", this);
                return;
            }

            DestroyGenerated();
            _generatedThisSession = true;

            SetTilemapRendererEnabled(_tilemap, false);
            if (generateSurface) SetTilemapRendererEnabled(_surfaceTilemap, false);

            _tilemap.CompressBounds();
            var frontBounds = _tilemap.cellBounds;
            var cellSize = _tilemap.cellSize;

            if (Mathf.Abs(_tilemap.transform.position.z) > 1e-3f)
            {
                Debug.LogWarning($"WaterTilemapBuilder: the front tilemap sits at world Z = {_tilemap.transform.position.z:0.###}. The water " +
                                 "surface starts at world Z = 0, so move the tilemap's Grid to Z = 0 or the surface will not meet the front.", this);
            }

            var fieldOrigin = new Vector2(
                _tilemap.GetCellCenterWorld(new Vector3Int(frontBounds.xMin, 0, 0)).x - cellSize.x * 0.5f - _fieldMargin,
                0f);
            var fieldWidth = (frontBounds.xMax - frontBounds.xMin) * cellSize.x + _fieldMargin * 2f;

            BoundsInt surfaceBounds = default;
            var surfaceCellSize = Vector3.zero;
            var fieldDepth = _fieldMargin;
            if (generateSurface)
            {
                _surfaceTilemap.CompressBounds();
                surfaceBounds = _surfaceTilemap.cellBounds;
                surfaceCellSize = _surfaceTilemap.cellSize;
                fieldDepth = (surfaceBounds.yMax - surfaceBounds.yMin) * surfaceCellSize.y + _fieldMargin;
            }

            var field = BuildField(fieldOrigin, new Vector2(fieldWidth, fieldDepth));
            Shader.SetGlobalVector(WaterHorizonSeamId, Vector4.zero);
            Shader.SetGlobalVector(WaterSurfaceDisplacementId, Vector4.zero);
            Shader.SetGlobalColor(WaterSurfaceFogColorId, Color.clear);
            Shader.SetGlobalVector(WaterUnderwaterFogId, Vector4.zero);

            var columnSurfaceY = new Dictionary<int, float>();
            var lineTileCount = 0;
            var bodyTileCount = 0;
            var straySurfacePaintOnFront = 0;
            var chunkSize = Mathf.Max(_chunkSize, 1);
            var lineChunks = new Dictionary<Vector2Int, ChunkMesh>();
            var bodyChunks = new Dictionary<Vector2Int, ChunkMesh>();
            var contactBands = new List<Rect>();
            var lastBandCell = new Vector2Int(int.MinValue, int.MinValue);

            for (var y = frontBounds.yMax - 1; y >= frontBounds.yMin; y--)
            {
                for (var x = frontBounds.xMin; x < frontBounds.xMax; x++)
                {
                    var kind = GetKind(_tilemap, x, y);
                    if (kind == null) continue;
                    if (kind == WaterCellKind.Surface) straySurfacePaintOnFront++;

                    var cellCenter = _tilemap.GetCellCenterWorld(new Vector3Int(x, y, 0));

                    var openAirAbove = GetKind(_tilemap, x, y + 1) == null;
                    if (openAirAbove)
                    {
                        var surfaceY = columnSurfaceY.TryGetValue(x, out var existing)
                            ? existing
                            : cellCenter.y + cellSize.y * 0.5f;
                        if (!columnSurfaceY.ContainsKey(x)) columnSurfaceY[x] = surfaceY;

                        AddLineCell(GetChunk(lineChunks, x, y, chunkSize, cellCenter), cellCenter, cellSize, surfaceY);
                        lineTileCount++;

                        if (lastBandCell == new Vector2Int(x - 1, y))
                        {
                            var band = contactBands[^1];
                            band.xMax += cellSize.x;
                            contactBands[^1] = band;
                        }
                        else
                        {
                            contactBands.Add(new Rect(cellCenter.x - cellSize.x * 0.5f,
                                cellCenter.y + (cellSize.y - _contactRippleBandThickness) * 0.5f,
                                cellSize.x, _contactRippleBandThickness));
                        }
                        lastBandCell = new Vector2Int(x, y);
                    }
                    else
                    {
                        var surfaceY = columnSurfaceY.TryGetValue(x, out var sy)
                            ? sy
                            : FindNearestColumnSurfaceY(columnSurfaceY, x, frontBounds, cellCenter.y + cellSize.y * 0.5f);
                        AddBodyCell(GetChunk(bodyChunks, x, y, chunkSize, cellCenter), cellCenter, cellSize, surfaceY);
                        bodyTileCount++;
                    }
                }
            }

            var lineBlock = new MaterialPropertyBlock();
            lineBlock.SetFloat(RippleScaleId, _lineMeshMaterial.GetFloat(RippleScaleId) * _displacementScale);
            foreach (var (key, chunk) in lineChunks)
            {
                CreateChunkObject($"WaterChunk_Line_{key.x}_{key.y}", chunk, _lineMeshMaterial, SpookyWaterSettings.Instance.FrontSortingLayerId,
                    WaterCellKind.Line, field, _generatedRoot, lineBlock, DisplacementBoundsPadding);
            }
            var bodyBlock = new MaterialPropertyBlock();
            bodyBlock.SetFloat(RippleScaleId, _bodyMeshMaterial.GetFloat(RippleScaleId) * _displacementScale);
            foreach (var (key, chunk) in bodyChunks)
            {
                CreateChunkObject($"WaterChunk_Body_{key.x}_{key.y}", chunk, _bodyMeshMaterial, SpookyWaterSettings.Instance.FrontSortingLayerId,
                    WaterCellKind.Body, null, _generatedRoot, bodyBlock, 0f);
            }
            BuildContactBands(contactBands, field);

            var averageSurfaceY = 0f;
            foreach (var y in columnSurfaceY.Values) averageSurfaceY += y;
            if (columnSurfaceY.Count > 0) averageSurfaceY /= columnSurfaceY.Count;
            field.SetSurfaceHeight(averageSurfaceY);
            WarnIfSurfaceHeightsDiffer(columnSurfaceY, cellSize.y);

            var paintedFarZ = generateSurface
                ? (surfaceBounds.yMax - surfaceBounds.yMin) * (float)surfaceCellSize.y
                : fieldDepth;
            var farEdgeOpen = !generateSurface || (bool)_horizonMaterial;
            var openEdgeWidth = Mathf.Max(_rippleGrid.openEdgeWidth, 1e-3f);

            field.SetWaterMask(worldXZ =>
            {
                var col = _tilemap.WorldToCell(new Vector3(worldXZ.x, 0f, 0f)).x;
                if (!columnSurfaceY.ContainsKey(col)) return (false, 0f);

                var z = worldXZ.y;
                if (z < 0f) return (true, -z / openEdgeWidth);
                if (z >= paintedFarZ) return farEdgeOpen ? (true, (z - paintedFarZ) / openEdgeWidth) : (false, 0f);
                if (!generateSurface) return (true, 0f);

                var row = surfaceBounds.yMin + Mathf.FloorToInt(z / surfaceCellSize.y);
                return ((bool)_surfaceTilemap.GetTile<WaterTileData>(new Vector3Int(col, row, 0)), 0f);
            });
            var surfaceTileCount = 0;
            var orphanSurfaceCells = 0;
            var chunkCount = lineChunks.Count + bodyChunks.Count;
            if (generateSurface)
            {
                SetupReflection(field, fieldOrigin, fieldWidth, averageSurfaceY);

                var surfaceChunks = new Dictionary<Vector2Int, ChunkMesh>();
                var surfaceCellY = new Dictionary<Vector2Int, float>();
                var cellSizeZ = (float)surfaceCellSize.y;
                float ColumnCenterX(int col) => _tilemap.GetCellCenterWorld(new Vector3Int(col, frontBounds.yMin, 0)).x;

                for (var row = surfaceBounds.yMin; row < surfaceBounds.yMax; row++)
                {
                    for (var col = surfaceBounds.xMin; col < surfaceBounds.xMax; col++)
                    {
                        if (!_surfaceTilemap.GetTile<WaterTileData>(new Vector3Int(col, row, 0))) continue;

                        if (!columnSurfaceY.TryGetValue(col, out var worldY))
                        {
                            orphanSurfaceCells++;
                            continue;
                        }

                        var corner = new Vector3(ColumnCenterX(col) - cellSize.x * 0.5f, worldY, (row - surfaceBounds.yMin) * cellSizeZ);
                        var isDetailed = row - surfaceBounds.yMin < _surfaceDetailedRowCount;
                        var segments = isDetailed ? _surfaceNearSegments : _surfaceFarSegments;
                        AddSurfaceCell(GetChunk(surfaceChunks, col, row, chunkSize, corner), corner, cellSize.x, cellSizeZ, segments);
                        surfaceCellY[new Vector2Int(col, row)] = worldY;
                        surfaceTileCount++;
                    }
                }

                var surfaceBlock = new MaterialPropertyBlock();
                surfaceBlock.SetFloat(RippleScaleId, _surfaceMeshMaterial.GetFloat(RippleScaleId) * _displacementScale);
                PublishSurfaceGlobals();
                foreach (var (key, chunk) in surfaceChunks)
                {
                    CreateChunkObject($"WaterChunk_Surface_{key.x}_{key.y}", chunk, _surfaceMeshMaterial, SpookyWaterSettings.Instance.SurfaceSortingLayerId,
                        WaterCellKind.Surface, field, field.transform, surfaceBlock, DisplacementBoundsPadding);
                }
                chunkCount += surfaceChunks.Count;

                BuildSurfaceColliders(surfaceCellY, surfaceBounds, ColumnCenterX, cellSize.x, cellSizeZ, field);

                if (_horizonMaterial)
                {
                    BuildHorizonExtension(fieldOrigin, fieldWidth, paintedFarZ, averageSurfaceY);
                }
            }

            LogGenerationSummary(surfaceTileCount, lineTileCount, bodyTileCount, chunkCount, orphanSurfaceCells, straySurfacePaintOnFront);
        }


        private static float FindNearestColumnSurfaceY(Dictionary<int, float> columnSurfaceY, int x, BoundsInt bounds, float fallback)
        {
            var maxRadius = bounds.xMax - bounds.xMin;
            for (var radius = 1; radius <= maxRadius; radius++)
            {
                if (columnSurfaceY.TryGetValue(x - radius, out var left)) return left;
                if (columnSurfaceY.TryGetValue(x + radius, out var right)) return right;
            }

            return fallback;
        }

        private static WaterCellKind? GetKind(Tilemap tilemap, int x, int y)
        {
            var tile = tilemap.GetTile<WaterTileData>(new Vector3Int(x, y, 0));
            return tile ? tile.CellKind : null;
        }

        private static void SetTilemapRendererEnabled(Tilemap tilemap, bool enabled)
        {
            if (tilemap.TryGetComponent<TilemapRenderer>(out var renderer))
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.RecordObject(renderer, "Generate Water");
#endif
                renderer.enabled = enabled;
            }
        }

        private void LogGenerationSummary(int surfaceTileCount, int lineTileCount, int bodyTileCount, int chunkCount,
            int orphanSurfaceCells, int straySurfacePaintOnFront)
        {
            if (straySurfacePaintOnFront > 0)
            {
                Debug.LogWarning($"WaterTilemapBuilder: {straySurfacePaintOnFront} cell(s) on the front tilemap are " +
                                  "painted with the Surface WaterTileData - that asset belongs on the Surface tilemap. " +
                                  "They were treated as ordinary front water cells (Line or Body by position).", this);
            }

            if (orphanSurfaceCells > 0)
            {
                Debug.LogWarning($"WaterTilemapBuilder: skipped {orphanSurfaceCells} Surface cell(s) whose column has no " +
                                  "corresponding front water cell - paint front water in that column so the Surface tile " +
                                  "knows its world height.", this);
            }

            if (lineTileCount == 0 && bodyTileCount == 0)
            {
                var painted = _tilemap.GetUsedTilesCount();
                if (painted == 0)
                {
                    Debug.LogWarning($"WaterTilemapBuilder: '{_tilemap.name}' has no painted cells - nothing to generate.", this);
                }
                else
                {
                    Debug.LogWarning($"WaterTilemapBuilder: '{_tilemap.name}' has {painted} painted cell(s), but none are " +
                                      "WaterTileData tiles - paint with WaterBodyTile.asset from SpookyWater/Tiles, not a " +
                                      "tile created directly in the Tile Palette.", this);
                }
                return;
            }

            Debug.Log($"WaterTilemapBuilder: generated {surfaceTileCount} surface tile(s), {lineTileCount} line tile(s), " +
                      $"{bodyTileCount} body tile(s), merged into {chunkCount} chunk(s).", this);
        }

        public void Clear()
        {
            DestroyGenerated(false);
            if (_tilemap) SetTilemapRendererEnabled(_tilemap, true);
            if (_surfaceTilemap) SetTilemapRendererEnabled(_surfaceTilemap, true);
        }

        private void DestroyGenerated(bool createRoot = true)
        {
            var existing = transform.Find(GeneratedRootName);
            if (existing) DestroyGeneratedObject(existing.gameObject);

            _field = null;
            _reflection = null;
            _generatedRoot = null;
            _nextDiscoverTime = 0f;
            if (!createRoot) return;

            var root = new GameObject(GeneratedRootName);
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(root, "Generate Water");
#endif
            _generatedRoot = root.transform;
            _generatedRoot.SetParent(transform, false);
        }

        private static void DestroyGeneratedObject(GameObject target)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.DestroyObjectImmediate(target);
                return;
            }
#endif
            DestroyImmediate(target);
        }

        private void WarnIfSurfaceHeightsDiffer(Dictionary<int, float> columnSurfaceY, float cellHeight)
        {
            if (columnSurfaceY.Count < 2) return;

            var min = float.MaxValue;
            var max = float.MinValue;
            foreach (var y in columnSurfaceY.Values)
            {
                min = Mathf.Min(min, y);
                max = Mathf.Max(max, y);
            }

            if (max - min > cellHeight * 0.5f)
            {
                Debug.LogWarning($"WaterTilemapBuilder: the water surface is not level across the painted columns ({min:0.##} to {max:0.##}). " +
                                 "A scene has one reflection plane at the average height and one shared ripple field, so ponds at " +
                                 "different heights will reflect and ripple incorrectly. Keep every pond's surface at the same height.", this);
            }
        }

        private WaterField BuildField(Vector2 origin, Vector2 size)
        {
            var go = new GameObject("WaterField");
            go.transform.SetParent(_generatedRoot, false);
            go.transform.position = new Vector3(origin.x, 0f, origin.y);

            var field = go.AddComponent<WaterField>();
            field.Initialize(origin, size, _rippleSimulationMaterial, _rippleGrid, _ambientRipples, _frontRipples, _verboseFieldLogging);
            field.SetWind(_wind);
            _field = field;
            return field;
        }

        private void SetupReflection(WaterField field, Vector2 fieldOrigin, float fieldWidth, float surfaceY)
        {
            var go = new GameObject("WaterReflection");
            go.transform.SetParent(field.transform, false);
            go.transform.position = new Vector3(fieldOrigin.x + fieldWidth * 0.5f, surfaceY, fieldOrigin.y);

            var reflectionCam = new GameObject("ReflectionCam", typeof(Camera));
            reflectionCam.transform.SetParent(go.transform, false);

            var reflectionTexture = new RenderTexture(256, 256, 16) { name = "WaterField_Reflection", wrapMode = TextureWrapMode.Clamp };
            var reflection = go.AddComponent<WaterReflectionRenderer>();
            reflection.Initialize(reflectionCam.GetComponent<Camera>(), reflectionTexture, _reflectionMask, _underwaterClipMargin, _reflectionResolutionScale);
            _reflection = reflection;

            Shader.SetGlobalTexture(PlanarReflectionTexId, reflectionTexture);
        }
    }
}
