using System;
using UnityEngine;

namespace Deprem.Core
{
    // Sistemler birbirini doğrudan çağırmaz; bu event'lerle konuşur.
    // Durum değiştiren event'ler (ItemSecured, PhaseChanged vb.) GameState üzerinden tetiklenir;
    // diğerleri doğrudan Raise metotlarıyla.
    public static class GameEvents
    {
        // Döngü ve faz (GameState tetikler)
        public static event Action<int> LoopStarted;
        public static event Action<GamePhase> PhaseChanged;
        public static event Action<LoseReason, ItemId> LoopFailed;
        public static event Action GameWon;

        // Deprem (Mehmet tetikler)
        public static event Action ForeshockStarted;
        public static event Action MainQuakeStarted;
        public static event Action MainQuakeEnded;
        public static event Action AftershockStarted;
        public static event Action<ItemId> ItemToppled;
        public static event Action<int> PlayerInjured; // toplam yaralanma sayısı

        // Ruh fazı ve minigame'ler (Berke tetikler)
        public static event Action<float> SpiritTimeChanged; // kalan saniye
        public static event Action<ItemId, MinigameType> MinigameOpened;
        public static event Action<ItemId, bool> MinigameClosed; // bool: başarılı mı
        public static event Action<ItemId> ItemSecured;
        public static event Action<BagItemId> BagItemCollected;

        // Aile ve kabin (Mehmet tetikler)
        public static event Action<FamilyMemberId> FamilyMemberRescued;
        public static event Action<FamilyMemberId> FamilyMemberEnteredCabin;
        public static event Action PlayerEnteredCabin;

        // Genel
        public static event Action<bool> PauseChanged;
        public static event Action<string> PromptChanged; // texts.csv anahtarı; boş = ipucu yok (Mehmet tetikler, HUD gösterir)

        public static void RaiseLoopStarted(int loop) => LoopStarted?.Invoke(loop);
        public static void RaisePhaseChanged(GamePhase phase) => PhaseChanged?.Invoke(phase);
        public static void RaiseLoopFailed(LoseReason reason, ItemId item) => LoopFailed?.Invoke(reason, item);
        public static void RaiseGameWon() => GameWon?.Invoke();

        public static void RaiseForeshockStarted() => ForeshockStarted?.Invoke();
        public static void RaiseMainQuakeStarted() => MainQuakeStarted?.Invoke();
        public static void RaiseMainQuakeEnded() => MainQuakeEnded?.Invoke();
        public static void RaiseAftershockStarted() => AftershockStarted?.Invoke();
        public static void RaiseItemToppled(ItemId id) => ItemToppled?.Invoke(id);
        public static void RaisePlayerInjured(int injuries) => PlayerInjured?.Invoke(injuries);

        public static void RaiseSpiritTimeChanged(float secondsLeft) => SpiritTimeChanged?.Invoke(secondsLeft);
        public static void RaiseMinigameOpened(ItemId id, MinigameType type) => MinigameOpened?.Invoke(id, type);
        public static void RaiseMinigameClosed(ItemId id, bool success) => MinigameClosed?.Invoke(id, success);
        public static void RaiseItemSecured(ItemId id) => ItemSecured?.Invoke(id);
        public static void RaiseBagItemCollected(BagItemId id) => BagItemCollected?.Invoke(id);

        public static void RaiseFamilyMemberRescued(FamilyMemberId id) => FamilyMemberRescued?.Invoke(id);
        public static void RaiseFamilyMemberEnteredCabin(FamilyMemberId id) => FamilyMemberEnteredCabin?.Invoke(id);
        public static void RaisePlayerEnteredCabin() => PlayerEnteredCabin?.Invoke();

        public static void RaisePauseChanged(bool paused) => PauseChanged?.Invoke(paused);
        public static void RaisePromptChanged(string key) => PromptChanged?.Invoke(key ?? "");

        // Domain reload kapalıyken eski sahneden kalan abonelikler temizlensin
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearSubscribers()
        {
            LoopStarted = null;
            PhaseChanged = null;
            LoopFailed = null;
            GameWon = null;
            ForeshockStarted = null;
            MainQuakeStarted = null;
            MainQuakeEnded = null;
            AftershockStarted = null;
            ItemToppled = null;
            PlayerInjured = null;
            SpiritTimeChanged = null;
            MinigameOpened = null;
            MinigameClosed = null;
            ItemSecured = null;
            BagItemCollected = null;
            FamilyMemberRescued = null;
            FamilyMemberEnteredCabin = null;
            PlayerEnteredCabin = null;
            PauseChanged = null;
            PromptChanged = null;
        }
    }
}
