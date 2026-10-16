using UnityEngine;

namespace Deprem.Player
{
    /// <summary>
    /// Odalar arası kapı: başta kapalı, karakterin yolunu keser; bedenle F ile açılır ve açık kalır.
    /// Menteşe kapı boşluğunun arka kenarında: açılınca kanat karakter düzleminin arkasında, kameraya dönük durur
    /// ve oyuncunun olmadığı odaya doğru açılır. Oda örtüsü (Visual.RoomVisibility) açık kapıları okur.
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

        public bool IsOpen { get; private set; }
        public string LeftRoom => leftRoom;
        public string RightRoom => rightRoom;

        private float targetAngle;
        private bool registered;

        public void Configure(Transform hingeTransform, Collider blockerCollider, string left, string right)
        {
            hinge = hingeTransform;
            blocker = blockerCollider;
            leftRoom = left;
            rightRoom = right;
        }

        private void OnEnable() => EnsureRegistered();

        private void OnDisable()
        {
            Interactables.Unregister(this);
            registered = false;
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

        public void Interact(PlayerController player) => Open(player.transform.position.x);

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
    }
}
