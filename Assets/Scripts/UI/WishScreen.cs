using System.Collections;
using Deprem.Core;
using Deprem.Localization;
using Deprem.Loop;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Keşke ekranı: kaybedince ekran yavaşça kararır, keşke cümlesi harf harf yazılır, kısa bir sessizlikten
    // sonra altında bilgi satırı da yazılır (oyuncu sindirsin; Berke'nin play testi).
    // Oyuncu F'ye (ya da Space/Enter/tık) basınca döngü yöneticisi zamanı geri sarar.
    // Yeni denemede siyahtan açılarak başlar. Canvas'ı kendisi kurar; sahnede tek bir objede durması yeter.
    public class WishScreen : MonoBehaviour
    {
        [Header("Zamanlama (sn)")]
        [SerializeField] float fadeToBlack = 1.6f;
        [SerializeField] float wishDelay = 0.9f;
        [Tooltip("Keşke cümlesinin yazılma hızı (harf/sn)")]
        [SerializeField] float wishCharsPerSecond = 14f;
        [Tooltip("Cümle bittikten sonra bilgi satırından önceki sessizlik")]
        [SerializeField] float infoDelay = 1.6f;
        [SerializeField] float infoCharsPerSecond = 40f;
        [Tooltip("Her şey yazıldıktan sonra devam ipucundan önceki bekleme")]
        [SerializeField] float hintDelay = 1.5f;
        [SerializeField] float fadeFromBlack = 0.8f;

        [Header("Görünüm")]
        [Tooltip("Boşsa Unity'nin yerleşik fontu kullanılır; pixel font gelince buraya")]
        [SerializeField] Font font;
        [SerializeField] int wishFontSize = 32;
        [SerializeField] int infoFontSize = 14;
        [SerializeField] Color wishColor = Color.white;
        [SerializeField] Color infoColor = new Color(0.75f, 0.75f, 0.75f);
        [SerializeField] int sortingOrder = 100;

        CanvasGroup group;
        Text wishText;
        Text infoText;
        Text hintText;
        bool canContinue;

        void Awake() => BuildCanvas();

        void OnEnable()
        {
            LoopManager.WishScreenPresent = true;
            GameEvents.LoopFailed += HandleLoopFailed;
            GameEvents.LoopStarted += HandleLoopStarted;
        }

        void OnDisable()
        {
            LoopManager.WishScreenPresent = false;
            GameEvents.LoopFailed -= HandleLoopFailed;
            GameEvents.LoopStarted -= HandleLoopStarted;
        }

        void Start()
        {
            // Zaman geri sarıldıysa siyah başla; yeni deneme başlayınca açılır
            Hide();
            if (GameState.FailedAttempts > 0) group.alpha = 1f;
        }

        void HandleLoopStarted(int loop)
        {
            StopAllCoroutines();
            if (group.alpha > 0f) StartCoroutine(FadeFromBlack());
        }

        void Update()
        {
            if (canContinue && ContinuePressed())
            {
                canContinue = false;
                hintText.enabled = false;
                if (LoopManager.Instance) LoopManager.Instance.ContinueAfterWish();
            }
        }

        void HandleLoopFailed(LoseReason reason, ItemId item)
        {
            StopAllCoroutines();
            StartCoroutine(Show(WishLines.Wish(reason, item), WishLines.Info(reason)));
        }

        IEnumerator Show(string wish, string info)
        {
            canContinue = false;
            wishText.text = string.Empty;
            infoText.text = string.Empty;
            hintText.enabled = false;

            yield return Fade(group.alpha, 1f, fadeToBlack);
            yield return new WaitForSecondsRealtime(wishDelay);
            yield return Type(wishText, wish, wishCharsPerSecond);
            yield return new WaitForSecondsRealtime(infoDelay);
            yield return Type(infoText, info, infoCharsPerSecond);
            yield return new WaitForSecondsRealtime(hintDelay);
            hintText.text = Texts.Get("prompt.continue");
            hintText.enabled = true;
            canContinue = true;
        }

        // Harf harf yazar; noktalama işaretlerinde kısa durur. Hızlıca okumak isteyen tıklayınca tamamlanır.
        IEnumerator Type(Text target, string text, float charsPerSecond)
        {
            target.text = string.Empty;
            if (string.IsNullOrEmpty(text)) yield break;
            float interval = 1f / Mathf.Max(1f, charsPerSecond);
            for (int i = 1; i <= text.Length; i++)
            {
                target.text = text.Substring(0, i);
                char c = text[i - 1];
                float wait = c == ',' || c == ';' ? interval * 5f : c == '.' || c == '!' || c == '?' ? interval * 8f : interval;
                for (float t = 0f; t < wait; t += Time.unscaledDeltaTime)
                {
                    if (ContinuePressed())
                    {
                        target.text = text;
                        yield break;
                    }
                    yield return null;
                }
            }
        }

        IEnumerator FadeFromBlack()
        {
            group.alpha = 1f;
            wishText.text = string.Empty;
            infoText.text = string.Empty;
            hintText.enabled = false;
            yield return Fade(1f, 0f, fadeFromBlack);
        }

        IEnumerator Fade(float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            group.alpha = to;
        }

        void Hide()
        {
            group.alpha = 0f;
            canContinue = false;
            hintText.enabled = false;
        }

        static bool ContinuePressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.fKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame
                                     || keyboard.enterKey.wasPressedThisFrame))
                return true;
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        void BuildCanvas()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "WishCanvas", sortingOrder);
            group = canvas.GetComponent<CanvasGroup>();

            var background = UiBuilder.CreateImage(canvas.transform, "Background", Color.black);
            UiBuilder.Stretch(background.rectTransform, Vector2.zero, Vector2.one);

            wishText = UiBuilder.CreateText(canvas.transform, "Wish", font, wishFontSize, wishColor);
            UiBuilder.Stretch(wishText.rectTransform, new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.65f));

            infoText = UiBuilder.CreateText(canvas.transform, "Info", font, infoFontSize, infoColor);
            UiBuilder.Stretch(infoText.rectTransform, new Vector2(0.2f, 0.32f), new Vector2(0.8f, 0.48f));

            hintText = UiBuilder.CreateText(canvas.transform, "Hint", font, infoFontSize, infoColor);
            UiBuilder.Stretch(hintText.rectTransform, new Vector2(0.3f, 0.05f), new Vector2(0.7f, 0.12f));
        }
    }
}
