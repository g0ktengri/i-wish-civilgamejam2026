using Deprem.Localization;
using Deprem.UI;
using UnityEngine;

namespace Deprem.Player
{
    /// <summary>
    /// Karakterin başının üstünde kısa konuşma balonu (döngü 1-2'de karakterin ne düşündüğü, aile tepkisi).
    /// Metin texts.csv anahtarıyla verilir: Say("bubble.ignore_foreshock", 3f).
    /// Görünüş Berke'nin ortak UI stilinden (UiBuilder.CreateSpeechBubble, UiStyle: font, balon, kuyruk).
    /// </summary>
    public sealed class SpeechBubble : MonoBehaviour
    {
        [Tooltip("Kuyruğun ucunun karakterin ayağından yüksekliği (m)")]
        [SerializeField] private float headHeight = 2.1f;
        [Tooltip("Yatarken balonun yüksekliği (yatakta 2,1 m çok yukarıda kalıyordu)")]
        [SerializeField] private float lyingHeadHeight = 1.15f;
        [SerializeField] private int fontSize = 14;
        [SerializeField] private float maxTextWidth = 220f;

        private Canvas canvas;
        private UiBubble bubble;
        private float hideAt;

        public bool IsShowing => bubble != null && bubble.gameObject.activeSelf;

        public void Say(string key, float seconds)
        {
            EnsureUi();
            bubble.SetText(Texts.Get(key), maxTextWidth);
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
            var controller = GetComponent<PlayerController>();
            float height = controller != null && controller.IsLying ? lyingHeadHeight : headHeight;
            // Viewport üzerinden: kamera düşük çözünürlüklü dokuya çizerken de (UI.PixelScreen) doğru yer
            var viewport = cam.WorldToViewportPoint(transform.position + Vector3.up * height);
            var screen = new Vector3(viewport.x * Screen.width, viewport.y * Screen.height, viewport.z);
            var canvasRect = (RectTransform)canvas.transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local))
                bubble.Root.anchoredPosition = local; // kökün pivotu kuyruğun ucu
        }

        private void EnsureUi()
        {
            if (canvas != null) return;
            canvas = UiBuilder.CreateCanvas(transform, "SpeechBubbleCanvas", 30);
            bubble = UiBuilder.CreateSpeechBubble(canvas.transform, null, fontSize);
            bubble.gameObject.SetActive(false);
        }
    }
}
