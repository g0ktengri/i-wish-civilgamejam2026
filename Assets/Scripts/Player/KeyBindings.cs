using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deprem.Player
{
    /// <summary>
    /// Değiştirilebilir tuşlar (Play testi 23, ENTEGRASYON M40). Oyuncu, etkileşim, aile, kalkma ve çök-kapan buradan
    /// okur; Berke'nin ayarlar ekranı (Ayarlar > Kontroller) değiştirip Save() çağırır. Oyun açılırken PlayerPrefs'ten
    /// yüklenir. Ruh hareketi de buradan okunur (SpiritController, Berke).
    /// </summary>
    public static class KeyBindings
    {
        public static Key Left = Key.A;
        public static Key Right = Key.D;
        public static Key Jump = Key.Space;
        /// <summary>Eğilme ve çök-kapan-tutun (aynı tuş, S)</summary>
        public static Key Crouch = Key.S;
        /// <summary>Etkileşim, kalk, aileyi topla, kabine gir, yarayı sar</summary>
        public static Key Interact = Key.F;
        public static Key SpiritUp = Key.W;
        public static Key SpiritDown = Key.S;
        public static Key SpiritLeft = Key.A;
        public static Key SpiritRight = Key.D;

        /// <summary>Bir tuş değişince (ipucu yazıları güncellensin)</summary>
        public static event Action Changed;

        private const string Prefix = "key.";
        private static readonly string[] Names =
            { "Left", "Right", "Jump", "Crouch", "Interact", "SpiritUp", "SpiritDown", "SpiritLeft", "SpiritRight" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            ResetDefaults(false);
            foreach (var name in Names)
            {
                string saved = PlayerPrefs.GetString(Prefix + name, null);
                if (!string.IsNullOrEmpty(saved) && Enum.TryParse(saved, out Key key) && key != Key.None) Set(name, key, false);
            }
            Changed?.Invoke();
        }

        public static void Save()
        {
            foreach (var name in Names) PlayerPrefs.SetString(Prefix + name, Get(name).ToString());
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Varsayılan tuşlar (A/D, Space, S, F; ruh W/A/S/D)</summary>
        public static void ResetDefaults(bool save = true)
        {
            Left = Key.A; Right = Key.D; Jump = Key.Space; Crouch = Key.S; Interact = Key.F;
            SpiritUp = Key.W; SpiritDown = Key.S; SpiritLeft = Key.A; SpiritRight = Key.D;
            if (save) Save();
        }

        /// <summary>Ayarlar ekranı için: eylem adıyla oku / yaz ("Left", "Interact"...)</summary>
        public static Key Get(string action) => action switch
        {
            "Left" => Left, "Right" => Right, "Jump" => Jump, "Crouch" => Crouch, "Interact" => Interact,
            "SpiritUp" => SpiritUp, "SpiritDown" => SpiritDown, "SpiritLeft" => SpiritLeft, "SpiritRight" => SpiritRight,
            _ => Key.None,
        };

        public static void Set(string action, Key key, bool save = true)
        {
            switch (action)
            {
                case "Left": Left = key; break;
                case "Right": Right = key; break;
                case "Jump": Jump = key; break;
                case "Crouch": Crouch = key; break;
                case "Interact": Interact = key; break;
                case "SpiritUp": SpiritUp = key; break;
                case "SpiritDown": SpiritDown = key; break;
                case "SpiritLeft": SpiritLeft = key; break;
                case "SpiritRight": SpiritRight = key; break;
            }
            if (save) Save();
        }

        /// <summary>Ekranda gösterilecek kısa ad (ör. "F", "Space")</summary>
        public static string Label(Key key)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                var control = keyboard[key];
                if (control != null && !string.IsNullOrEmpty(control.displayName)) return control.displayName;
            }
            return key.ToString();
        }

        public static bool Held(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && key != Key.None && keyboard[key].isPressed;
        }

        public static bool Pressed(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && key != Key.None && keyboard[key].wasPressedThisFrame;
        }
    }
}
