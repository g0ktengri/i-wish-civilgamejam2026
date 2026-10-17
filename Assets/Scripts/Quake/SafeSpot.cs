using System.Collections.Generic;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Çök-kapan-tutun için güvenli nokta: önde duran sağlam masanın altı (AFAD), devrilecek eşyalardan uzak.
    /// Sarsıntıda masanın konturu sarı parlar (M16; eski yeşil zemin şeridi kalktı);
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
        [Tooltip("Masanın kontur kopyaları (Deprem/Outline)")]
        [SerializeField] private Renderer[] outlineRenderers;
        [SerializeField] private Color outlineColor = new Color(1f, 0.92f, 0.55f);

        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private MaterialPropertyBlock block;

        public void Configure(float width, GameObject glow, Renderer[] tableOutline = null)
        {
            halfWidth = width * 0.5f;
            visual = glow;
            outlineRenderers = tableOutline;
        }

        private void SetVisible(bool visible)
        {
            if (visual != null) visual.SetActive(visible);
            if (outlineRenderers == null) return;
            block ??= new MaterialPropertyBlock();
            block.SetColor(OutlineColorId, outlineColor);
            foreach (var r in outlineRenderers)
            {
                if (r == null) continue;
                r.SetPropertyBlock(block);
                r.enabled = visible;
            }
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

        public static void ShowAll(bool visible)
        {
            foreach (var spot in AllSpots) spot.SetVisible(visible);
        }
    }
}
