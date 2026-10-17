using Deprem.Core;
using Deprem.Localization;
using UnityEngine;

namespace Deprem.UI
{
    // Tuş ipucu: GameEvents.PromptChanged ile gelen texts.csv anahtarını ekranın altında gösterir
    // ("F: Kalk" → [F] Kalk). Boş anahtar ipucunu gizler (ENTEGRASYON M1).
    // M24: ortak HUD stili (UiPrompt: pixel panel, ayrı tuş kapakları); ekranın en altına yakın, çök-kapan
    // çubuğu ile çakışmaz.
    public class PromptHud : MonoBehaviour
    {
        [SerializeField] Font font;
        [Tooltip("0: UiStyle.hudFontSize")]
        [SerializeField] int fontSize;
        [Tooltip("Ekranın altından yükseklik (referans pikseli)")]
        [SerializeField] float bottomOffset = 22f;
        [SerializeField] int sortingOrder = 60;

        UiPrompt prompt;
        string key = string.Empty;

        void Awake()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "PromptCanvas", sortingOrder);
            prompt = UiPrompt.Create(canvas.transform, "Prompt", font, fontSize);
            var rect = prompt.Rect;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, bottomOffset);
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
            prompt.Set(string.IsNullOrEmpty(key) ? string.Empty : Texts.Get(key));
        }
    }
}
