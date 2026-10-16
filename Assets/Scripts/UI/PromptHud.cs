using Deprem.Core;
using Deprem.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Tuş ipucu: GameEvents.PromptChanged ile gelen texts.csv anahtarını ekranın altında gösterir
    // ("F: Kalk", "C: Çök-Kapan-Tutun"). Boş anahtar ipucunu gizler (ENTEGRASYON M1).
    public class PromptHud : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] int fontSize = 20;
        [SerializeField] Color color = Color.white;
        [SerializeField] Color backColor = new Color(0f, 0f, 0f, 0.5f);
        [SerializeField] int sortingOrder = 60;
        [Tooltip("hud_ipucu.png, 192 x 32: ipucunun arka planı (Assets/Art/UI, prefab kurulurken bağlanır)")]
        [SerializeField] Sprite backSprite;

        Image back;
        Text label;
        string key = string.Empty;

        void Awake()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "PromptCanvas", sortingOrder);

            back = UiBuilder.CreateImage(canvas.transform, "Back", backSprite ? Color.white : backColor);
            back.sprite = backSprite;
            var backRect = back.rectTransform;
            backRect.anchorMin = backRect.anchorMax = new Vector2(0.5f, 0.18f);
            backRect.sizeDelta = new Vector2(192f, 32f);

            label = UiBuilder.CreateText(back.transform, "Label", font, fontSize, color);
            UiBuilder.Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.horizontalOverflow = HorizontalWrapMode.Overflow; // uzun ipucu tek satırda taşsın

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
            if (!back) return;
            bool visible = !string.IsNullOrEmpty(key);
            back.enabled = visible;
            label.enabled = visible;
            if (visible) label.text = Texts.Get(key);
        }
    }
}
