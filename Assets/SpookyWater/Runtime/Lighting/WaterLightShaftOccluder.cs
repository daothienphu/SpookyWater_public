using System.Collections.Generic;
using UnityEngine;

namespace SpookyWater
{
    public enum WaterLightShaftOccluderShape
    {
        Auto,
        Box,
        Circle
    }

    [ExecuteAlways]
    [AddComponentMenu("SpookyWater/Light Shaft Occluder")]
    [HelpURL(SpookyWaterDocs.LightShaftOccluder)]
    [DisallowMultipleComponent]
    public class WaterLightShaftOccluder : MonoBehaviour
    {
        private static readonly List<WaterLightShaftOccluder> ActiveOccluders = new();
        public static IReadOnlyList<WaterLightShaftOccluder> Active => ActiveOccluders;

        private readonly Vector2[] _corners = new Vector2[4];
        private readonly Vector2[] _projected = new Vector2[4];

        [Group("Shape")]
        [Tooltip("Blocks Water Light Shafts: everything below this object along a shaft's angle is in its shadow, like a floating log or a rock leaving a dark streak in the light. Auto uses this object's Collider2D (Box and Circle exactly, other colliders by their bounds), or its sprite's bounds when there is no collider. Box and Circle use Size and Offset below.")]
        [SerializeField] private WaterLightShaftOccluderShape _shape = WaterLightShaftOccluderShape.Auto;
        [Tooltip("Box: width and height in this object's local space. Circle: x = diameter. Scaled and rotated by the transform. Ignored in Auto.")]
        [SerializeField] private Vector2 _size = Vector2.one;
        [Tooltip("Centre of the Box or Circle in this object's local space. Ignored in Auto.")]
        [SerializeField] private Vector2 _offset;
        [Tooltip("Grows (or shrinks, if negative) the shadow on every side, world units.")]
        [SerializeField] private float _padding;

        [Group("Shadow")]
        [Tooltip("How dark the shadow is. 1 = no light at all, 0 = no shadow.")]
        [SerializeField, Range(0f, 1f)] private float _strength = 0.85f;
        [Tooltip("Blur of the shadow's edges right at the object, world units.")]
        [SerializeField, Min(0f)] private float _softness = 0.3f;
        [Tooltip("How much blurrier the edges get per world unit below the object, so the shadow spreads and softens with distance like a real penumbra. 0 = equally sharp all the way down.")]
        [SerializeField, Range(0f, 1f)] private float _softnessGrowth = 0.1f;
        [Tooltip("How far below the object the shadow reaches before it has faded out, world units along the shaft. 0 = all the way to the bottom of the shaft.")]
        [SerializeField, Min(0f)] private float _shadowLength;

        public float Strength
        {
            get => _strength;
            set => _strength = Mathf.Clamp01(value);
        }

        private void OnEnable() => ActiveOccluders.Add(this);
        private void OnDisable() => ActiveOccluders.Remove(this);

        public bool TryProject(Vector2 down, Vector2 across, out Vector4 interval, out Vector4 outline, out Vector4 shadow)
        {
            interval = outline = shadow = default;
            if (_strength <= 0f) return false;

            float min, max, circle = 0f;
            if (TryGetCircle(out var center, out var radius))
            {
                radius = Mathf.Max(radius + _padding, 1e-4f);
                var s = Vector2.Dot(center, across);
                min = s - radius;
                max = s + radius;
                circle = radius;
                outline = new Vector4(s, Vector2.Dot(center, down), 0f, 0f);
            }
            else if (TryGetCorners())
            {
                int left = 0, right = 0, top = 0;
                for (var i = 0; i < 4; i++)
                {
                    _projected[i] = new Vector2(Vector2.Dot(_corners[i], across), Vector2.Dot(_corners[i], down));
                }

                for (var i = 1; i < 4; i++)
                {
                    var p = _projected[i];
                    if (Before(p.x, p.y, _projected[left].x, _projected[left].y)) left = i;
                    if (Before(-p.x, p.y, -_projected[right].x, _projected[right].y)) right = i;
                    if (p.y < _projected[top].y) top = i;
                }

                var apex = _projected[top];
                min = _projected[left].x - _padding;
                max = _projected[right].x + _padding;
                outline = new Vector4(apex.x, apex.y - _padding, Slope(_projected[left], apex, -1e4f), Slope(apex, _projected[right], 1e4f));
            }
            else
            {
                return false;
            }

            if (max <= min) return false;

            interval = new Vector4(min, max, transform.position.z, _shadowLength > 0f ? 1f / _shadowLength : 0f);
            shadow = new Vector4(_strength, _softness, _softnessGrowth, circle);
            return true;

            static bool Before(float a, float d, float bestA, float bestD) =>
                a < bestA - 1e-4f || (a <= bestA + 1e-4f && d < bestD);

            static float Slope(Vector2 from, Vector2 to, float vertical)
            {
                var run = to.x - from.x;
                var rise = to.y - from.y;
                if (run > 1e-4f) return Mathf.Clamp(rise / run, -1e4f, 1e4f);
                return vertical;
            }
        }

        private bool TryGetCircle(out Vector2 center, out float radius)
        {
            center = default;
            radius = 0f;
            var scale = transform.lossyScale;
            var maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            switch (_shape)
            {
                case WaterLightShaftOccluderShape.Circle:
                    center = transform.TransformPoint(_offset);
                    radius = _size.x * 0.5f * maxScale;
                    return true;
                case WaterLightShaftOccluderShape.Auto when TryGetComponent<CircleCollider2D>(out var circle):
                    center = transform.TransformPoint(circle.offset);
                    radius = circle.radius * maxScale;
                    return true;
                default:
                    return false;
            }
        }

        private bool TryGetCorners()
        {
            switch (_shape)
            {
                case WaterLightShaftOccluderShape.Box:
                    SetLocalBox(_offset, _size);
                    return true;
                case WaterLightShaftOccluderShape.Circle:
                    return false;
            }

            if (TryGetComponent<BoxCollider2D>(out var box))
            {
                SetLocalBox(box.offset, box.size);
                return true;
            }

            if (TryGetComponent<Collider2D>(out var collider) && collider.enabled)
            {
                SetBounds(collider.bounds);
                return true;
            }

            if (TryGetComponent<Renderer>(out var spriteRenderer))
            {
                SetBounds(spriteRenderer.bounds);
                return true;
            }

            return false;
        }

        private void SetLocalBox(Vector2 offset, Vector2 size)
        {
            var half = size * 0.5f;
            _corners[0] = transform.TransformPoint(offset + new Vector2(-half.x, -half.y));
            _corners[1] = transform.TransformPoint(offset + new Vector2(half.x, -half.y));
            _corners[2] = transform.TransformPoint(offset + new Vector2(half.x, half.y));
            _corners[3] = transform.TransformPoint(offset + new Vector2(-half.x, half.y));
        }

        private void SetBounds(Bounds bounds)
        {
            Vector2 min = bounds.min, max = bounds.max;
            _corners[0] = new Vector2(min.x, min.y);
            _corners[1] = new Vector2(max.x, min.y);
            _corners[2] = new Vector2(max.x, max.y);
            _corners[3] = new Vector2(min.x, max.y);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var z = transform.position.z;
            UnityEditor.Handles.color = new Color(0.1f, 0.1f, 0.2f, 0.9f);
            if (TryGetCircle(out var center, out var radius))
            {
                UnityEditor.Handles.DrawWireDisc(new Vector3(center.x, center.y, z), Vector3.forward, radius + _padding);
            }
            else if (TryGetCorners())
            {
                var points = new Vector3[5];
                for (var i = 0; i < 4; i++) points[i] = new Vector3(_corners[i].x, _corners[i].y, z);
                points[4] = points[0];
                UnityEditor.Handles.DrawPolyLine(points);
            }
        }
#endif
    }
}
