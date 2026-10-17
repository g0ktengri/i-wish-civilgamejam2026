using System.Collections;
using Deprem.Localization;
using Deprem.UI;
using UnityEngine;

namespace Deprem.Spirit
{
    // Ruhun konuşma balonu (Berke'nin play testi: ruh formuna geçince karakter hiçbir şey demiyordu).
    // Oyunun ortak balon stilini kullanır (UiBuilder.CreateSpeechBubble). SpiritPhase ekler ve konuşturur.
    public class SpiritBubble : MonoBehaviour
    {
        [Tooltip("Balonun ruhun üstündeki yüksekliği (m)")]
        [SerializeField] float height = 0.45f;
        [Tooltip("Balon fontu; boşsa ortak UI fontu. SpiritPhase verir (Mehmet'in balon fontu DepremBalon)")]
        [SerializeField] Font font;
        [SerializeField] int fontSize = 18;
        [SerializeField] float maxTextWidth = 220f;
        [SerializeField] int sortingOrder = 31;

        Canvas canvas;
        UiBubble bubble;

        // İlk Say'den önce çağrılır (balon o anda kurulur)
        public void SetFont(Font bubbleFont, int size)
        {
            if (canvas != null) return;
            font = bubbleFont;
            if (size > 0) fontSize = size;
        }

        // Sırayla söyler: her satır (anahtar, saniye)
        public void Say(params (string key, float seconds)[] lines)
        {
            EnsureUi();
            StopAllCoroutines();
            StartCoroutine(SayRoutine(lines));
        }

        public void Hide()
        {
            StopAllCoroutines();
            if (bubble != null) bubble.gameObject.SetActive(false);
        }

        void OnDisable() => Hide();

        IEnumerator SayRoutine((string key, float seconds)[] lines)
        {
            foreach (var (key, seconds) in lines)
            {
                bubble.SetText(Texts.Get(key), maxTextWidth);
                bubble.gameObject.SetActive(true);
                for (float t = 0f; t < seconds; t += Time.deltaTime)
                {
                    Place();
                    yield return null;
                }
            }
            bubble.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (bubble != null && bubble.gameObject.activeSelf) Place();
        }

        void Place()
        {
            var cam = Camera.main;
            if (cam == null) return;
            // Viewport üzerinden: kamera düşük çözünürlüklü dokuya çizerken de (UI.PixelScreen) doğru yer
            var viewport = cam.WorldToViewportPoint(transform.position + Vector3.up * height);
            var screen = new Vector3(viewport.x * Screen.width, viewport.y * Screen.height, viewport.z);
            var canvasRect = (RectTransform)canvas.transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local))
                bubble.Root.anchoredPosition = local; // kökün pivotu kuyruğun ucu
        }

        void EnsureUi()
        {
            if (canvas != null) return;
            // Ruh objesinin altında değil: ruh kapanıp açıldıkça balon kaybolmasın diye ayrı kök
            canvas = UiBuilder.CreateCanvas(null, "SpiritBubbleCanvas", sortingOrder);
            bubble = UiBuilder.CreateSpeechBubble(canvas.transform, font, fontSize);
            bubble.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }
    }
}
