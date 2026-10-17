using Deprem.Core;
using UnityEngine;

namespace Deprem.Loop
{
    // Kayıt (M42): her döngü başında döngü numarası ve keşke sayısı PlayerPrefs'e yazılır; ana menüdeki "Devam et"
    // kayıtlı döngüden başlatır. Yetenekler (kalkma, ruh fazı) döngü numarasından çıkar; eşyalar, çanta ve aile her
    // denemede zaten sıfırlanır, kaydedilmez. Oyun kazanılınca (final ekranı) kayıt silinir; "Yeni oyun" da siler.
    public static class SaveGame
    {
        const string LoopKey = "save.loop";
        const string FailedKey = "save.failed";

        public static bool Exists => PlayerPrefs.GetInt(LoopKey, 0) > 0;
        public static int Loop => Mathf.Clamp(PlayerPrefs.GetInt(LoopKey, 1), 1, GameConstants.LoopCount);
        // "Devam et"te GameState.RestoreProgress ile geri yüklenir (LoopManager.ContinueFromSave)
        public static int FailedAttempts => PlayerPrefs.GetInt(FailedKey, 0);

        public static void Save(int loop, int failedAttempts)
        {
            PlayerPrefs.SetInt(LoopKey, Mathf.Clamp(loop, 1, GameConstants.LoopCount));
            PlayerPrefs.SetInt(FailedKey, Mathf.Max(0, failedAttempts));
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(LoopKey);
            PlayerPrefs.DeleteKey(FailedKey);
            PlayerPrefs.Save();
        }

        static void HandleLoopStarted(int loop) => Save(loop, GameState.FailedAttempts);
        static void HandleGameWon() => Clear();

        // GameEvents abonelerini SubsystemRegistration'da temizler; ondan sonra, domain reload kapalıyken de tek abonelik
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Subscribe()
        {
            GameEvents.LoopStarted -= HandleLoopStarted;
            GameEvents.GameWon -= HandleGameWon;
            GameEvents.LoopStarted += HandleLoopStarted;
            GameEvents.GameWon += HandleGameWon;
        }
    }
}
