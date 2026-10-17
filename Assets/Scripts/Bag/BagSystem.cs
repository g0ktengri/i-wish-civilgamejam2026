using System;
using Deprem.Core;
using UnityEngine;

namespace Deprem.Bag
{
    // Deprem çantası: malzemeler evin farklı yerlerinde, çanta kabinin yanında (Marker_Bag).
    // Ruh fazında ruh bir malzemeyi alır (taşı-yerleştir minigame'i), çantaya kadar taşır,
    // F ile çantaya koyar → GameState.AddBagItem. Aynı anda tek malzeme taşınır.
    // Ruh fazı biterken taşınan malzeme yerine döner. Objeleri kendisi kurar.
    public class BagSystem : MonoBehaviour
    {
        [Serializable]
        public class SupplyDef
        {
            public BagItemId id;
            public bool enabled = true;
            [Tooltip("Malzemenin duracağı işaret noktası")]
            public string markerName;
            public Vector3 offset;
            public Color color = Color.white;
            [Tooltip("Mehmet'in modeli (Prefabs/Mehmet/BagItems/BagItem_{id}, pivot taban ortası, sarı kontur). " +
                     "Prefab kurulurken bağlanır; yoksa ikon, o da yoksa renkli küp")]
            public GameObject model;
        }

        [SerializeField] SupplyDef[] supplies =
        {
            new SupplyDef { id = BagItemId.FirstAidKit, markerName = "Marker_FirstAid", color = new Color(0.85f, 0.2f, 0.2f) },
            new SupplyDef { id = BagItemId.Flashlight, markerName = "Marker_Flashlight", color = new Color(0.95f, 0.8f, 0.25f) },
            // Ekstralar (GDD: sadece her şey bittiyse); işaret noktası yok, açılınca yer verilmeli
            new SupplyDef { id = BagItemId.Water, enabled = false, markerName = "Marker_Flashlight", offset = new Vector3(0.6f, 0f, 0f), color = new Color(0.4f, 0.7f, 1f) },
            new SupplyDef { id = BagItemId.Whistle, enabled = false, markerName = "Marker_FirstAid", offset = new Vector3(0.6f, 0f, 0f), color = new Color(0.7f, 0.7f, 0.75f) },
        };

        [SerializeField] string bagMarkerName = "Marker_Bag";
        [SerializeField] float supplySize = 0.3f;  // 32 px ≈ 0,5 m; okunurluk için biraz küçük
        [SerializeField] Vector3 bagSize = new Vector3(0.6f, 0.6f, 0.35f); // 48 px ≈ 0,75 m
        [SerializeField] Color bagColor = new Color(0.9f, 0.45f, 0.15f);
        [Tooltip("M23: Mehmet'in çanta modeli (Prefabs/Mehmet/BagItems/EarthquakeBag, pivot taban, sarı kontur). " +
                 "Prefab gelince kendiliğinden bağlanır; yoksa deprem_cantasi sprite'ı ya da küp")]
        [SerializeField] GameObject bagModel;
        [SerializeField] float interactRange = 1.2f;
        [SerializeField] Vector3 carryOffset = new Vector3(0.35f, -0.25f, 0f);

        [Header("Görseller (Assets/Art/UI, prefab kurulurken bağlanır)")]
        [Tooltip("ikon_ilk_yardim.png, 32 x 32: dünyada ve UI'da aynı")]
        [SerializeField] Sprite firstAidIcon;
        [Tooltip("ikon_el_feneri.png, 32 x 32")]
        [SerializeField] Sprite flashlightIcon;
        [Tooltip("ikon_su.png, 32 x 32")]
        [SerializeField] Sprite waterIcon;
        [Tooltip("ikon_duduk.png, 32 x 32")]
        [SerializeField] Sprite whistleIcon;
        [Tooltip("deprem_cantasi.png, 48 x 48: kabinin yanındaki çanta")]
        [SerializeField] Sprite bagSprite;

        public static BagSystem Instance { get; private set; }
        public static BagItemId Carrying { get; private set; }
        public static event Action<BagItemId> CarryingChanged;

        public float InteractRange => interactRange;
        public Vector3 CarryOffset => carryOffset;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // İkon yokken HUD'da kullanılan renk (dünyadaki geçici küplerle aynı)
        public Color ColorFor(BagItemId id)
        {
            foreach (var def in supplies)
                if (def.id == id) return def.color;
            return Color.white;
        }

        public Sprite IconFor(BagItemId id)
        {
            switch (id)
            {
                case BagItemId.FirstAidKit: return firstAidIcon;
                case BagItemId.Flashlight: return flashlightIcon;
                case BagItemId.Water: return waterIcon;
                case BagItemId.Whistle: return whistleIcon;
                default: return null;
            }
        }

        void Start()
        {
            SetCarrying(BagItemId.None);

            foreach (var def in supplies)
            {
                if (!def.enabled || def.id == BagItemId.None) continue;
                var marker = GameObject.Find(def.markerName);
                if (!marker)
                {
                    Debug.LogWarning($"[Bag] İşaret noktası yok: {def.markerName} ({def.id})");
                    continue;
                }
                BagSupply.Create(this, def.id, marker.transform.position + def.offset, supplySize, def.color,
                    IconFor(def.id), def.model);
            }

            var bagMarker = GameObject.Find(bagMarkerName);
            if (bagMarker) BagDropPoint.Create(this, bagMarker.transform.position, bagSize, bagColor, bagSprite, bagModel);
            else Debug.LogWarning($"[Bag] İşaret noktası yok: {bagMarkerName}");
        }

        void OnEnable() => GameEvents.PhaseChanged += HandlePhaseChanged;
        void OnDisable() => GameEvents.PhaseChanged -= HandlePhaseChanged;

        // Ruh fazı biterken çantaya konmamış malzeme yerine döner
        void HandlePhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Spirit && Carrying != BagItemId.None) SetCarrying(BagItemId.None);
        }

        internal static void SetCarrying(BagItemId id)
        {
            if (Carrying == id) return;
            Carrying = id;
            CarryingChanged?.Invoke(id);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Instance = null;
            Carrying = BagItemId.None;
            CarryingChanged = null;
        }
    }
}
