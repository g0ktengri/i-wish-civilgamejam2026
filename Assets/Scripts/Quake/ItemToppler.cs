using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Sabitlenebilir eşyanın devrilmesi: sarsıntıda tıkırdar, ana depremde fizikle öne (oyuncunun
    /// düzlemine) devrilir, durunca sabitlenip engel olur. Sabitlenip sabitlenmediğine QuakeDirector bakar.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ItemToppler : MonoBehaviour
    {
        [Tooltip("Devrilirken üst kenara verilen öne itme hızı (m/s)")]
        [SerializeField] private float pushSpeed = 1.6f;
        [Tooltip("Bu kadar süre hareketsiz kalınca yerinde sabitlenir (sn)")]
        [SerializeField] private float settleTime = 0.6f;
        [Tooltip("Sarsıntıda tıkırdama açısı (derece)")]
        [SerializeField] private float rattleAngle = 1.5f;

        public bool HasToppled { get; private set; }
        public bool HasSettled => HasToppled && body.isKinematic;

        private Rigidbody body;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private float stillTime;

        private void Awake()
        {
            Initialize();
        }

        /// <summary>Awake'te çağrılır; edit mode testleri için ayrıca çağrılabilir.</summary>
        public void Initialize()
        {
            if (body != null) return;
            body = GetComponent<Rigidbody>();
            restPosition = transform.position;
            restRotation = transform.rotation;
        }

        /// <summary>Sarsıntı sırasında her kare çağrılır; 0 verilince eşya yerine oturur.</summary>
        public void Rattle(float intensity)
        {
            if (HasToppled) return;
            if (intensity <= 0f)
            {
                transform.SetPositionAndRotation(restPosition, restRotation);
                return;
            }
            float angle = rattleAngle * intensity;
            transform.rotation = restRotation * Quaternion.Euler(Random.Range(-angle, angle) * 0.3f, 0f, Random.Range(-angle, angle));
        }

        public void Topple()
        {
            if (HasToppled) return;
            HasToppled = true;
            stillTime = 0f;
            transform.SetPositionAndRotation(restPosition, restRotation);
            body.isKinematic = false;
            body.WakeUp();

            var bounds = CombinedBounds();
            var top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            body.AddForceAtPosition(Vector3.back * pushSpeed * body.mass, top, ForceMode.Impulse);
        }

        private void FixedUpdate()
        {
            Tick(Time.fixedDeltaTime);
        }

        /// <summary>Durunca kinematik yap: oyuncu itip kaydıramasın, sabit engel olsun.</summary>
        public void Tick(float dt)
        {
            if (!HasToppled || body.isKinematic) return;
            bool still = body.linearVelocity.sqrMagnitude < 0.01f && body.angularVelocity.sqrMagnitude < 0.01f;
            stillTime = still ? stillTime + dt : 0f;
            if (stillTime >= settleTime) body.isKinematic = true;
        }

        public Bounds CombinedBounds()
        {
            var colliders = GetComponentsInChildren<Collider>();
            if (colliders.Length == 0) return new Bounds(transform.position, Vector3.zero);
            var bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++) bounds.Encapsulate(colliders[i].bounds);
            return bounds;
        }
    }
}
