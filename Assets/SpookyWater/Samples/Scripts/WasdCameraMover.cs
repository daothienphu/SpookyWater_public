using UnityEngine;

namespace SpookyWater.Samples
{
    [AddComponentMenu("SpookyWater/Samples/Wasd Camera Mover")]
    public class WasdCameraMover : MonoBehaviour
    {
        [Tooltip("Testing camera. Q/E move forward and backward along the camera's facing, Z/C raise and lower it (relative to the target when one is set), hold Shift to move faster. Works for orthographic and perspective cameras. Movement speed in world units per second.")]
        [SerializeField, Min(0f)] private float _moveSpeed = 5f;
        [Tooltip("Speed multiplier while Shift is held.")]
        [SerializeField, Min(1f)] private float _fastMoveMultiplier = 3f;

        [Tooltip("Optional: the camera keeps this target framed in X/Y. Leave empty for a fixed camera.")]
        [SerializeField] private Transform _followTarget;
        [Tooltip("Offset in world units from the target that the camera keeps in X and Y. Z/C change its height.")]
        [SerializeField] private Vector2 _followOffset = new(0f, 1f);
        [Tooltip("Higher = snappier follow.")]
        [SerializeField, Min(0f)] private float _followSharpness = 5f;

        private void LateUpdate()
        {
            var isFast = SampleInput.GetKey(KeyCode.LeftShift) || SampleInput.GetKey(KeyCode.RightShift);
            var speed = _moveSpeed * (isFast ? _fastMoveMultiplier : 1f);

            var move = Vector3.zero;
            if (SampleInput.GetKey(KeyCode.Q)) move += transform.forward;
            if (SampleInput.GetKey(KeyCode.E)) move -= transform.forward;

            if (move.sqrMagnitude > 0f)
            {
                transform.position += move.normalized * speed * Time.deltaTime;
            }

            var raise = 0f;
            if (SampleInput.GetKey(KeyCode.Z)) raise += 1f;
            if (SampleInput.GetKey(KeyCode.C)) raise -= 1f;
            if (raise != 0f)
            {
                if (_followTarget) _followOffset.y += raise * speed * Time.deltaTime;
                else transform.position += Vector3.up * (raise * speed * Time.deltaTime);
            }

            if (_followTarget)
            {
                var position = transform.position;
                var target = (Vector2)_followTarget.position + _followOffset;
                var t = 1f - Mathf.Exp(-_followSharpness * Time.deltaTime);
                position.x = Mathf.Lerp(position.x, target.x, t);
                position.y = Mathf.Lerp(position.y, target.y, t);
                transform.position = position;
            }
        }
    }
}
