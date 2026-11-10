using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SpookyWater
{
    [ExecuteAlways]
    [AddComponentMenu("SpookyWater/Glow Light")]
    [HelpURL(SpookyWaterDocs.GlowLight)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light2D))]
    public class WaterGlowLight : MonoBehaviour
    {
        [Group("Above Water")]
        [Tooltip("Color of the glow in the air. HDR, so values above 1 feed URP Bloom on the character's light.")]
        [ColorUsage(false, true)]
        [SerializeField] private Color _color = new(0.85f, 0.95f, 1f);
        [Tooltip("Brightness of the glow in the air. 2D lights add on top of the Global Light 2D, so in a fully lit daytime scene keep this moderate (about 0.5-0.8) or the area around the character blows out to white.")]
        [SerializeField, Min(0f)] private float _intensity = 0.7f;
        [Tooltip("How far the glow reaches in the air, world units (the Light 2D's outer radius).")]
        [SerializeField, Min(0.01f)] private float _radius = 4f;

        [Group("Underwater")]
        [Tooltip("Color of the glow while this object is under the water. A cool, saturated tint reads as light scattered by the water.")]
        [ColorUsage(false, true)]
        [SerializeField] private Color _underwaterColor = new(0.55f, 0.85f, 1f);
        [Tooltip("Brightness of the glow under the water. Usually a little higher than in the air: underwater scenes are darker, so the character's light carries the scene. Added on top of the Global Light 2D; about 1 against a full-bright global light, up to 1-1.5 when the global light is dim.")]
        [SerializeField, Min(0f)] private float _underwaterIntensity = 1f;
        [Tooltip("How far the glow reaches under the water, world units.")]
        [SerializeField, Min(0.01f)] private float _underwaterRadius = 6.5f;
        [Tooltip("Seconds to blend between the air and underwater looks when crossing the surface. 0 = instant.")]
        [SerializeField, Min(0f)] private float _blendTime = 0.4f;

        [Group("Shape")]
        [Tooltip("Fraction of the radius that is fully bright before the falloff starts. 0 = the light starts fading right at the center.")]
        [SerializeField, Range(0f, 1f)] private float _core = 0.12f;
        [Tooltip("Falloff curve of the Light 2D. Lower = a long soft tail that lights the surroundings gently; higher = a tighter pool of light.")]
        [SerializeField, Range(0f, 1f)] private float _falloff = 0.45f;
        [Tooltip("How high above the sprites the light floats, world units (the Light 2D's Normal Map Distance). Lower = light grazes sprites and their auto normals show strong relief and rim light; higher = flatter, front-on lighting. Only affects sprites using a normal-map-aware material such as Sprite Lit Auto Normals.")]
        [SerializeField, Min(0.01f)] private float _lightHeight = 1.5f;
        [Tooltip("Brightness of the visible halo drawn around the light itself (the Light 2D's volume), as a fraction of its intensity. 0 = only lit sprites show the light.")]
        [SerializeField, Range(0f, 1f)] private float _halo = 0.08f;

        [Group("Lit Layers")]
        [Tooltip("Sorting layers this glow shines on (drives the Light 2D's Target Sorting Layers). Untick distant background layers (sky, far hills, their fog) so the character's light doesn't reach scenery far behind them; those keep only the Global Light 2D. Layers added later start ticked.")]
        [AllSortingLayers]
        [SerializeField] private SortingLayerFilter _litSortingLayers = SortingLayerFilter.Everything;

        [Group("Pulse")]
        [Tooltip("How much the glow breathes, as a fraction of its brightness and radius. 0 = steady.")]
        [SerializeField, Range(0f, 0.5f)] private float _pulseAmount = 0.06f;
        [Tooltip("Breaths per second.")]
        [SerializeField, Min(0f)] private float _pulseSpeed = 0.5f;

        private static readonly List<int> LayerIds = new();

        private Light2D _light;
        private float _underwater;
        private float _phase;

        public bool IsUnderwater => Water.IsUnderwater(transform.position);

        public float Intensity
        {
            get => _intensity;
            set => _intensity = Mathf.Max(0f, value);
        }

        public float UnderwaterIntensity
        {
            get => _underwaterIntensity;
            set => _underwaterIntensity = Mathf.Max(0f, value);
        }

        private Light2D Light => _light ? _light : _light = GetComponent<Light2D>();

        private void Reset()
        {
            var light = Light;
            light.lightType = Light2D.LightType.Point;
            light.shadowsEnabled = false;
            _litSortingLayers = FilterFromLight(light);
#if UNITY_EDITOR
            ApplyNormalSettings(light, _lightHeight);
#endif
            OnValidate();
        }

        private void OnEnable()
        {
            _phase = Random.value * 10f;
            _underwater = IsUnderwater ? 1f : 0f;
            ApplyLayers();
            Apply(0f);
        }

        private void OnValidate()
        {
            if (!Light) return;
            _underwater = IsUnderwater ? 1f : 0f;
            ApplyLayers();
            Apply(0f);
#if UNITY_EDITOR
            var light = Light;
            var height = _lightHeight;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (light) ApplyNormalSettings(light, height);
            };
#endif
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            var target = IsUnderwater ? 1f : 0f;
            _underwater = _blendTime > 0f ? Mathf.MoveTowards(_underwater, target, Time.deltaTime / _blendTime) : target;

            var pulse = _pulseAmount > 0f ? Mathf.Sin((Time.time + _phase) * _pulseSpeed * Mathf.PI * 2f) * _pulseAmount : 0f;
            Apply(pulse);
        }

        private void Apply(float pulse)
        {
            var light = Light;
            if (!light) return;

            var t = Mathf.SmoothStep(0f, 1f, _underwater);
            var radius = Mathf.Lerp(_radius, _underwaterRadius, t) * (1f + pulse * 0.5f);
            var intensity = Mathf.Lerp(_intensity, _underwaterIntensity, t) * (1f + pulse);

            light.color = Color.Lerp(_color, _underwaterColor, t);
            light.intensity = intensity;
            light.pointLightOuterRadius = radius;
            light.pointLightInnerRadius = radius * _core;
            light.falloffIntensity = _falloff;
            light.volumetricEnabled = _halo > 0f;
            light.volumeIntensity = _halo;
        }

        private void ApplyLayers()
        {
            var light = Light;
            if (!light) return;
            LayerIds.Clear();
            foreach (var layer in SortingLayer.layers)
                if (_litSortingLayers.Includes(layer.name)) LayerIds.Add(layer.id);

            var current = light.targetSortingLayers;
            if (current != null && current.Length == LayerIds.Count)
            {
                var same = true;
                for (var i = 0; i < current.Length && same; i++) same = LayerIds.Contains(current[i]);
                if (same) return;
            }

            light.targetSortingLayers = LayerIds.ToArray();
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(light);
#endif
        }

        private static SortingLayerFilter FilterFromLight(Light2D light)
        {
            var filter = SortingLayerFilter.Everything;
            var targets = light.targetSortingLayers;
            if (targets == null || targets.Length == 0) return filter;
            foreach (var layer in SortingLayer.layers)
                if (System.Array.IndexOf(targets, layer.id) < 0) filter.SetIncluded(layer.name, false);
            return filter;
        }

#if UNITY_EDITOR
        private static void ApplyNormalSettings(Light2D light, float height)
        {
            var so = new UnityEditor.SerializedObject(light);
            var quality = so.FindProperty("m_NormalMapQuality");
            var distance = so.FindProperty("m_NormalMapDistance");
            var changed = false;
            if (quality != null && quality.intValue == (int)Light2D.NormalMapQuality.Disabled)
            {
                quality.intValue = (int)Light2D.NormalMapQuality.Accurate;
                changed = true;
            }
            if (distance != null && !Mathf.Approximately(distance.floatValue, height))
            {
                distance.floatValue = height;
                changed = true;
            }
            if (changed) so.ApplyModifiedProperties();
        }
#endif
    }
}
