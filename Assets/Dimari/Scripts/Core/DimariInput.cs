using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Dimari.Core
{
    /// <summary>
    /// Универсальная обёртка ввода для Unity 6.6 (поддерживает и новый Input System, и старый Input Manager).
    /// </summary>
    public static class DimariInput
    {
        public static Vector2 GetMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                float x = 0f;
                float y = 0f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;
                Vector2 v = new Vector2(x, y);
                if (v.sqrMagnitude > 1f) v.Normalize();
                return v;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            float lx = Input.GetAxisRaw("Horizontal");
            float ly = Input.GetAxisRaw("Vertical");
            Vector2 lv = new Vector2(lx, ly);
            if (lv.sqrMagnitude > 1f) lv.Normalize();
            return lv;
#else
            return Vector2.zero;
#endif
        }

        public static Vector2 GetLookDelta()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.delta.ReadValue() * 0.1f;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#else
            return Vector2.zero;
#endif
        }

        public static bool IsSprintHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
            return false;
#endif
        }

        public static bool IsJumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.spaceKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }

        public static bool IsInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F);
#else
            return false;
#endif
        }

        public static bool IsToggleCameraPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.vKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.V);
#else
            return false;
#endif
        }

        public static bool IsSkipOrContinuePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#else
            return false;
#endif
        }

        public static bool IsPauseMenuPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.escapeKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        public static bool IsQuickSavePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.f5Key.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F5);
#else
            return false;
#endif
        }

        public static bool IsSettingsShortcutPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.oKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.O);
#else
            return false;
#endif
        }
    }
}
