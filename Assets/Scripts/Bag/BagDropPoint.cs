using System.Collections;
using Deprem.Core;
using Deprem.Spirit;
using UnityEngine;

namespace Deprem.Bag
{
    // Kabinin yanındaki deprem çantası. Ruh bir malzeme taşıyorsa F ile çantaya koyar.
    public class BagDropPoint : MonoBehaviour, ISpiritTarget
    {
        const float FlashIntensity = 4f;
        const float FlashSeconds = 0.6f;

        BagSystem system;
        Light flash;

        public Vector3 Point => transform.position;
        public float Range => system ? system.InteractRange : 1.2f;
        public string PromptKey => "prompt.put_in_bag";
        public bool CanInteract => GameState.Phase == GamePhase.Spirit && BagSystem.Carrying != BagItemId.None;

        // sprite varsa dünyada sprite (tabanı yere basar), yoksa renkli küp
        public static BagDropPoint Create(BagSystem system, Vector3 floorPosition, Vector3 size, Color color,
            Sprite sprite)
        {
            GameObject go;
            if (sprite)
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
            flash.transform.localPosition = new Vector3(0f, 0f, -0.5f);
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.85f, 0.5f);
            flash.range = 2.5f;
            flash.shadows = LightShadows.None;
            flash.enabled = false;

            var drop = go.AddComponent<BagDropPoint>();
            drop.system = system;
            drop.flash = flash;
            return drop;
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
