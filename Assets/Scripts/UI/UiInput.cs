using UnityEngine.InputSystem;

namespace Deprem.UI
{
    // Menü ve ekranlarda ortak tuş okuma (Keyboard.current / Mouse.current, ENTEGRASYON B5).
    public static class UiInput
    {
        public static bool Confirm()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.fKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                                        || keyboard.numpadEnterKey.wasPressedThisFrame
                                        || keyboard.spaceKey.wasPressedThisFrame);
        }

        public static bool ConfirmOrClick()
        {
            var mouse = Mouse.current;
            return Confirm() || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        }

        public static bool Back()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }

        public static bool Up()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame);
        }

        public static bool Down()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame);
        }
    }
}
