using Deprem.Core;
using Deprem.Minigames;
using Deprem.Spirit;
using Deprem.Visual;
using UnityEngine;

namespace Deprem.Bag
{
    // Evdeki bir çanta malzemesi. Ruh fazında F → taşı-yerleştir minigame'i → ruh taşır.
    // Taşınırken ruhun yanında durur; taşıma iptal olursa yerine döner.
    public class BagSupply : MonoBehaviour, ISpiritTarget
    {
        const string SpiritObjectName = "Spirit"; // Kararlar: ruh objesinin adı sabit

        BagSystem system;
        BagItemId id;
        Vector3 home;
        Light glow;
        Renderer visual;
        PickupHighlight pickup;
        bool shown = true;
        Sprite icon;
        Transform spirit;
        bool opening;

        public BagItemId Id => id;
        public Vector3 Point => transform.position;
        public float Range => system ? system.InteractRange : 1.2f;
        public string PromptKey => "prompt.pick_up";

        public bool CanInteract =>
            !opening && GameState.Phase == GamePhase.Spirit && BagSystem.Carrying == BagItemId.None &&
            !GameState.HasBagItem(id);

        // position: işaret noktası (masa / komodin yüzeyi). Görünüş sırası: Mehmet'in modeli (pivot taban ortası,
        // sarı kontur), ikon sprite'ı (64 PPU), renkli küp. Sprite ve küp merkezli: yüzeye gömülmesin diye yarım boy yukarı.
        public static BagSupply Create(BagSystem system, BagItemId id, Vector3 position, float size, Color color,
            Sprite icon, GameObject model = null)
        {
            GameObject go;
            float centerHeight = size * 0.5f;
            if (model)
            {
                go = Instantiate(model);
                foreach (var collider in go.GetComponentsInChildren<Collider>()) Destroy(collider);
            }
            else if (icon)
            {
                go = new GameObject();
                go.AddComponent<SpriteRenderer>().sprite = icon;
                position += Vector3.up * icon.bounds.extents.y;
                centerHeight = 0f;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());
                go.transform.localScale = Vector3.one * size;
                go.GetComponent<Renderer>().material.color = color;
                position += Vector3.up * centerHeight;
                centerHeight = 0f;
            }
            go.name = $"BagSupply_{id}";
            go.transform.SetParent(system.transform, false);
            go.transform.position = position;

            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(go.transform, false);
            glow.transform.position = position + Vector3.up * centerHeight; // modelin ortası
            glow.type = LightType.Point;
            glow.color = color;
            glow.range = 1.5f;
            glow.intensity = 1.5f;
            glow.shadows = LightShadows.None;

            var supply = go.AddComponent<BagSupply>();
            supply.system = system;
            supply.id = id;
            supply.home = position;
            supply.glow = glow;
            supply.pickup = go.GetComponent<PickupHighlight>();
            supply.visual = supply.pickup ? null : go.GetComponent<Renderer>();
            supply.icon = icon;
            return supply;
        }

        void OnEnable() => SpiritTargets.Register(this);
        void OnDisable() => SpiritTargets.Unregister(this);

        public void Interact()
        {
            if (!CanInteract) return;
            var host = MinigameHost.Instance;
            if (!host)
            {
                PickUp();
                return;
            }
            // Başlık malzemenin adı; el feneri kayan boşluklu, diğerleri tek duvar (MinigameHost.Open)
            opening = host.Open(MinigameType.CarryAndPlace, success =>
            {
                opening = false;
                if (success && GameState.Phase == GamePhase.Spirit) PickUp();
            }, icon, Localization.Texts.Get($"bag.{id}"), id == BagItemId.Flashlight ? "flashlight" : "box");
        }

        void PickUp() => BagSystem.SetCarrying(id);

        void LateUpdate()
        {
            bool carried = BagSystem.Carrying == id;
            bool inBag = GameState.HasBagItem(id);

            if (carried)
            {
                if (!spirit)
                {
                    var spiritObject = GameObject.Find(SpiritObjectName);
                    if (spiritObject) spirit = spiritObject.transform;
                }
                if (spirit) transform.position = spirit.position + system.CarryOffset;
            }
            else if (!inBag)
            {
                transform.position = home;
            }

            // Çantaya konan malzeme dünyada görünmez (çantanın içinde)
            var visible = !inBag;
            if (pickup)
            {
                if (shown != visible)
                {
                    shown = visible;
                    pickup.SetShown(visible);
                }
                pickup.SetHighlight(!carried); // taşınırken sarı kontur kapalı
            }
            else if (visual && visual.enabled != visible)
            {
                visual.enabled = visible;
            }
            glow.enabled = visible && GameState.Phase == GamePhase.Spirit;
        }
    }
}
