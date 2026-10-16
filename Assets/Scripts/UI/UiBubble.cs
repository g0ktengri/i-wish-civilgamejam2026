using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // UiBuilder.CreateSpeechBubble'ın kurduğu konuşma balonu. SetText yazıyı koyar ve balonu
    // yazıya göre boyutlandırır (en fazla maxWidth genişlikte, sonra alt satıra geçer).
    // Kökün pivotu kuyruğun ucu: Root.anchoredPosition ile karakterin başının üstüne konur.
    public class UiBubble : MonoBehaviour
    {
        public static readonly Vector2 DefaultPadding = new Vector2(10f, 6f);

        RectTransform body;
        float tailHeight;

        public RectTransform Root => (RectTransform)transform;
        public Text Label { get; private set; }
        public Vector2 Padding { get; set; } = DefaultPadding;

        internal void Setup(RectTransform bodyRect, Text label, float tail)
        {
            body = bodyRect;
            Label = label;
            tailHeight = tail;
        }

        public void SetText(string text, float maxWidth = 220f)
        {
            Label.text = text;

            // Önce en geniş haliyle ölç, sonra yazıya göre daralt
            var rect = Label.rectTransform;
            rect.sizeDelta = new Vector2(maxWidth, 0f);
            float width = Mathf.Min(maxWidth, Label.preferredWidth);
            rect.sizeDelta = new Vector2(width, 0f);
            float height = Label.preferredHeight;
            rect.sizeDelta = new Vector2(width, height);

            Root.sizeDelta = new Vector2(width + Padding.x * 2f, height + Padding.y * 2f + tailHeight);
        }
    }
}
