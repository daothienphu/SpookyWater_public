using UnityEngine;
using UnityEngine.Events;

namespace SpookyWater
{
    [AddComponentMenu("SpookyWater/Water Swimmer")]
    [HelpURL(SpookyWaterDocs.Swimmer)]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(Rigidbody2D))]
    public class WaterSwimmer : MonoBehaviour
    {
        [Group("Water")]
        [Tooltip("Makes a character interact with the water: reports whether it is in the water and how deep, and leaves a wake along the surface, spray, a dash trail and bubbles as it swims. Crossing the waterline already ripples and splashes by itself for any Rigidbody2D. This is the height above the feet that floats level with the water surface.")]
        [SerializeField, Min(0f)] private float _floatHeight = 0.9f;
        [Tooltip("How far below the floating point the body counts as submerged, in world units. Above it the swimmer kicks up surface spray; below it, it trails bubbles.")]
        [SerializeField, Min(0f)] private float _surfaceDepth = 0.4f;
        [Tooltip("Normal swimming speed in world units per second. Surface spray scales with speed up to this. The sample WaterPlayerController overwrites it with its own swim speed unless its Drive Swimmer Speeds is off.")]
        [SerializeField, Min(0.01f)] private float _swimSpeed = 3f;
        [Tooltip("Speed in world units per second at which the dash spray trail is at full strength, such as a dash. Between the swim speed and this the trail fades in. The sample WaterPlayerController overwrites it with its dash speed unless its Drive Swimmer Speeds is off.")]
        [SerializeField, Min(0.01f)] private float _fastSpeed = 9f;

        [Group("Wake")]
        [Tooltip("Scales the wake dragged through the ripple simulation while swimming along the surface. 0 = no wake.")]
        [SerializeField, Min(0f)] private float _wakeStrength = 1f;
        [Tooltip("Multiplies the mound the wake pushes up just ahead of the swimmer, on top of the Ripple Grid's " +
                 "Wake Bow Ratio. 1 leaves the grid's shape; higher makes the water bulge up more where the swimmer " +
                 "pushes through it, while the trough behind stays the same.")]
        [SerializeField, Min(0f)] private float _wakeBulge = 2f;
        [Tooltip("Only feet within this depth of the surface (world units) leave a wake.")]
        [SerializeField, Min(0f)] private float _wakeMaxDepth = 1.6f;
        [Tooltip("Multiplies the wake strength while Dashing is set.")]
        [SerializeField, Min(0f)] private float _dashWakeMultiplier = 3f;

        [Group("Bow Wave")]
        [Tooltip("Height in world units of the mound of water a swimmer pushes along the surface after swimming steadily one way. It starts small behind the character, grows and catches up until it carries the character, and when the character stops or turns it rolls on ahead and dies down. 0 = none. Drawn by the water shaders, so it is smooth and never spreads across the lake.")]
        [SerializeField, Min(0f)] private float _bowWaveHeight = 0.6f;
        [Tooltip("Half-length of the fully built mound along the direction of travel, in world units. It starts at about 70% of this.")]
        [SerializeField, Min(0.05f)] private float _bowWaveLength = 0.9f;
        [Tooltip("How far the mound reaches into the water (world Z) either side of the swimmer, in world units. It must reach the water's front edge to show on the waterline.")]
        [SerializeField, Min(0.05f)] private float _bowWaveDepth = 1.5f;
        [Tooltip("Width of the mound's leading face relative to its back. Lower makes a steeper front, like water being shoved forward.")]
        [SerializeField, Range(0.2f, 1f)] private float _bowWaveFrontWidth = 0.6f;
        [Tooltip("Depth of the shallow dip trailing the mound, relative to its height. 0 = no dip.")]
        [SerializeField, Range(0f, 1f)] private float _bowWaveTrough = 0.2f;
        [Tooltip("Slowest horizontal speed, as a fraction of Swim Speed, that counts as swimming steadily. Slower, or turning around, lets the mound go.")]
        [SerializeField, Range(0.05f, 1f)] private float _bowWaveMinSpeed = 0.5f;
        [Tooltip("Seconds of steady swimming one way before the mound appears.")]
        [SerializeField, Min(0f)] private float _bowWaveDelay = 0.35f;
        [Tooltip("Seconds the mound takes, once it has appeared, to grow to full size and catch up with the character.")]
        [SerializeField, Min(0.05f)] private float _bowWaveBuildTime = 1.4f;
        [Tooltip("How far behind the character the mound appears, in world units. It closes in as it grows.")]
        [SerializeField] private float _bowWaveStartBehind = 1.3f;
        [Tooltip("Where the fully built mound sits relative to the character, in world units behind it. 0 centres it under the character; negative pushes it slightly ahead.")]
        [SerializeField] private float _bowWaveEndBehind = 0f;
        [Tooltip("Seconds a let-go mound keeps rolling forward while it dies down. It fades faster when it runs into a shore.")]
        [SerializeField, Min(0.05f)] private float _bowWaveFadeTime = 1.1f;
        [Tooltip("How quickly a let-go mound slows down, per second. With the default it rolls on about a body length or two past where the character stopped.")]
        [SerializeField, Min(0f)] private float _bowWaveDrag = 1.2f;

        [Group("Surface Spray")]
        [Tooltip("Spray bursts per second while swimming along the surface at full swim speed. 0 = no spray. Needs a WaterSplashEffects in the scene.")]
        [SerializeField, Min(0f)] private float _sprayRate = 7f;
        [Tooltip("How far ahead of the body's middle, in world units, the dash spray starts. Faster than the swim speed the spray becomes a thin trail of low droplets left behind.")]
        [SerializeField, Min(0f)] private float _bowOffset = 0.3f;

        [Group("Bubbles")]
        [Tooltip("Bubbles per world unit swum underwater. 0 = no bubble trail. Needs a WaterSplashEffects in the scene.")]
        [SerializeField, Min(0f)] private float _bubblesPerUnit = 4f;
        [Tooltip("Multiplies the bubble trail while Dashing is set.")]
        [SerializeField, Min(0f)] private float _dashBubbleMultiplier = 3f;
        [Tooltip("Seconds between breath bubbles while underwater, a random time in this range. Set both to 0 for none.")]
        [SerializeField] private Vector2 _breathInterval = new(1.2f, 2.6f);
        [Tooltip("Height above the feet, in world units, the bubbles come from. About the chest.")]
        [SerializeField, Min(0f)] private float _bubbleHeight = 0.7f;

        [Group("Events")]
        [Tooltip("Raised when the feet go from outside the water to inside it.")]
        public UnityEvent OnEnterWater = new();
        [Tooltip("Raised when the feet go from inside the water to outside it.")]
        public UnityEvent OnExitWater = new();

        private Rigidbody2D _body;
        private double _sampledAt = double.NegativeInfinity;
        private Vector2 _sampledPosition;
        private bool _inWater;
        private float _surfaceY;
        private bool _wasInWater;
        private bool _hasWakePoint;
        private Vector3 _lastWakePoint;
        private bool _hasBowPoint;
        private Vector3 _lastBowPoint;
        private bool _hasBubblePoint;
        private Vector3 _lastBubblePoint;
        private float _sprayAccumulator;
        private float _bubbleAccumulator;
        private float _nextBreathTime;
        private float _dashDirection = 1f;
        private WaterField _bowField;
        private int _bowHandle = -1;
        private float _bowDirection;
        private float _bowSustain;
        private float _bowStall;
        private float _bowCarrySpeed;

        private const float BowWaveStallGrace = 0.12f;
        private const float BowWaveAppearTime = 0.3f;
        private const float BowWaveCarryDecay = 2f;

        public bool Dashing { get; set; }

        public bool FacingLeft { get; set; }

        public bool EffectsEnabled { get; set; } = true;

        public bool InWater
        {
            get
            {
                Sample();
                return _inWater;
            }
        }

        public float SurfaceY
        {
            get
            {
                Sample();
                return _surfaceY;
            }
        }

        public float Depth
        {
            get
            {
                Sample();
                return _inWater ? _surfaceY - (_sampledPosition.y + _floatHeight) : 0f;
            }
        }

        public bool Submerged
        {
            get
            {
                Sample();
                return _inWater && Depth >= _surfaceDepth;
            }
        }

        public void SetSpeedRange(float swimSpeed, float fastSpeed)
        {
            _swimSpeed = Mathf.Max(swimSpeed, 0.01f);
            _fastSpeed = Mathf.Max(fastSpeed, 0.01f);
        }

        public void DashBurst(float direction)
        {
            _dashDirection = direction;
            var splashes = WaterSplashEffects.Instance;
            if (!splashes || !InWater || !Submerged) return;

            var position = BubblePosition();
            splashes.Bubbles(position - new Vector3(_dashDirection * 0.3f, 0f, 0f), 8, 0.15f, -_dashDirection);
        }

        public void ResetTrail()
        {
            _hasWakePoint = false;
            _hasBowPoint = false;
            _hasBubblePoint = false;
            LetGoOfBowWave();
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
        }

        private void OnDisable()
        {
            ResetTrail();
            _wasInWater = false;
        }

        private void LateUpdate()
        {
            UpdateBowWave(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            Sample();
            if (_inWater != _wasInWater)
            {
                _wasInWater = _inWater;
                if (_inWater) OnEnterWater?.Invoke();
                else OnExitWater?.Invoke();
            }

            if (!_inWater || !EffectsEnabled)
            {
                ResetTrail();
                return;
            }

            var feet = _body.position;
            UpdateWake(feet);
            UpdateSwimParticles(feet, Depth, _body.linearVelocity);
        }

        private void Sample()
        {
            if (!_body) _body = GetComponent<Rigidbody2D>();
            if (!_body) return;

            var now = Time.fixedTimeAsDouble;
            var position = _body.position;
            if (now == _sampledAt && position == _sampledPosition) return;

            _sampledAt = now;
            _sampledPosition = position;
            _inWater = Water.TryGetSurfaceY(position, out _surfaceY);
        }

        private void UpdateBowWave(float deltaTime)
        {
            if (deltaTime <= 0f || !_body) return;

            var field = Water.FieldAt(transform.position);
            if (field != _bowField)
            {
                LetGoOfBowWave();
                _bowField = field;
            }

            var velocityX = _body.linearVelocity.x;
            var speed = Mathf.Abs(velocityX);
            var direction = velocityX < 0f ? -1f : 1f;
            var eligible = field && _bowWaveHeight > 0f && EffectsEnabled && InWater && !Submerged;
            var moving = eligible && speed >= _bowWaveMinSpeed * _swimSpeed;

            if (moving && _bowSustain > 0f && direction != _bowDirection) LetGoOfBowWave();

            if (moving)
            {
                _bowDirection = direction;
                _bowSustain += deltaTime;
                _bowStall = 0f;
                _bowCarrySpeed = speed > _bowCarrySpeed
                    ? speed
                    : Mathf.Lerp(_bowCarrySpeed, speed, 1f - Mathf.Exp(-BowWaveCarryDecay * deltaTime));
            }
            else if (_bowSustain > 0f)
            {
                _bowStall += deltaTime;
                if (!eligible || _bowStall >= BowWaveStallGrace)
                {
                    LetGoOfBowWave();
                    return;
                }
            }
            else
            {
                return;
            }

            var grown = _bowSustain - _bowWaveDelay;
            if (grown <= 0f) return;

            if (_bowHandle < 0)
            {
                if (_bowStall > 0f) return;
                _bowHandle = field.BeginBowWave();
                if (_bowHandle < 0) return;
            }

            var charge = Mathf.SmoothStep(0f, 1f, grown / _bowWaveBuildTime);
            var appear = Mathf.Clamp01(grown / BowWaveAppearTime);
            var pace = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(_bowCarrySpeed / _swimSpeed));
            var behind = Mathf.Lerp(_bowWaveStartBehind, _bowWaveEndBehind, charge);
            var position = transform.position;
            var centre = new Vector2(position.x - _bowDirection * behind, Mathf.Max(position.z, field.FieldRect.yMin));
            var shape = new WaterBowWaveShape
            {
                Height = _bowWaveHeight * Mathf.Lerp(0.3f, 1f, charge) * appear * pace,
                Length = _bowWaveLength * Mathf.Lerp(0.7f, 1f, charge),
                Depth = _bowWaveDepth,
                Direction = _bowDirection,
                FrontWidth = _bowWaveFrontWidth,
                Trough = _bowWaveTrough
            };
            if (!field.UpdateBowWave(_bowHandle, centre, velocityX, shape)) _bowHandle = -1;
        }

        private void LetGoOfBowWave()
        {
            if (_bowHandle >= 0 && _bowField)
                _bowField.ReleaseBowWave(_bowHandle, _bowDirection * _bowCarrySpeed, _bowWaveFadeTime, _bowWaveDrag);
            _bowHandle = -1;
            _bowSustain = 0f;
            _bowStall = 0f;
            _bowCarrySpeed = 0f;
        }

        private Vector3 BubblePosition()
        {
            var feet = _body.position;
            return new Vector3(feet.x, feet.y + _bubbleHeight, transform.position.z);
        }

        private void UpdateWake(Vector2 feet)
        {
            if (_wakeStrength <= 0f || _surfaceY - feet.y > _wakeMaxDepth)
            {
                _hasWakePoint = false;
                return;
            }

            var point = new Vector3(feet.x, _surfaceY, transform.position.z);
            if (_hasWakePoint) Water.Wake(_lastWakePoint, point, _wakeStrength * (Dashing ? _dashWakeMultiplier : 1f), _wakeBulge);
            _lastWakePoint = point;
            _hasWakePoint = true;
        }

        private void UpdateSwimParticles(Vector2 feet, float depth, Vector2 velocity)
        {
            var splashes = WaterSplashEffects.Instance;
            if (!splashes) return;

            var boost = Dashing ? _dashBubbleMultiplier : 1f;
            var position = BubblePosition();
            if (depth < _surfaceDepth)
            {
                _bubbleAccumulator = 0f;
                _hasBubblePoint = false;

                var surge = Mathf.InverseLerp(_swimSpeed, _fastSpeed, Mathf.Abs(velocity.x));
                if (surge > 0.05f)
                {
                    var bow = position + new Vector3(Mathf.Sign(velocity.x) * _bowOffset, 0f, 0f);
                    if (_hasBowPoint) splashes.Wake(_lastBowPoint, bow, _surfaceY, surge);
                    _lastBowPoint = bow;
                    _hasBowPoint = true;
                    _sprayAccumulator = 0f;
                    return;
                }
                _hasBowPoint = false;

                var speed01 = Mathf.Clamp01(Mathf.Abs(velocity.x) / _swimSpeed);
                _sprayAccumulator += _sprayRate * speed01 * Time.fixedDeltaTime;
                if (_sprayAccumulator >= 1f)
                {
                    _sprayAccumulator -= 1f;
                    var kick = position + new Vector3(-Mathf.Sign(velocity.x) * 0.35f, 0f, 0f);
                    splashes.Spray(kick, _surfaceY, Mathf.Max(speed01, 0.3f), velocity);
                }
                return;
            }

            if (!_hasBubblePoint) _lastBubblePoint = position;
            _hasBubblePoint = true;
            _hasBowPoint = false;
            _sprayAccumulator = 0f;
            _bubbleAccumulator += velocity.magnitude * Time.fixedDeltaTime * _bubblesPerUnit * boost;
            if (_bubbleAccumulator >= 1f)
            {
                var count = Mathf.FloorToInt(_bubbleAccumulator);
                _bubbleAccumulator -= count;
                splashes.BubbleTrail(_lastBubblePoint, position, count, Dashing ? 0.2f : 0.12f, -velocity.x * 0.15f);
                _lastBubblePoint = position;
            }

            if (_breathInterval.y > 0f && Time.time >= _nextBreathTime)
            {
                _nextBreathTime = Time.time + Random.Range(_breathInterval.x, _breathInterval.y);
                var head = position + new Vector3(FacingLeft ? -0.15f : 0.15f, 0.45f, 0f);
                splashes.Bubbles(head, Random.Range(1, 4), 0.05f, 0f, 0.8f);
            }
        }
    }
}
