using System.Collections.Generic;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Çök-kapan-tutun için güvenli nokta: önde duran sağlam masanın altı (AFAD), devrilecek eşyalardan uzak.
    /// Sarsıntıda masanın üst tablası ince sarı konturla parlar, altında yumuşak ışık yanar (M16);
    /// C sadece bu noktalardan birindeyken sayılır. HouseBuilder kurar.
    /// </summary>
    public sealed class SafeSpot : MonoBehaviour
    {
        private static readonly List<SafeSpot> AllSpots = new List<SafeSpot>();
        public static IReadOnlyList<SafeSpot> All => AllSpots;

        [Tooltip("Noktanın X ekseninde yarı genişliği (m)")]
        [SerializeField] private float halfWidth = 0.7f;
        [Tooltip("Eski zemin şeridi (artık kurulmuyor, boş kalabilir)")]
        [SerializeField] private GameObject visual;
        [Tooltip("Masanın üst tablasının ince konturu (Deprem/Outline, kontur_masa materyali)")]
        [SerializeField] private Renderer[] outlineRenderers;
        [Tooltip("Masanın altındaki yumuşak ışık")]
        [SerializeField] private Light underGlow;

        private float baseGlow = 1.6f;

        public void Configure(float width, GameObject glow, Renderer[] tableOutline = null, Light under = null)
        {
            halfWidth = width * 0.5f;
            visual = glow;
            outlineRenderers = tableOutline;
            underGlow = under;
        }

        private void Awake()
        {
            if (underGlow != null) baseGlow = underGlow.intensity;
        }

        private void SetVisible(bool visible)
        {
            if (visual != null) visual.SetActive(visible);
            if (underGlow != null) underGlow.enabled = visible;
            if (outlineRenderers == null) return;
            foreach (var r in outlineRenderers)
                if (r != null) r.enabled = visible;
        }

        private void OnEnable()
        {
            Register();
        }

        private void OnDisable()
        {
            AllSpots.Remove(this);
        }

        /// <summary>OnEnable'da çağrılır; edit mode testleri için ayrıca çağrılabilir.</summary>
        public void Register()
        {
            if (!AllSpots.Contains(this)) AllSpots.Add(this);
        }

        /// <summary>Ayak pozisyonu bu noktanın üstünde mi (aynı kat, X aralığı)</summary>
        public bool Contains(Vector3 feet)
        {
            var center = transform.position;
            return Mathf.Abs(feet.x - center.x) <= halfWidth && Mathf.Abs(feet.y - center.y) < 0.6f;
        }

        public static bool AnyContains(Vector3 feet)
        {
            foreach (var spot in AllSpots)
                if (spot.Contains(feet)) return true;
            return false;
        }

        /// <summary>Yanlış yerde C basılınca masaların altındaki ışık güçlenir: "buraya gel"</summary>
        public static void Emphasize(bool on)
        {
            foreach (var spot in AllSpots)
                if (spot.underGlow != null && spot.underGlow.enabled)
                    spot.underGlow.intensity = on ? spot.baseGlow * 2.5f : spot.baseGlow;
        }

        public static void ShowAll(bool visible)
        {
            foreach (var spot in AllSpots) spot.SetVisible(visible);
        }
    }
}
