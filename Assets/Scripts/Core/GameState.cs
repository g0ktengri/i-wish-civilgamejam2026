using System.Collections.Generic;
using UnityEngine;

namespace Deprem.Core
{
    // Sistemler arası ortak durum. Yazma metotları ilgili GameEvents event'ini de tetikler,
    // böylece durum ve event birbirinden kopmaz.
    public static class GameState
    {
        static readonly HashSet<ItemId> securedItems = new HashSet<ItemId>();
        static readonly HashSet<BagItemId> bagItems = new HashSet<BagItemId>();
        static readonly HashSet<FamilyMemberId> rescuedFamily = new HashSet<FamilyMemberId>();

        // Döngüler arasında kalır
        public static int CurrentLoop { get; private set; } = 1;
        public static int FailedAttempts { get; private set; } // Kurtarma süresine gizli +10 sn için
        public static LoseReason LastLoseReason { get; private set; }
        public static ItemId LastLoseItem { get; private set; }

        // Her denemede sıfırlanır (zaman geri sarılır)
        public static GamePhase Phase { get; private set; }
        public static int AftershockInjuries { get; private set; }
        public static IReadOnlyCollection<ItemId> SecuredItems => securedItems;
        public static IReadOnlyCollection<BagItemId> BagItems => bagItems;
        public static IReadOnlyCollection<FamilyMemberId> RescuedFamily => rescuedFamily;

        public static bool IsItemSecured(ItemId id) => securedItems.Contains(id);
        public static bool HasBagItem(BagItemId id) => bagItems.Contains(id);
        public static bool IsRescued(FamilyMemberId id) => rescuedFamily.Contains(id);

        public static bool SpiritPhaseUnlocked => CurrentLoop >= GameConstants.SpiritPhaseUnlockLoop;
        public static bool StandUpUnlocked => CurrentLoop >= GameConstants.StandUpUnlockLoop;
        public static GamePhase StartPhase => SpiritPhaseUnlocked ? GamePhase.Spirit : GamePhase.Foreshock;

        // Döngü yöneticisi (Berke) çağırır: yeni deneme başlar, deneme durumu sıfırlanır.
        public static void BeginAttempt(int loop)
        {
            CurrentLoop = Mathf.Clamp(loop, 1, GameConstants.LoopCount);
            securedItems.Clear();
            bagItems.Clear();
            rescuedFamily.Clear();
            AftershockInjuries = 0;
            Phase = GamePhase.None;
            GameEvents.RaiseLoopStarted(CurrentLoop);
            SetPhase(StartPhase);
        }

        public static void SetPhase(GamePhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            GameEvents.RaisePhaseChanged(phase);
        }

        public static void MarkItemSecured(ItemId id)
        {
            if (id == ItemId.None || !securedItems.Add(id)) return;
            GameEvents.RaiseItemSecured(id);
        }

        public static void AddBagItem(BagItemId id)
        {
            if (id == BagItemId.None || !bagItems.Add(id)) return;
            GameEvents.RaiseBagItemCollected(id);
        }

        public static void MarkRescued(FamilyMemberId id)
        {
            if (id == FamilyMemberId.None || !rescuedFamily.Add(id)) return;
            GameEvents.RaiseFamilyMemberRescued(id);
        }

        public static void AddAftershockInjury()
        {
            AftershockInjuries++;
            GameEvents.RaisePlayerInjured(AftershockInjuries);
            if (AftershockInjuries >= GameConstants.MaxAftershockInjuries)
                FailLoop(LoseReason.AftershockInjury);
        }

        // Kaybetme durumu: item, keşke cümlesindeki eşyadır (yoksa None).
        public static void FailLoop(LoseReason reason, ItemId item = ItemId.None)
        {
            if (Phase == GamePhase.Failed || Phase == GamePhase.Won) return;
            LastLoseReason = reason;
            LastLoseItem = item;
            FailedAttempts++;
            SetPhase(GamePhase.Failed);
            GameEvents.RaiseLoopFailed(reason, item);
        }

        public static void WinLoop()
        {
            if (Phase == GamePhase.Failed || Phase == GamePhase.Won) return;
            SetPhase(GamePhase.Won);
            GameEvents.RaiseGameWon();
        }

        // Ana menüden yeni oyun ya da bölüm seçimi
        public static void ResetAll()
        {
            CurrentLoop = 1;
            FailedAttempts = 0;
            LastLoseReason = LoseReason.None;
            LastLoseItem = ItemId.None;
            Phase = GamePhase.None;
            AftershockInjuries = 0;
            securedItems.Clear();
            bagItems.Clear();
            rescuedFamily.Clear();
        }

        // Kayıttan devam (M42): ilerlemeyi geri yükler, event tetiklemez. Ardından BeginAttempt(loop) çağrılır.
        public static void RestoreProgress(int loop, int failedAttempts)
        {
            ResetAll();
            CurrentLoop = Mathf.Clamp(loop, 1, GameConstants.LoopCount);
            FailedAttempts = Mathf.Max(0, failedAttempts);
        }

        // Domain reload kapalıyken Play'e girişte statik durum temizlensin
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => ResetAll();
    }
}
