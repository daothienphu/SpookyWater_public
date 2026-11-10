using UnityEngine;

namespace SpookyWater
{
    public partial class WaterField
    {
        private static readonly int WaterWindId = Shader.PropertyToID("_WaterWind");
        private static readonly int WaterWindShapeId = Shader.PropertyToID("_WaterWindShape");
        private static readonly int WaterWindPhaseId = Shader.PropertyToID("_WaterWindPhase");

        private const float WindOctave1 = 0.47f;
        private const float WindOctave2 = 0.23f;
        private const float WindPhaseWrap = 4096f;

        private WindSettings _windSettings;
        private Vector4 _windPhase;

        public void SetWind(WindSettings settings)
        {
            _windSettings = settings;
            PublishWindState();
        }

        private void UpdateWind(float deltaTime)
        {
            var s = _windSettings;
            if (s == null || !s.enabled)
            {
                PublishVector(WaterWindId, Vector4.zero);
                return;
            }

            var speed = Mathf.Max(s.speed, 0f) * deltaTime;
            _windPhase.x = Mathf.Repeat(_windPhase.x + speed, WindPhaseWrap);
            _windPhase.y = Mathf.Repeat(_windPhase.y + speed * Mathf.Sqrt(WindOctave1), WindPhaseWrap);
            _windPhase.z = Mathf.Repeat(_windPhase.z + speed * Mathf.Sqrt(WindOctave2), WindPhaseWrap);
            _windPhase.w = Mathf.Repeat(_windPhase.w + Mathf.Max(s.gustSpeed, 0f) * deltaTime, WindPhaseWrap);
            PublishWindState();
        }

        private void PublishWindState()
        {
            if (!TryGetWindVectors(_windSettings, _windPhase, _surfaceY, out var wind, out var shape))
            {
                PublishVector(WaterWindId, Vector4.zero);
                return;
            }

            PublishVector(WaterWindId, wind);
            PublishVector(WaterWindShapeId, shape);
            PublishVector(WaterWindPhaseId, _windPhase);
        }

        public static void PublishWind(WindSettings s, Vector4 phase, float surfaceY)
        {
            if (!TryGetWindVectors(s, phase, surfaceY, out var wind, out var shape))
            {
                Shader.SetGlobalVector(WaterWindId, Vector4.zero);
                return;
            }

            Shader.SetGlobalVector(WaterWindId, wind);
            Shader.SetGlobalVector(WaterWindShapeId, shape);
            Shader.SetGlobalVector(WaterWindPhaseId, phase);
        }

        private static bool TryGetWindVectors(WindSettings s, Vector4 phase, float surfaceY, out Vector4 wind, out Vector4 shape)
        {
            wind = shape = Vector4.zero;
            if (s == null || !s.enabled || s.strength <= 0f || s.waveHeight <= 0f) return false;

            var angle = s.direction * Mathf.Deg2Rad;
            wind = new Vector4(s.waveHeight * s.strength, Mathf.Max(s.wavelength, 0.05f), Mathf.Clamp01(s.gustiness), surfaceY);
            shape = new Vector4(Mathf.Cos(angle), Mathf.Sin(angle), Mathf.Max(s.crestLength, 1f), Mathf.Max(s.gustSize, 0.1f));
            return true;
        }

        public static void ClearWind() => Shader.SetGlobalVector(WaterWindId, Vector4.zero);
    }
}
