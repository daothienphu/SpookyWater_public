using UnityEngine;
#if SPOOKYWATER_INPUTSYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SpookyWater.Samples
{
    public static class SampleInput
    {
        public static bool GetKey(KeyCode key)
        {
#if SPOOKYWATER_INPUTSYSTEM && ENABLE_INPUT_SYSTEM
            var control = FindControl(key);
            return control != null && control.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(key);
#else
            return false;
#endif
        }

        public static bool GetKeyDown(KeyCode key)
        {
#if SPOOKYWATER_INPUTSYSTEM && ENABLE_INPUT_SYSTEM
            var control = FindControl(key);
            return control != null && control.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }

#if SPOOKYWATER_INPUTSYSTEM && ENABLE_INPUT_SYSTEM
        private static UnityEngine.InputSystem.Controls.KeyControl FindControl(KeyCode key)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return null;

            switch (key)
            {
                case KeyCode.A: return keyboard.aKey;
                case KeyCode.C: return keyboard.cKey;
                case KeyCode.D: return keyboard.dKey;
                case KeyCode.E: return keyboard.eKey;
                case KeyCode.J: return keyboard.jKey;
                case KeyCode.K: return keyboard.kKey;
                case KeyCode.L: return keyboard.lKey;
                case KeyCode.Q: return keyboard.qKey;
                case KeyCode.R: return keyboard.rKey;
                case KeyCode.S: return keyboard.sKey;
                case KeyCode.W: return keyboard.wKey;
                case KeyCode.Z: return keyboard.zKey;
                case KeyCode.Space: return keyboard.spaceKey;
                case KeyCode.LeftShift: return keyboard.leftShiftKey;
                case KeyCode.RightShift: return keyboard.rightShiftKey;
                default: return null;
            }
        }
#endif
    }
}
