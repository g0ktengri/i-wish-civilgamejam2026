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
        [SerializeField] float interactRange = 1.2f;
        [SerializeField] Vector3 carryOffset = new Vector3(0.35f, -0.25f, 0f);

        public static BagItemId Carrying { get; private set; }
        public static event Action<BagItemId> CarryingChanged;

        public float InteractRange => interactRange;
        public Vector3 CarryOffset => carryOffset;

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
                BagSupply.Create(this, def.id, marker.transform.position + def.offset, supplySize, def.color);
            }

            var bagMarker = GameObject.Find(bagMarkerName);
            if (bagMarker) BagDropPoint.Create(this, bagMarker.transform.position, bagSize, bagColor);
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
            Carrying = BagItemId.None;
            CarryingChanged = null;
        }
    }
}
