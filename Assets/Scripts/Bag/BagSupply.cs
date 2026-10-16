using Deprem.Core;
using Deprem.Minigames;
using Deprem.Spirit;
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

        // icon varsa dünyada sprite (64 PPU, ölçeklenmez), yoksa renkli küp
        public static BagSupply Create(BagSystem system, BagItemId id, Vector3 position, float size, Color color,
            Sprite icon)
        {
            GameObject go;
            if (icon)
            {
                go = new GameObject();
                go.AddComponent<SpriteRenderer>().sprite = icon;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());
                go.transform.localScale = Vector3.one * size;
                go.GetComponent<Renderer>().material.color = color;
            }
            go.name = $"BagSupply_{id}";
            go.transform.SetParent(system.transform, false);
            go.transform.position = position;

            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(go.transform, false);
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
            supply.visual = go.GetComponent<Renderer>();
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
            opening = host.Open(MinigameType.CarryAndPlace, success =>
            {
                opening = false;
                if (success && GameState.Phase == GamePhase.Spirit) PickUp();
            }, icon);
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
            if (visual.enabled != visible) visual.enabled = visible;
            glow.enabled = visible && GameState.Phase == GamePhase.Spirit;
        }
    }
}
