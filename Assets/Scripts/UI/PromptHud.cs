using System.Reflection;
using Deprem.Core;
using Deprem.Localization;
using UnityEngine;

namespace Deprem.UI
{
    // Tuş ipucu: GameEvents.PromptChanged ile gelen texts.csv anahtarını ekranın altında gösterir
    // ("F: Kalk" → [F] Kalk). Boş anahtar ipucunu gizler (ENTEGRASYON M1).
    // M24: ortak HUD stili (UiPrompt: pixel panel, ayrı tuş kapakları); ekranın en altına yakın, çök-kapan
    // çubuğu ile çakışmaz.
    // M46 (Mehmet'in play testi 24): ipucu ilgili objenin üstünde ve daha küçük. Konum Mehmet'in
    // Deprem.Player.PlayerInteractor.PromptAnchor'ından (statik Vector3?, dünya koordinatı); null ise eski yerinde
    // (alt orta). O alan henüz yoksa oyuncunun başının üstü. Ekran kenarına taşmaz.
    public class PromptHud : MonoBehaviour
    {
        [SerializeField] Font font;
        [Tooltip("0: UiStyle.hudFontSize")]
        [SerializeField] int fontSize;
        [Tooltip("Ekranın altından yükseklik (referans pikseli; M33: alta yapışmasın, 22'den 60'a)")]
        [SerializeField] float bottomOffset = 60f;
        [Tooltip("M46: objenin üstündeki ipucunun yazı boyu (alt ortadakinden küçük)")]
        [SerializeField] int worldFontSize = 16;
        [Tooltip("Objenin tepesinden yukarı pay (referans pikseli)")]
        [SerializeField] float worldLift = 10f;
        [Tooltip("Ekran kenarından en az bu kadar içeride (referans pikseli)")]
        [SerializeField] float screenMargin = 12f;
        [Tooltip("PromptAnchor yokken oyuncunun başının üstü: oyuncu pivotundan yükseklik (m)")]
        [SerializeField] float playerHeadHeight = 2.1f;
        [SerializeField] int sortingOrder = 60;

        UiPrompt prompt;
        UiPrompt worldPrompt;
        RectTransform canvasRect;
        Transform player;
        string key = string.Empty;
        string text = string.Empty;

        static bool anchorLookedUp;
        static PropertyInfo anchorProperty;
        static FieldInfo anchorField;

        void Awake()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "PromptCanvas", sortingOrder);
            canvasRect = (RectTransform)canvas.transform;
            prompt = UiPrompt.Create(canvas.transform, "Prompt", font, fontSize);
            var rect = prompt.Rect;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, bottomOffset);

            worldPrompt = UiPrompt.Create(canvas.transform, "WorldPrompt", font, worldFontSize);
            var worldRect = worldPrompt.Rect;
            worldRect.anchorMin = worldRect.anchorMax = new Vector2(0.5f, 0.5f);
            worldRect.pivot = new Vector2(0.5f, 0f);
            Apply();
        }

        void OnEnable()
        {
            GameEvents.PromptChanged += HandlePromptChanged;
            GameEvents.LoopFailed += HandleLoopFailed;
            Texts.LanguageChanged += Apply;
        }

        void OnDisable()
        {
            GameEvents.PromptChanged -= HandlePromptChanged;
            GameEvents.LoopFailed -= HandleLoopFailed;
            Texts.LanguageChanged -= Apply;
        }

        void HandlePromptChanged(string newKey)
        {
            key = newKey ?? string.Empty;
            Apply();
        }

        void HandleLoopFailed(LoseReason reason, ItemId item) => HandlePromptChanged(string.Empty);

        void Apply()
        {
            if (!prompt) return;
            text = string.IsNullOrEmpty(key) ? string.Empty : Texts.Get(key);
            Place();
        }

        void LateUpdate() => Place();

        // Dünya konumu varsa küçük ipucu orada (ekrana sığacak şekilde), yoksa büyük ipucu alt ortada
        void Place()
        {
            if (!prompt) return;
            var cam = Camera.main;
            Vector3? anchor = string.IsNullOrEmpty(text) || !cam ? null : Anchor();
            Vector3 viewport = anchor.HasValue ? cam.WorldToViewportPoint(anchor.Value) : Vector3.zero;
            bool world = anchor.HasValue && viewport.z > 0f;

            prompt.Set(world ? string.Empty : text);
            worldPrompt.Set(world ? text : string.Empty);
            if (!world) return;

            Vector2 size = canvasRect.rect.size;
            Vector2 half = worldPrompt.Rect.sizeDelta * 0.5f;
            float x = (viewport.x - 0.5f) * size.x;
            float y = (viewport.y - 0.5f) * size.y + worldLift;
            x = Mathf.Clamp(x, -size.x * 0.5f + screenMargin + half.x, size.x * 0.5f - screenMargin - half.x);
            y = Mathf.Clamp(y, -size.y * 0.5f + screenMargin, size.y * 0.5f - screenMargin - half.y * 2f);
            worldPrompt.Rect.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
        }

        // Mehmet'in PromptAnchor'ı (yansımayla: o alan gelmeden de derlensin); alan hiç yoksa oyuncunun başı
        Vector3? Anchor()
        {
            if (!anchorLookedUp)
            {
                anchorLookedUp = true;
                var type = System.Type.GetType("Deprem.Player.PlayerInteractor, Assembly-CSharp");
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
                anchorProperty = type?.GetProperty("PromptAnchor", flags);
                anchorField = anchorProperty == null ? type?.GetField("PromptAnchor", flags) : null;
            }
            if (anchorProperty != null) return anchorProperty.GetValue(null) as Vector3?;
            if (anchorField != null) return anchorField.GetValue(null) as Vector3?;

            if (!player)
            {
                var playerObject = GameObject.Find("Player");
                if (playerObject) player = playerObject.transform;
            }
            return player ? player.position + Vector3.up * playerHeadHeight : (Vector3?)null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            anchorLookedUp = false;
            anchorProperty = null;
            anchorField = null;
        }
    }
}
