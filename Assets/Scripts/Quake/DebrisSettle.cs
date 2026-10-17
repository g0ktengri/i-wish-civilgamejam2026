using Deprem.Audio;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Düşen parça yere ya da bedenin üstüne oturunca yerinde donar (kinematik): kayıp gitmez, üst üste yığılan
    /// parçalar birbirini itip dağıtmaz (Play testi 17: yıkıntı üstümüzde kalsın).
    /// Play testi 20: tavandan düşenlerin sesi yoktu; ilk sert çarpmada düşme sesi (impactKey boşsa sessiz).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DebrisSettle : MonoBehaviour
    {
        [SerializeField] private float minAge = 0.35f;
        [SerializeField] private float stillTime = 0.25f;
        [Tooltip("Çarpınca çalan ses anahtarı (boş = sessiz)")]
        [SerializeField] private string impactKey;
        [SerializeField] private float impactVolume = 0.7f;

        private Rigidbody body;
        private float age;
        private float still;
        private bool sounded;
        private static float lastSoundTime;

        public void SetImpactSound(string key, float volume)
        {
            impactKey = key;
            impactVolume = volume;
        }

        private void Awake() => body = GetComponent<Rigidbody>();

        private void OnCollisionEnter(Collision collision)
        {
            if (sounded || string.IsNullOrEmpty(impactKey) || collision.relativeVelocity.magnitude < 1f) return;
            sounded = true;
            // Aynı anda düşen çok parça tek tek çatırdamasın
            if (Time.time - lastSoundTime < 0.06f) return;
            lastSoundTime = Time.time;
            Sfx.PlayAt(impactKey, transform.position, impactVolume);
        }

        private void FixedUpdate()
        {
            age += Time.fixedDeltaTime;
            if (age < minAge) return;
            bool slow = body.linearVelocity.sqrMagnitude < 0.04f && body.angularVelocity.sqrMagnitude < 0.25f;
            still = slow ? still + Time.fixedDeltaTime : 0f;
            if (still < stillTime) return;
            body.isKinematic = true;
            enabled = false;
        }
    }
}
