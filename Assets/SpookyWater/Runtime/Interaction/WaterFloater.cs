using UnityEngine;

namespace SpookyWater
{
    public enum WaterFloaterMode
    {
        Platform,
        Object
    }

    [AddComponentMenu("SpookyWater/Water Floater")]
    [HelpURL(SpookyWaterDocs.Floater)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public class WaterFloater : MonoBehaviour
    {
        [Tooltip("Platform: a stiff raft that dips a little when landed on, leans toward whoever stands near an end, never lets its top go under and stays where it was placed. Object: a crate, log or barrel that sinks under anything standing on it and bobs back up once it steps off.")]
        [SerializeField] private WaterFloaterMode _mode = WaterFloaterMode.Platform;
        [Tooltip("The collider whose shape floats: Box/Capsule float as their box, Circle as a circle, " +
                 "Polygon as its bounding box. Defaults to the first one on this object.")]
        [SerializeField] private Collider2D _shape;

        [Group("Buoyancy")]
        [Tooltip("Fraction of its height under water with nothing on it. Buoyancy is worked out per vertical slice of the body, each pushed up by how deep it sits, so weight on one end sinks that end and tilts the body.")]
        [SerializeField, Range(0.05f, 0.95f)] private float _floatDepth = 0.35f;
        [Tooltip("How hard it resists being pushed deeper than that (1 = plain buoyancy). A load of " +
                 "weight W sinks it by about Float Depth x W / (own weight x this) of its height, so " +
                 "raising this or the Rigidbody2D's mass makes it sink less.")]
        [SerializeField, Min(0.1f)] private float _loadStiffness = 6f;
        [Tooltip("Vertical slices the buoyancy is worked out over; more = smoother tilting.")]
        [SerializeField, Range(2, 16)] private int _slices = 8;
        [Tooltip("How quickly bobbing and rocking die out, as a fraction of critical damping: 0 = rings " +
                 "forever, 1 = settles without overshooting. A rider's extra weight lowers it a little.")]
        [SerializeField, Range(0f, 2f)] private float _dampingRatio = 0.8f;
        [Tooltip("Drag on sideways drifting, per second, while in the water.")]
        [SerializeField, Min(0f)] private float _waterDrag = 2f;

        [Group("Platform")]
        [Tooltip("Its top always stays at least this far above the water, however much weight is on it.")]
        [SerializeField, Min(0f)] private float _minFreeboard = 0.05f;
        [Tooltip("Most it can lean, in degrees.")]
        [SerializeField, Range(0f, 45f)] private float _maxTilt = 12f;
        [Tooltip("How strongly it's pulled back to where it was placed along X (0 = drifts freely).")]
        [SerializeField, Min(0f)] private float _anchorStrength = 8f;

        [Group("Object")]
        [Tooltip("How much lower it floats while something stands on it.")]
        [SerializeField, Min(0f)] private float _riddenSinkDepth = 1.5f;
        [Tooltip("World units per second it sinks / comes back up.")]
        [SerializeField, Min(0.01f)] private float _sinkSpeed = 1.2f;
        [Tooltip("Seconds after the last rider steps off before it starts coming back up.")]
        [SerializeField, Min(0f)] private float _resurfaceDelay = 0.4f;
        [Tooltip("Once sunk, anything dynamic within this height above it still counts as riding - so " +
                 "it stays down under someone swimming over it rather than bobbing against their feet.")]
        [SerializeField, Min(0f)] private float _riderReach = 1.5f;
        [Tooltip("How strongly it turns back to the angle it was placed at while in the water (0 = rolls " +
                 "freely, e.g. a barrel). Without it a half-submerged box floats corner-down, like a real one.")]
        [SerializeField, Min(0f)] private float _uprightStrength = 20f;

        [Group("Ripples")]
        [Tooltip("Moving through the water ripples it: each stretch of the waterline ripples as it bobs and drifting drags a wake. Falling in or bobbing out splashes through the waterline contact bands like any other Rigidbody2D. This is the width in world units of the stretches of its waterline that ripple separately.")]
        [SerializeField, Min(0.1f)] private float _rippleSpacing = 0.75f;
        [Tooltip("Vertical travel (world units) a stretch builds up before it ripples.")]
        [SerializeField, Min(0.001f)] private float _rippleThreshold = 0.03f;
        [Tooltip("Ripple strength per world unit of vertical travel (an ordinary contact is 0.2-0.6).")]
        [SerializeField, Min(0f)] private float _rippleStrength = 8f;
        [Tooltip("Strongest ripple a single stretch can make.")]
        [SerializeField, Min(0f)] private float _maxRippleStrength = 1.5f;
        [Tooltip("Scales the wake dragged while drifting along the surface (0 = no wake).")]
        [SerializeField, Min(0f)] private float _wakeStrength = 0.5f;

        private const int MaxRippleStretches = 4;

        private readonly ContactPoint2D[] _contacts = new ContactPoint2D[16];
        private readonly Collider2D[] _overlaps = new Collider2D[16];
        private readonly Vector2[] _strip = new Vector2[4];
        private readonly Vector2[] _clipped = new Vector2[8];
        private Rigidbody2D _body;
        private Rect _local;
        private bool _isCircle;
        private float[] _sliceShare;
        private float[] _rippleTravel;
        private float _homeX;
        private float _homeRotation;
        private float _sinkOffset;
        private float _lastRiddenTime = float.NegativeInfinity;
        private bool _hasWakePoint;
        private Vector3 _lastWakePoint;

        public float SinkOffset => _sinkOffset;

        private void Reset()
        {
            _shape = GetComponent<Collider2D>();
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            if (!_shape) _shape = GetComponent<Collider2D>();

            _local = GetLocalShape(out _isCircle);
            _homeX = _body.position.x;
            _homeRotation = _body.rotation;

            _sliceShare = new float[_slices];
            var total = 0f;
            for (var i = 0; i < _slices; i++)
            {
                GetSpan(SliceX(i, _slices), out var bottom, out var top);
                total += _sliceShare[i] = top - bottom;
            }
            for (var i = 0; i < _slices; i++) _sliceShare[i] /= Mathf.Max(total, 1e-5f);

            var worldWidth = _local.width * Mathf.Abs(transform.lossyScale.x);
            _rippleTravel = new float[Mathf.Clamp(Mathf.RoundToInt(worldWidth / _rippleSpacing), 1, MaxRippleStretches)];
        }

        private void FixedUpdate()
        {
            if (!Water.Level) return;

            var dt = Time.fixedDeltaTime;
            if (_mode == WaterFloaterMode.Object)
            {
                if (HasRider()) _lastRiddenTime = Time.time;
                var target = Time.time - _lastRiddenTime <= _resurfaceDelay ? _riddenSinkDepth : 0f;
                _sinkOffset = Mathf.MoveTowards(_sinkOffset, target, _sinkSpeed * dt);
            }
            else _sinkOffset = 0f;

            var weight = _body.mass * Physics2D.gravity.magnitude * _body.gravityScale;
            var freeboardDeficit = 0f;
            var inWater = false;

            var sliceWidth = _local.width / _slices;
            for (var i = 0; i < _slices; i++)
            {
                var x = SliceX(i, _slices);
                GetSpan(x, out var localBottom, out var localTop);
                _strip[0] = transform.TransformPoint(new Vector3(x - sliceWidth * 0.5f, localBottom));
                _strip[1] = transform.TransformPoint(new Vector3(x + sliceWidth * 0.5f, localBottom));
                _strip[2] = transform.TransformPoint(new Vector3(x + sliceWidth * 0.5f, localTop));
                _strip[3] = transform.TransformPoint(new Vector3(x - sliceWidth * 0.5f, localTop));

                var lowest = _strip[0];
                var highestY = lowest.y;
                for (var c = 1; c < 4; c++)
                {
                    if (_strip[c].y < lowest.y) lowest = _strip[c];
                    highestY = Mathf.Max(highestY, _strip[c].y);
                }
                if (!Water.TryGetSurfaceY(lowest, out var surfaceY)) continue;

                if (_mode == WaterFloaterMode.Platform)
                {
                    Vector2 top = transform.TransformPoint(new Vector3(x, localTop));
                    freeboardDeficit = Mathf.Max(freeboardDeficit, surfaceY + _minFreeboard - top.y);
                }

                surfaceY -= _sinkOffset;
                var area = ClipBelow(surfaceY, out var centre);
                var fullArea = PolygonArea(_strip);
                if (area <= 0f || fullArea <= 0f) continue;

                inWater = true;
                var submerged = Mathf.Clamp01(area / fullArea);
                var deep = submerged > _floatDepth;
                var lift = deep
                    ? 1f + _loadStiffness * (submerged - _floatDepth) / _floatDepth
                    : submerged / _floatDepth;

                var sliceMass = _body.mass * _sliceShare[i];
                var extent = Mathf.Max(highestY - lowest.y, 1e-3f);
                var stiffness = weight * _sliceShare[i] * (deep ? _loadStiffness : 1f) / (_floatDepth * extent);
                var wet = Mathf.Min(submerged / _floatDepth, 1f);
                var damping = Mathf.Min(2f * _dampingRatio * Mathf.Sqrt(stiffness * sliceMass), sliceMass / dt) * wet;
                var drag = Mathf.Min(_waterDrag * sliceMass, sliceMass / dt) * wet;
                var velocity = _body.GetPointVelocity(centre);
                _body.AddForceAtPosition(
                    new Vector2(-velocity.x * drag, weight * _sliceShare[i] * lift - velocity.y * damping), centre);
            }

            if (_mode == WaterFloaterMode.Platform && inWater) HoldPlatform(freeboardDeficit);
            if (_mode == WaterFloaterMode.Object && inWater) KeepUpright();
            UpdateRipples(dt);
        }

        private void KeepUpright()
        {
            if (_uprightStrength <= 0f) return;

            var angle = Mathf.DeltaAngle(_homeRotation, _body.rotation) * Mathf.Deg2Rad;
            var angularVelocity = _body.angularVelocity * Mathf.Deg2Rad;
            var accel = -angle * _uprightStrength - angularVelocity * 2f * Mathf.Sqrt(_uprightStrength);
            _body.AddTorque(accel * _body.inertia);
        }

        private float ClipBelow(float waterY, out Vector2 centroid)
        {
            var count = 0;
            for (var i = 0; i < 4; i++)
            {
                var a = _strip[i];
                var b = _strip[(i + 1) % 4];
                var aIn = a.y <= waterY;
                var bIn = b.y <= waterY;
                if (aIn) _clipped[count++] = a;
                if (aIn != bIn) _clipped[count++] = Vector2.Lerp(a, b, (waterY - a.y) / (b.y - a.y));
            }

            centroid = Vector2.zero;
            if (count < 3) return 0f;

            var area = 0f;
            for (var i = 0; i < count; i++)
            {
                var a = _clipped[i];
                var b = _clipped[(i + 1) % count];
                var cross = a.x * b.y - b.x * a.y;
                area += cross;
                centroid += (a + b) * cross;
            }
            if (Mathf.Abs(area) < 1e-8f) return 0f;

            centroid /= 3f * area;
            return Mathf.Abs(area) * 0.5f;
        }

        private static float PolygonArea(Vector2[] points)
        {
            var area = 0f;
            for (var i = 0; i < points.Length; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Length];
                area += a.x * b.y - b.x * a.y;
            }
            return Mathf.Abs(area) * 0.5f;
        }

        private void HoldPlatform(float freeboardDeficit)
        {
            if (_anchorStrength > 0f)
            {
                var offset = _homeX - _body.position.x;
                var accel = offset * _anchorStrength - _body.linearVelocity.x * 2f * Mathf.Sqrt(_anchorStrength);
                _body.AddForce(Vector2.right * (accel * _body.mass));
            }

            var tilt = Mathf.DeltaAngle(_homeRotation, _body.rotation);
            if (Mathf.Abs(tilt) > _maxTilt)
            {
                _body.rotation = _homeRotation + Mathf.Sign(tilt) * _maxTilt;
                if (_body.angularVelocity * tilt > 0f) _body.angularVelocity = 0f;
            }

            if (freeboardDeficit > 0f)
            {
                _body.position += Vector2.up * freeboardDeficit;
                if (_body.linearVelocity.y < 0f) _body.linearVelocity = new Vector2(_body.linearVelocity.x, 0f);
            }
        }

        private bool HasRider()
        {
            var count = _body.GetContacts(_contacts);
            for (var i = 0; i < count; i++)
            {
                var contact = _contacts[i];
                var other = contact.collider && contact.collider.attachedRigidbody != _body
                    ? contact.collider : contact.otherCollider;
                if (!IsOtherDynamic(other, out var otherBody)) continue;
                if (otherBody.worldCenterOfMass.y <= _body.worldCenterOfMass.y) continue;
                if (transform.InverseTransformPoint(contact.point).y > _local.center.y) return true;
            }

            if (_sinkOffset <= 0f || _riderReach <= 0f) return false;

            var scale = transform.lossyScale;
            var size = new Vector2(_local.width * Mathf.Abs(scale.x), _riderReach);
            var centre = transform.TransformPoint(new Vector3(_local.center.x, _local.yMax)) +
                         transform.up * (_riderReach * 0.5f);
            var filter = new ContactFilter2D { useTriggers = false };
            count = Physics2D.OverlapBox(centre, size, _body.rotation, filter, _overlaps);
            for (var i = 0; i < count; i++)
            {
                if (IsOtherDynamic(_overlaps[i], out _)) return true;
            }
            return false;
        }

        private bool IsOtherDynamic(Collider2D other, out Rigidbody2D otherBody)
        {
            otherBody = other && !other.isTrigger ? other.attachedRigidbody : null;
            return otherBody && otherBody != _body && otherBody.bodyType == RigidbodyType2D.Dynamic;
        }

        private void UpdateRipples(float dt)
        {
            var field = Water.FieldAt(_body ? (Vector3)_body.worldCenterOfMass : transform.position);
            if (!field) return;

            var z = transform.position.z;
            var stretchCount = _rippleTravel.Length;
            var halfStretch = _local.width * 0.5f / stretchCount;
            var cutsWaterline = false;
            var waterlineY = 0f;

            for (var i = 0; i < stretchCount; i++)
            {
                var x = SliceX(i, stretchCount);
                GetSpan(x, out var localBottom, out var localTop);
                Vector2 bottom = transform.TransformPoint(new Vector3(x, localBottom));
                Vector2 top = transform.TransformPoint(new Vector3(x, localTop));
                if (!Water.TryGetSurfaceY(bottom, out var surfaceY) || top.y <= surfaceY)
                {
                    _rippleTravel[i] = 0f;
                    continue;
                }

                cutsWaterline = true;
                waterlineY = surfaceY;
                var t = (surfaceY - bottom.y) / Mathf.Max(top.y - bottom.y, 1e-3f);
                var point = Vector2.Lerp(bottom, top, t);
                _rippleTravel[i] += _body.GetPointVelocity(point).y * dt;
                if (Mathf.Abs(_rippleTravel[i]) < _rippleThreshold) continue;

                var strength = Mathf.Min(Mathf.Abs(_rippleTravel[i]) * _rippleStrength, _maxRippleStrength);
                Vector2 left = transform.TransformPoint(new Vector3(x - halfStretch, localBottom));
                Vector2 right = transform.TransformPoint(new Vector3(x + halfStretch, localBottom));
                field.CreateContactRippleAlong(new Vector3(left.x, surfaceY, z), new Vector3(right.x, surfaceY, z),
                    strength, _rippleTravel[i] > 0f);
                _rippleTravel[i] = 0f;
            }

            if (!cutsWaterline || _wakeStrength <= 0f)
            {
                _hasWakePoint = false;
                return;
            }

            var wakePoint = new Vector3(_body.worldCenterOfMass.x, waterlineY, z);
            if (_hasWakePoint) field.CreateWakeSegment(_lastWakePoint, wakePoint, _wakeStrength);
            _lastWakePoint = wakePoint;
            _hasWakePoint = true;
        }

        private float SliceX(int index, int count) => _local.xMin + (index + 0.5f) * _local.width / count;

        private void GetSpan(float x, out float bottom, out float top)
        {
            if (!_isCircle)
            {
                bottom = _local.yMin;
                top = _local.yMax;
                return;
            }

            var radius = _local.width * 0.5f;
            var dx = x - _local.center.x;
            var half = Mathf.Sqrt(Mathf.Max(radius * radius - dx * dx, 0f));
            bottom = _local.center.y - half;
            top = _local.center.y + half;
        }

        private Rect GetLocalShape(out bool isCircle)
        {
            isCircle = false;
            switch (_shape)
            {
                case BoxCollider2D box:
                    return new Rect(box.offset - box.size * 0.5f, box.size);
                case CapsuleCollider2D capsule:
                    return new Rect(capsule.offset - capsule.size * 0.5f, capsule.size);
                case CircleCollider2D circle:
                    isCircle = true;
                    return new Rect(circle.offset - Vector2.one * circle.radius, Vector2.one * (circle.radius * 2f));
                case PolygonCollider2D polygon when polygon.points.Length > 0:
                    var min = polygon.points[0];
                    var max = min;
                    foreach (var p in polygon.points)
                    {
                        min = Vector2.Min(min, p);
                        max = Vector2.Max(max, p);
                    }
                    return new Rect(min + polygon.offset, max - min);
                case not null:
                    var bounds = _shape.bounds;
                    Vector2 localMin = transform.InverseTransformPoint(bounds.min);
                    Vector2 localMax = transform.InverseTransformPoint(bounds.max);
                    return Rect.MinMaxRect(Mathf.Min(localMin.x, localMax.x), Mathf.Min(localMin.y, localMax.y),
                        Mathf.Max(localMin.x, localMax.x), Mathf.Max(localMin.y, localMax.y));
                default:
                    return new Rect(-0.5f, -0.5f, 1f, 1f);
            }
        }
    }
}
