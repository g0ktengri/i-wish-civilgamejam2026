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
    public class SpiritHud : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] int timerFontSize = 24;
        [Tooltip("0: UiStyle.hudFontSize")]
        [SerializeField] int promptFontSize;
        [SerializeField] int hintFontSize = 13;
        [Tooltip("Son kaç saniyede sayaç kırmızıya döner ve atar")]
        [SerializeField] float warningSeconds = 10f;
        [SerializeField] float promptBottomOffset = 22f;
        [SerializeField] int sortingOrder = 50;

        CanvasGroup group;
        RectTransform timerPanel;
        Text timerText;
        UiPrompt prompt;
        UiPrompt flyHint;
        RectTransform carryPanel;
        Text carryText;
        Image carryIcon;
        int shownSeconds = -1;
        string shownPrompt;
        BagItemId shownCarry = (BagItemId)(-1);

        void Awake()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "SpiritHudCanvas", sortingOrder);
            group = canvas.GetComponent<CanvasGroup>();
            float margin = UiBuilder.HudMargin;

            timerPanel = UiBuilder.CreatePanel(canvas.transform, "Timer").rectTransform;
            timerPanel.anchorMin = timerPanel.anchorMax = new Vector2(0.5f, 1f);
            timerPanel.pivot = new Vector2(0.5f, 1f);
            timerPanel.anchoredPosition = new Vector2(0f, -margin);
            timerPanel.sizeDelta = new Vector2(84f, 38f);
            timerText = UiBuilder.CreateText(timerPanel, "Text", font, timerFontSize, UiBuilder.HudText);
            UiBuilder.Stretch(timerText.rectTransform, Vector2.zero, Vector2.one);

            prompt = UiPrompt.Create(canvas.transform, "Prompt", font, promptFontSize);
            prompt.Rect.anchorMin = prompt.Rect.anchorMax = new Vector2(0.5f, 0f);
            prompt.Rect.pivot = new Vector2(0.5f, 0f);
            prompt.Rect.anchoredPosition = new Vector2(0f, promptBottomOffset);

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
            iconRect.anchoredPosition = new Vector2(10f, 0f);
            iconRect.sizeDelta = new Vector2(24f, 24f);
            carryText = UiBuilder.CreateText(carryPanel, "Text", font, UiBuilder.HudFontSize, UiBuilder.HudText,
                TextAnchor.MiddleLeft);
            var textRect = carryText.rectTransform;
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 0.5f);
            textRect.anchoredPosition = new Vector2(42f, 0f);
            carryText.horizontalOverflow = HorizontalWrapMode.Overflow;
            carryText.supportRichText = true;
            carryPanel.gameObject.SetActive(false);

            group.alpha = 0f;
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
            prompt.Set(string.IsNullOrEmpty(shownPrompt) ? string.Empty : Texts.Get(shownPrompt));
            flyHint.Set(Texts.Get("prompt.fly"));

            bool carrying = shownCarry > BagItemId.None;
            carryPanel.gameObject.SetActive(carrying);
            if (!carrying) return;
            carryText.text = Texts.Format("hud.carrying",
                $"<color={UiBuilder.Hex(UiBuilder.HudAccent)}>{Texts.Get($"bag.{shownCarry}")}</color>");
            var bag = BagSystem.Instance;
            var icon = bag ? bag.IconFor(shownCarry) : UiBuilder.PixelBagIcon(shownCarry);
            UiBuilder.ApplySprite(carryIcon, icon);
            carryPanel.sizeDelta = new Vector2(42f + carryText.preferredWidth + 12f, 36f);
        }
    }
}
