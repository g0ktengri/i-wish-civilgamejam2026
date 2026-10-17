using System.Collections;
using Deprem.Audio;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Evin ana giriş kapısı (mutfağın sağ dış duvarı, intro sinematiği). Menteşe kapının arka kenarında; açılınca
    /// kanat mutfağa doğru döner, arkasındaki loş dış görünür. Oyuncu bu kapıyı kullanmaz: intro açıp kapar.
    /// HouseBuilder kurar.
    /// </summary>
    public sealed class EntranceDoor : MonoBehaviour
    {
        [SerializeField] private Transform hinge;
        [Tooltip("Açıkken kanadın Y açısı (derece; artı = mutfağa doğru, menteşe kapının arka kenarında)")]
        [SerializeField] private float openAngle = 80f;
        [SerializeField] private float swingTime = 0.6f;

        public static EntranceDoor Instance { get; private set; }

        /// <summary>Kapının iç tarafta, karakter düzlemindeki eşiği (oyuncu buradan girer)</summary>
        public Vector3 Threshold => new Vector3(transform.position.x - 0.45f, transform.position.y, 1f);

        public void Configure(Transform hingeTransform) => hinge = hingeTransform;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetOpenImmediate(bool open)
        {
            if (hinge != null) hinge.localRotation = Quaternion.Euler(0f, open ? openAngle : 0f, 0f);
        }

        public IEnumerator Swing(bool open)
        {
            if (hinge == null) yield break;
            if (!open) Sfx.PlayAt("cabin.door", transform.position, 0.6f);
            var from = hinge.localRotation;
            var to = Quaternion.Euler(0f, open ? openAngle : 0f, 0f);
            for (float t = 0f; t < swingTime; t += Time.deltaTime)
            {
                hinge.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / swingTime));
                yield return null;
            }
            hinge.localRotation = to;
        }
    }
}
