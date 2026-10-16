using System.Collections.Generic;
using Deprem.Core;
using Deprem.Visual;
using UnityEngine;

namespace Deprem.Spirit
{
    // Ruh fazında sabitlenmemiş eşyaları vurgular.
    // Eşyada Mehmet'in ItemOutline'ı varsa kontur kendi kendine yanar; burada sadece ruhun yanındaki
    // eşya SetEmphasis ile öne çıkarılır (ENTEGRASYON M8). Konturu olmayan eşyaya yedek olarak
    // önünde nabız gibi atan bir ışık konur.
    public class SpiritHighlighter : MonoBehaviour
    {
        [Header("Yedek ışık (konturu olmayan eşyalar)")]
        [SerializeField] Color color = new Color(1f, 0.85f, 0.45f);
        [SerializeField] float baseIntensity = 0.6f;
        [SerializeField] float pulseAmount = 0.4f;
        [SerializeField] float pulseSpeed = 3f;
        [SerializeField] float nearestIntensity = 2.5f;
        [SerializeField] float range = 1.8f;
        [Tooltip("Işığın eşyanın ön yüzünden kameraya doğru uzaklığı")]
        [SerializeField] float frontOffset = 0.4f;

        readonly Dictionary<SecurableItem, Light> lights = new Dictionary<SecurableItem, Light>();
        ItemOutline emphasized;

        void OnEnable()
        {
            GameEvents.PhaseChanged += HandlePhaseChanged;
            GameEvents.ItemSecured += HandleItemSecured;
        }

        void OnDisable()
        {
            GameEvents.PhaseChanged -= HandlePhaseChanged;
            GameEvents.ItemSecured -= HandleItemSecured;
            Clear();
        }

        void HandlePhaseChanged(GamePhase phase)
        {
            Clear();
            if (phase != GamePhase.Spirit) return;
            foreach (var item in SecurableItem.All)
                if (!item.IsSecured && !item.GetComponent<ItemOutline>())
                    lights[item] = CreateLight(item);
        }

        void HandleItemSecured(ItemId id)
        {
            var item = SecurableItem.Find(id);
            if (item && lights.TryGetValue(item, out var light))
            {
                if (light) Destroy(light.gameObject);
                lights.Remove(item);
            }
        }

        void Update()
        {
            var nearest = SpiritInteractor.Nearest;
            Emphasize(nearest ? nearest.GetComponent<ItemOutline>() : null);

            if (lights.Count == 0) return;
            float pulse = baseIntensity + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            foreach (var pair in lights)
                if (pair.Value) pair.Value.intensity = pair.Key == nearest ? nearestIntensity : pulse;
        }

        void Emphasize(ItemOutline outline)
        {
            if (outline == emphasized) return;
            if (emphasized) emphasized.SetEmphasis(false);
            emphasized = outline;
            if (emphasized) emphasized.SetEmphasis(true);
        }

        Light CreateLight(SecurableItem item)
        {
            var collider = item.GetComponent<Collider>();
            var bounds = collider ? collider.bounds : new Bounds(item.transform.position, Vector3.one);

            var light = new GameObject($"Highlight_{item.Id}").AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - frontOffset);
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.shadows = LightShadows.None;
            light.intensity = baseIntensity;
            return light;
        }

        void Clear()
        {
            Emphasize(null);
            foreach (var light in lights.Values)
                if (light) Destroy(light.gameObject);
            lights.Clear();
        }
    }
}
