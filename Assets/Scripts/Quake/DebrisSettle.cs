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

        [Tooltip("Play testi 28 (ses föyü): aynı ses en az bu kadar arayla çalar (sn); çök-kapanda üst üste düşen taşlar " +
                 "rastgele anlarda sürekli çatırdıyordu. Arada düşenler sessiz kalır")]
        [SerializeField] private float minInterval = 0.45f;
        [Tooltip("Her çarpmada ses bu aralıkta rastgele kısılır: aynı tok ses tekrar tekrar aynı güçte gelmesin")]
        [SerializeField] private Vector2 volumeJitter = new Vector2(0.55f, 1f);

        private Rigidbody body;
        private float age;
        private float still;
        private bool sounded;
        private static readonly System.Collections.Generic.Dictionary<string, float> LastSoundTime =
            new System.Collections.Generic.Dictionary<string, float>();

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
            // Aynı anda ve art arda düşen parçalar tek tek çatırdamasın (anahtar başına en az minInterval)
            if (LastSoundTime.TryGetValue(impactKey, out float last) && Time.time - last < minInterval && Time.time >= last) return;
            LastSoundTime[impactKey] = Time.time;
            Sfx.PlayAt(impactKey, transform.position, impactVolume * Random.Range(volumeJitter.x, volumeJitter.y));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => LastSoundTime.Clear();

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
