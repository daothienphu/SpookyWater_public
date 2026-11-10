using System.Collections.Generic;
using UnityEngine;

namespace SpookyWater
{
    [System.Serializable]
    public class ContactRippleSettings
    {
        [Tooltip("Initial strength of the ripple a body makes when it touches the waterline, as a (min, max) " +
                 "range re-rolled for every contact. Around 1 is an ordinary touch; larger is a heavy splash.")]
        public Vector2 strengthRange = new(0.2f, 0.6f);

        [Header("Splash Ripple")]
        [Tooltip("Extra ripple strength added when a body splashes in or out, per world unit per second of its " +
                 "vertical speed, so a fast dive or a burst out of the water pushes the water much harder than a " +
                 "slow dip. It is scaled by the body's size like the splash. 0 leaves only the touch ripple above.")]
        [Min(0f)] public float splashRipplePerSpeed = 0.18f;
        [Tooltip("Largest strength the splash ripple can reach, however fast the body moves.")]
        [Min(0f)] public float maxSplashRipple = 3f;
        [Tooltip("Size of the splash ripple's bump relative to the Ripple Grid's Impact Radius. Wider pushes more " +
                 "water and throws longer rings.")]
        [Min(0.1f)] public float splashRippleRadius = 2f;

        [Header("Falling In")]
        [Tooltip("A body that is at most this much under water (as a fraction of its height) is ready to " +
                 "splash as it falls back in. The gap to Enter Splash Fraction stops a body bobbing at rest " +
                 "from splashing over and over.")]
        [Range(0f, 1f)] public float enterArmFraction = 0.2f;
        [Tooltip("Once a ready body is this much under water (fraction of its height) while moving down, it splashes.")]
        [Range(0f, 1f)] public float enterSplashFraction = 0.25f;

        [Header("Coming Out")]
        [Tooltip("A body that is at least this much under water (fraction of its height) is ready to " +
                 "splash as it comes up.")]
        [Range(0f, 1f)] public float exitArmFraction = 0.85f;
        [Tooltip("Once a ready body is only this much under water (fraction of its height) while moving up, it splashes.")]
        [Range(0f, 1f)] public float exitSplashFraction = 0.75f;
    }

    [AddComponentMenu("")]
    [RequireComponent(typeof(WaterTile))]
    public class WaterContactRipple : MonoBehaviour
    {
        private struct Crossing
        {
            public bool CanEnter;
            public bool CanExit;
            public Vector2 PeakVelocity;
        }

        private static readonly ContactRippleSettings DefaultSettings = new();

        private ContactRippleSettings _settings = DefaultSettings;
        private LayerMask _layers = ~0;
        private WaterField _field;
        private Collider2D[] _bands;
        private readonly Dictionary<Collider2D, Crossing> _crossings = new();

        public void Initialize(ContactRippleSettings settings, LayerMask contactLayers)
        {
            _settings = settings ?? DefaultSettings;
            _layers = contactLayers;
        }

        private void Awake()
        {
            _field = GetComponent<WaterTile>().OwningField;
            _bands = GetComponents<Collider2D>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_field) return;
            if ((_layers.value & (1 << other.gameObject.layer)) == 0) return;

            var up = other.attachedRigidbody
                ? other.attachedRigidbody.linearVelocity.y > 0f
                : Random.value > 0.5f;

            _field.CreateContactRippleAt(other.transform.position,
                Random.Range(_settings.strengthRange.x, _settings.strengthRange.y), up);

            var body = other.attachedRigidbody;
            if (!body) return;
            _crossings[other] = new Crossing { CanEnter = !up, CanExit = up, PeakVelocity = body.linearVelocity };
            TrackSplash(other);
        }

        private void OnTriggerStay2D(Collider2D other) => TrackSplash(other);

        private void OnTriggerExit2D(Collider2D other) => _crossings.Remove(other);

        private void TrackSplash(Collider2D other)
        {
            var body = other.attachedRigidbody;
            var splashes = WaterSplashEffects.Instance;
            if (!body || !splashes || !_crossings.TryGetValue(other, out var c)) return;
            if (!TryGetWaterline(other.bounds, out var surfaceY)) return;

            var bounds = other.bounds;
            var submerged = Mathf.Clamp01((surfaceY - bounds.min.y) / Mathf.Max(bounds.size.y, 1e-3f));
            var velocity = body.linearVelocity;

            if (c.CanEnter && velocity.y < c.PeakVelocity.y || c.CanExit && velocity.y > c.PeakVelocity.y)
                c.PeakVelocity = velocity;

            if (submerged <= _settings.enterArmFraction && !c.CanEnter)
            {
                c.CanEnter = true;
                c.PeakVelocity = velocity;
            }
            if (submerged >= _settings.exitArmFraction && !c.CanExit)
            {
                c.CanExit = true;
                c.PeakVelocity = velocity;
            }

            var sizeScale = Mathf.Sqrt(Mathf.Max(bounds.size.x, 0.1f));
            if (c.CanEnter && submerged >= _settings.enterSplashFraction && velocity.y < 0f)
            {
                splashes.Impact(body, surfaceY, c.PeakVelocity, sizeScale);
                SplashRipple(other, surfaceY, c.PeakVelocity, sizeScale);
                c.CanEnter = false;
            }
            else if (c.CanExit && submerged <= _settings.exitSplashFraction && velocity.y > 0f)
            {
                splashes.Impact(body, surfaceY, c.PeakVelocity, sizeScale);
                SplashRipple(other, surfaceY, c.PeakVelocity, sizeScale);
                c.CanExit = false;
            }
            _crossings[other] = c;
        }

        private void SplashRipple(Collider2D other, float surfaceY, Vector2 velocity, float sizeScale)
        {
            var strength = Mathf.Min(Mathf.Abs(velocity.y) * _settings.splashRipplePerSpeed * sizeScale, _settings.maxSplashRipple);
            if (strength <= 0f || !_field) return;

            var centre = other.bounds.center;
            _field.CreateContactRippleAt(new Vector3(centre.x, surfaceY, other.transform.position.z), strength,
                velocity.y > 0f, _settings.splashRippleRadius);
        }

        private bool TryGetWaterline(Bounds body, out float surfaceY)
        {
            surfaceY = 0f;
            var bestOverlap = 0f;
            foreach (var band in _bands)
            {
                var b = band.bounds;
                var overlap = Mathf.Min(b.max.x, body.max.x) - Mathf.Max(b.min.x, body.min.x);
                if (overlap <= bestOverlap || b.max.y < body.min.y || b.min.y > body.max.y) continue;

                bestOverlap = overlap;
                surfaceY = b.max.y;
            }
            return bestOverlap > 0f;
        }
    }
}
