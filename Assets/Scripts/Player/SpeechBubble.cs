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
        [Tooltip("Balon yazısının fontu; boşsa ortak UI fontu (Play testi 13: Pixelify Sans balonda zor okunuyordu)")]
        [SerializeField] private Font font;
        [Tooltip("Play testi 11: 14 çok küçüktü, okunmuyordu")]
        [SerializeField] private int fontSize = 18;
        [SerializeField] private float maxTextWidth = 340f;

        [Header("Giriş animasyonu (Play testi 11: yazı bir anda geliyordu)")]
        [Tooltip("Daktilo hızı (harf / sn)")]
        [SerializeField] private float charsPerSecond = 45f;
        [Tooltip("Balon küçükten büyür (sn)")]
        [SerializeField] private float popTime = 0.18f;
        [Tooltip("Yazı bittikten sonra en az bu kadar ekranda kalır (sn)")]
        [SerializeField] private float minReadTime = 1.2f;

        [Header("Yazı sesi (Play testi 23: Undertale gibi)")]
        [Tooltip("Konuşanın sesi: perde (oyuncu 1, baba kalın, kardeş ince)")]
        [SerializeField] private float blipPitch = 1f;
        [Tooltip("Kaç harfte bir blip (her harfte çok sık)")]
        [SerializeField] private int blipEvery = 2;
        [SerializeField, Range(0f, 1f)] private float blipVolume = 0.4f;
        private int blipCounter;

        private Canvas canvas;
        private UiBubble bubble;
        private float hideAt;
        private float shownAt;
        private string fullText = "";
        private int typedChars = -1;

        public bool IsShowing => bubble != null && bubble.gameObject.activeSelf;

        public void Say(string key, float seconds)
        {
            EnsureUi();
            fullText = Texts.Get(key);
            bubble.SetText(fullText, maxTextWidth); // boyut tam metne göre; yazı sonra harf harf gelir
            bubble.gameObject.SetActive(true);
            shownAt = Time.time;
            typedChars = -1;
            float typeTime = fullText.Length / Mathf.Max(1f, charsPerSecond);
            hideAt = Time.time + Mathf.Max(seconds, typeTime + minReadTime);
            LateUpdate();
        }

        /// <summary>Harf harf yazı ve küçükten büyüyen balon</summary>
        private void Animate()
        {
            float t = Time.time - shownAt;
            float pop = popTime > 0f ? Mathf.Clamp01(t / popTime) : 1f;
            // Hafif taşma: 0,6 -> 1,08 -> 1
            float scale = pop < 1f ? Mathf.Lerp(0.6f, 1.08f, pop) : Mathf.Lerp(1.08f, 1f, Mathf.Clamp01((t - popTime) / 0.08f));
            bubble.Root.localScale = Vector3.one * scale;

            int chars = Mathf.Clamp(Mathf.FloorToInt(t * charsPerSecond), 0, fullText.Length);
            if (chars == typedChars) return;
            // Yeni çıkan harflerde blip: boşluk ve noktalamada sessiz, kişiye göre perde, hafif rastgele
            for (int i = Mathf.Max(0, typedChars); i < chars; i++)
            {
                if (!char.IsLetterOrDigit(fullText[i])) continue;
                if (blipCounter++ % Mathf.Max(1, blipEvery) != 0) continue;
                Deprem.Audio.Sfx.PlayPitched("bubble.blip", blipVolume, blipPitch * Random.Range(0.94f, 1.06f));
            }
            typedChars = chars;
            // Yazılmamış kısım saydam: satırlar ve balon boyu yazarken kaymaz
            bubble.Label.text = chars >= fullText.Length
                ? fullText
                : fullText.Substring(0, chars) + "<color=#00000000>" + fullText.Substring(chars) + "</color>";
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
            Animate();

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
            // Sinema bantlarının (97) ve intro karartmasının (98) üstünde: merdivendeki annenin balonu bandın altında kalıyordu
            canvas = UiBuilder.CreateCanvas(transform, "SpeechBubbleCanvas", 99);
            bubble = UiBuilder.CreateSpeechBubble(canvas.transform, font, fontSize);
            bubble.gameObject.SetActive(false);
        }
    }
}
