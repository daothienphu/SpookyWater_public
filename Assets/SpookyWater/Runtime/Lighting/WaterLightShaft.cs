using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpookyWater
{
    [ExecuteAlways]
    [AddComponentMenu("SpookyWater/Light Shaft")]
    [HelpURL(SpookyWaterDocs.LightShaft)]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WaterLightShaft : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly int SideFadeId = Shader.PropertyToID("_SideFade");
        private static readonly int BandTexAId = Shader.PropertyToID("_BandTex");
        private static readonly int BandShapeAId = Shader.PropertyToID("_BandShape1");
        private static readonly int BandLightAId = Shader.PropertyToID("_BandLight1");
        private static readonly int BandTexBId = Shader.PropertyToID("_BandTex2");
        private static readonly int BandShapeBId = Shader.PropertyToID("_BandShape2");
        private static readonly int BandLightBId = Shader.PropertyToID("_BandLight2");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int ClipYId = Shader.PropertyToID("_WaterClipY");
        private static readonly int ClipSoftnessId = Shader.PropertyToID("_WaterClipSoftness");
        private static readonly int ShadowAxesId = Shader.PropertyToID("_ShaftShadowAxes");
        private static readonly int OccludersId = Shader.PropertyToID("_ShaftOccluders");
        private static readonly int OccluderOutlinesId = Shader.PropertyToID("_ShaftOccluderOutlines");
        private static readonly int OccluderShadowsId = Shader.PropertyToID("_ShaftOccluderShadows");
        private static readonly int OccluderCountId = Shader.PropertyToID("_ShaftOccluderCount");
        private const int Rows = 16;
        private const int MaxOccluders = 16;

        [Group("Shape")]
        [Tooltip("Underwater god rays: a leaning sheet of big parallel light bands, filled with two band textures scrolling sideways. Horizontal width of the sheet, world units; its top edge is centred on this object. Drag the side handles in the Scene view.")]
        [SerializeField, Min(0.01f)] private float _width = 12f;
        [Tooltip("How far down the light reaches from the top edge, world units. Drag the bottom handle in the Scene view.")]
        [SerializeField, Min(0.01f)] private float _height = 9f;
        [Tooltip("Put the top edge on the water surface wherever this object sits, so only X/Z placement matters. Off = the top edge is at this object's position.")]
        [SerializeField] private bool _snapTopToWaterline = true;
        [Tooltip("How far above the waterline the top edge sits, world units, so the light reaches the very top of the front glass (its foam line and ripple crests). The light is always clipped exactly at the front glass's moving top edge, so this only needs to cover the tallest crests.")]
        [SerializeField, Min(0f)] private float _topOffset = 0.5f;
        [Tooltip("Lean of the light from vertical, degrees. Positive = it runs down to the right, negative = down to the left, 0 = straight down. The whole shape leans with the bands (a parallelogram, not a rectangle), so its sides run along the bands and no band is cut off by an edge.")]
        [SerializeField, Range(-75f, 75f)] private float _angle = 30f;

        [Group("Light")]
        [Tooltip("Color of the light. HDR, so values above 1 glow.")]
        [ColorUsage(false, true)]
        [SerializeField] private Color _color = new(0.55f, 0.95f, 1f);
        [Tooltip("Brightness multiplier. The light is additive, so above about 1 (with an HDR color) it feeds URP Bloom.")]
        [SerializeField, Min(0f)] private float _intensity = 0.6f;
        [Tooltip("How see-through the whole shaft is, 0 = invisible, 1 = full. Fades it without retuning the layers.")]
        [SerializeField, Range(0f, 1f)] private float _opacity = 0.4f;
        [Tooltip("Light in the gaps between bands, as a fraction of a band's. 0 = dark gaps, 1 = one even sheet.")]
        [SerializeField, Range(0f, 1f)] private float _fill = 0.25f;

        [Group("Bands")]
        [Tooltip("How soft the band edges are, for both layers: the width of the blur, world units (a smooth gaussian). 0 = as sharp as the images.")]
        [SerializeField, Range(0f, 4f)] private float _edgeBlurWidth = 0.8f;
        [Tooltip("Strength of a wide, blurred copy of the bands added on top: a soft haze of light around them. 0 = none.")]
        [SerializeField, Range(0f, 2f)] private float _glow = 0.4f;
        [Tooltip("How wide that glow spreads, world units (a smooth gaussian blur of the bands).")]
        [SerializeField, Range(0f, 10f)] private float _glowWidth = 3f;

        [Group("Band Layer A")]
        [Tooltip("The first band texture. The two layers combine as a union: light is wherever at least one has a band, so crossings are no brighter.")]
        [SerializeField] private LightShaftBandLayer _layerA = new(12f, 0.4f);

        [Group("Band Layer B")]
        [Tooltip("The second band texture. Give it a slightly different Band Scale and an opposite Scroll Speed to Layer A, so the two never line up and drift through each other: the shaft shimmers in place instead of sliding. Strength 0 = Layer A only.")]
        [SerializeField] private LightShaftBandLayer _layerB = new(9.5f, -0.55f);

        [Group("Fade")]
        [Tooltip("Brightness from the top edge (0) to the bottom (1). The default falls off fast and then trails away to exactly 0 with a flat end, like light dying in the water, so the shaft melts into it instead of stopping. A curve that reaches 0 at a slant leaves a visible end.")]
        [SerializeField] private AnimationCurve _brightnessAlongHeight = DefaultBrightness();
        [Tooltip("How softly the left and right edges fade, as a fraction of the width per side. 0 = hard edges.")]
        [SerializeField, Range(0f, 0.5f)] private float _sideFade = 0.5f;
        [Tooltip("World units over which the light fades in below the front glass's moving top edge (where it is clipped). Small = the light starts right at the top of the waterline; larger = a softer start further down.")]
        [SerializeField, Min(0f)] private float _surfaceFade = 0.08f;

        [Group("Water")]
        [Tooltip("Use Waterline Y below instead of the water surface height found in the scene. The light is always cut above the waterline.")]
        [SerializeField] private bool _overrideWaterline;
        [Tooltip("World Y of the water surface, used when Override Waterline is on or when the scene has no water yet.")]
        [SerializeField] private float _waterlineY;

        [Group("Rendering")]
        [Tooltip("A material using SpookyWater/Light Shaft, shared by every shaft: LightShaft_Underwater (Depth Test Always, so copies behind the front glass don't fail against its depth).")]
        [SerializeField] private Material _material;
        [Tooltip("Sorting layer it is drawn on. Underwater shafts go on the front water layer so they also light the surface's underside when the camera is below it.")]
        [SortingLayerDropdown]
        [SerializeField] private int _sortingLayer;
        [Tooltip("Order inside the sorting layer. 500: after the front glass and before the spray, which uses 1000.")]
        [SerializeField] private int _sortingOrder = 500;
        [Tooltip("Shifts both band layers, so neighbouring shafts don't line up or move in step.")]
        [SerializeField] private float _seed;

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _propertyBlock;
        private Mesh _mesh;
        private bool _meshDirty = true;
        private float _appliedWaterY = float.NaN;
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector4> _uvs = new();
        private readonly List<int> _triangles = new();
        private readonly Vector4[] _occluders = new Vector4[MaxOccluders];
        private readonly Vector4[] _occluderOutlines = new Vector4[MaxOccluders];
        private readonly Vector4[] _occluderShadows = new Vector4[MaxOccluders];
        private int _appliedOccluders;

        public Color Color
        {
            get => _color;
            set
            {
                _color = value;
                ApplyProperties();
            }
        }

        public float Intensity
        {
            get => _intensity;
            set
            {
                _intensity = value;
                ApplyProperties();
            }
        }

        public float Opacity
        {
            get => _opacity;
            set
            {
                _opacity = Mathf.Clamp01(value);
                ApplyProperties();
            }
        }

        public float Width => _width;
        public float Height => _height;
        public float Lean => Mathf.Tan(_angle * Mathf.Deg2Rad);

        public float LocalTop
        {
            get
            {
                if (!_snapTopToWaterline) return 0f;
                var position = transform.position;
                return transform.InverseTransformPoint(new Vector3(position.x, WaterlineY() + _topOffset, position.z)).y;
            }
        }

        private static AnimationCurve DefaultBrightness() => new(
            new Keyframe(0f, 1f, -3.5f, -3.5f),
            new Keyframe(0.25f, 0.42f, -1.6f, -1.6f),
            new Keyframe(0.6f, 0.12f, -0.45f, -0.45f),
            new Keyframe(1f, 0f, 0f, 0f));

        private void Reset()
        {
            _sortingLayer = SpookyWaterSettings.Instance.FrontSortingLayerId;
            _sortingOrder = SpookyWaterSettings.Instance.underwaterShaftSortingOrder;
            if (!SortingLayer.IsValid(_sortingLayer)) _sortingLayer = SortingLayer.NameToID("Default");
            _seed = Random.Range(0f, 100f);
#if UNITY_EDITOR
            _material = WaterEditorDefaults.Load<Material>(WaterEditorDefaults.LightShaftUnderwaterMaterial);
            _layerA.texture = WaterEditorDefaults.Load<Texture2D>(WaterEditorDefaults.LightShaftBandsTexture);
            _layerB.texture = WaterEditorDefaults.Load<Texture2D>(WaterEditorDefaults.LightShaftBandsFineTexture);
#endif
            OnValidate();
        }

        private void OnValidate()
        {
            _meshDirty = true;
            _appliedWaterY = float.NaN;
            if (!isActiveAndEnabled) return;
            ApplyProperties();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this && isActiveAndEnabled) RebuildIfDirty();
            };
#endif
        }

        private void OnEnable()
        {
            _meshDirty = true;
            _appliedWaterY = float.NaN;
            RebuildIfDirty();
            ApplyProperties();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= FollowWaterline;
            if (!Application.isPlaying) UnityEditor.EditorApplication.update += FollowWaterline;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= FollowWaterline;
#endif
            if (!_mesh) return;
            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
            _mesh = null;
        }

        private void LateUpdate()
        {
            if (transform.hasChanged)
            {
                transform.hasChanged = false;
                if (_snapTopToWaterline) _meshDirty = true;
                ApplyProperties();
            }
            FollowWaterline();
            RebuildIfDirty();
        }

        private void FollowWaterline()
        {
            if (!this) return;
            var waterMoved = !Mathf.Approximately(WaterlineY(), _appliedWaterY);
            if (waterMoved && _snapTopToWaterline) _meshDirty = true;
            if (waterMoved || WaterLightShaftOccluder.Active.Count > 0 || _appliedOccluders > 0) ApplyProperties();
            if (waterMoved && !Application.isPlaying) RebuildIfDirty();
        }

        private void RebuildIfDirty()
        {
            if (!_meshDirty && _mesh) return;
            _meshDirty = false;

            if (!_mesh)
            {
                _mesh = new Mesh { name = "WaterLightShaft", hideFlags = HideFlags.DontSave };
                _mesh.MarkDynamic();
            }
            BuildMesh();
            GetComponent<MeshFilter>().sharedMesh = _mesh;

            if (!_renderer) _renderer = GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.sortingLayerID = _sortingLayer;
            _renderer.sortingOrder = _sortingOrder;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void BuildMesh()
        {
            _vertices.Clear();
            _uvs.Clear();
            _triangles.Clear();

            AddSheet(LocalTop);

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateBounds();
        }

        private void AddSheet(float top)
        {
            var first = _vertices.Count;
            var half = _width * 0.5f;
            var lean = Lean * _height;
            for (var row = 0; row <= Rows; row++)
            {
                var along = row / (float)Rows;
                var y = top - along * _height;
                var brightness = Mathf.Max(0f, _brightnessAlongHeight.Evaluate(along));
                var x = along * lean;
                _vertices.Add(new Vector3(x - half, y, 0f));
                _vertices.Add(new Vector3(x + half, y, 0f));
                _uvs.Add(new Vector4(0f, along, brightness, 0f));
                _uvs.Add(new Vector4(1f, along, brightness, 0f));
            }

            for (var row = 0; row < Rows; row++)
            {
                var i = first + row * 2;
                _triangles.Add(i);
                _triangles.Add(i + 2);
                _triangles.Add(i + 1);
                _triangles.Add(i + 1);
                _triangles.Add(i + 2);
                _triangles.Add(i + 3);
            }
        }

        private void ApplyProperties()
        {
            if (!_renderer) _renderer = GetComponent<MeshRenderer>();
            if (!_renderer) return;

            _appliedWaterY = WaterlineY();
            var worldWidth = _width * Mathf.Abs(transform.lossyScale.x);
            _propertyBlock ??= new MaterialPropertyBlock();
            _propertyBlock.Clear();
            _propertyBlock.SetColor(ColorId, _color);
            _propertyBlock.SetFloat(IntensityId, _intensity);
            _propertyBlock.SetFloat(OpacityId, _opacity);
            _propertyBlock.SetFloat(FillId, _fill);
            _propertyBlock.SetFloat(SideFadeId, _sideFade);
            _layerA.Apply(_propertyBlock, BandTexAId, BandShapeAId, BandLightAId, worldWidth, _edgeBlurWidth, _glow, _glowWidth);
            _layerB.Apply(_propertyBlock, BandTexBId, BandShapeBId, BandLightBId, worldWidth, _edgeBlurWidth, _glow, _glowWidth);
            _propertyBlock.SetFloat(SeedId, _seed);
            _propertyBlock.SetFloat(ClipYId, _appliedWaterY);
            _propertyBlock.SetFloat(ClipSoftnessId, _surfaceFade);
            ApplyOccluders();
            _renderer.SetPropertyBlock(_propertyBlock);
        }

        private void ApplyOccluders()
        {
            var down3 = transform.TransformDirection(new Vector3(Lean, -1f, 0f));
            var down = new Vector2(down3.x, down3.y);
            down = down.sqrMagnitude > 1e-8f ? down.normalized : Vector2.down;
            var across = new Vector2(-down.y, down.x);

            var top = LocalTop;
            var half = _width * 0.5f;
            var shift = Lean * _height;
            var shaftMin = float.MaxValue;
            var shaftMax = float.MinValue;
            Extend(new Vector3(-half, top, 0f));
            Extend(new Vector3(half, top, 0f));
            Extend(new Vector3(shift - half, top - _height, 0f));
            Extend(new Vector3(shift + half, top - _height, 0f));

            var count = 0;
            var reach = _height * Mathf.Abs(transform.lossyScale.y);
            foreach (var occluder in WaterLightShaftOccluder.Active)
            {
                if (count >= MaxOccluders) break;
                if (!occluder.TryProject(down, across, out var interval, out var outline, out var shadow)) continue;
                var margin = shadow.y + shadow.z * reach;
                if (interval.y + margin < shaftMin || interval.x - margin > shaftMax) continue;
                _occluders[count] = interval;
                _occluderOutlines[count] = outline;
                _occluderShadows[count] = shadow;
                count++;
            }

            _appliedOccluders = count;
            _propertyBlock.SetVector(ShadowAxesId, new Vector4(down.x, down.y, across.x, across.y));
            _propertyBlock.SetVectorArray(OccludersId, _occluders);
            _propertyBlock.SetVectorArray(OccluderOutlinesId, _occluderOutlines);
            _propertyBlock.SetVectorArray(OccluderShadowsId, _occluderShadows);
            _propertyBlock.SetFloat(OccluderCountId, count);

            void Extend(Vector3 local)
            {
                var s = Vector2.Dot(transform.TransformPoint(local), across);
                shaftMin = Mathf.Min(shaftMin, s);
                shaftMax = Mathf.Max(shaftMax, s);
            }
        }

        private float WaterlineY()
        {
            if (_overrideWaterline) return _waterlineY;
            return Water.TryGetWaterlineY(out var y) ? y : _waterlineY;
        }
    }

    [System.Serializable]
    public class LightShaftBandLayer
    {
        [Tooltip("Mask of where the bands are: flat white vertical bands on black. Only the layout is read; color comes from the shaft. Scrolled sideways and stretched over the shaft's height. Must tile left-right; import with Wrap U = Repeat, Wrap V = Clamp and mipmaps on. Empty = the material's texture for this layer (Textures/Lighting/LightShaftBands for A, LightShaftBandsFine for B).")]
        public Texture2D texture;
        [Tooltip("Brightness of this layer's bands. 0 = layer off.")]
        [Range(0f, 1f)] public float strength = 1f;
        [Tooltip("World units one repeat of the image covers across. Bigger = wider bands. Independent of the shaft's Width, so a wider shaft gets more bands instead of stretched ones.")]
        [Min(0.01f)] public float bandScale = 10f;
        [Tooltip("How fast this layer's bands slide sideways, world units per second. Negative = to the left. Scroll the two layers opposite ways at different speeds so they pass through each other. Keep it slow.")]
        public float scrollSpeed = 0.3f;

        public LightShaftBandLayer()
        {
        }

        public LightShaftBandLayer(float bandScale, float scrollSpeed)
        {
            this.bandScale = bandScale;
            this.scrollSpeed = scrollSpeed;
        }

        public void Apply(MaterialPropertyBlock block, int textureId, int shapeId, int lightId, float worldWidth,
            float edgeBlurWidth, float glow, float glowWidth)
        {
            var scale = Mathf.Max(0.01f, bandScale);
            if (texture) block.SetTexture(textureId, texture);
            block.SetVector(shapeId, new Vector4(worldWidth / scale, scrollSpeed / scale, edgeBlurWidth / scale, glowWidth / scale));
            block.SetVector(lightId, new Vector4(strength, glow, 0f, 0f));
        }
    }
}
