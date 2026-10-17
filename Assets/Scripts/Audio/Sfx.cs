using UnityEngine;

namespace Deprem.Audio
{
    /// <summary>
    /// Diğer sistemlerin ses çalması için tek giriş (Texts.Get gibi): Sfx.Play("minigame.latch").
    /// Anahtarlar ENTEGRASYON'daki ses tablosunda. Ses sistemi sahnede yoksa ya da anahtarın sesi yoksa
    /// hiçbir şey olmaz; çağıran taraf hata almaz. Sesler Time.timeScale'den etkilenmez (keşke ekranı, duraklatma).
    /// </summary>
    public static class Sfx
    {
        /// <summary>Ayarlar menüsü: genel ses düzeyi 0..1. Kaydetmek (PlayerPrefs) ayarlar menüsünün işi</summary>
        public static float MasterVolume { get; set; } = 1f;
        /// <summary>Ayarlar menüsü: müzik ve gerilim müziği 0..1</summary>
        public static float MusicVolume { get; set; } = 1f;
        /// <summary>Ayarlar menüsü: efektler, ortam sesleri ve adımlar 0..1</summary>
        public static float EffectsVolume { get; set; } = 1f;

        /// <summary>Ses sistemi (AudioDirector) kurulunca kendini buraya bağlar</summary>
        internal static ISoundBackend Backend;

        /// <summary>Ekrana bağlı (2D) tek seferlik ses</summary>
        public static void Play(string key, float volume = 1f) => Backend?.Play(key, volume, null, 1f);

        /// <summary>Perdesi değiştirilmiş ekran sesi (ör. konuşma balonu yazı sesi, kişiye göre)</summary>
        public static void PlayPitched(string key, float volume, float pitch) => Backend?.Play(key, volume, null, pitch);

        /// <summary>Dünyada bir noktadan gelen ses (eşya, aile üyesi)</summary>
        public static void PlayAt(string key, Vector3 position, float volume = 1f) => Backend?.Play(key, volume, position, 1f);

        /// <summary>Müzik ya da uzun ambiyans; aynı anda tek müzik çalar, öncekiyle yumuşak geçiş</summary>
        public static void PlayMusic(string key, float fadeSeconds = 1f) => Backend?.PlayMusic(key, fadeSeconds);

        public static void StopMusic(float fadeSeconds = 1f) => Backend?.StopMusic(fadeSeconds);

        /// <summary>Kurtarma süresinin gerilimi 0..1 (RescueTimer): uzak uğultu yükselir, kalp atışı hızlanır</summary>
        public static void SetTension(float value) => Backend?.SetTension(value);
    }

    internal interface ISoundBackend
    {
        void Play(string key, float volume, Vector3? position, float pitch);
        void PlayMusic(string key, float fadeSeconds);
        void StopMusic(float fadeSeconds);
        void SetTension(float value);
    }
}
