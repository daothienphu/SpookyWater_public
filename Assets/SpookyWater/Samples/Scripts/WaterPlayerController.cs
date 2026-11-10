using UnityEngine;

namespace SpookyWater.Samples
{
    [AddComponentMenu("SpookyWater/Samples/Water Player Controller")]
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(WaterSwimmer))]
    public class WaterPlayerController : MonoBehaviour
    {
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int RunningSpeedId = Animator.StringToHash("RunningSpeed");
        private static readonly int JumpId = Animator.StringToHash("Jump");
        private static readonly Quaternion FlippedRotation = new(0f, 0f, 1f, 0f);

        [Group("Character")]
        [Tooltip("Animator driven with the Grounded, RunningSpeed and Jump parameters. Side-scroller sample character that walks on land and swims in tile-painted water: A/D move, W/S swim up and down, Space or J jump (also out of the water), K dash while swimming, L shoot up while in the water or slam straight down while in the air, R respawn. The water effects come from the WaterSwimmer on the same object.")]
        [SerializeField] private Animator _animator;
        [Tooltip("Flipped on X to face the movement direction.")]
        [SerializeField] private Transform _puppet;
        [Tooltip("Optional physics ponytail: its root body is driven to the anchor every physics step.")]
        [SerializeField] private Transform _tailAnchor;
        [Tooltip("Root rigidbody of the ponytail, driven to the Tail Anchor.")]
        [SerializeField] private Rigidbody2D _tailRigidbody;

        [Group("Land")]
        [Tooltip("Running speed in world units per second.")]
        [SerializeField, Min(0.01f)] private float _runSpeed = 4f;
        [Tooltip("How quickly it reaches the running speed, in world units per second squared.")]
        [SerializeField, Min(0f)] private float _runAcceleration = 30f;
        [Tooltip("Upward speed of a jump from the ground.")]
        [SerializeField, Min(0f)] private float _jumpSpeed = 8f;
        [Tooltip("Gravity scale while rising.")]
        [SerializeField, Min(0f)] private float _riseGravityScale = 1f;
        [Tooltip("Gravity scale while falling.")]
        [SerializeField, Min(0f)] private float _fallGravityScale = 2.5f;
        [Tooltip("Grace period in seconds after walking off a ledge during which a jump still counts.")]
        [SerializeField, Min(0f)] private float _coyoteTime = 0.1f;
        [Tooltip("A jump pressed this many seconds before landing still fires on touchdown.")]
        [SerializeField, Min(0f)] private float _jumpBufferTime = 0.15f;
        [Tooltip("Physics layers that count as ground.")]
        [SerializeField] private LayerMask _groundMask = ~0;

        [Group("Swimming")]
        [Tooltip("Swimming speed in world units per second. Also tells the WaterSwimmer what a normal swim speed is.")]
        [SerializeField, Min(0.01f)] private float _swimSpeed = 3f;
        [Tooltip("When on, the Swim Speed and Dash Speed here are copied into the WaterSwimmer when the game starts, replacing the speeds set on the swimmer. Turn it off to tune the swimmer's own Swim Speed and Fast Speed on its component.")]
        [SerializeField] private bool _driveSwimmerSpeeds = true;
        [Tooltip("How quickly it reaches the swimming speed, in world units per second squared.")]
        [SerializeField, Min(0f)] private float _swimAcceleration = 12f;
        [Tooltip("Vertical speed per unit of depth below the floating point, pulling the body back up.")]
        [SerializeField, Min(0f)] private float _buoyancy = 4f;
        [Tooltip("Fastest the buoyancy can push the body up, in world units per second.")]
        [SerializeField, Min(0f)] private float _maxBuoyantSpeed = 4f;
        [Tooltip("Upward speed of a jump out of the water.")]
        [SerializeField, Min(0f)] private float _waterJumpSpeed = 7f;

        [Group("Dash")]
        [Tooltip("Dash speed in world units per second (K, in water). Also tells the WaterSwimmer when the spray trail is at full strength.")]
        [SerializeField, Min(0.01f)] private float _dashSpeed = 9f;
        [Tooltip("Seconds a dash lasts.")]
        [SerializeField, Min(0f)] private float _dashDuration = 0.35f;
        [Tooltip("Seconds before another dash can start.")]
        [SerializeField, Min(0f)] private float _dashCooldown = 0.6f;

        [Group("Leap")]
        [Tooltip("Upward speed (L, in the water). The body is held at this speed until it leaves the water.")]
        [SerializeField, Min(0f)] private float _diveLaunchSpeed = 16f;
        [Tooltip("Downward speed the body is thrown at (L, in the air), starting immediately from wherever it is.")]
        [SerializeField, Min(0f)] private float _diveSlamSpeed = 16f;
        [Tooltip("Ripple strength of the landing splash. An ordinary contact is 0.2 to 0.6.")]
        [SerializeField, Min(0f)] private float _diveSplashStrength = 2.5f;
        [Tooltip("Scales the ripple simulation's impact radius for the landing splash. Wider makes a bigger wave.")]
        [SerializeField, Min(0.1f)] private float _diveSplashRadiusScale = 3f;

        private enum DiveState { None, ShootingUp, Slamming }

        private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[8];
        private Rigidbody2D _body;
        private ContactFilter2D _groundFilter;
        private Vector2 _groundVelocity;
        private Vector3 _spawnPosition;
        private WaterSwimmer _swimmer;

        private float _moveInput;
        private float _swimInput;
        private float _lastJumpPressTime = float.NegativeInfinity;
        private float _lastGroundedTime = float.NegativeInfinity;
        private float _waterJumpTime = float.NegativeInfinity;
        private float _lastDashPressTime = float.NegativeInfinity;
        private float _lastDivePressTime = float.NegativeInfinity;
        private float _dashStartTime = float.NegativeInfinity;
        private float _dashDirection;
        private DiveState _diveState;
        private bool _isFlipped;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _groundFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = _groundMask };
            _spawnPosition = transform.position;
            _swimmer = GetComponent<WaterSwimmer>();
            if (!_swimmer) _swimmer = gameObject.AddComponent<WaterSwimmer>();
            if (_driveSwimmerSpeeds) _swimmer.SetSpeedRange(_swimSpeed, _dashSpeed);
        }

        private void Update()
        {
            _moveInput = 0f;
            if (SampleInput.GetKey(KeyCode.A)) _moveInput -= 1f;
            if (SampleInput.GetKey(KeyCode.D)) _moveInput += 1f;

            _swimInput = 0f;
            if (SampleInput.GetKey(KeyCode.S)) _swimInput -= 1f;
            if (SampleInput.GetKey(KeyCode.W)) _swimInput += 1f;

            if (SampleInput.GetKeyDown(KeyCode.Space) || SampleInput.GetKeyDown(KeyCode.J)) _lastJumpPressTime = Time.time;
            if (SampleInput.GetKeyDown(KeyCode.K)) _lastDashPressTime = Time.time;
            if (SampleInput.GetKeyDown(KeyCode.L)) _lastDivePressTime = Time.time;
            if (SampleInput.GetKeyDown(KeyCode.R)) Respawn();
        }

        private void FixedUpdate()
        {
            var feet = _body.position;
            var inWater = _swimmer.InWater;
            var surfaceY = _swimmer.SurfaceY;
            var grounded = IsGrounded();
            if (grounded) _lastGroundedTime = Time.time;

            var leavingWater = Time.time - _waterJumpTime < 0.25f;
            var velocity = _body.linearVelocity;

            if (inWater && !leavingWater)
            {
                if (_diveState == DiveState.Slamming)
                {
                    _diveState = DiveState.None;
                    Water.Ripple(new Vector3(feet.x, surfaceY, transform.position.z), _diveSplashStrength, false, _diveSplashRadiusScale);
                }

                _body.gravityScale = 0f;

                var dashStarted = Time.time - _lastDashPressTime <= _jumpBufferTime && Time.time - _dashStartTime >= _dashCooldown;
                if (dashStarted)
                {
                    _lastDashPressTime = float.NegativeInfinity;
                    _dashStartTime = Time.time;
                    _dashDirection = _moveInput != 0f ? Mathf.Sign(_moveInput) : _isFlipped ? -1f : 1f;
                    _swimmer.DashBurst(_dashDirection);
                }
                var dashing = Time.time - _dashStartTime < _dashDuration;

                velocity.x = dashing ? _dashDirection * _dashSpeed
                    : Mathf.MoveTowards(velocity.x, _moveInput * _swimSpeed, _swimAcceleration * Time.fixedDeltaTime);

                var depth = _swimmer.Depth;
                var buoyantSpeed = Mathf.Clamp(depth * _buoyancy, -_maxBuoyantSpeed, _maxBuoyantSpeed);
                var targetVertical = _swimInput < 0f ? _swimInput * _swimSpeed
                    : _swimInput > 0f ? Mathf.Min(_swimSpeed, buoyantSpeed) : buoyantSpeed;
                velocity.y = Mathf.MoveTowards(velocity.y, targetVertical, _swimAcceleration * Time.fixedDeltaTime);

                var nearSurface = !_swimmer.Submerged || grounded;
                if (ConsumeDive())
                {
                    _diveState = DiveState.ShootingUp;
                    if (_animator) _animator.SetTrigger(JumpId);
                }
                else if (ConsumeJump(nearSurface))
                {
                    velocity.y = _waterJumpSpeed;
                    _waterJumpTime = Time.time;
                }
                if (_diveState == DiveState.ShootingUp) velocity.y = Mathf.Max(velocity.y, _diveLaunchSpeed);

                _swimmer.Dashing = dashing;
                _swimmer.EffectsEnabled = true;
            }
            else
            {
                _body.gravityScale = velocity.y - _groundVelocity.y > 0f ? _riseGravityScale : _fallGravityScale;
                velocity.x = Mathf.MoveTowards(velocity.x, _moveInput * _runSpeed, _runAcceleration * Time.fixedDeltaTime);

                var canJump = Time.time - _lastGroundedTime <= _coyoteTime;
                if (_diveState == DiveState.ShootingUp) _diveState = DiveState.None;
                if (!grounded && ConsumeDive()) _diveState = DiveState.Slamming;
                else if (ConsumeJump(canJump)) velocity.y = _jumpSpeed;

                if (_diveState == DiveState.Slamming)
                {
                    velocity.y = Mathf.Min(velocity.y, -_diveSlamSpeed);
                    if (grounded) _diveState = DiveState.None;
                }

                _swimmer.Dashing = false;
                _swimmer.EffectsEnabled = false;
            }

            _body.linearVelocity = velocity;

            UpdateFacing(velocity.x);
            _swimmer.FacingLeft = _isFlipped;
            UpdateTailPose();
            if (_animator)
            {
                _animator.SetBool(GroundedId, grounded || (inWater && !leavingWater));
                _animator.SetFloat(RunningSpeedId, Mathf.Abs(velocity.x) / _runSpeed);
            }

            if (feet.y < _spawnPosition.y - 50f) Respawn();
        }

        private bool ConsumeJump(bool canJump)
        {
            if (!canJump || Time.time - _lastJumpPressTime > _jumpBufferTime) return false;

            _lastJumpPressTime = float.NegativeInfinity;
            _lastGroundedTime = float.NegativeInfinity;
            if (_animator) _animator.SetTrigger(JumpId);
            return true;
        }

        private bool ConsumeDive()
        {
            if (Time.time - _lastDivePressTime > _jumpBufferTime) return false;

            _lastDivePressTime = float.NegativeInfinity;
            _lastGroundedTime = float.NegativeInfinity;
            return true;
        }

        private bool IsGrounded()
        {
            _groundVelocity = Vector2.zero;
            var count = _body.Cast(Vector2.down, _groundFilter, _groundHits, 0.05f);
            for (var i = 0; i < count; i++)
            {
                var hit = _groundHits[i];
                if (hit.normal.y <= 0.5f) continue;

                var groundVelocity = hit.rigidbody ? hit.rigidbody.GetPointVelocity(hit.point) : Vector2.zero;
                if (_body.linearVelocity.y - groundVelocity.y > 0.01f) return false;

                _groundVelocity = groundVelocity;
                return true;
            }
            return false;
        }

        private void UpdateFacing(float horizontalSpeed)
        {
            if (!_puppet) return;

            if (horizontalSpeed > 0.1f) _isFlipped = false;
            else if (horizontalSpeed < -0.1f) _isFlipped = true;
            _puppet.localScale = _isFlipped ? new Vector3(-1f, 1f, 1f) : Vector3.one;
        }

        private void UpdateTailPose()
        {
            if (!_tailAnchor || !_tailRigidbody) return;

            _tailRigidbody.MovePosition((Vector2)_tailAnchor.position + _body.linearVelocity * Time.fixedDeltaTime);
            _tailRigidbody.SetRotation(_isFlipped ? _tailAnchor.rotation * FlippedRotation : _tailAnchor.rotation);
        }

        private void Respawn()
        {
            _body.position = _spawnPosition;
            _body.linearVelocity = Vector2.zero;
            _swimmer.ResetTrail();
            _diveState = DiveState.None;
        }
    }
}
