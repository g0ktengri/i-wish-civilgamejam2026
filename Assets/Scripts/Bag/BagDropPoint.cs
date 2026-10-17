using System.Collections;
using Deprem.Core;
using Deprem.Spirit;
using Deprem.Visual;
using UnityEngine;

namespace Deprem.Bag
{
    // Kabinin yanındaki deprem çantası. Ruh bir malzeme taşıyorsa F ile çantaya koyar.
    public class BagDropPoint : MonoBehaviour, ISpiritTarget
    {
        const float FlashIntensity = 4f;
        const float FlashSeconds = 0.6f;

        [Tooltip("Malzeme taşınırken çanta hafifçe nabız gibi büyür (M23: daha belirgin)")]
        [SerializeField] float carryPulseScale = 0.06f;
        [SerializeField] float carryPulseSpeed = 5f;

        BagSystem system;
        Light flash;
        Light beacon;
        PickupHighlight pickup;
        Vector3 baseScale;

        public Vector3 Point => transform.position;
        public float Range => system ? system.InteractRange : 1.2f;
        public string PromptKey => "prompt.put_in_bag";
        public bool CanInteract => GameState.Phase == GamePhase.Spirit && BagSystem.Carrying != BagItemId.None;

        // Görünüş sırası: Mehmet'in modeli (M23, pivot taban, sarı kontur), sprite (tabanı yere basar), renkli küp
        public static BagDropPoint Create(BagSystem system, Vector3 floorPosition, Vector3 size, Color color,
            Sprite sprite, GameObject model = null)
        {
            GameObject go;
            float lightHeight = 0f;
            if (model)
            {
                go = Instantiate(model);
                foreach (var collider in go.GetComponentsInChildren<Collider>()) Destroy(collider);
                go.transform.SetParent(system.transform, true);
                go.transform.position = floorPosition;
                lightHeight = size.y * 0.5f;
            }
            else if (sprite)
            {
                go = new GameObject();
                go.AddComponent<SpriteRenderer>().sprite = sprite;
                go.transform.SetParent(system.transform, false);
                go.transform.position = floorPosition + new Vector3(0f, sprite.bounds.extents.y - sprite.bounds.center.y, 0f);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(system.transform, false);
                go.transform.position = floorPosition + new Vector3(0f, size.y * 0.5f, 0f);
                go.transform.localScale = size;
                go.GetComponent<Renderer>().material.color = color;
            }
            go.name = "EarthquakeBag";

            // Malzeme konunca çantada kısa bir parlama (ENTEGRASYON M13)
            var flash = new GameObject("Flash").AddComponent<Light>();
            flash.transform.SetParent(go.transform, false);
            flash.transform.localPosition = new Vector3(0f, lightHeight, -0.5f);
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.85f, 0.5f);
            flash.range = 2.5f;
            flash.shadows = LightShadows.None;
            flash.enabled = false;

            // Malzeme taşınırken çantayı gösteren sönük ışık
            var beacon = new GameObject("Beacon").AddComponent<Light>();
            beacon.transform.SetParent(go.transform, false);
            beacon.transform.localPosition = new Vector3(0f, lightHeight, -0.5f);
            beacon.type = LightType.Point;
            beacon.color = new Color(1f, 0.85f, 0.3f);
            beacon.range = 1.6f;
            beacon.intensity = 1.2f;
            beacon.shadows = LightShadows.None;
            beacon.enabled = false;

            var drop = go.AddComponent<BagDropPoint>();
            drop.system = system;
            drop.flash = flash;
            drop.beacon = beacon;
            drop.pickup = go.GetComponent<PickupHighlight>();
            drop.baseScale = go.transform.localScale;
            return drop;
        }

        // Kontur ruh fazında PickupHighlight'ın kendisinde; taşınırken çanta nabız gibi büyür ve ışık yanar
        void LateUpdate()
        {
            bool carrying = GameState.Phase == GamePhase.Spirit && BagSystem.Carrying != BagItemId.None;
            if (beacon && beacon.enabled != carrying) beacon.enabled = carrying;
            float pulse = carrying ? (Mathf.Sin(Time.time * carryPulseSpeed) * 0.5f + 0.5f) * carryPulseScale : 0f;
            transform.localScale = baseScale * (1f + pulse);
        }

        void OnEnable()
        {
            SpiritTargets.Register(this);
            GameEvents.BagItemCollected += HandleBagItemCollected;
        }

        void OnDisable()
        {
            SpiritTargets.Unregister(this);
            GameEvents.BagItemCollected -= HandleBagItemCollected;
        }

        void HandleBagItemCollected(BagItemId id)
        {
            if (!flash) return;
            StopAllCoroutines();
            StartCoroutine(Flash());
        }

        IEnumerator Flash()
        {
            flash.enabled = true;
            for (float t = 0f; t < FlashSeconds; t += Time.deltaTime)
            {
                flash.intensity = Mathf.Lerp(FlashIntensity, 0f, t / FlashSeconds);
                yield return null;
            }
            flash.enabled = false;
        }

        public void Interact()
        {
            if (!CanInteract) return;
            var id = BagSystem.Carrying;
            BagSystem.SetCarrying(BagItemId.None);
            GameState.AddBagItem(id);
        }
    }
}
