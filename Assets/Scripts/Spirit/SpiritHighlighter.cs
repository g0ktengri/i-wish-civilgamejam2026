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
    // M62 (Mehmet'in play testi 31: oyuncular hangi eşyanın önemli olduğunu bilmiyordu): birinin üstüne düşecek eşyalar
    // (yatak rafı ve her aile üyesinin DangerItem'ı) kırmızımsı konturlu ve daha parlak, diğerleri (ayna, çerçeve...)
    // soluk; yedek ışıkta da aynı ayrım.
    // M68 (Mehmet'in play testi 32: hâlâ tam belli olmuyordu): önemli konturlar kalın ve hızlı nabızlı (ItemOutline),
    // isteğe bağlılar daha soluk; önemli ve sabitlenmemiş eşyanın üstünde sallanan kırmızı pixel "!".
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
        [Tooltip("M62: önemli eşyanın yedek ışık rengi ve isteğe bağlı eşyanın ışık oranı")]
        [SerializeField] Color importantColor = new Color(1f, 0.5f, 0.38f);
        [SerializeField] float optionalIntensityScale = 0.4f;
        readonly HashSet<ItemId> important = new HashSet<ItemId>();

        [Header("M68: önemli eşyanın üstündeki ünlem")]
        [SerializeField] Color markColor = new Color(1f, 0.3f, 0.24f);
        [Tooltip("Eşyanın tepesinden yukarı uzaklık (m); tavana çarparsa altında kalır")]
        [SerializeField] float markHeight = 0.32f;
        [SerializeField] float markBob = 0.05f;
        [SerializeField] float markBobSpeed = 4f;
        [Tooltip("Pixel boyu: metre başına doku pikseli")]
        [SerializeField] float markPixelsPerUnit = 36f;

        class Mark
        {
            public SpriteRenderer Renderer;
            public Vector3 Position;
        }

        readonly Dictionary<SecurableItem, Mark> marks = new Dictionary<SecurableItem, Mark>();
        Sprite markSprite;

        /// <summary>M62: birinin üstüne düşecek eşya mı (yatak rafı ya da bir aile üyesinin tehlikesi)</summary>
        public static bool IsImportant(ItemId id)
        {
            if (id == ItemId.BedShelf) return true;
            foreach (var member in Family.FamilyMember.All)
                if (member && member.DangerItem == id) return true;
            return false;
        }

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
            important.Clear();
            foreach (var item in SecurableItem.All)
            {
                bool isImportant = IsImportant(item.Id);
                if (isImportant) important.Add(item.Id);
                var outline = item.GetComponent<ItemOutline>();
                if (outline) outline.SetPriority(isImportant);
                if (!item.IsSecured && !outline) lights[item] = CreateLight(item);
                if (!item.IsSecured && isImportant) marks[item] = CreateMark(item);
            }
        }

        void HandleItemSecured(ItemId id)
        {
            var item = SecurableItem.Find(id);
            if (item && lights.TryGetValue(item, out var light))
            {
                if (light) Destroy(light.gameObject);
                lights.Remove(item);
            }
            if (item && marks.TryGetValue(item, out var mark))
            {
                if (mark.Renderer) Destroy(mark.Renderer.gameObject);
                marks.Remove(item);
            }
        }

        void Update()
        {
            var nearest = SpiritInteractor.Nearest;
            Emphasize(nearest ? nearest.GetComponent<ItemOutline>() : null);
            UpdateMarks(nearest);

            if (lights.Count == 0) return;
            float pulse = baseIntensity + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            foreach (var pair in lights)
                if (pair.Value)
                    pair.Value.intensity = pair.Key == nearest ? nearestIntensity
                        : important.Contains(pair.Key.Id) ? pulse : pulse * optionalIntensityScale;
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
            light.color = IsImportant(item.Id) ? importantColor : color;

            light.range = range;
            light.shadows = LightShadows.None;
            light.intensity = baseIntensity;
            return light;
        }

        // Ünlem sallanır ve nabız gibi parlar; ruhun yanındaki eşyada gizlenir (orada F ipucu var)
        void UpdateMarks(SecurableItem nearest)
        {
            if (marks.Count == 0) return;
            float bob = Mathf.Sin(Time.time * markBobSpeed) * markBob;
            float glow = 0.75f + 0.25f * Mathf.Sin(Time.time * markBobSpeed * 1.5f);
            foreach (var pair in marks)
            {
                var mark = pair.Value;
                if (!mark.Renderer) continue;
                mark.Renderer.enabled = pair.Key != nearest;
                mark.Renderer.transform.position = mark.Position + Vector3.up * bob;
                mark.Renderer.color = new Color(glow, glow, glow, 1f);
            }
        }

        Mark CreateMark(SecurableItem item)
        {
            var collider = item.GetComponent<Collider>();
            var bounds = collider ? collider.bounds : new Bounds(item.transform.position, Vector3.one);
            var top = new Vector3(bounds.center.x, bounds.max.y + 0.01f, bounds.min.z - 0.05f);
            float height = markHeight;
            if (Physics.Raycast(top, Vector3.up, out var hit, markHeight + 0.25f, ~0, QueryTriggerInteraction.Ignore))
                height = hit.distance - 0.25f; // tavana gömülmesin; gerekirse eşyanın önüne iner

            var renderer = new GameObject($"ImportantMark_{item.Id}").AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, false);
            renderer.sprite = MarkSprite();
            renderer.sortingOrder = 50;
            var mark = new Mark { Renderer = renderer, Position = top + Vector3.up * (height + 0.15f) };
            renderer.transform.position = mark.Position;
            return mark;
        }

        // 5x11 pixel ünlem: koyu konturlu kırmızı
        Sprite MarkSprite()
        {
            if (markSprite) return markSprite;
            string[] rows =
            {
                ".###.",
                "#rrr#",
                "#rrr#",
                "#rrr#",
                ".#r#.",
                ".#r#.",
                "..#..",
                ".###.",
                "#rrr#",
                "#rrr#",
                ".###.",
            };
            var outline = new Color(0.1f, 0.04f, 0.04f);
            var texture = new Texture2D(5, rows.Length) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < 5; x++)
            {
                char c = rows[y][x];
                texture.SetPixel(x, rows.Length - 1 - y, c == 'r' ? markColor : c == '#' ? outline : Color.clear);
            }
            texture.Apply();
            markSprite = Sprite.Create(texture, new Rect(0f, 0f, 5f, rows.Length), new Vector2(0.5f, 0.5f), markPixelsPerUnit);
            return markSprite;
        }

        void Clear()
        {
            Emphasize(null);
            foreach (var mark in marks.Values)
                if (mark.Renderer) Destroy(mark.Renderer.gameObject);
            marks.Clear();
            foreach (var light in lights.Values)
                if (light) Destroy(light.gameObject);
            lights.Clear();
        }
    }
}
