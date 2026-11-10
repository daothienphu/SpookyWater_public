using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpookyWater
{
    [AddComponentMenu("")]
    public partial class WaterField : MonoBehaviour
    {
        private const int MaxObstacles = 32;
        private const int MaxImpulses = 16;
        private const int MaxContacts = 64;
        private const int MaxStaticMaskResolution = 4096;
        private const int MinShiftTexels = 4;

        private const float MaxCourant2Sum = 0.8f;
        private const int MaxTicksPerFrame = 4;
        private const float WallBandTexels = 4f;

        private const int PassBuildMask = 0;
        private const int PassStep = 1;
        private const int PassImpulse = 2;
        private const int PassShift = 3;
        private const int PassFillWalls = 4;
        private const float MaxShoreDistance = 1000f;
        private const float Unreachable = 1e20f;

        private static readonly int MicroRipplesId = Shader.PropertyToID("_WaterMicroRipples");
        private static readonly int MicroRippleParamsId = Shader.PropertyToID("_WaterMicroRippleParams");
        private static readonly int RippleSimTexId = Shader.PropertyToID("_RippleSimTexture");
        private static readonly int RippleStaticMaskTexId = Shader.PropertyToID("_RippleStaticMaskTex");
        private static readonly int RippleMaskTexId = Shader.PropertyToID("_RippleMaskTex");
        private static readonly int RippleCourant2Id = Shader.PropertyToID("_RippleCourant2");
        private static readonly int RippleZSquashId = Shader.PropertyToID("_RippleZSquash");
        private static readonly int RippleStepDampingId = Shader.PropertyToID("_RippleStepDamping");
        private static readonly int RippleStepRelaxId = Shader.PropertyToID("_RippleStepRelax");
        private static readonly int RippleSpringStepId = Shader.PropertyToID("_RippleSpringStep");
        private static readonly int RippleObstacleAId = Shader.PropertyToID("_RippleObstacleA");
        private static readonly int RippleObstacleBId = Shader.PropertyToID("_RippleObstacleB");
        private static readonly int RippleObstacleCountId = Shader.PropertyToID("_RippleObstacleCount");
        private static readonly int RippleImpulsesId = Shader.PropertyToID("_RippleImpulses");
        private static readonly int RippleImpulseEndsId = Shader.PropertyToID("_RippleImpulseEnds");
        private static readonly int RippleImpulseCountId = Shader.PropertyToID("_RippleImpulseCount");
        private static readonly int WaterFieldOriginId = Shader.PropertyToID("_WaterFieldOrigin");
        private static readonly int WaterFieldSizeId = Shader.PropertyToID("_WaterFieldSize");
        private static readonly int RippleSimOriginId = Shader.PropertyToID("_RippleSimOrigin");
        private static readonly int RippleSimSizeId = Shader.PropertyToID("_RippleSimSize");
        private static readonly int RippleSpongeStepId = Shader.PropertyToID("_RippleSpongeStep");
        private static readonly int RippleWallBandId = Shader.PropertyToID("_RippleWallBand");
        private static readonly int RippleWallAbsorbStepId = Shader.PropertyToID("_RippleWallAbsorbStep");
        private static readonly int RippleShoreAbsorbId = Shader.PropertyToID("_RippleShoreAbsorb");
        private static readonly int RippleObstacleAbsorbId = Shader.PropertyToID("_RippleObstacleAbsorb");
        private static readonly int RippleStaticMaskOriginId = Shader.PropertyToID("_RippleStaticMaskOrigin");
        private static readonly int RippleStaticMaskSizeId = Shader.PropertyToID("_RippleStaticMaskSize");
        private static readonly int RippleWindowSpongeId = Shader.PropertyToID("_RippleWindowSponge");
        private static readonly int RippleWindowSpongeWidthId = Shader.PropertyToID("_RippleWindowSpongeWidth");
        private static readonly int RippleShiftUVId = Shader.PropertyToID("_RippleShiftUV");
        private static readonly int WaterShoreDistanceTexId = Shader.PropertyToID("_WaterShoreDistanceTex");
        private static readonly int WaterShoreDistanceRectId = Shader.PropertyToID("_WaterShoreDistanceRect");
        private static readonly int WaterContactsId = Shader.PropertyToID("_WaterContacts");
        private static readonly int WaterContactCountId = Shader.PropertyToID("_WaterContactCount");
        private static readonly Comparison<Vector4> NearestContactFirst = (a, b) => a.z.CompareTo(b.z);

        private const float DebugMarkerLifetime = 1.5f;

        private bool _verboseLogging;

        private float _surfaceY;

        private Vector2 _worldOrigin;
        private Vector2 _worldSize;
        private Vector2 _levelOrigin;
        private Vector2 _levelSize;
        private Vector2 _simOrigin;
        private Vector2 _simSize;
        private Vector2 _texelSize;
        private Vector2 _staticMaskOrigin;
        private Vector2 _staticMaskSize;
        private RippleGridSettings _settings;
        private AmbientRippleSettings _ambientSettings;
        private float _ambientTimer;
        private FrontRippleSettings _frontSettings;
        private float _frontTimer;
        private const int MaxMicroRipples = 16;
        private readonly Vector4[] _microRipples = new Vector4[MaxMicroRipples];
        private int _nextMicroRipple;
        private byte[] _staticMaskPixels;
        private Material _simMaterial;
        private RenderTexture _simA;
        private RenderTexture _simB;
        private RenderTexture _mask;
        private Texture2D _staticMask;
        private Texture2D _shoreDistance;
        private int _substeps = 1;
        private Vector4 _courant2;
        private float _stepDamping;
        private float _stepRelax;
        private float _springStep;
        private float _spongeStep;
        private float _wallBand;
        private float _wallAbsorbStep;
        private float _shoreAbsorb;
        private float _obstacleAbsorb;
        private float _tickAccumulator;

        private readonly Vector4[] _obstacleA = new Vector4[MaxObstacles];
        private readonly Vector4[] _obstacleB = new Vector4[MaxObstacles];
        private readonly Vector4[] _impulses = new Vector4[MaxImpulses];
        private readonly Vector4[] _impulseEnds = new Vector4[MaxImpulses];
        private int _impulseCount;
        private bool _warnedObstacleOverflow;
        private readonly Vector4[] _contacts = new Vector4[MaxContacts];
        private readonly List<Vector4> _contactCandidates = new();
        private readonly List<Bounds> _contactBounds = new();
        private readonly Plane[] _frustumPlanes = new Plane[6];
        private readonly List<(Vector3 worldPos, float time, bool up)> _debugRecentRipples = new();

        private const int MaxRecentPushes = 96;
        private const float MaxPushAge = 4f;
        private struct RipplePush
        {
            public Vector2 From;
            public Vector2 To;
            public float Radius;
            public float Volume;
            public float Time;
        }
        private readonly RipplePush[] _recentPushes = new RipplePush[MaxRecentPushes];
        private int _nextPush;
        private float[] _shoreDistancePixels;

        public Camera FocusCamera { get; set; }

        public Camera ViewCamera => FocusCamera ? FocusCamera : Water.Camera;

        public bool VerboseLogging
        {
            get => _verboseLogging;
            set => _verboseLogging = value;
        }

        public void Initialize(Vector2 worldOrigin, Vector2 worldSize,
            Material simulationMaterial, RippleGridSettings settings, AmbientRippleSettings ambientSettings,
            FrontRippleSettings frontSettings = null, bool verboseLogging = false)
        {
            _verboseLogging = verboseLogging;
            _frontSettings = frontSettings;
            _frontTimer = NextFrontInterval();
            _worldOrigin = worldOrigin;
            _worldSize = new Vector2(Mathf.Max(worldSize.x, 0.0001f), Mathf.Max(worldSize.y, 0.0001f));
            _settings = settings;
            _ambientSettings = ambientSettings;
            _ambientTimer = NextAmbientInterval();

            PublishVector(WaterFieldOriginId, _worldOrigin);
            PublishVector(WaterFieldSizeId, _worldSize);

            var openEdge = Mathf.Max(settings.openEdgeWidth, 0f);
            _levelOrigin = new Vector2(_worldOrigin.x, _worldOrigin.y - openEdge);
            _levelSize = new Vector2(_worldSize.x, _worldSize.y + openEdge * 2f);

            ReleaseSimulation();
            _simMaterial = new Material(simulationMaterial) { hideFlags = HideFlags.HideAndDontSave };

            var density = Mathf.Max(settings.texelsPerUnit, 1f);
            var densityZ = density / Mathf.Clamp(settings.zSquash, 0.1f, 1f);
            var maxRes = Mathf.Max(settings.maxResolution, 8);
            var width = Mathf.Clamp(Mathf.CeilToInt(_levelSize.x * density), 8, maxRes);
            var height = Mathf.Clamp(Mathf.CeilToInt(_levelSize.y * densityZ), 8, maxRes);
            _texelSize = new Vector2(1f / density, 1f / densityZ);
            _simSize = Vector2.Scale(new Vector2(width, height), _texelSize);
            _simOrigin = _levelOrigin;
            _simOrigin = TargetWindowOrigin();
            PublishWindow();

            _simA = CreateTarget("WaterField_RippleSimA", width, height, SimFormat());
            _simB = CreateTarget("WaterField_RippleSimB", width, height, SimFormat());
            _mask = CreateTarget("WaterField_RippleMask", width, height, MaskFormat());
            ClearTarget(_simA);
            ClearTarget(_simB);

            var maskDensity = Mathf.Max(settings.maskTexelsPerUnit, 1f);
            var maskWidth = Mathf.Clamp(Mathf.CeilToInt(_levelSize.x * maskDensity), 8, MaxStaticMaskResolution);
            var maskHeight = Mathf.Clamp(Mathf.CeilToInt(_levelSize.y * maskDensity), 8, MaxStaticMaskResolution);
            _staticMaskOrigin = _levelOrigin;
            _staticMaskSize = new Vector2(Mathf.Max(maskWidth / maskDensity, _levelSize.x), Mathf.Max(maskHeight / maskDensity, _levelSize.y));
            _staticMask = BakeStaticMask(maskWidth, maskHeight, null);
            BakeShoreDistance();
            UpdateStepParameters();

            BindMaterialState();
            UploadObstacles();
            Graphics.Blit(null, _mask, _simMaterial, PassBuildMask);
            PublishTexture(RippleSimTexId, _simA);

            if (_verboseLogging)
            {
                Debug.Log($"WaterField: initialized - world origin {_worldOrigin}, size {_worldSize}, level rect {new Rect(_levelOrigin, _levelSize)}, " +
                          $"simulation window {SimulationRect}, ripple grid {width}x{height} ({SimFormat()}), shoreline mask " +
                          $"{maskWidth}x{maskHeight}, {_substeps} substep(s) per {1f / settings.tickRate:F4}s tick.", this);
            }
        }

        public void SetSurfaceHeight(float worldY) => _surfaceY = worldY;

        public Rect SimulationRect => new(_simOrigin, _simSize);

        public Rect FieldRect => new(_worldOrigin, _worldSize);





        public void CreateContactRippleAt(Vector3 worldPosition, float initialStrength, bool initialUp = true,
            float radiusScale = 1f)
        {
            var signedHeight = (initialUp ? initialStrength : -initialStrength) * (_settings?.impactHeight ?? 1f);
            var radius = (_settings?.impactRadius ?? 0.2f) * radiusScale;
            QueueImpulse(worldPosition, worldPosition, radius, signedHeight);

            if (_verboseLogging)
            {
                var rawU = (worldPosition.x - _worldOrigin.x) / _worldSize.x;
                var rawV = (worldPosition.z - _worldOrigin.y) / _worldSize.y;
                var wasClamped = rawU is < 0f or > 1f || rawV is < 0f or > 1f;
                Debug.Log($"WaterField: ripple queued at world {worldPosition}" +
                          $"{(wasClamped ? " [OUTSIDE FIELD BOUNDS]" : "")}, strength {initialStrength:F2}, " +
                          $"{(initialUp ? "up" : "down")}.", this);
                _debugRecentRipples.Add((worldPosition, Time.time, initialUp));
            }
        }

        public void CreateContactRippleAlong(Vector3 from, Vector3 to, float initialStrength, bool initialUp = true,
            float radiusScale = 1f)
        {
            var signedHeight = (initialUp ? initialStrength : -initialStrength) * (_settings?.impactHeight ?? 1f);
            QueueImpulse(from, to, (_settings?.impactRadius ?? 0.2f) * radiusScale, signedHeight);
        }

        public void CreateWakeSegment(Vector3 from, Vector3 to, float strength = 1f, float bulge = 1f)
        {
            var radius = _settings?.wakeRadius ?? 0.15f;
            var length = Vector2.Distance(new Vector2(from.x, from.z), new Vector2(to.x, to.z));
            if (length <= 1e-5f) return;

            var height = (_settings?.wakeHeight ?? 1f) * strength * length / (length + radius * 1.57f);
            QueueImpulse(from, to, radius, -height);

            var direction = (to - from) / length;
            direction.y = 0f;
            var bow = to + direction.normalized * radius;
            var bowRatio = _settings?.wakeBowRatio ?? 0.6f;
            bowRatio *= Mathf.Max(bulge, 0f);
            if (bowRatio > 0f) QueueImpulse(bow, bow, radius, height * bowRatio);
        }

        private void QueueImpulse(Vector3 from, Vector3 to, float radius, float signedHeight)
        {
            if (_impulseCount == MaxImpulses)
            {
                Array.Copy(_impulses, 1, _impulses, 0, MaxImpulses - 1);
                Array.Copy(_impulseEnds, 1, _impulseEnds, 0, MaxImpulses - 1);
                _impulseCount--;
            }

            _impulses[_impulseCount] = new Vector4(from.x, from.z, radius, signedHeight);
            _impulseEnds[_impulseCount] = new Vector4(to.x, to.z, 0f, 0f);
            _impulseCount++;

            _recentPushes[_nextPush] = new RipplePush
            {
                From = new Vector2(from.x, from.z),
                To = new Vector2(to.x, to.z),
                Radius = Mathf.Max(radius, 1e-3f),
                Volume = Mathf.Abs(signedHeight) * radius * radius,
                Time = Time.time
            };
            _nextPush = (_nextPush + 1) % MaxRecentPushes;
        }

        private void Update()
        {
            if (!_local) Water.RefreshCamera();
            if (!_simMaterial || !_simA) return;

            PublishShoreDistance();
            PublishContacts();
            UpdateWindow();
            UpdateAmbientRipples();
            UpdateFrontRipples();
            UpdateBowWaves(Time.deltaTime);
            UpdateWind(Time.deltaTime);

            var tickDt = 1f / Mathf.Max(_settings.tickRate, 1f);
            _tickAccumulator = Mathf.Min(_tickAccumulator + Time.deltaTime, tickDt * MaxTicksPerFrame);
            if (_tickAccumulator < tickDt && _impulseCount == 0) return;

            UpdateStepParameters();
            BindMaterialState();
            UploadObstacles();
            Graphics.Blit(null, _mask, _simMaterial, PassBuildMask);

            if (_impulseCount > 0)
            {
                _simMaterial.SetVectorArray(RippleImpulsesId, _impulses);
                _simMaterial.SetVectorArray(RippleImpulseEndsId, _impulseEnds);
                _simMaterial.SetInt(RippleImpulseCountId, _impulseCount);
                Graphics.Blit(_simA, _simB, _simMaterial, PassImpulse);
                Swap();
                _impulseCount = 0;
            }

            while (_tickAccumulator >= tickDt)
            {
                _tickAccumulator -= tickDt;
                for (var i = 0; i < _substeps; i++)
                {
                    Graphics.Blit(_simA, _simB, _simMaterial, PassStep);
                    Swap();
                }
            }

            Graphics.Blit(_simA, _simB, _simMaterial, PassFillWalls);
            Swap();

            PublishTexture(RippleSimTexId, _simA);
        }

        private void UpdateWindow()
        {
            var target = TargetWindowOrigin();
            var shift = new Vector2Int(
                Mathf.RoundToInt((target.x - _simOrigin.x) / _texelSize.x),
                Mathf.RoundToInt((target.y - _simOrigin.y) / _texelSize.y));
            if (Mathf.Abs(shift.x) < MinShiftTexels && Mathf.Abs(shift.y) < MinShiftTexels) return;

            _simOrigin = target;
            PublishWindow();

            _simMaterial.SetVector(RippleShiftUVId, new Vector4((float)shift.x / _simA.width, (float)shift.y / _simA.height));
            Graphics.Blit(_simA, _simB, _simMaterial, PassShift);
            Swap();
            PublishTexture(RippleSimTexId, _simA);
        }

        private Vector2 TargetWindowOrigin()
        {
            var origin = _simOrigin;
            var cam = ViewCamera;
            if (cam)
            {
                GetViewFootprint(cam, out var min, out var max);
                var spongeWidth = Mathf.Max(_settings.openEdgeWidth, 0f);
                origin = new Vector2((min.x + max.x - _simSize.x) * 0.5f, min.y - spongeWidth);
            }

            var maxOrigin = _levelOrigin + _levelSize - _simSize;
            origin.x = Mathf.Clamp(origin.x, _levelOrigin.x, Mathf.Max(maxOrigin.x, _levelOrigin.x));
            origin.y = Mathf.Clamp(origin.y, _levelOrigin.y, Mathf.Max(maxOrigin.y, _levelOrigin.y));
            return _levelOrigin + Vector2.Scale(new Vector2(
                Mathf.Round((origin.x - _levelOrigin.x) / _texelSize.x),
                Mathf.Round((origin.y - _levelOrigin.y) / _texelSize.y)), _texelSize);
        }

        private void GetViewFootprint(Camera cam, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);

            var plane = new Plane(Vector3.up, new Vector3(0f, _surfaceY, 0f));
            for (var i = 0; i < 4; i++)
            {
                var ray = cam.ViewportPointToRay(new Vector3(i & 1, i >> 1, 0f));
                var point = plane.Raycast(ray, out var distance) && distance <= cam.farClipPlane
                    ? ray.GetPoint(distance)
                    : ray.origin;
                var xz = new Vector2(point.x, point.z);
                min = Vector2.Min(min, xz);
                max = Vector2.Max(max, xz);
            }
        }

        private void PublishWindow()
        {
            PublishVector(RippleSimOriginId, _simOrigin);
            PublishVector(RippleSimSizeId, _simSize);
        }









        private void OnDestroy()
        {
            ReleaseSimulation();
            PublishVector(MicroRippleParamsId, Vector4.zero);
            if (!_local) ClearWind();
        }

        private void Swap() => (_simA, _simB) = (_simB, _simA);

        private void UpdateStepParameters()
        {
            var tickDt = 1f / Mathf.Max(_settings.tickRate, 1f);
            var dx = _simSize.x / _mask.width;
            var dz = _simSize.y / _mask.height;
            var speed = Mathf.Max(_settings.speed, 0f);

            var cx2Tick = Mathf.Pow(speed * tickDt / dx, 2f);
            var cz2Tick = Mathf.Pow(speed * ZSquash * tickDt / dz, 2f);
            var spring2Tick = Mathf.Pow(2f * Mathf.PI * Mathf.Max(_settings.springFrequency, 0f) * tickDt, 2f);
            _substeps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt((cx2Tick + cz2Tick + 0.25f * spring2Tick) / MaxCourant2Sum)));

            var n2 = (float)_substeps * _substeps;
            var stepDt = tickDt / _substeps;
            _courant2 = new Vector4(cx2Tick / n2, cz2Tick / n2, 0f, 0f);
            _springStep = spring2Tick / n2;

            var distanceRate = -Mathf.Log(1f - Mathf.Clamp(_settings.distanceLoss, 0f, 0.99f)) * speed;
            var lossRate = Mathf.Max(_settings.damping, 0f) + distanceRate;
            _stepDamping = Mathf.Exp(-lossRate * stepDt);
            _stepRelax = Mathf.Exp(-0.25f * lossRate * stepDt);

            var openEdge = Mathf.Max(_settings.openEdgeWidth, 1e-3f);
            _spongeStep = 12f * speed / openEdge * stepDt;

            _wallBand = WallBandTexels * Mathf.Max(dx, dz);
            _wallAbsorbStep = 9f * speed / _wallBand * stepDt;
            _shoreAbsorb = ReflectivityToAbsorption(_settings.shoreReflectivity);
            _obstacleAbsorb = ReflectivityToAbsorption(_settings.obstacleReflectivity);
        }

        private static float ReflectivityToAbsorption(float reflectivity)
        {
            return Mathf.Clamp01(-Mathf.Log(Mathf.Clamp(reflectivity, 1e-4f, 1f)) / 12f);
        }

        private float ZSquash => Mathf.Clamp(_settings?.zSquash ?? 1f, 0.1f, 1f);

        private void BindMaterialState()
        {
            _simMaterial.SetTexture(RippleStaticMaskTexId, _staticMask);
            _simMaterial.SetTexture(RippleMaskTexId, _mask);
            _simMaterial.SetVector(RippleCourant2Id, _courant2);
            _simMaterial.SetFloat(RippleZSquashId, ZSquash);
            _simMaterial.SetFloat(RippleStepDampingId, _stepDamping);
            _simMaterial.SetFloat(RippleStepRelaxId, _stepRelax);
            _simMaterial.SetFloat(RippleSpringStepId, _springStep);
            _simMaterial.SetFloat(RippleSpongeStepId, _spongeStep);
            _simMaterial.SetFloat(RippleWallBandId, _wallBand);
            _simMaterial.SetFloat(RippleWallAbsorbStepId, _wallAbsorbStep);
            _simMaterial.SetFloat(RippleShoreAbsorbId, _shoreAbsorb);
            _simMaterial.SetFloat(RippleObstacleAbsorbId, _obstacleAbsorb);
            _simMaterial.SetVector(RippleStaticMaskOriginId, _staticMaskOrigin);
            _simMaterial.SetVector(RippleStaticMaskSizeId, _staticMaskSize);

            var epsilon = Mathf.Min(_texelSize.x, _texelSize.y) * 0.5f;
            var windowMax = _simOrigin + _simSize;
            var levelMax = _levelOrigin + _levelSize;
            _simMaterial.SetVector(RippleWindowSpongeId, new Vector4(
                _simOrigin.x > _levelOrigin.x + epsilon ? 1f : 0f,
                windowMax.x < levelMax.x - epsilon ? 1f : 0f,
                _simOrigin.y > _levelOrigin.y + epsilon ? 1f : 0f,
                windowMax.y < levelMax.y - epsilon ? 1f : 0f));
            var spongeWidth = Mathf.Max(_settings.openEdgeWidth, 1e-3f);
            _simMaterial.SetVector(RippleWindowSpongeWidthId, new Vector4(spongeWidth / _simSize.x, spongeWidth / _simSize.y));
        }









        private static RenderTextureFormat MaskFormat()
        {
            return SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RG16)
                ? RenderTextureFormat.RG16
                : RenderTextureFormat.ARGB32;
        }

        private static RenderTextureFormat SimFormat()
        {
            return SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGFloat)
                ? RenderTextureFormat.RGFloat
                : RenderTextureFormat.RGHalf;
        }

        private static RenderTexture CreateTarget(string name, int width, int height, RenderTextureFormat format)
        {
            var rt = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            rt.Create();
            return rt;
        }

        private static void ClearTarget(RenderTexture rt)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = previous;
        }

        private void ReleaseSimulation()
        {
            DestroyTarget(ref _simA);
            DestroyTarget(ref _simB);
            DestroyTarget(ref _mask);
            if (_staticMask) DestroyImmediate(_staticMask);
            if (_shoreDistance) DestroyImmediate(_shoreDistance);
            if (_simMaterial) DestroyImmediate(_simMaterial);
            _staticMask = null;
            _shoreDistance = null;
            _shoreDistancePixels = null;
            _simMaterial = null;
            PublishFloat(WaterContactCountId, 0);
            ClearBowWaves();
        }

        private static void DestroyTarget(ref RenderTexture rt)
        {
            if (!rt) return;
            rt.Release();
            DestroyImmediate(rt);
            rt = null;
        }

        private void OnDrawGizmos()
        {
            if (_worldSize == Vector2.zero) return;

            var center = new Vector3(_worldOrigin.x + _worldSize.x * 0.5f, _surfaceY, _worldOrigin.y + _worldSize.y * 0.5f);
            var size = new Vector3(_worldSize.x, 0f, _worldSize.y);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(center, size);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(new Vector3(_simOrigin.x + _simSize.x * 0.5f, _surfaceY, _simOrigin.y + _simSize.y * 0.5f),
                new Vector3(_simSize.x, 0f, _simSize.y));

            if (!_verboseLogging) return;

            for (var i = _debugRecentRipples.Count - 1; i >= 0; i--)
            {
                var (worldPos, time, up) = _debugRecentRipples[i];
                var age = Time.time - time;
                if (age > DebugMarkerLifetime)
                {
                    _debugRecentRipples.RemoveAt(i);
                    continue;
                }

                var fade = 1f - age / DebugMarkerLifetime;
                var color = up ? Color.red : Color.blue;
                color.a = fade;
                Gizmos.color = color;
                Gizmos.DrawSphere(worldPos, 0.15f * fade + 0.05f);
            }
        }
    }
}

