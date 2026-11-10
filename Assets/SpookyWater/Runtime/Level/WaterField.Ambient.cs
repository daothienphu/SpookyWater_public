using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpookyWater
{
    public partial class WaterField
    {
        private void UpdateAmbientRipples()
        {
            var s = _ambientSettings;
            if (s == null || !s.enabled) return;

            _ambientTimer -= Time.deltaTime;
            if (_ambientTimer > 0f) return;
            _ambientTimer = NextAmbientInterval();

            if (!TryPickOpenWater(null, out var centre)) return;

            var count = UnityEngine.Random.Range(Mathf.Max(s.count.x, 1), Mathf.Max(s.count.y, s.count.x, 1) + 1);
            for (var i = 0; i < count; i++)
            {
                var spot = centre;
                if (i > 0 && !TryPickOpenWater(centre, out spot)) continue;

                var radius = UnityEngine.Random.Range(s.radius.x, s.radius.y);
                var height = UnityEngine.Random.Range(s.strength.x, s.strength.y);
                if (UnityEngine.Random.value < s.downChance) height = -height;
                QueueImpulse(new Vector3(spot.x, _surfaceY, spot.y), new Vector3(spot.x, _surfaceY, spot.y), Mathf.Max(radius, 1e-3f), height);
            }
        }

        private void UpdateFrontRipples()
        {
            var s = _frontSettings;
            if (s == null || !s.enabled)
            {
                PublishVector(MicroRippleParamsId, Vector4.zero);
                return;
            }

            _frontTimer -= Time.deltaTime;
            if (_frontTimer <= 0f)
            {
                _frontTimer = NextFrontInterval();
                if (TryPickFrontSpot(s.maxDistance, out var spot))
                {
                    var height = UnityEngine.Random.Range(s.strength.x, s.strength.y);
                    if (UnityEngine.Random.value < 0.5f) height = -height;
                    _microRipples[_nextMicroRipple] = new Vector4(spot.x, spot.y, Time.timeSinceLevelLoad, height);
                    _nextMicroRipple = (_nextMicroRipple + 1) % MaxMicroRipples;
                }
            }

            PublishVectorArray(MicroRipplesId, _microRipples);
            PublishVector(MicroRippleParamsId, new Vector4(Mathf.Max(s.speed, 0.01f),
                Mathf.Max(s.wavelength, 0.01f), Mathf.Max(s.maxRadius, 0.01f), Mathf.Max(s.lifetime, 0.01f)));
        }

        private bool TryPickFrontSpot(float maxDistance, out Vector2 spot)
        {
            var cam = ViewCamera;
            var minX = _worldOrigin.x;
            var maxX = _worldOrigin.x + _worldSize.x;
            if (cam)
            {
                var depth = _worldOrigin.y - cam.transform.position.z;
                if (depth > 0f)
                {
                    minX = Mathf.Max(minX, cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, depth)).x);
                    maxX = Mathf.Min(maxX, cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, depth)).x);
                }
            }
            return TryPickOpenWaterIn(new Vector2(minX, _worldOrigin.y),
                new Vector2(maxX - minX, Mathf.Min(Mathf.Max(maxDistance, 0f), _worldSize.y)), out spot);
        }

        private float NextFrontInterval()
        {
            var s = _frontSettings;
            if (s == null) return 1f;
            return Mathf.Max(UnityEngine.Random.Range(s.interval.x, s.interval.y), 0.02f);
        }

        private float NextAmbientInterval()
        {
            var s = _ambientSettings;
            if (s == null) return 1f;
            return Mathf.Max(UnityEngine.Random.Range(s.interval.x, s.interval.y), 0.02f);
        }

        private bool TryPickOpenWater(Vector2? near, out Vector2 spot)
        {
            return TryPickOpenWaterIn(_worldOrigin, _worldSize, out spot, near);
        }

        private bool TryPickOpenWaterIn(Vector2 rectMin, Vector2 rectSize, out Vector2 spot, Vector2? near = null)
        {
            spot = default;
            if (_staticMaskPixels == null) return false;

            var areaMin = Vector2.Max(Vector2.Max(_worldOrigin, _simOrigin), rectMin);
            var areaSize = Vector2.Min(Vector2.Min(_worldOrigin + _worldSize, _simOrigin + _simSize), rectMin + rectSize) - areaMin;
            if (areaSize.x <= 0f || areaSize.y <= 0f) return false;

            var width = _staticMask.width;
            var height = _staticMask.height;
            for (var attempt = 0; attempt < 12; attempt++)
            {
                spot = near.HasValue
                    ? near.Value + UnityEngine.Random.insideUnitCircle * Mathf.Max(_ambientSettings.clusterSpread, 0f)
                    : areaMin + new Vector2(UnityEngine.Random.value * areaSize.x, UnityEngine.Random.value * areaSize.y);

                var x = Mathf.FloorToInt((spot.x - _staticMaskOrigin.x) / _staticMaskSize.x * width);
                var y = Mathf.FloorToInt((spot.y - _staticMaskOrigin.y) / _staticMaskSize.y * height);
                if (x < 0 || y < 0 || x >= width || y >= height) continue;

                var i = (y * width + x) * 2;
                if (_staticMaskPixels[i] < 128 || _staticMaskPixels[i + 1] > 0) continue;
                if (InsideAnyObstacle(spot)) continue;
                return true;
            }

            return false;
        }
    }
}
