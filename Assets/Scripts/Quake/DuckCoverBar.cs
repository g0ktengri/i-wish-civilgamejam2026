using Deprem.UI;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Çök-kapan-tutun sırasında sarsıntının ne kadar süreceğini gösteren parçalı pixel çubuk (SegmentMeter).
    /// Play testi 12: ekranın altında ipucunun üstüne biniyordu; artık karakterin başının üstünde, onunla gider.
    /// C basılıyken yeşil, bırakılınca soluk. QuakeDirector çağırır (Follow, Show, Hide).
    /// </summary>
    public sealed class DuckCoverBar : MonoBehaviour
    {
        [SerializeField] private int segments = 10;
        [SerializeField] private Vector2 segmentSize = new Vector2(12f, 12f); // Play testi 21: küçüktü
        [SerializeField] private float segmentGap = 3f;
        [Tooltip("Çubuğun karakterin ayağından yüksekliği (m); çömelince baş ~1 m'de")]
        [SerializeField] private float height = 1.55f;
        [SerializeField] private Color holdingColor = new Color(0.45f, 0.85f, 0.5f);
        [SerializeField] private Color idleColor = new Color(0.75f, 0.75f, 0.72f);

        private Canvas canvas;
        private SegmentMeter meter;
        private Transform target;

        /// <summary>Çubuğun üstünde duracağı karakter</summary>
        public void Follow(Transform character) => target = character;

        /// <param name="remaining">Kalan süre oranı (1 = başta, 0 = sarsıntı bitti)</param>
        public void Show(float remaining, bool holding)
        {
            EnsureUi();
            canvas.gameObject.SetActive(true);
            meter.SetValue(remaining, holding ? holdingColor : idleColor);
            Place();
        }

        public void Hide()
        {
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (canvas != null && canvas.gameObject.activeSelf) Place();
        }

        /// <summary>Karakterin başının üstüne (viewport üzerinden: düşük çözünürlüklü pixel ekranda da doğru yer)</summary>
        private void Place()
        {
            var cam = Camera.main;
            if (cam == null || target == null) return;
            var viewport = cam.WorldToViewportPoint(target.position + Vector3.up * height);
            var screen = new Vector3(viewport.x * Screen.width, viewport.y * Screen.height, viewport.z);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, null, out var local))
                meter.Root.anchoredPosition = local;
        }

        private void EnsureUi()
        {
            if (canvas != null) return;
            canvas = UiBuilder.CreateCanvas(transform, "DuckCoverBar", 35);
            meter = SegmentMeter.Create(canvas.transform, "Meter", segments, segmentSize, segmentGap, null);
            meter.Root.anchorMin = meter.Root.anchorMax = new Vector2(0.5f, 0.5f);
            meter.Root.pivot = new Vector2(0.5f, 0f);
        }
    }
}
