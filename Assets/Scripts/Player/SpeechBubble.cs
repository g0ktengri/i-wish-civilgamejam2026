using Deprem.Localization;
using Deprem.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Player
{
    /// <summary>
    /// Karakterin başının üstünde kısa konuşma balonu (döngü 1-2'de karakterin ne düşündüğü).
    /// Metin texts.csv anahtarıyla verilir: Say("bubble.ignore_foreshock", 3f).
    /// </summary>
    public sealed class SpeechBubble : MonoBehaviour
    {
        [SerializeField] private float headHeight = 2.1f;
        [SerializeField] private int fontSize = 14;
        [SerializeField] private float maxTextWidth = 220f;
        [SerializeField] private Vector2 padding = new Vector2(10f, 6f);
        [SerializeField] private Color background = new Color(1f, 1f, 1f, 0.92f);
        [SerializeField] private Color textColor = new Color(0.08f, 0.08f, 0.1f);

        private Canvas canvas;
        private RectTransform bubble;
        private Text label;
        private float hideAt;

        public bool IsShowing => bubble != null && bubble.gameObject.activeSelf;

        public void Say(string key, float seconds)
        {
            EnsureUi();
            label.text = Texts.Get(key);

            // Önce en geniş haliyle ölç, sonra yazıya göre daralt
            label.rectTransform.sizeDelta = new Vector2(maxTextWidth, 0f);
            float width = Mathf.Min(maxTextWidth, label.preferredWidth);
            label.rectTransform.sizeDelta = new Vector2(width, 0f);
            float height = label.preferredHeight;
            label.rectTransform.sizeDelta = new Vector2(width, height);
            bubble.sizeDelta = new Vector2(width, height) + padding * 2f;

            bubble.gameObject.SetActive(true);
            hideAt = Time.time + seconds;
            LateUpdate();
        }

        public void Hide()
        {
            if (bubble != null) bubble.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!IsShowing) return;
            if (Time.time >= hideAt)
            {
                Hide();
                return;
            }

            var cam = Camera.main;
            if (cam == null) return;
            var screen = cam.WorldToScreenPoint(transform.position + Vector3.up * headHeight);
            var canvasRect = (RectTransform)canvas.transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local))
                bubble.anchoredPosition = local;
        }

        private void EnsureUi()
        {
            if (canvas != null) return;
            canvas = UiBuilder.CreateCanvas(transform, "SpeechBubbleCanvas", 30);

            var image = UiBuilder.CreateImage(canvas.transform, "Bubble", background);
            bubble = image.rectTransform;
            bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 0.5f);
            bubble.pivot = new Vector2(0.5f, 0f);

            label = UiBuilder.CreateText(bubble, "Text", null, fontSize, textColor);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            bubble.gameObject.SetActive(false);
        }
    }
}
