using UnityEngine;

namespace SpookyWater
{
    [AddComponentMenu("SpookyWater/Water Fog Sprite")]
    [HelpURL(SpookyWaterDocs.FogSprite)]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class WaterFogSprite : MonoBehaviour
    {
        public enum WaterSide
        {
            Everywhere = 0,
            AboveWaterOnly = 1,
            BelowWaterOnly = 2
        }

        [Tooltip("Which side of the water surface the fog is kept on. Above Water Only is mist that should not show through the water's front glass; Below Water Only is murk that should not rise into the air. The sprite needs the Sprite Fog material (Materials/Sprites/Fog). Never add Water Reflectable to a fog sprite: its mirrored copy starts abruptly at the water and draws a hard line across the reflection.")]
        [SerializeField] private WaterSide _side = WaterSide.Everywhere;
        [Tooltip("World units over which the fog fades out toward the waterline, on the kept side only so nothing leaks past it. The cut is a flat plane while the waves bob around it, so raise this if waves visibly cut the fog. 0 = hard edge.")]
        [Min(0f)] [SerializeField] private float _edgeSoftness = 0.1f;
        [Tooltip("Use Waterline Y below instead of the water surface height found in the scene.")]
        [SerializeField] private bool _overrideWaterline;
        [Tooltip("World Y of the cut, used when Override Waterline is on or when the scene has no water yet.")]
        [SerializeField] private float _waterlineY;
        [Tooltip("Below Water Only: ignore this sprite's color and use the water's underwater murk, as thick as the water between this sheet and the next nearer matching sheet. Put one in front of each background band and they add up to the same fade as the far water surface.")]
        [SerializeField] private bool _matchWaterMurk;

        private static readonly int ClipModeId = Shader.PropertyToID("_WaterClipMode");
        private static readonly int ClipYId = Shader.PropertyToID("_WaterClipY");
        private static readonly int ClipSoftnessId = Shader.PropertyToID("_WaterClipSoftness");
        private static readonly int MurkMatchId = Shader.PropertyToID("_WaterMurkMatch");
        private static readonly int MurkFromZId = Shader.PropertyToID("_WaterMurkFromZ");
        private const float NoMurkFromZ = -100000f;

        private static readonly System.Collections.Generic.List<WaterFogSprite> Active = new();

        private SpriteRenderer _renderer;
        private MaterialPropertyBlock _block;
        private float _appliedY = float.NaN;
        private float _appliedFromZ = float.NaN;
        private bool _warnedShader;

        public WaterSide Side
        {
            get => _side;
            set
            {
                _side = value;
                Apply();
            }
        }

        private bool MatchesMurk => _matchWaterMurk && _side == WaterSide.BelowWaterOnly;

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
            _appliedY = float.NaN;
            Apply();
        }

        private void OnDisable()
        {
            Active.Remove(this);
            if (!_renderer) return;
            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(ClipModeId, 0f);
            _block.SetFloat(MurkMatchId, 0f);
            _renderer.SetPropertyBlock(_block);
        }

        private void OnValidate()
        {
            _appliedY = float.NaN;
            if (isActiveAndEnabled) Apply();
        }

        private void LateUpdate()
        {
            if (_side == WaterSide.Everywhere) return;
            var y = WaterlineY();
            if (!Mathf.Approximately(y, _appliedY) || (MatchesMurk && !Mathf.Approximately(MurkFromZ(), _appliedFromZ))) Apply();
        }

        private void Apply()
        {
            if (!_renderer) _renderer = GetComponent<SpriteRenderer>();
            if (!_renderer) return;

            if (_side != WaterSide.Everywhere && !_warnedShader && _renderer.sharedMaterial &&
                !_renderer.sharedMaterial.HasFloat(ClipModeId))
            {
                _warnedShader = true;
                Debug.LogWarning($"[WaterFogSprite] '{name}' uses material '{_renderer.sharedMaterial.name}', which isn't " +
                                 "SpookyWater/Sprite Fog - it won't be limited to one side of the water. Assign the Fog material.", this);
            }

            _appliedY = WaterlineY();
            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(ClipModeId, (float)_side);
            _block.SetFloat(ClipYId, _appliedY);
            _block.SetFloat(ClipSoftnessId, _edgeSoftness);
            _appliedFromZ = MatchesMurk ? MurkFromZ() : NoMurkFromZ;
            _block.SetFloat(MurkMatchId, MatchesMurk ? 1f : 0f);
            _block.SetFloat(MurkFromZId, _appliedFromZ);
            _renderer.SetPropertyBlock(_block);
        }

        private float MurkFromZ()
        {
            var z = transform.position.z;
            var fromZ = NoMurkFromZ;
            foreach (var other in Active)
            {
                if (other == this || !other.MatchesMurk) continue;
                var otherZ = other.transform.position.z;
                if (otherZ < z && otherZ > fromZ) fromZ = otherZ;
            }

            return fromZ;
        }

        private float WaterlineY()
        {
            if (_overrideWaterline) return _waterlineY;
            return Water.TryGetWaterlineY(out var y) ? y : _waterlineY;
        }

    }
}
