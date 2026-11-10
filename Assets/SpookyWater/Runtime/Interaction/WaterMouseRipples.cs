using UnityEngine;
#if SPOOKYWATER_INPUTSYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SpookyWater
{
    [AddComponentMenu("SpookyWater/Water Mouse Ripples")]
    [HelpURL(SpookyWaterDocs.MouseRipples)]
    [DisallowMultipleComponent]
    public class WaterMouseRipples : MonoBehaviour
    {
        private const float MaxWakeStepDistance = 1.5f;

        [Tooltip("Camera used to aim the cursor ray. Empty = the water camera (Water.Camera: Water.SetCamera, an enabled Water Camera Source, Water.CameraResolver, then the camera tagged MainCamera), looked up on every click so a camera spawned or swapped later is picked up.")]
        [SerializeField] private Camera _camera;
        [Tooltip("Strength of the ripple a click makes: left button pushes the water up, right button pushes it down.")]
        [SerializeField, Min(0f)] private float _rippleStrength = 0.4f;
        [Tooltip("Scales the wake left while dragging with the left button held. 0 = no wakes.")]
        [SerializeField, Min(0f)] private float _wakeStrength = 1f;
        [Tooltip("Physics layers the cursor ray can hit. Only the water surface tiles are used, so leave it on Everything unless another collider blocks the ray.")]
        [SerializeField] private LayerMask _raycastMask = ~0;
        [Tooltip("Logs every click and why it did or did not create a ripple. Turn off once it works.")]
        [SerializeField] private bool _debugLog;

        private WaterField _wakeField;
        private Vector3 _lastWakePoint;

        public Camera Camera
        {
            get => _camera;
            set => _camera = value;
        }

        private void Update()
        {
            if (ButtonDown(0)) TryCreateRipple(true);
            if (ButtonDown(1)) TryCreateRipple(false);

            if (ButtonHeld(0) && _wakeStrength > 0f) UpdateWake();
            else _wakeField = null;
        }

        private static bool ButtonDown(int button)
        {
#if SPOOKYWATER_INPUTSYSTEM && ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return false;
            return button == 0 ? mouse.leftButton.wasPressedThisFrame : mouse.rightButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(button);
#else
            return false;
#endif
        }

        private static bool ButtonHeld(int button)
        {
#if SPOOKYWATER_INPUTSYSTEM && ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return false;
            return button == 0 ? mouse.leftButton.isPressed : mouse.rightButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(button);
#else
            return false;
#endif
        }

        private static Vector3 PointerPosition()
        {
#if SPOOKYWATER_INPUTSYSTEM && ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse == null ? Vector3.zero : (Vector3)mouse.position.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector3.zero;
#endif
        }

        private void UpdateWake()
        {
            if (!TryGetWaterHit(false, out var point, out var field))
            {
                _wakeField = null;
                return;
            }

            if (_wakeField == field && Vector3.Distance(_lastWakePoint, point) <= MaxWakeStepDistance)
            {
                field.CreateWakeSegment(_lastWakePoint, point, _wakeStrength);
            }

            _wakeField = field;
            _lastWakePoint = point;
        }

        private void TryCreateRipple(bool initialUp)
        {
            if (!TryGetWaterHit(_debugLog, out var point, out var field)) return;

            if (_debugLog) Debug.Log($"[WaterMouseRipples] ripple at {point}, up={initialUp}", this);
            field.CreateContactRippleAt(point, _rippleStrength, initialUp);
        }

        private bool TryGetWaterHit(bool log, out Vector3 point, out WaterField field)
        {
            point = default;
            field = null;

            var camera = _camera ? _camera : Water.Camera;
            if (!camera)
            {
                if (log) Debug.LogWarning("[WaterMouseRipples] no camera: assign one, add Water Camera Source to your camera, set Water.CameraResolver, or tag a camera MainCamera.", this);
                return false;
            }

            var ray = camera.ScreenPointToRay(PointerPosition());

            if (!Physics.Raycast(ray, out var hit, Mathf.Infinity, _raycastMask))
            {
                if (log) Debug.Log("[WaterMouseRipples] no collider hit.", this);
                return false;
            }

            var tile = hit.collider.GetComponentInParent<WaterTile>();
            if (!tile)
            {
                if (log) Debug.LogWarning($"[WaterMouseRipples] hit {hit.collider.name}, no WaterTile in its parents.", this);
                return false;
            }

            if (!tile.OwningField)
            {
                if (log) Debug.LogWarning($"[WaterMouseRipples] {tile.name} has no OwningField.", this);
                return false;
            }

            point = hit.point;
            field = tile.OwningField;
            return true;
        }
    }
}
