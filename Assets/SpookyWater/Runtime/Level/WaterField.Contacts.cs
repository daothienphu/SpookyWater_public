using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpookyWater
{
    public partial class WaterField
    {
        public bool IsInsideObstacle(Vector2 worldXZ) => InsideAnyObstacle(worldXZ);

        private bool InsideAnyObstacle(Vector2 point)
        {
            var obstacles = WaterRippleObstacle.Active;
            for (var i = 0; i < obstacles.Count; i++)
            {
                if (!obstacles[i].TryGetFootprint(_surfaceY, out var a, out var b)) continue;

                var d = point - new Vector2(a.x, a.y);
                if (b.z > 0.5f)
                {
                    if (d.sqrMagnitude <= a.z * a.z) return true;
                    continue;
                }

                var axisX = new Vector2(b.x, b.y);
                var axisZ = new Vector2(-axisX.y, axisX.x);
                if (Mathf.Abs(Vector2.Dot(d, axisX)) <= a.z && Mathf.Abs(Vector2.Dot(d, axisZ)) <= a.w) return true;
            }

            return false;
        }

        public Vector2 AccumulateRippleKicks(Vector2 worldXZ, float fromTime, float toTime)
        {
            var kick = Vector2.zero;
            if (_settings == null || toTime <= fromTime) return kick;

            var speed = Mathf.Max(_settings.speed, 0.01f);
            var squash = Mathf.Clamp(_settings.zSquash, 0.1f, 1f);
            var distanceRate = -Mathf.Log(1f - Mathf.Clamp(_settings.distanceLoss, 0f, 0.99f)) * speed;
            var heightLossRate = 0.5f * (Mathf.Max(_settings.damping, 0f) + distanceRate);
            var point = new Vector2(worldXZ.x, worldXZ.y / squash);

            for (var i = 0; i < MaxRecentPushes; i++)
            {
                ref var push = ref _recentPushes[i];
                if (push.Volume <= 0f) continue;

                var ageNow = toTime - push.Time;
                if (ageNow <= 0f) continue;
                if (ageNow > MaxPushAge)
                {
                    push.Volume = 0f;
                    continue;
                }

                var a = new Vector2(push.From.x, push.From.y / squash);
                var b = new Vector2(push.To.x, push.To.y / squash);
                var ab = b - a;
                var t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
                var offset = point - (a + ab * t);
                var distance = offset.magnitude;

                var reach = Mathf.Max(distance - push.Radius, 0f);
                if (reach > speed * ageNow || reach <= speed * (fromTime - push.Time)) continue;

                var direction = distance > 1e-4f
                    ? new Vector2(offset.x, offset.y * squash).normalized
                    : new Vector2(Mathf.Cos(push.Time * 7919f), Mathf.Sin(push.Time * 7919f));
                var strength = push.Volume / Mathf.Sqrt(push.Radius * (push.Radius + reach))
                               * Mathf.Exp(-heightLossRate * ageNow);
                kick += direction * strength;
            }

            return kick;
        }

        private void PublishContacts()
        {
            _contactCandidates.Clear();
            _contactBounds.Clear();
            WaterReflectable.CollectWaterContacts(_surfaceY, _contactBounds);

            var obstacles = WaterRippleObstacle.Active;
            for (var i = 0; i < obstacles.Count; i++)
            {
                if (!obstacles[i].TryGetFootprint(_surfaceY, out var a, out var b)) continue;
                var extentX = b.z > 0.5f ? a.z : Mathf.Abs(b.x) * a.z + Mathf.Abs(b.y) * a.w;
                var extentZ = b.z > 0.5f ? a.z : Mathf.Abs(b.y) * a.z + Mathf.Abs(b.x) * a.w;
                _contactBounds.Add(new Bounds(new Vector3(a.x, _surfaceY, a.y), new Vector3(extentX, 0f, extentZ) * 2f));
            }

            var cam = ViewCamera;
            if (cam) GeometryUtility.CalculateFrustumPlanes(cam, _frustumPlanes);
            foreach (var bounds in _contactBounds)
            {
                if (cam && !GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds)) continue;
                _contactCandidates.Add(new Vector4(bounds.min.x, bounds.max.x, bounds.min.z, 0f));
            }
            if (_contactCandidates.Count > MaxContacts) _contactCandidates.Sort(NearestContactFirst);

            var count = Mathf.Min(_contactCandidates.Count, MaxContacts);
            for (var i = 0; i < count; i++) _contacts[i] = _contactCandidates[i];
            PublishVectorArray(WaterContactsId, _contacts);
            PublishFloat(WaterContactCountId, count);
        }

        private void UploadObstacles()
        {
            var count = 0;
            var obstacles = WaterRippleObstacle.Active;
            var minHalf = 0.75f * Mathf.Max(_simSize.x / _mask.width, _simSize.y / _mask.height);

            for (var i = 0; i < obstacles.Count; i++)
            {
                if (!obstacles[i].TryGetFootprint(_surfaceY, out var a, out var b)) continue;
                if (count == MaxObstacles)
                {
                    if (!_warnedObstacleOverflow)
                    {
                        Debug.LogWarning($"WaterField: more than {MaxObstacles} WaterRippleObstacles cut the water - extras are ignored.", this);
                        _warnedObstacleOverflow = true;
                    }
                    break;
                }

                a.z = Mathf.Max(a.z, minHalf);
                a.w = Mathf.Max(a.w, minHalf);
                _obstacleA[count] = a;
                _obstacleB[count] = b;
                count++;
            }

            _simMaterial.SetVectorArray(RippleObstacleAId, _obstacleA);
            _simMaterial.SetVectorArray(RippleObstacleBId, _obstacleB);
            _simMaterial.SetInt(RippleObstacleCountId, count);
        }
    }
}
