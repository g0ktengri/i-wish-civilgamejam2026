using System.Collections.Generic;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Çök-kapan-tutun için güvenli nokta (sağlam bir eşyanın yanı, devrilecek eşyalardan uzak).
    /// Sarsıntıda yerde parlar; C sadece bu noktalardan birindeyken sayılır (GDD: "Sağlam bir eşyanın yanında çök").
    /// HouseBuilder kurar.
    /// </summary>
    public sealed class SafeSpot : MonoBehaviour
    {
        private static readonly List<SafeSpot> AllSpots = new List<SafeSpot>();
        public static IReadOnlyList<SafeSpot> All => AllSpots;

        [Tooltip("Noktanın X ekseninde yarı genişliği (m)")]
        [SerializeField] private float halfWidth = 0.7f;
        [SerializeField] private GameObject visual;

        public void Configure(float width, GameObject glow)
        {
            halfWidth = width * 0.5f;
            visual = glow;
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
            foreach (var spot in AllSpots)
                if (spot.visual != null) spot.visual.SetActive(visible);
        }
    }
}
