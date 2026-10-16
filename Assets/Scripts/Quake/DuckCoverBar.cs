using Deprem.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Quake
{
    /// <summary>
    /// Çök-kapan-tutun sırasında sarsıntının ne kadar süreceğini gösteren çubuk (ekranın altında, ipucunun üstünde).
    /// C basılıyken yeşil, bırakılınca soluk. Görünüş Berke'nin ortak paneli (UiBuilder.CreatePanel).
    /// QuakeDirector çağırır.
    /// </summary>
    public sealed class DuckCoverBar : MonoBehaviour
    {
        [SerializeField] private Vector2 size = new Vector2(200f, 14f);
        [Tooltip("Ekranın altından yükseklik (referans px, 960 x 540)")]
        [SerializeField] private float bottomMargin = 70f;
        [SerializeField] private Color holdingColor = new Color(0.45f, 0.85f, 0.5f);
        [SerializeField] private Color idleColor = new Color(0.7f, 0.7f, 0.72f);

        private Canvas canvas;
        private RectTransform fill;
        private Image fillImage;

        /// <param name="remaining">Kalan süre oranı (1 = başta, 0 = sarsıntı bitti)</param>
        public void Show(float remaining, bool holding)
        {
            EnsureUi();
            canvas.gameObject.SetActive(true);
            remaining = Mathf.Clamp01(remaining);
            fill.anchorMax = new Vector2(remaining, 1f);
            fillImage.color = holding ? holdingColor : idleColor;
        }

        public void Hide()
        {
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        private void EnsureUi()
        {
            if (canvas != null) return;
            canvas = UiBuilder.CreateCanvas(transform, "DuckCoverBar", 35);

            var frame = UiBuilder.CreatePanel(canvas.transform, "Frame").rectTransform;
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0f);
            frame.pivot = new Vector2(0.5f, 0f);
            frame.sizeDelta = size + new Vector2(8f, 8f);
            frame.anchoredPosition = new Vector2(0f, bottomMargin);

            fillImage = UiBuilder.CreateImage(frame, "Fill", holdingColor);
            fill = fillImage.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);
        }
    }
}
