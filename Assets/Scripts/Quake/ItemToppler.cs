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

        [Header("2.5D: arkada duran eşya")]
        [Tooltip("Açıksa devrilmeden önce sarsıntıyla öne, walkToZ'ye yürür: kanepe gibi arka duvarda durur, yine de " +
                 "karakterin düzlemine devrilir. Yan görünüşte Z hareketi pek seçilmez (HouseBuilder ayarlar)")]
        [SerializeField] private bool walkForward;
        [Tooltip("Yürüyüşün sonunda eşyanın merkez Z'si")]
        [SerializeField] private float walkToZ;
        [SerializeField] private float walkSeconds = 0.55f;

        public bool HasToppled { get; private set; }
        public bool HasSettled => HasToppled && walkTime < 0f && body.isKinematic;

        private Rigidbody body;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private float stillTime;
        private float walkTime = -1f;

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
            if (walkForward && Mathf.Abs(walkToZ - restPosition.z) > 0.01f)
            {
                walkTime = 0f; // Tick yürütür, sonra Release
                return;
            }
            Release();
        }

        private void Release()
        {
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
            if (!HasToppled) return;
            if (walkTime >= 0f)
            {
                Walk(dt);
                return;
            }
            if (body.isKinematic) return;
            bool still = body.linearVelocity.sqrMagnitude < 0.01f && body.angularVelocity.sqrMagnitude < 0.01f;
            stillTime = still ? stillTime + dt : 0f;
            if (stillTime >= settleTime) body.isKinematic = true;
        }

        // Depremde yerinden kayan mobilya gibi: sallanarak öne gelir, varınca fizikle devrilir
        private void Walk(float dt)
        {
            walkTime += dt;
            float t = Mathf.Clamp01(walkTime / Mathf.Max(0.01f, walkSeconds));
            var position = restPosition;
            position.z = Mathf.Lerp(restPosition.z, walkToZ, t * t * (3f - 2f * t));
            float wobble = rattleAngle * 2f * (1f - t);
            var rotation = restRotation * Quaternion.Euler(0f, 0f, Random.Range(-wobble, wobble));
            if (t >= 1f)
            {
                walkTime = -1f;
                rotation = restRotation;
            }
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            if (walkTime < 0f) Release();
        }

        /// <summary>
        /// Senaryolu düşüş (Play testi 17: yatan oyuncunun üstüne düşen raf içinden geçip kayboluyordu): eşya
        /// yerinden kopar, öne kapaklanarak hedefe düşer, küçük bir sekme yapar ve orada kalır (kinematik).
        /// Hedef, eşyanın kaplama kutusunun (collider'lar) merkezi; dönüş dünya uzayında.
        /// </summary>
        public System.Collections.IEnumerator DropOnto(Vector3 boundsCenter, Quaternion rotation, float duration)
        {
            var endPosition = DroppedPosition(boundsCenter, rotation);

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float fall = k * k; // yerçekimi gibi hızlanır
                transform.SetPositionAndRotation(Vector3.Lerp(restPosition, endPosition, fall),
                    Quaternion.Slerp(restRotation, rotation, Mathf.Sqrt(k)));
                yield return null;
            }
            // Çarpınca küçük sekme
            for (float t = 0f; t < 0.18f; t += Time.deltaTime)
            {
                transform.SetPositionAndRotation(endPosition + Vector3.up * Mathf.Sin(t / 0.18f * Mathf.PI) * 0.06f, rotation);
                yield return null;
            }
            transform.SetPositionAndRotation(endPosition, rotation);
        }

        /// <summary>Düşmüş halini anında kurar (önizleme ve testler için)</summary>
        public void PlaceDropped(Vector3 boundsCenter, Quaternion rotation)
        {
            transform.SetPositionAndRotation(DroppedPosition(boundsCenter, rotation), rotation);
            Physics.SyncTransforms();
        }

        // Kinematik yapar, yerine oturtur; kutu merkezi hedefe gelecek pivot konumunu son dönüşte ölçer
        private Vector3 DroppedPosition(Vector3 boundsCenter, Quaternion rotation)
        {
            Initialize();
            HasToppled = true;
            walkTime = -1f;
            body.isKinematic = true;
            transform.SetPositionAndRotation(restPosition, rotation);
            Physics.SyncTransforms();
            var position = boundsCenter - (CombinedBounds().center - transform.position);
            transform.SetPositionAndRotation(restPosition, restRotation);
            Physics.SyncTransforms();
            return position;
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
