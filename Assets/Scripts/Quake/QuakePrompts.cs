using Deprem.Core;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Deprem sırasında ekranda gösterilecek tuş ipucu (texts.csv anahtarı: prompt.stand_up, prompt.duck_cover).
    /// Değişince GameEvents.PromptChanged tetiklenir; HUD (Berke) onu dinleyip gösterir. Boş anahtar = ipucu yok.
    /// </summary>
    public static class QuakePrompts
    {
        public const string StandUp = "prompt.stand_up";
        public const string DuckCover = "prompt.duck_cover";
        // Güvenli masanın dışında C basılınca: "Burada olmaz! Parlayan masanın altına geç"
        public const string DuckCoverFind = "prompt.duck_cover_find";

        public static string Current { get; private set; } = "";

        public static void Set(string key)
        {
            key ??= "";
            if (key == Current) return;
            Current = key;
            GameEvents.RaisePromptChanged(key);
        }

        public static void Clear() => Set("");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Current = "";
        }
    }
}
