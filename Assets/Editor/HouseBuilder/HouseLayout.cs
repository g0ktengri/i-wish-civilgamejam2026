using UnityEngine;

namespace Deprem.HouseBuilder
{
    /// <summary>
    /// Evin ölçüleri (GDD "Ev ölçüleri" tablosu). 1 Unity birimi = 1 m = 64 px.
    /// Koordinatlar: X sağa, Y yukarı, Z kameradan uzağa. Evin önü (kesit yüzü) Z = 0'dadır.
    /// Burası değişirse GDD'deki tablolar da güncellenir.
    /// </summary>
    internal static class HouseDims
    {
        public const float Thickness = 0.25f;
        public const float GroundHeight = 3.5f;
        public const float UpperHeight = 3.25f;
        public const float UpperFloorY = GroundHeight + Thickness;

        // Odalar arası kapı: ön kenardan 0,5 m içeride, 1 x 2 m
        public const float DoorZ0 = 0.5f;
        public const float DoorZ1 = 1.5f;
        public const float DoorHeight = 2f;

        // Karakterlerin yürüdüğü düzlem: kapı boşluğunun ortası
        public const float PlayerPlaneZ = 1f;

        // Merdiven koridorun arka şeridinde (karakter düzleminin arkasında), sağa doğru yükselir.
        // Karakter düzleminde olsaydı zemin kattaki koridor yolunu keserdi.
        // 15 basamak: her basamak 0,3125 x 0,25 m = 20 x 16 px
        public const float StairX0 = 9.5f;
        public const float StairLength = 4.6875f;
        public const int StairSteps = 15;
        public const float StairZ0 = 1.75f;
        public const float StairZ1 = 3.5f;

        // Üst kat döşemesindeki merdiven boşluğu (merdivende baş yüksekliği için)
        public const float StairHoleX0 = StairX0 + 2f;
        public const float StairHoleX1 = StairX0 + StairLength;

        // Deprem kabini merdivenin yüksek ucunun altında (basamak altı 2 m'den yüksek olan kısım)
        public static readonly Vector3 CabinMin = new Vector3(StairX0 + 2.6f, 0f, 2f);
        public static readonly Vector3 CabinMax = new Vector3(StairX0 + 4.1f, 2f, 3.5f);
    }

    internal sealed class RoomDef
    {
        /// <summary>Doku dosya adı ön eki: Assets/Art/Rooms/{Key}_arka.png vb.</summary>
        public readonly string Key;
        public readonly string Name;
        public readonly int Floor;
        public readonly float X0;
        public readonly float Width;
        public readonly float Depth;

        public RoomDef(string key, string name, int floor, float x0, float width, float depth)
        {
            Key = key;
            Name = name;
            Floor = floor;
            X0 = x0;
            Width = width;
            Depth = depth;
        }

        public float X1 => X0 + Width;
        public float FloorY => Floor == 0 ? 0f : HouseDims.UpperFloorY;
        public float Height => Floor == 0 ? HouseDims.GroundHeight : HouseDims.UpperHeight;
        public float CeilingY => FloorY + Height;
    }

    internal enum ItemMount
    {
        Floor,
        BackWall,
    }

    internal sealed class ItemDef
    {
        public int Number;
        /// <summary>Doku dosya adı ön eki: Assets/Art/Items/{Key}_on.png, _yan.png, _ust.png</summary>
        public string Key;
        public string Name;
        public string Room;
        /// <summary>En x boy x derinlik (m)</summary>
        public Vector3 Size;
        public float CenterX;
        /// <summary>Oda zemininden yükseklik (m)</summary>
        public float BaseY;
        public ItemMount Mount;
        /// <summary>Ayna ve çerçeve: yan ve üst yüzey düz renk</summary>
        public bool PlainSides;
    }

    internal sealed class MarkerDef
    {
        public readonly string Name;
        public readonly Vector3 Position;

        public MarkerDef(string name, Vector3 position)
        {
            Name = name;
            Position = position;
        }
    }

    /// <summary>
    /// Odalar, 9 sabitlenebilir eşya ve diğer sistemlerin kullanacağı işaret noktaları.
    /// Eşya ve işaret konumları ilk yerleşimdir; oynanış testine göre buradan ayarlanır.
    /// </summary>
    internal static class HouseLayout
    {
        // GDD sırası: odalar soldan sağa
        public static readonly RoomDef[] Rooms =
        {
            new RoomDef("oyuncu", "Oyuncunun odası", 0, 0f, 6f, 4.5f),
            new RoomDef("koridor", "Koridor ve merdiven", 0, 6f, 12f, 3.5f),
            new RoomDef("salon", "Salon", 0, 18f, 9f, 5.5f),
            new RoomDef("mutfak", "Mutfak", 0, 27f, 5.5f, 4.5f),
            new RoomDef("kardes", "Kardeşin odası", 1, 0f, 7f, 4.5f),
            new RoomDef("sahanlik", "Merdiven sahanlığı", 1, 7f, 11f, 3.5f),
            new RoomDef("ebeveyn", "Ebeveyn odası", 1, 18f, 9f, 5f),
        };

        // GDD "Sabitlenebilir eşyalar" tabloları
        public static readonly ItemDef[] Items =
        {
            new ItemDef { Number = 1, Key = "yatak_rafi", Name = "Yatağın üstündeki raf", Room = "oyuncu",
                Size = new Vector3(1f, 0.5f, 0.25f), CenterX = 2f, BaseY = 1.6f, Mount = ItemMount.BackWall },
            new ItemDef { Number = 2, Key = "kitaplik", Name = "Kitaplık", Room = "salon",
                Size = new Vector3(1f, 2f, 0.375f), CenterX = 22f, Mount = ItemMount.Floor },
            new ItemDef { Number = 3, Key = "tv_unitesi", Name = "TV ünitesi", Room = "salon",
                Size = new Vector3(1.5f, 0.5f, 0.5f), CenterX = 19.25f, Mount = ItemMount.Floor },
            new ItemDef { Number = 4, Key = "mutfak_dolabi", Name = "Mutfak üst dolapları", Room = "mutfak",
                Size = new Vector3(1.5f, 0.75f, 0.375f), CenterX = 30f, BaseY = 1.8f, Mount = ItemMount.BackWall },
            // Merdivenin altında: basamak altı burada 1 m'den yüksek
            new ItemDef { Number = 5, Key = "ayakkabilik", Name = "Ayakkabılık", Room = "koridor",
                Size = new Vector3(1f, 1f, 0.375f), CenterX = HouseDims.StairX0 + 1.85f, Mount = ItemMount.Floor },
            new ItemDef { Number = 6, Key = "ayna", Name = "Merdivendeki ayna", Room = "sahanlik",
                Size = new Vector3(0.75f, 1f, 0.0625f), CenterX = 8f, BaseY = 1f, Mount = ItemMount.BackWall, PlainSides = true },
            new ItemDef { Number = 7, Key = "oyuncak_rafi", Name = "Oyuncak rafı", Room = "kardes",
                Size = new Vector3(1f, 1.5f, 0.375f), CenterX = 3.5f, Mount = ItemMount.Floor },
            new ItemDef { Number = 8, Key = "gardirop", Name = "Gardırop", Room = "ebeveyn",
                Size = new Vector3(1.5f, 2f, 0.5f), CenterX = 23.5f, Mount = ItemMount.Floor },
            new ItemDef { Number = 9, Key = "cerceve", Name = "Ebeveyn yatağının üstündeki çerçeve", Room = "ebeveyn",
                Size = new Vector3(0.75f, 0.5f, 0.0625f), CenterX = 20.5f, BaseY = 1.4f, Mount = ItemMount.BackWall, PlainSides = true },
        };

        // Yerde duran eşyaların ön yüzü bu Z'de: devrilince karakter düzlemine düşerler
        public const float FloorItemFrontZ = 1.5f;

        private const float Z = HouseDims.PlayerPlaneZ;
        private const float Up = HouseDims.UpperFloorY;

        public static readonly MarkerDef[] Markers =
        {
            new MarkerDef("Marker_PlayerBed", new Vector3(2f, 0f, 3.6f)),
            new MarkerDef("Marker_PlayerStart", new Vector3(3f, 0f, Z)),
            new MarkerDef("Marker_Father", new Vector3(22f, 0f, Z)),
            new MarkerDef("Marker_Mother", new Vector3(23.5f, Up, Z)),
            new MarkerDef("Marker_Sibling", new Vector3(3.5f, Up, Z)),
            new MarkerDef("Marker_Bag", new Vector3(HouseDims.StairX0 + 4.6f, 0f, 1.3f)),
            new MarkerDef("Marker_Flashlight", new Vector3(28.5f, 0.9f, 3f)),
            new MarkerDef("Marker_FirstAid", new Vector3(19.5f, Up + 0.6f, 4.5f)),
            new MarkerDef("Marker_CabinEntrance", new Vector3(HouseDims.StairX0 + 3.35f, 0f, Z)),
            // Merdiven yolunun iki ucu (asıl yol House/Stairs üzerindeki StairPath'te)
            new MarkerDef("Marker_StairBottom", new Vector3(HouseDims.StairX0 - 0.2f, 0f, Z)),
            new MarkerDef("Marker_StairTop", new Vector3(HouseDims.StairHoleX1 + 0.3f, Up, Z)),
        };

        public static RoomDef Room(string key)
        {
            foreach (var room in Rooms)
                if (room.Key == key) return room;
            throw new System.ArgumentException("Bilinmeyen oda: " + key);
        }

        public static bool HasNeighbor(RoomDef room, bool right)
        {
            foreach (var other in Rooms)
            {
                if (other == room || other.Floor != room.Floor) continue;
                if (right && Mathf.Approximately(other.X0, room.X1)) return true;
                if (!right && Mathf.Approximately(other.X1, room.X0)) return true;
            }
            return false;
        }

        /// <summary>Zemin kat odasının üstünde bir üst kat odası var mı (mutfağın üstü kapalı çatı)</summary>
        public static bool HasRoomAbove(RoomDef room)
        {
            if (room.Floor != 0) return false;
            foreach (var other in Rooms)
                if (other.Floor == 1 && other.X0 < room.X1 - 0.01f && other.X1 > room.X0 + 0.01f) return true;
            return false;
        }
    }
}
