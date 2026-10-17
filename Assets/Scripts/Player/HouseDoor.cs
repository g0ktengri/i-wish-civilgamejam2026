using Deprem.Core;
using UnityEngine;

namespace Deprem.Player
{
    /// <summary>
    /// Odalar arası kapı: başta kapalı, karakterin yolunu keser; bedenle F ile açılır ve açık kalır.
    /// Menteşe kapı boşluğunun arka kenarında: açılınca kanat karakter düzleminin arkasında, kameraya dönük durur
    /// ve oyuncunun olmadığı odaya doğru açılır. Oda örtüsü (Visual.RoomVisibility) açık kapıları okur.
    /// M17: arkasındaki eşya (jammedBy) devrilirse kapı açılmaz; oyuncu "kapı açılmıyor" der.
    /// HouseBuilder kurar.
    /// </summary>
    public sealed class HouseDoor : MonoBehaviour, IInteractable, IPromptSource
    {
        [Tooltip("Menteşe noktasında, kanadın kökü")]
        [SerializeField] private Transform hinge;
        [SerializeField] private Collider blocker;
        [Tooltip("Kapının iki yanındaki odalar (HouseLayout anahtarı)")]
        [SerializeField] private string leftRoom;
        [SerializeField] private string rightRoom;
        [SerializeField] private float openTime = 0.35f;
        [SerializeField] private float interactRange = 1.1f;
        [Tooltip("M17: bu eşya devrilirse kapının önü kapanır, açılmaz (yoksa None)")]
        [SerializeField] private ItemId jammedBy = ItemId.None;
        [SerializeField] private string jammedBubbleKey = "bubble.door_jammed";

        public bool IsOpen { get; private set; }
        public bool IsJammed { get; private set; }
        public ItemId JammedBy => jammedBy;

        /// <summary>M17: oyuncunun açmaya çalıştığı ama kilitli kalan kapının eşyası (yoksa None).
        /// RescueTimer keşke cümlesinde bu eşyaya öncelik verir.</summary>
        public static ItemId BlockedByItem { get; private set; }
        public string LeftRoom => leftRoom;
        public string RightRoom => rightRoom;

        private float targetAngle;
        private bool registered;

        public void Configure(Transform hingeTransform, Collider blockerCollider, string left, string right,
            ItemId jammingItem = ItemId.None)
        {
            hinge = hingeTransform;
            blocker = blockerCollider;
            leftRoom = left;
            rightRoom = right;
            jammedBy = jammingItem;
        }

        private void OnEnable()
        {
            EnsureRegistered();
            GameEvents.ItemToppled += HandleItemToppled;
        }

        private void OnDisable()
        {
            Interactables.Unregister(this);
            registered = false;
            GameEvents.ItemToppled -= HandleItemToppled;
        }

        private void HandleItemToppled(ItemId id)
        {
            if (jammedBy != ItemId.None && id == jammedBy && !IsOpen) IsJammed = true;
        }

        /// <summary>OnEnable'da çağrılır; edit mode testleri için ayrıca çağrılabilir.</summary>
        public void EnsureRegistered()
        {
            if (registered) return;
            registered = true;
            Interactables.Register(this);
        }

        // ------------------------------------------------------------------ F etkileşimi

        /// <summary>Kapının karakter düzlemindeki ortası (transform buraya konur)</summary>
        public Vector3 InteractionPoint => transform.position;
        public float InteractionRange => interactRange;
        public string PromptKey => "prompt.open_door";

        public bool CanInteract(PlayerController player) => !IsOpen;

        public void Interact(PlayerController player)
        {
            if (IsJammed)
            {
                BlockedByItem = jammedBy;
                var bubble = player.GetComponent<SpeechBubble>();
                if (bubble != null) bubble.Say(jammedBubbleKey, 3f);
                // Play testi 27: kapı açılmayınca oyuncu boş boş bekliyordu. Ruh fazı açılmış döngülerde ("Eyvah, kapı
                // açılmıyor!") artçı gelir, tavan çöker; keşke kapıyı kapatan eşya. İlk döngülerde oyuncu yukarı çıkıp
                // annenin durumunu görmeli (ruha dönüşme sekansı), orada bitmez.
                if (GameState.SpiritPhaseUnlocked && GameState.Phase == GamePhase.Rescue)
                    Deprem.Quake.QuakeDirector.RequestFatalAftershock(LoseReason.RescueTimeout, jammedBy);
                return;
            }
            Open(player.transform.position.x);
        }

        /// <summary>Kapıyı açar; kanat fromX'in olmadığı tarafa döner. Çarpışma hemen kalkar.</summary>
        public void Open(float fromX)
        {
            if (IsOpen) return;
            IsOpen = true;
            if (blocker != null) blocker.enabled = false;
            // Kanat menteşeden öne (-Z) uzanır; Y ekseninde +90° sola (-X), -90° sağa (+X) açar
            targetAngle = fromX < transform.position.x ? -90f : 90f;
        }

        private void Update()
        {
            if (!IsOpen || hinge == null) return;
            var target = Quaternion.Euler(0f, targetAngle, 0f);
            float speed = 90f / Mathf.Max(0.01f, openTime);
            hinge.localRotation = Quaternion.RotateTowards(hinge.localRotation, target, speed * Time.deltaTime);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => BlockedByItem = ItemId.None;
    }
}
