using System.Collections.Generic;
using Deprem.Core;
using Deprem.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deprem.Family
{
    public enum FamilyState
    {
        Waiting,   // kendi odasında, sağ
        Ducking,   // ana depremde çök-kapan-tutun
        Injured,   // yaralı, tedavi bekliyor (baba: pencere camı)
        Hit,       // üstüne eşya devrildi
        Following, // oyuncuyu takip ediyor
        InCabin,
    }

    /// <summary>
    /// Bir aile üyesi (GDD "Artçılar ve kurtarma"):
    ///   - dangerItem sabitlenmeden devrilirse üstüne düşer; oyuncu yanına gelince FamilyHitByItem.
    ///   - requiredBagItem çantada yoksa oyuncu yanına gelince MissingBagItem (baba: ilk yardım, kardeş: el feneri).
    ///   - Yaralıysa F basılı tutularak tedavi edilir, değilse F ile toplanır; sonra takip eder.
    /// Takip ve kabin FamilyDirector'da.
    /// </summary>
    public sealed class FamilyMember : MonoBehaviour, IInteractable
    {
        private static readonly List<FamilyMember> AllMembers = new List<FamilyMember>();
        public static IReadOnlyList<FamilyMember> All => AllMembers;

        [SerializeField] private FamilyMemberId id;
        [SerializeField] private string homeMarker = "Marker_Father";
        [Tooltip("Sabitlenmeden devrilirse bu üyenin üstüne düşen eşya")]
        [SerializeField] private ItemId dangerItem;
        [Tooltip("Kurtarmak için çantada olması gereken malzeme (yoksa None)")]
        [SerializeField] private BagItemId requiredBagItem;
        [Tooltip("Ana depremden sonra yaralı mı (baba: pencere camı, GDD)")]
        [SerializeField] private bool injuredAfterQuake;

        [SerializeField] private float treatHoldTime = 3f;
        [Tooltip("Oyuncu bu kadar yaklaşınca durumu fark eder (aynı kat, X ekseninde)")]
        [SerializeField] private float discoverRange = 3.5f;
        [SerializeField] private float interactRange = 1.2f;
        [Tooltip("Fark ettikten sonra keşke ekranına kadar süre")]
        [SerializeField] private float failDelay = 1.5f;
        [SerializeField] private Key interactKey = Key.F;

        [Header("Görünüş")]
        [SerializeField] private Transform visual;

        public FamilyMemberId Id => id;
        public FamilyState State { get; private set; } = FamilyState.Waiting;
        public ItemId HitBy { get; private set; }
        public bool IsTreating { get; private set; }
        public float TreatProgress => treatHoldTime > 0f ? Mathf.Clamp01(treatTimer / treatHoldTime) : 1f;
        public int Facing { get; private set; } = -1;

        private float discoverTimer;
        private float treatTimer;
        private PlayerController player;
        private bool registered;

        public static FamilyMember Find(FamilyMemberId memberId)
        {
            foreach (var member in AllMembers)
                if (member.id == memberId) return member;
            return null;
        }

        /// <summary>Listeyi boşaltır (Play başında; editör testlerinde sahne yeniden açılınca)</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ClearRegistry()
        {
            AllMembers.Clear();
        }

        private void OnEnable()
        {
            EnsureRegistered();
            GameEvents.PhaseChanged += OnPhaseChanged;
            GameEvents.ItemToppled += OnItemToppled;
        }

        private void OnDisable()
        {
            AllMembers.Remove(this);
            Interactables.Unregister(this);
            registered = false;
            GameEvents.PhaseChanged -= OnPhaseChanged;
            GameEvents.ItemToppled -= OnItemToppled;
        }

        private void Start()
        {
            PlaceAtHome();
        }

        /// <summary>OnEnable'da çağrılır; edit mode testleri için ayrıca çağrılabilir.</summary>
        public void EnsureRegistered()
        {
            if (registered) return;
            registered = true;
            if (!AllMembers.Contains(this)) AllMembers.Add(this);
            Interactables.Register(this);
        }

        public void PlaceAtHome()
        {
            var marker = GameObject.Find(homeMarker);
            if (marker != null) transform.position = marker.transform.position;
        }

        public void OnPhaseChanged(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.MainQuake:
                    if (State == FamilyState.Waiting) State = FamilyState.Ducking;
                    break;
                case GamePhase.Rescue:
                    if (HitBy != ItemId.None) State = FamilyState.Hit;
                    else if (State == FamilyState.Ducking || State == FamilyState.Waiting)
                        State = injuredAfterQuake ? FamilyState.Injured : FamilyState.Waiting;
                    break;
            }
        }

        public void OnItemToppled(ItemId item)
        {
            if (item != ItemId.None && item == dangerItem && State != FamilyState.Following && State != FamilyState.InCabin)
                HitBy = item;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            Tick(Time.deltaTime, keyboard != null && keyboard[interactKey].isPressed);
        }

        /// <summary>Kurtarma fazı kuralları. Update'ten çağrılır; testlerde elle çağrılabilir.</summary>
        public void Tick(float dt, bool interactHeld)
        {
            if (GameState.Phase != GamePhase.Rescue) return;
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (player == null) return;

            bool missingBagItem = requiredBagItem != BagItemId.None && !GameState.HasBagItem(requiredBagItem);
            bool lost = State == FamilyState.Hit || (missingBagItem && (State == FamilyState.Waiting || State == FamilyState.Injured));
            if (lost && IsNear(player.transform.position, discoverRange))
            {
                discoverTimer += dt;
                if (discoverTimer >= failDelay)
                {
                    if (State == FamilyState.Hit) GameState.FailLoop(LoseReason.FamilyHitByItem, HitBy);
                    else GameState.FailLoop(LoseReason.MissingBagItem);
                }
                return;
            }

            if (IsTreating)
            {
                if (!interactHeld || !IsNear(player.transform.position, interactRange * 1.5f))
                {
                    IsTreating = false;
                    treatTimer = 0f;
                }
                else
                {
                    treatTimer += dt;
                    if (treatTimer >= treatHoldTime) Rescue();
                }
            }
        }

        // ------------------------------------------------------------------ F etkileşimi

        public Vector3 InteractionPoint => transform.position;
        public float InteractionRange => interactRange;

        public bool CanInteract(PlayerController who)
        {
            if (GameState.Phase != GamePhase.Rescue || IsTreating) return false;
            if (State != FamilyState.Waiting && State != FamilyState.Injured) return false;
            return requiredBagItem == BagItemId.None || GameState.HasBagItem(requiredBagItem);
        }

        public void Interact(PlayerController who)
        {
            if (State == FamilyState.Injured)
            {
                // İlk yardım: F basılı tutulur (Tick sayar)
                IsTreating = true;
                treatTimer = 0f;
            }
            else
            {
                Rescue();
            }
        }

        private void Rescue()
        {
            IsTreating = false;
            State = FamilyState.Following;
            GameState.MarkRescued(id); // FamilyDirector FamilyMemberRescued'i dinleyip takibe alır
        }

        // ------------------------------------------------------------------ Takip ve kabin (FamilyDirector çağırır)

        public void SetFollowPosition(Vector3 position)
        {
            float dx = position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.001f) Facing = dx > 0f ? 1 : -1;
            transform.position = position;
        }

        public void EnterCabin(Vector3 inside)
        {
            State = FamilyState.InCabin;
            transform.position = inside;
            if (visual != null) visual.gameObject.SetActive(false);
            GameEvents.RaiseFamilyMemberEnteredCabin(id);
        }

        private bool IsNear(Vector3 position, float range)
        {
            var p = transform.position;
            return Mathf.Abs(position.x - p.x) <= range && Mathf.Abs(position.y - p.y) < 1.5f;
        }

        // ------------------------------------------------------------------ Görünüş (yer tutucu)

        private void LateUpdate()
        {
            if (visual == null) return;
            bool lying = State == FamilyState.Injured || State == FamilyState.Hit;
            bool ducking = State == FamilyState.Ducking;
            visual.localRotation = lying ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            visual.localScale = new Vector3(Facing, ducking ? 0.6f : 1f, 1f);
        }
    }
}
