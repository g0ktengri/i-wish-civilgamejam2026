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

        Image back;
        Text label;
        string key = string.Empty;

        void Awake()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "PromptCanvas", sortingOrder);

            back = UiBuilder.CreateImage(canvas.transform, "Back", backColor);
            UiBuilder.Stretch(back.rectTransform, new Vector2(0.35f, 0.14f), new Vector2(0.65f, 0.22f));

            label = UiBuilder.CreateText(back.transform, "Label", font, fontSize, color);
            UiBuilder.Stretch(label.rectTransform, Vector2.zero, Vector2.one);

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
