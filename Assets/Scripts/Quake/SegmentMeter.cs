using Deprem.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Quake
{
    /// <summary>
    /// Pixel stil parçalı (segmentli) çubuk: Berke'nin ortak panelinde (UiBuilder.CreatePanel) yan yana kareler,
    /// solda isteğe bağlı kısa etiket. Dolu kareler renkli, boşlar sönük; istenirse son dolu kare yanıp söner.
    /// Play testi 12: düz şerit çubuklar çirkindi. Kurtarma süresi (RescueTimer) ve çök-kapan (DuckCoverBar) kullanır.
    /// </summary>
    public sealed class SegmentMeter : MonoBehaviour
    {
        private Image[] segments;
        private Text label;
        private float flashClock;

        public RectTransform Root => (RectTransform)transform;

        /// <param name="labelText">Boşsa etiket yok</param>
        public static SegmentMeter Create(Transform parent, string name, int count, Vector2 segmentSize, float gap,
            string labelText, int fontSize = 12)
        {
            var panel = UiBuilder.CreatePanel(parent, name);
            var meter = panel.gameObject.AddComponent<SegmentMeter>();
            var root = panel.rectTransform;
            root.pivot = new Vector2(0.5f, 0.5f);

            const float pad = 6f;
            float labelWidth = 0f;
            if (!string.IsNullOrEmpty(labelText))
            {
                meter.label = UiBuilder.CreateText(root, "Label", null, fontSize, new Color(0.95f, 0.9f, 0.8f));
                meter.label.text = labelText;
                meter.label.alignment = TextAnchor.MiddleLeft;
                meter.label.horizontalOverflow = HorizontalWrapMode.Overflow;
                labelWidth = meter.label.preferredWidth + 8f;
                var lr = meter.label.rectTransform;
                lr.anchorMin = lr.anchorMax = new Vector2(0f, 0.5f);
                lr.pivot = new Vector2(0f, 0.5f);
                lr.sizeDelta = new Vector2(labelWidth, segmentSize.y + 4f);
                lr.anchoredPosition = new Vector2(pad, 0f);
            }

            float barWidth = count * segmentSize.x + (count - 1) * gap;
            root.sizeDelta = new Vector2(pad * 2f + labelWidth + barWidth, segmentSize.y + pad * 2f);
            meter.segments = new Image[count];
            for (int i = 0; i < count; i++)
            {
                var segment = UiBuilder.CreateImage(root, "Segment_" + i, Color.white);
                var r = segment.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
                r.pivot = new Vector2(0f, 0.5f);
                r.sizeDelta = segmentSize;
                r.anchoredPosition = new Vector2(pad + labelWidth + i * (segmentSize.x + gap), 0f);
                meter.segments[i] = segment;
            }
            return meter;
        }

        /// <param name="fraction">0–1 dolu oranı</param>
        /// <param name="flashLast">Son dolu kare yanıp söner (süre azalırken)</param>
        public void SetValue(float fraction, Color color, bool flashLast = false)
        {
            if (segments == null) return;
            flashClock += Time.unscaledDeltaTime;
            int lit = Mathf.CeilToInt(Mathf.Clamp01(fraction) * segments.Length);
            var empty = new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.6f);
            bool blinkOff = flashLast && Mathf.Repeat(flashClock * 4f, 1f) < 0.5f;
            for (int i = 0; i < segments.Length; i++)
            {
                bool on = i < lit && !(blinkOff && i == lit - 1);
                // Dolu karelerde hafif üstten alta parlaklık yok (düz pixel), sadece renk
                segments[i].color = on ? color : empty;
            }
        }
    }
}
