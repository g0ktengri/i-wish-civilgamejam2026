using System;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Sinematik sürüyor mu (intro, giriş kapısı yakın çekimi). ENTEGRASYON "Intro sinematiği": Berke'nin arayüzü
    /// IsPlaying iken sinema bantlarını gösterir, HUD'u gizler; CanSkip iken "Geç" ipucu gösterip Skip çağırır.
    /// Oyuncu yönlendiremez, tuş ipuçları gösterilmez (PlayerInteractor).
    /// </summary>
    public static class Cinematic
    {
        public static bool IsPlaying { get; private set; }
        /// <summary>Sadece intro geçilebilir</summary>
        public static bool CanSkip { get; private set; }

        /// <summary>Başlayınca ve bitince (CanSkip değişince de)</summary>
        public static event Action Changed;

        private static Action skip;

        public static void Begin(bool canSkip, Action onSkip = null)
        {
            IsPlaying = true;
            CanSkip = canSkip && onSkip != null;
            skip = onSkip;
            Changed?.Invoke();
        }

        public static void End()
        {
            if (!IsPlaying) return;
            IsPlaying = false;
            CanSkip = false;
            skip = null;
            Changed?.Invoke();
        }

        /// <summary>Geçilebilir sinematiği bitirir (intro: oyuncu yatağında, gece olur)</summary>
        public static void Skip()
        {
            if (!IsPlaying || !CanSkip) return;
            CanSkip = false;
            var action = skip;
            skip = null;
            Changed?.Invoke();
            action?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            IsPlaying = false;
            CanSkip = false;
            skip = null;
            Changed = null;
        }
    }
}
