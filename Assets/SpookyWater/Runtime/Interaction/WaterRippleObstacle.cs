using System.Collections.Generic;
using UnityEngine;

namespace SpookyWater
{
    public enum WaterRippleObstacleShape
    {
        Auto,
        Circle,
        Box
    }

    [AddComponentMenu("SpookyWater/Water Ripple Obstacle")]
    [HelpURL(SpookyWaterDocs.RippleObstacle)]
    public class WaterRippleObstacle : MonoBehaviour
    {
        private static readonly List<WaterRippleObstacle> ActiveObstacles = new();
        public static IReadOnlyList<WaterRippleObstacle> Active => ActiveObstacles;

        [Tooltip("Makes ripples bounce off this object where it cuts the water. Auto derives the footprint from this object's components: a 3D SphereCollider " +
                 "becomes the circle where it slices the water plane, a BoxCollider an oriented box, " +
                 "and a 2D sprite / Collider2D (standing upright in the XY plane) a thin strip across " +
                 "its width at its own Z. Circle/Box use the Size field below instead.")]
        [SerializeField] private WaterRippleObstacleShape _shape = WaterRippleObstacleShape.Auto;
        [Tooltip("Circle: x = diameter. Box: (x, z) size in this object's local space. Scaled by the transform. Ignored in Auto.")]
        [SerializeField] private Vector2 _size = Vector2.one;
        [Tooltip("Depth (world Z) of the footprint strip Auto derives for 2D sprites/colliders, which have no thickness of their own.")]
        [SerializeField, Min(0.01f)] private float _spriteThickness = 0.15f;
        [Tooltip("Grows (or shrinks, if negative) the footprint on every side, in world units.")]
        [SerializeField] private float _padding;

        private void OnEnable() => ActiveObstacles.Add(this);
        private void OnDisable() => ActiveObstacles.Remove(this);

        public bool TryGetFootprint(float waterY, out Vector4 a, out Vector4 b)
        {
            a = b = default;
            if (!SpansWaterPlane(waterY)) return false;

            bool found;
            switch (_shape)
            {
                case WaterRippleObstacleShape.Circle:
                    var scale = transform.lossyScale;
                    var radius = _size.x * 0.5f * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                    found = Circle(transform.position, radius, out a, out b);
                    break;
                case WaterRippleObstacleShape.Box:
                    var s = transform.lossyScale;
                    found = Box(transform.position, transform.right,
                        _size.x * 0.5f * Mathf.Abs(s.x), _size.y * 0.5f * Mathf.Abs(s.z), out a, out b);
                    break;
                default:
                    found = TryGetAutoFootprint(waterY, out a, out b);
                    break;
            }

            if (!found) return false;

            a.z = Mathf.Max(a.z + _padding, 0f);
            a.w = Mathf.Max(a.w + _padding, 0f);
            return a.z > 0f;
        }

        private bool TryGetAutoFootprint(float waterY, out Vector4 a, out Vector4 b)
        {
            a = b = default;

            if (TryGetComponent<SphereCollider>(out var sphere))
            {
                var center = transform.TransformPoint(sphere.center);
                var s = transform.lossyScale;
                var r = sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                var dy = waterY - center.y;
                if (Mathf.Abs(dy) >= r) return false;
                return Circle(center, Mathf.Sqrt(r * r - dy * dy), out a, out b);
            }

            if (TryGetComponent<BoxCollider>(out var box))
            {
                var s = transform.lossyScale;
                return Box(transform.TransformPoint(box.center), transform.right,
                    box.size.x * 0.5f * Mathf.Abs(s.x), box.size.z * 0.5f * Mathf.Abs(s.z), out a, out b);
            }

            if (TryGetComponent<Collider>(out var collider3D))
            {
                var bounds = collider3D.bounds;
                return Box(bounds.center, Vector3.right, bounds.extents.x, bounds.extents.z, out a, out b);
            }

            if (TryGet2DWaterlineSpan(waterY, out var minX, out var maxX))
            {
                return Box(new Vector3((minX + maxX) * 0.5f, 0f, transform.position.z), Vector3.right,
                    (maxX - minX) * 0.5f, _spriteThickness * 0.5f, out a, out b);
            }

            if (TryGetComponent<Renderer>(out var anyRenderer))
            {
                var bounds = anyRenderer.bounds;
                return Box(bounds.center, Vector3.right, bounds.extents.x, bounds.extents.z, out a, out b);
            }

            return false;
        }

        private bool TryGet2DWaterlineSpan(float waterY, out float minX, out float maxX)
        {
            minX = float.MaxValue;
            maxX = float.MinValue;

            Rect localRect;
            if (TryGetComponent<BoxCollider2D>(out var box2D))
            {
                localRect = new Rect(box2D.offset - box2D.size * 0.5f, box2D.size);
            }
            else if (TryGetComponent<SpriteRenderer>(out var sprite) && sprite.sprite)
            {
                var b = sprite.sprite.bounds;
                localRect = new Rect(b.min, b.size);
            }
            else if (TryGetComponent<Collider2D>(out var collider2D))
            {
                var b = collider2D.bounds;
                minX = b.min.x;
                maxX = b.max.x;
                return true;
            }
            else
            {
                return false;
            }

            var corners = new[]
            {
                transform.TransformPoint(new Vector3(localRect.xMin, localRect.yMin)),
                transform.TransformPoint(new Vector3(localRect.xMax, localRect.yMin)),
                transform.TransformPoint(new Vector3(localRect.xMax, localRect.yMax)),
                transform.TransformPoint(new Vector3(localRect.xMin, localRect.yMax))
            };

            for (var i = 0; i < 4; i++)
            {
                var p = corners[i];
                var q = corners[(i + 1) % 4];
                if ((p.y - waterY) * (q.y - waterY) > 0f) continue;
                var t = Mathf.Approximately(p.y, q.y) ? 0f : (waterY - p.y) / (q.y - p.y);
                var x = Mathf.Lerp(p.x, q.x, t);
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
            }

            return maxX >= minX;
        }

        private bool TryGet2DBounds(out Bounds bounds)
        {
            if (TryGetComponent<Collider2D>(out var collider2D))
            {
                bounds = collider2D.bounds;
                return true;
            }

            if (TryGetComponent<SpriteRenderer>(out var sprite))
            {
                bounds = sprite.bounds;
                return true;
            }

            bounds = default;
            return false;
        }

        private bool SpansWaterPlane(float waterY)
        {
            Bounds bounds;
            if (TryGetComponent<Collider>(out var collider3D)) bounds = collider3D.bounds;
            else if (TryGet2DBounds(out var bounds2D)) bounds = bounds2D;
            else if (TryGetComponent<Renderer>(out var anyRenderer)) bounds = anyRenderer.bounds;
            else return true;

            return bounds.min.y <= waterY && bounds.max.y >= waterY;
        }

        private static bool Circle(Vector3 center, float radius, out Vector4 a, out Vector4 b)
        {
            a = new Vector4(center.x, center.z, radius, radius);
            b = new Vector4(1f, 0f, 1f, 0f);
            return true;
        }

        private static bool Box(Vector3 center, Vector3 right, float halfX, float halfZ, out Vector4 a, out Vector4 b)
        {
            var axis = new Vector2(right.x, right.z);
            axis = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector2.right;
            a = new Vector4(center.x, center.z, halfX, halfZ);
            b = new Vector4(axis.x, axis.y, 0f, 0f);
            return true;
        }
    }
}
