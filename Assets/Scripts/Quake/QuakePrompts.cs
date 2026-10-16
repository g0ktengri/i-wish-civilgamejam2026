using System;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Deprem sırasında ekranda gösterilecek tuş ipucu (texts.csv anahtarı: prompt.stand_up, prompt.duck_cover).
    /// HUD (Berke) Changed'e abone olup gösterir; boş anahtar = ipucu yok.
    /// Not: kalıcı yeri Core/GameEvents olmalı; Berke ile konuşulunca taşınır.
    /// </summary>
    public static class QuakePrompts
    {
        public const string StandUp = "prompt.stand_up";
        public const string DuckCover = "prompt.duck_cover";

        public static string Current { get; private set; } = "";
        public static event Action<string> Changed;

        public static void Set(string key)
        {
            key ??= "";
            if (key == Current) return;
            Current = key;
            Changed?.Invoke(key);
        }

        public static void Clear() => Set("");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Current = "";
            Changed = null;
        }
    }
}
