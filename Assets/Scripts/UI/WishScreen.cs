using System.Collections;
using Deprem.Core;
using Deprem.Localization;
using Deprem.Loop;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Keşke ekranı: kaybedince ekran kararır, keşke cümlesi ve altında bilgi satırı çıkar.
    // Oyuncu F'ye (ya da Space/Enter/tık) basınca döngü yöneticisi zamanı geri sarar.
    // Yeni denemede siyahtan açılarak başlar. Canvas'ı kendisi kurar; sahnede tek bir objede durması yeter.
    public class WishScreen : MonoBehaviour
    {
        [Header("Zamanlama (sn)")]
        [SerializeField] float fadeToBlack = 0.6f;
        [SerializeField] float wishDelay = 0.8f;
        [SerializeField] float infoDelay = 1.2f;
        [SerializeField] float minShowTime = 2.5f;
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
            wishText.text = wish;
            yield return new WaitForSecondsRealtime(infoDelay);
            infoText.text = info;

            float shown = wishDelay + infoDelay;
            if (shown < minShowTime) yield return new WaitForSecondsRealtime(minShowTime - shown);
            hintText.text = Texts.Get("prompt.continue");
            hintText.enabled = true;
            canContinue = true;
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
