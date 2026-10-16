using UnityEngine;

namespace Deprem.Player
{
    /// <summary>
    /// Evin her katının iç sınırları (X, Y). HouseBuilder doldurur. Oyuncu bu sınırları aşamaz;
    /// ruh da (Berke) aynı sınırları kullanabilir: HouseBounds.Instance.ClampToHouse(...)
    /// </summary>
    public sealed class HouseBounds : MonoBehaviour
    {
        [System.Serializable]
        public struct Floor
        {
            public float minX;
            public float maxX;
            public float floorY;
            public float ceilingY;
        }

        [SerializeField] private Floor[] floors;

        public static HouseBounds Instance { get; private set; }
        public Floor[] Floors => floors;

        public void Configure(Floor[] houseFloors)
        {
            floors = houseFloors;
        }

        private void OnEnable()
        {
            Activate();
        }

        /// <summary>OnEnable'da çağrılır; edit mode testleri için ayrıca çağrılabilir.</summary>
        public void Activate()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Bu yükseklikteki kat (ayak hizası); bulunamazsa en yakın kat</summary>
        public Floor FloorAt(float y)
        {
            var best = floors[0];
            float bestDistance = float.MaxValue;
            foreach (var floor in floors)
            {
                if (y >= floor.floorY - 0.5f && y < floor.ceilingY) return floor;
                float distance = Mathf.Abs(y - floor.floorY);
                if (distance < bestDistance)
                {
                    best = floor;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>X'i bulunduğu katın iç sınırlarına sıkıştırır (margin: gövde yarıçapı)</summary>
        public float ClampX(Vector3 position, float margin)
        {
            var floor = FloorAt(position.y);
            return Mathf.Clamp(position.x, floor.minX + margin, floor.maxX - margin);
        }

        /// <summary>Uçan ruh gibi katlar arası serbest objeler için: evin dış kutusuna sıkıştırır</summary>
        public Vector3 ClampToHouse(Vector3 position, float margin)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var floor in floors)
            {
                minX = Mathf.Min(minX, floor.minX);
                maxX = Mathf.Max(maxX, floor.maxX);
                minY = Mathf.Min(minY, floor.floorY);
                maxY = Mathf.Max(maxY, floor.ceilingY);
            }
            position.x = Mathf.Clamp(position.x, minX + margin, maxX - margin);
            position.y = Mathf.Clamp(position.y, minY, maxY - margin);
            // Üst katın olmadığı yerde (mutfağın üstü) üst kat yüksekliğine çıkmasın
            var at = FloorAt(position.y);
            position.x = Mathf.Clamp(position.x, at.minX + margin, at.maxX - margin);
            return position;
        }
    }
}
