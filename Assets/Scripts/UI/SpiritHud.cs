using Deprem.Bag;
using Deprem.Core;
using Deprem.Localization;
using Deprem.Spirit;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Ruh fazı HUD'u (M24: ortak HUD stili): üst ortada kalan süre paneli (son saniyelerde kırmızı, nabız),
    // altta etkileşim ipucu ([F] Sabitle / Al / Çantaya koy), sol altta uçma ipucu ([W] [A] [S] [D] Uç),
    // sağ üstte taşınan çanta malzemesi (ikon + ad). Canvas'ı kendisi kurar; sadece ruh fazında görünür.
    // Berke'nin play testi: etkileşim ipucu hedefin (eşya, malzeme, çanta) üstünde ve daha küçük çıkar
    // (SpiritInteractor.PromptAnchor); konum yoksa eski yerinde, altta. Ekran kenarına taşmaz.
    // M71 (Mehmet'in play testi 32: malzemeyi nereye koyacağı belli değildi): malzeme taşınırken taşınan panelinin altında
    // "Çantaya götür"; çanta ekrandaysa üstünde sallanan işaret, değilse ekran kenarında çantaya giden ok. Çantanın
    // yanında ikisi de kalkar, "F: Çantaya koy" ipucu kalır.
    public class SpiritHud : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] int timerFontSize = 34;
        [Tooltip("0: UiStyle.hudFontSize")]
        [SerializeField] int promptFontSize;
        [SerializeField] int hintFontSize = 18;
        [Tooltip("M62: sayacın altındaki öncelik ipucunun yazı boyu")]
        [SerializeField] int priorityFontSize = 16;
        [Tooltip("Son kaç saniyede sayaç kırmızıya döner ve atar")]
        [SerializeField] float warningSeconds = 10f;
        [SerializeField] float promptBottomOffset = 60f;
        [Tooltip("Hedefin üstündeki ipucunun yazı boyu (alttakinden küçük)")]
        [SerializeField] int worldPromptFontSize = 16;
        [Tooltip("Hedefin tepesinden yukarı pay (referans pikseli)")]
        [SerializeField] float worldLift = 10f;
        [Tooltip("Ekran kenarından en az bu kadar içeride (referans pikseli)")]
        [SerializeField] float screenMargin = 12f;
        [SerializeField] int sortingOrder = 50;

        [Header("M71: çantaya yönlendirme")]
        [SerializeField] float bagArrowSize = 24f;
        [Tooltip("Çantanın tepesinden yukarı pay (referans pikseli)")]
        [SerializeField] float bagMarkerLift = 12f;
        [Tooltip("Kenar okunun güvenli alanı: yanlardan, üstten (sayaç ve öncelik yazısı) ve alttan (uçma ipucu)")]
        [SerializeField] float bagEdgeMargin = 40f;
        [SerializeField] float bagSafeTop = 110f;
        [SerializeField] float bagSafeBottom = 70f;

        CanvasGroup group;
        RectTransform timerPanel;
        Text timerText;
        Text priorityText;
        UiPrompt prompt;
        UiPrompt worldPrompt;
        RectTransform canvasRect;
        string promptText = string.Empty;
        UiPrompt flyHint;
        RectTransform carryPanel;
        Text carryText;
        Image carryIcon;
        Text carryHint;
        Image bagArrow;
        Image bagMarker;
        int shownSeconds = -1;
        string shownPrompt;
        BagItemId shownCarry = (BagItemId)(-1);

        void Awake()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "SpiritHudCanvas", sortingOrder);
            canvasRect = (RectTransform)canvas.transform;
            group = canvas.GetComponent<CanvasGroup>();
            float margin = UiBuilder.HudMargin;

            timerPanel = UiBuilder.CreatePanel(canvas.transform, "Timer").rectTransform;
            timerPanel.anchorMin = timerPanel.anchorMax = new Vector2(0.5f, 1f);
            timerPanel.pivot = new Vector2(0.5f, 1f);
            timerPanel.anchoredPosition = new Vector2(0f, -margin);
            timerPanel.sizeDelta = new Vector2(116f, 52f);
            timerText = UiBuilder.Outlined(UiBuilder.CreateText(timerPanel, "Text", font, timerFontSize, UiBuilder.HudText));
            UiBuilder.Stretch(timerText.rectTransform, Vector2.zero, Vector2.one);

            // M62: sayacın altında öncelik ipucu (kırmızı konturlu eşyalar önce)
            priorityText = UiBuilder.Outlined(UiBuilder.CreateText(canvas.transform, "Priority", font, priorityFontSize,
                new Color(1f, 0.62f, 0.5f)));
            var priorityRect = priorityText.rectTransform;
            priorityRect.anchorMin = priorityRect.anchorMax = new Vector2(0.5f, 1f);
            priorityRect.pivot = new Vector2(0.5f, 1f);
            priorityRect.sizeDelta = new Vector2(600f, 24f);
            priorityRect.anchoredPosition = new Vector2(0f, -margin - 58f);
            priorityText.horizontalOverflow = HorizontalWrapMode.Overflow;

            prompt = UiPrompt.Create(canvas.transform, "Prompt", font, promptFontSize);
            prompt.Rect.anchorMin = prompt.Rect.anchorMax = new Vector2(0.5f, 0f);
            prompt.Rect.pivot = new Vector2(0.5f, 0f);
            prompt.Rect.anchoredPosition = new Vector2(0f, promptBottomOffset);

            worldPrompt = UiPrompt.Create(canvas.transform, "WorldPrompt", font, worldPromptFontSize);
            worldPrompt.Rect.anchorMin = worldPrompt.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            worldPrompt.Rect.pivot = new Vector2(0.5f, 0f);

            flyHint = UiPrompt.Create(canvas.transform, "FlyHint", font, hintFontSize);
            flyHint.Rect.anchorMin = flyHint.Rect.anchorMax = Vector2.zero;
            flyHint.Rect.pivot = Vector2.zero;
            flyHint.Rect.anchoredPosition = new Vector2(margin, margin);

            // Taşınan malzeme: ikon (BagSystem'deki ikon ya da rengi) + "Taşınan: El feneri"
            carryPanel = UiBuilder.CreatePanel(canvas.transform, "Carrying").rectTransform;
            carryPanel.anchorMin = carryPanel.anchorMax = Vector2.one;
            carryPanel.pivot = Vector2.one;
            carryPanel.anchoredPosition = new Vector2(-margin, -margin);
            carryIcon = UiBuilder.CreateImage(carryPanel, "Icon", Color.white);
            var iconRect = carryIcon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(12f, 0f);
            iconRect.sizeDelta = new Vector2(32f, 32f);
            carryText = UiBuilder.CreateText(carryPanel, "Text", font, UiBuilder.HudFontSize, UiBuilder.HudText,
                TextAnchor.MiddleLeft);
            var textRect = carryText.rectTransform;
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 0.5f);
            textRect.anchoredPosition = new Vector2(54f, 0f);
            UiBuilder.Outlined(carryText);
            carryText.horizontalOverflow = HorizontalWrapMode.Overflow;
            carryText.supportRichText = true;
            carryPanel.gameObject.SetActive(false);

            carryHint = UiBuilder.Outlined(UiBuilder.CreateText(canvas.transform, "CarryHint", font, hintFontSize,
                UiBuilder.HudAccent, TextAnchor.MiddleRight));
            var hintRect = carryHint.rectTransform;
            hintRect.anchorMin = hintRect.anchorMax = Vector2.one;
            hintRect.pivot = Vector2.one;
            hintRect.sizeDelta = new Vector2(300f, 24f);
            hintRect.anchoredPosition = new Vector2(-margin - 4f, -margin - 54f);
            carryHint.horizontalOverflow = HorizontalWrapMode.Overflow;
            carryHint.gameObject.SetActive(false);

            bagArrow = UiBuilder.CreateImage(canvas.transform, "BagArrow", UiBuilder.HudAccent);
            bagArrow.sprite = ObjectiveHud.ArrowSprite;
            bagArrow.rectTransform.anchorMin = bagArrow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            bagArrow.rectTransform.sizeDelta = Vector2.one * bagArrowSize;
            bagArrow.gameObject.SetActive(false);

            bagMarker = UiBuilder.CreateImage(canvas.transform, "BagMarker", UiBuilder.HudAccent);
            bagMarker.sprite = ObjectiveHud.MarkerSprite;
            bagMarker.rectTransform.anchorMin = bagMarker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            bagMarker.rectTransform.pivot = new Vector2(0.5f, 0f);
            bagMarker.rectTransform.sizeDelta = Vector2.one * 18f;
            bagMarker.gameObject.SetActive(false);

            group.alpha = 0f;
        }

        void LateUpdate()
        {
            if (group.alpha <= 0f) return;
            PlacePrompt();
            PlaceBagPointer();
        }

        // M71: taşınırken çantayı gösterir; çantanın yanındayken (F ipucu çıkınca) kalkar
        void PlaceBagPointer()
        {
            bool carrying = BagSystem.Carrying > BagItemId.None;
            bool near = shownPrompt == "prompt.put_in_bag";
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            carryHint.gameObject.SetActive(carrying && !near);
            if (carrying)
            {
                var accent = UiBuilder.HudAccent;
                carryHint.color = new Color(accent.r, accent.g, accent.b, 0.6f + 0.4f * pulse);
            }

            var bag = carrying && !near ? BagDropPoint.Current : null;
            var cam = Camera.main;
            if (!bag || !cam)
            {
                bagArrow.gameObject.SetActive(false);
                bagMarker.gameObject.SetActive(false);
                return;
            }
            Vector3 viewport = cam.WorldToViewportPoint(bag.Top);
            bool behind = viewport.z < 0f;
            if (behind) viewport = new Vector3(1f - viewport.x, 1f - viewport.y, -viewport.z);
            bool onScreen = !behind && viewport.x > 0.03f && viewport.x < 0.97f && viewport.y > 0.05f && viewport.y < 0.95f;
            bagMarker.gameObject.SetActive(onScreen);
            bagArrow.gameObject.SetActive(!onScreen);
            Vector2 size = canvasRect.rect.size;
            if (onScreen)
            {
                bagMarker.rectTransform.anchoredPosition = new Vector2((viewport.x - 0.5f) * size.x,
                    (viewport.y - 0.5f) * size.y + bagMarkerLift + pulse * 4f);
                return;
            }

            // Yönü güvenli dikdörtgenin kenarına kadar uzat (ObjectiveHud'daki aile okları gibi)
            Vector2 direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            if (direction.sqrMagnitude < 1e-6f) direction = Vector2.down;
            float maxX = size.x * 0.5f - bagEdgeMargin, minX = -maxX;
            float maxY = size.y * 0.5f - bagSafeTop, minY = -size.y * 0.5f + bagSafeBottom;
            float scale = Mathf.Min(
                direction.x > 0f ? maxX / direction.x : minX / Mathf.Min(direction.x, -1e-4f),
                direction.y > 0f ? maxY / Mathf.Max(direction.y, 1e-4f) : minY / Mathf.Min(direction.y, -1e-4f));
            Vector2 unit = direction.normalized;
            bagArrow.rectTransform.anchoredPosition = direction * Mathf.Max(0f, scale) + unit * 5f * pulse;
            bagArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(unit.y, unit.x) * Mathf.Rad2Deg);
        }

        // Hedefin dünya konumu varsa küçük ipucu orada (ekrana sığacak şekilde), yoksa büyük ipucu altta
        void PlacePrompt()
        {
            var cam = Camera.main;
            Vector3? anchor = string.IsNullOrEmpty(promptText) || !cam ? null : SpiritInteractor.PromptAnchor;
            Vector3 viewport = anchor.HasValue ? cam.WorldToViewportPoint(anchor.Value) : Vector3.zero;
            bool world = anchor.HasValue && viewport.z > 0f;
            prompt.Set(world ? string.Empty : promptText);
            worldPrompt.Set(world ? promptText : string.Empty);
            if (!world) return;

            Vector2 size = canvasRect.rect.size;
            Vector2 half = worldPrompt.Rect.sizeDelta * 0.5f;
            float x = (viewport.x - 0.5f) * size.x;
            float y = (viewport.y - 0.5f) * size.y + worldLift;
            x = Mathf.Clamp(x, -size.x * 0.5f + screenMargin + half.x, size.x * 0.5f - screenMargin - half.x);
            y = Mathf.Clamp(y, -size.y * 0.5f + screenMargin, size.y * 0.5f - screenMargin - half.y * 2f);
            worldPrompt.Rect.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
        }

        void OnEnable() => Texts.LanguageChanged += RefreshStaticTexts;

        void OnDisable() => Texts.LanguageChanged -= RefreshStaticTexts;

        void Update()
        {
            bool visible = GameState.Phase == GamePhase.Spirit && SpiritPhase.Running;
            group.alpha = visible ? 1f : 0f;
            if (!visible)
            {
                shownSeconds = -1;
                shownPrompt = null;
                shownCarry = (BagItemId)(-1);
                return;
            }

            float timeLeft = SpiritPhase.TimeLeft;
            int seconds = Mathf.CeilToInt(timeLeft);
            bool warning = timeLeft <= warningSeconds;
            if (seconds != shownSeconds)
            {
                shownSeconds = seconds;
                timerText.text = $"{seconds / 60}:{seconds % 60:00}";
                timerText.color = warning ? UiBuilder.HudWarning : UiBuilder.HudText;
            }
            // Son saniyelerde her saniyenin başında kısa nabız
            float beat = warning ? Mathf.Clamp01(1f - (Mathf.Ceil(timeLeft) - timeLeft) * 4f) : 0f;
            timerPanel.localScale = Vector3.one * (1f + 0.08f * beat);

            if (SpiritInteractor.PromptKey != shownPrompt || BagSystem.Carrying != shownCarry)
            {
                shownPrompt = SpiritInteractor.PromptKey;
                shownCarry = BagSystem.Carrying;
                RefreshStaticTexts();
            }
        }

        void RefreshStaticTexts()
        {
            promptText = string.IsNullOrEmpty(shownPrompt) ? string.Empty : Texts.Get(shownPrompt);
            PlacePrompt();
            flyHint.Set(Texts.Get("prompt.fly"));
            priorityText.text = Texts.Get("spirit.priority");
            carryHint.text = Texts.Get("spirit.carry_to_bag");


            bool carrying = shownCarry > BagItemId.None;
            carryPanel.gameObject.SetActive(carrying);
            if (!carrying) return;
            carryText.text = Texts.Format("hud.carrying",
                $"<color={UiBuilder.Hex(UiBuilder.HudAccent)}>{Texts.Get($"bag.{shownCarry}")}</color>");
            var bag = BagSystem.Instance;
            var icon = bag ? bag.IconFor(shownCarry) : UiBuilder.PixelBagIcon(shownCarry);
            UiBuilder.ApplySprite(carryIcon, icon);
            carryPanel.sizeDelta = new Vector2(54f + carryText.preferredWidth + 16f, 48f);
        }
    }
}
