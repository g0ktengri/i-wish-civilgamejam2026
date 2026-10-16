using Deprem.Bag;
using Deprem.Core;
using Deprem.Localization;
using Deprem.Spirit;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Ruh fazı HUD'u: üstte kalan süre, altta etkileşim ipucu (F: Sabitle / Al / Çantaya koy),
    // taşınan çanta malzemesi ve uçma ipucu.
    // Canvas'ı kendisi kurar; sadece ruh fazında görünür.
    public class SpiritHud : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] int timerFontSize = 28;
        [SerializeField] int promptFontSize = 14;
        [SerializeField] Color timerColor = new Color(0.8f, 0.92f, 1f);
        [SerializeField] Color warningColor = new Color(1f, 0.45f, 0.4f);
        [Tooltip("Son kaç saniyede sayaç kırmızıya döner")]
        [SerializeField] float warningSeconds = 10f;
        [SerializeField] int sortingOrder = 50;

        [Header("Görseller (Assets/Art/UI, prefab kurulurken bağlanır)")]
        [Tooltip("hud_sayac.png, 64 x 32: sayacın arka planı")]
        [SerializeField] Sprite timerSprite;

        CanvasGroup group;
        Text timerText;
        Text promptText;
        Text hintText;
        Text carryText;
        Image carryIcon;
        int shownSeconds = -1;
        string shownPrompt;
        BagItemId shownCarry = (BagItemId)(-1);

        void Awake()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "SpiritHudCanvas", sortingOrder);
            group = canvas.GetComponent<CanvasGroup>();

            if (timerSprite)
            {
                var timerBack = UiBuilder.CreateImage(canvas.transform, "TimerBack", Color.white);
                timerBack.sprite = timerSprite;
                var rect = timerBack.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.915f);
                rect.sizeDelta = new Vector2(64f, 32f);
            }
            timerText = UiBuilder.CreateText(canvas.transform, "Timer", font, timerFontSize, timerColor);
            UiBuilder.Stretch(timerText.rectTransform, new Vector2(0.4f, 0.86f), new Vector2(0.6f, 0.97f));

            promptText = UiBuilder.CreateText(canvas.transform, "Prompt", font, promptFontSize, Color.white);
            UiBuilder.Stretch(promptText.rectTransform, new Vector2(0.3f, 0.12f), new Vector2(0.7f, 0.2f));

            hintText = UiBuilder.CreateText(canvas.transform, "Hint", font, promptFontSize, timerColor,
                TextAnchor.LowerLeft);
            UiBuilder.Stretch(hintText.rectTransform, new Vector2(0.02f, 0.02f), new Vector2(0.4f, 0.08f));

            carryText = UiBuilder.CreateText(canvas.transform, "Carrying", font, promptFontSize, Color.white,
                TextAnchor.UpperRight);
            UiBuilder.Stretch(carryText.rectTransform, new Vector2(0.6f, 0.88f), new Vector2(0.94f, 0.97f));

            // Taşınan malzemenin ikonu (32 x 32, BagSystem'deki ikon)
            carryIcon = UiBuilder.CreateImage(canvas.transform, "CarryIcon", Color.white);
            var iconRect = carryIcon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.97f, 0.925f);
            iconRect.sizeDelta = new Vector2(32f, 32f);
            carryIcon.enabled = false;

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

            int seconds = Mathf.CeilToInt(SpiritPhase.TimeLeft);
            if (seconds != shownSeconds)
            {
                shownSeconds = seconds;
                timerText.text = $"{seconds / 60}:{seconds % 60:00}";
                timerText.color = SpiritPhase.TimeLeft <= warningSeconds ? warningColor : timerColor;
            }

            if (SpiritInteractor.PromptKey != shownPrompt || BagSystem.Carrying != shownCarry)
            {
                shownPrompt = SpiritInteractor.PromptKey;
                shownCarry = BagSystem.Carrying;
                RefreshStaticTexts();
            }
        }

        void RefreshStaticTexts()
        {
            promptText.text = string.IsNullOrEmpty(shownPrompt) ? string.Empty : Texts.Get(shownPrompt);
            hintText.text = Texts.Get("prompt.fly");
            carryText.text = shownCarry > BagItemId.None
                ? Texts.Format("hud.carrying", Texts.Get($"bag.{shownCarry}"))
                : string.Empty;

            var icon = shownCarry > BagItemId.None && BagSystem.Instance ? BagSystem.Instance.IconFor(shownCarry) : null;
            carryIcon.sprite = icon;
            carryIcon.enabled = icon;
        }
    }
}
