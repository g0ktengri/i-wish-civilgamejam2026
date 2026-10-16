using Deprem.Core;
using Deprem.Spirit;
using UnityEngine;

namespace Deprem.Bag
{
    // Kabinin yanındaki deprem çantası. Ruh bir malzeme taşıyorsa F ile çantaya koyar.
    public class BagDropPoint : MonoBehaviour, ISpiritTarget
    {
        BagSystem system;

        public Vector3 Point => transform.position;
        public float Range => system ? system.InteractRange : 1.2f;
        public string PromptKey => "prompt.put_in_bag";
        public bool CanInteract => GameState.Phase == GamePhase.Spirit && BagSystem.Carrying != BagItemId.None;

        public static BagDropPoint Create(BagSystem system, Vector3 floorPosition, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "EarthquakeBag";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(system.transform, false);
            go.transform.position = floorPosition + new Vector3(0f, size.y * 0.5f, 0f);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().material.color = color;

            var drop = go.AddComponent<BagDropPoint>();
            drop.system = system;
            return drop;
        }

        void OnEnable() => SpiritTargets.Register(this);
        void OnDisable() => SpiritTargets.Unregister(this);

        public void Interact()
        {
            if (!CanInteract) return;
            var id = BagSystem.Carrying;
            BagSystem.SetCarrying(BagItemId.None);
            GameState.AddBagItem(id);
        }
    }
}
