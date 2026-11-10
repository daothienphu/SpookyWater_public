using UnityEngine;

namespace SpookyWater
{
    public struct WaterBowWaveShape
    {
        public float Height;
        public float Length;
        public float Depth;
        public float Direction;
        public float FrontWidth;
        public float Trough;
    }

    public partial class WaterField
    {
        private const int MaxBowWaves = 8;
        private const float OrphanedBowWaveTime = 0.25f;
        private const float BowWaveSpreadOnRelease = 0.6f;
        private const float BowWaveShoreFadeBoost = 4f;

        private static readonly int BowWavesId = Shader.PropertyToID("_WaterBowWaves");
        private static readonly int BowWaveShapesId = Shader.PropertyToID("_WaterBowWaveShapes");
        private static readonly int BowWaveCountId = Shader.PropertyToID("_WaterBowWaveCount");

        private struct BowWave
        {
            public bool Active;
            public bool Released;
            public int Generation;
            public Vector2 Position;
            public float Velocity;
            public WaterBowWaveShape Shape;
            public float ReleaseHeight;
            public float ReleaseLength;
            public float Age;
            public float FadeTime;
            public float Drag;
            public float LastTouched;
        }

        private readonly BowWave[] _bowWaves = new BowWave[MaxBowWaves];
        private readonly Vector4[] _bowWaveData = new Vector4[MaxBowWaves];
        private readonly Vector4[] _bowWaveShapeData = new Vector4[MaxBowWaves];
        private int _bowWaveGeneration;

        public int BeginBowWave()
        {
            var slot = -1;
            var oldestAge = float.MinValue;
            for (var i = 0; i < MaxBowWaves; i++)
            {
                if (!_bowWaves[i].Active)
                {
                    slot = i;
                    break;
                }

                if (_bowWaves[i].Released && _bowWaves[i].Age > oldestAge)
                {
                    oldestAge = _bowWaves[i].Age;
                    slot = i;
                }
            }
            if (slot < 0) return -1;

            _bowWaveGeneration = (_bowWaveGeneration + 1) & 0xFFFFF;
            _bowWaves[slot] = new BowWave
            {
                Active = true,
                Generation = _bowWaveGeneration,
                LastTouched = Time.time
            };
            return _bowWaveGeneration * MaxBowWaves + slot;
        }

        public bool UpdateBowWave(int handle, Vector2 positionXZ, float velocityX, WaterBowWaveShape shape)
        {
            if (!TryGetOwnedBowWave(handle, out var slot)) return false;

            ref var wave = ref _bowWaves[slot];
            wave.Position = positionXZ;
            wave.Velocity = velocityX;
            wave.Shape = shape;
            wave.LastTouched = Time.time;
            PublishBowWaves();
            return true;
        }

        public void ReleaseBowWave(int handle, float carrySpeed, float fadeTime, float drag)
        {
            if (!TryGetOwnedBowWave(handle, out var slot)) return;

            ref var wave = ref _bowWaves[slot];
            Release(ref wave, fadeTime, drag);
            wave.Velocity = carrySpeed;
        }

        private bool TryGetOwnedBowWave(int handle, out int slot)
        {
            slot = handle < 0 ? -1 : handle % MaxBowWaves;
            if (slot < 0) return false;
            ref var wave = ref _bowWaves[slot];
            return wave.Active && !wave.Released && wave.Generation == handle / MaxBowWaves;
        }

        private static void Release(ref BowWave wave, float fadeTime, float drag)
        {
            wave.Released = true;
            wave.Age = 0f;
            wave.FadeTime = Mathf.Max(fadeTime, 0.05f);
            wave.Drag = Mathf.Max(drag, 0f);
            wave.ReleaseHeight = wave.Shape.Height;
            wave.ReleaseLength = wave.Shape.Length;
        }

        private void UpdateBowWaves(float deltaTime)
        {
            for (var i = 0; i < MaxBowWaves; i++)
            {
                ref var wave = ref _bowWaves[i];
                if (!wave.Active) continue;

                if (!wave.Released)
                {
                    if (Time.time - wave.LastTouched > OrphanedBowWaveTime) Release(ref wave, 0.6f, 2f);
                    continue;
                }

                wave.Velocity *= Mathf.Exp(-wave.Drag * deltaTime);
                wave.Position.x += wave.Velocity * deltaTime;

                var lead = wave.Position + new Vector2(wave.Shape.Direction * wave.Shape.Length * 0.5f, 0f);
                var nearShore = SampleShoreDistance(lead) < wave.Shape.Length * 0.25f;
                wave.Age += deltaTime * (nearShore ? BowWaveShoreFadeBoost : 1f);

                var t = wave.Age / wave.FadeTime;
                if (t >= 1f)
                {
                    wave.Active = false;
                    continue;
                }

                var remaining = 1f - t;
                wave.Shape.Height = wave.ReleaseHeight * remaining * remaining;
                wave.Shape.Length = wave.ReleaseLength * (1f + BowWaveSpreadOnRelease * t);
            }

            PublishBowWaves();
        }

        private void PublishBowWaves()
        {
            var count = 0;
            for (var i = 0; i < MaxBowWaves; i++)
            {
                ref var wave = ref _bowWaves[i];
                if (!wave.Active || wave.Shape.Height == 0f) continue;

                var s = wave.Shape;
                _bowWaveData[count] = new Vector4(wave.Position.x, wave.Position.y, s.Height, Mathf.Max(s.Length, 1e-3f));
                _bowWaveShapeData[count] = new Vector4(s.Direction < 0f ? -1f : 1f, Mathf.Max(s.Depth, 1e-3f),
                    Mathf.Clamp(s.FrontWidth, 0.05f, 1f), Mathf.Max(s.Trough, 0f));
                count++;
            }

            PublishVectorArray(BowWavesId, _bowWaveData);
            PublishVectorArray(BowWaveShapesId, _bowWaveShapeData);
            PublishFloat(BowWaveCountId, count);
        }

        private void ClearBowWaves()
        {
            for (var i = 0; i < MaxBowWaves; i++) _bowWaves[i].Active = false;
            PublishFloat(BowWaveCountId, 0);
        }
    }
}
