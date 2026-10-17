using System.Collections;
using Deprem.Core;
using Deprem.Localization;
using Deprem.Loop;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Keşke ekranı (Berke'nin play testi: etkili ve çarpıcı olsun, oyuncu sindirsin):
    //   1. Kayıp anında ekran yavaşça kararır (iris ve cam kırılması denendi, beğenilmedi).
    //   2. Siyahın üstünde keşke cümlesi büyük harflerle, her harfte hafif sarsıntıyla yazılır; altında çizgi uzar.
    //   3. Kısa bir sessizlik, sonra bilgi satırı yazılır.
    //   4. F (ya da Space/Enter/tık): yazılar geri sarılarak silinir ("zaman geri sarılıyor"), döngü yöneticisi
    //      zamanı geri sarar. Yeni denemede siyahtan açılır. Canvas'ı kendisi kurar.
    public class WishScreen : MonoBehaviour
    {
        [Header("Kayıp anı")]
        [Tooltip("Ekranın kararma süresi (gerçek sn)")]
        [SerializeField] float fadeToBlack = 1.6f;

        [Header("Yazı (sn)")]
        [SerializeField] float wishDelay = 0.9f;
        [Tooltip("Keşke cümlesinin yazılma hızı (harf/sn)")]
        [SerializeField] float wishCharsPerSecond = 12f;
        [Tooltip("Her harfte yazının sarsılma miktarı (referans pikseli)")]
        [SerializeField] float letterShake = 2f;
        [SerializeField] float lineGrowSeconds = 0.7f;
        [Tooltip("Cümle bittikten sonra bilgi satırından önceki sessizlik")]
        [SerializeField] float infoDelay = 1.6f;
        [SerializeField] float infoCharsPerSecond = 40f;
        [Tooltip("Her şey yazıldıktan sonra devam ipucundan önceki bekleme")]
        [SerializeField] float hintDelay = 1.5f;
        [Tooltip("Devam edince yazıların geri sarılarak silinme hızı (harf/sn)")]
        [SerializeField] float rewindCharsPerSecond = 90f;
        [SerializeField] float fadeFromBlack = 0.8f;

        [Header("Görünüm")]
        [Tooltip("Boşsa UiStyle fontu (Pixelify Sans)")]
        [SerializeField] Font font;
        [SerializeField] int wishFontSize = 40;
        [SerializeField] int infoFontSize = 15;
        [SerializeField] Color wishColor = new Color(1f, 0.95f, 0.88f);
        [SerializeField] Color infoColor = new Color(0.72f, 0.72f, 0.75f);
        [SerializeField] Color lineColor = new Color(0.85f, 0.35f, 0.25f);
        [SerializeField] int sortingOrder = 100;

        CanvasGroup group;
        Image background;
        Text wishText;
        Text infoText;
        Text hintText;
        RectTransform line;
        RectTransform wishRect;
        Vector2 wishHome;
        bool canContinue;
        bool rewinding;

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
            if (Time.timeScale < 1f && Time.timeScale > 0f) Time.timeScale = 1f;
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
            RestoreTime();
            if (group.alpha > 0f) StartCoroutine(FadeFromBlack());
        }

        void Update()
        {
            if (canContinue && !rewinding && ContinuePressed())
            {
                canContinue = false;
                hintText.enabled = false;
                StartCoroutine(RewindAndContinue());
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
            rewinding = false;
            wishText.text = string.Empty;
            infoText.text = string.Empty;
            hintText.enabled = false;
            line.sizeDelta = new Vector2(0f, line.sizeDelta.y);

            background.enabled = true;
            yield return Fade(group.alpha, 1f, fadeToBlack);

            yield return new WaitForSecondsRealtime(wishDelay);
            yield return TypeWish(wish);
            yield return GrowLine();
            yield return new WaitForSecondsRealtime(infoDelay);
            yield return Type(infoText, info, infoCharsPerSecond);
            yield return new WaitForSecondsRealtime(hintDelay);
            hintText.text = Texts.Get("prompt.continue");
            hintText.enabled = true;
            canContinue = true;
        }

        // Büyük harflerle, her harfte hafif sarsıntı; noktalamada durur. Tıklayınca tamamlanır.
        IEnumerator TypeWish(string text)
        {
            wishText.text = string.Empty;
            if (string.IsNullOrEmpty(text)) yield break;
            text = text.ToUpper(new System.Globalization.CultureInfo(Texts.Current == Language.Turkish ? "tr-TR" : "en-US"));
            float interval = 1f / Mathf.Max(1f, wishCharsPerSecond);
            for (int i = 1; i <= text.Length; i++)
            {
                wishText.text = text.Substring(0, i);
                char c = text[i - 1];
                float wait = c == ',' || c == ';' ? interval * 5f : c == '.' || c == '!' || c == '?' ? interval * 8f : interval;
                for (float t = 0f; t < wait; t += Time.unscaledDeltaTime)
                {
                    float shake = c == ' ' ? 0f : letterShake * (1f - t / wait);
                    wishRect.anchoredPosition = wishHome + Random.insideUnitCircle * shake;
                    if (ContinuePressed())
                    {
                        wishText.text = text;
                        wishRect.anchoredPosition = wishHome;
                        yield break;
                    }
                    yield return null;
                }
            }
            wishRect.anchoredPosition = wishHome;
        }

        IEnumerator GrowLine()
        {
            float target = Mathf.Min(wishText.preferredWidth, ((RectTransform)wishText.canvas.transform).rect.width * 0.7f);
            for (float t = 0f; t < lineGrowSeconds; t += Time.unscaledDeltaTime)
            {
                float k = 1f - (1f - t / lineGrowSeconds) * (1f - t / lineGrowSeconds);
                line.sizeDelta = new Vector2(target * k, line.sizeDelta.y);
                yield return null;
            }
            line.sizeDelta = new Vector2(target, line.sizeDelta.y);
        }

        // Harf harf yazar; noktalama işaretlerinde kısa durur. Tıklayınca tamamlanır.
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

        // Devam: önce bilgi satırı, sonra keşke cümlesi sondan başa hızla silinir; çizgi kısalır; zaman geri sarılır
        IEnumerator RewindAndContinue()
        {
            rewinding = true;
            hintText.text = Texts.Get("wish.rewind");
            hintText.enabled = true;
            yield return Erase(infoText);
            float lineStart = line.sizeDelta.x;
            int wishLength = wishText.text.Length;
            float interval = 1f / Mathf.Max(1f, rewindCharsPerSecond);
            for (int i = wishLength; i >= 0; i--)
            {
                wishText.text = wishText.text.Substring(0, i);
                line.sizeDelta = new Vector2(wishLength > 0 ? lineStart * i / wishLength : 0f, line.sizeDelta.y);
                wishRect.anchoredPosition = wishHome + new Vector2(Random.Range(-1f, 1f), 0f);
                yield return new WaitForSecondsRealtime(interval);
            }
            wishRect.anchoredPosition = wishHome;
            hintText.enabled = false;
            rewinding = false;
            if (LoopManager.Instance) LoopManager.Instance.ContinueAfterWish();
        }

        IEnumerator Erase(Text target)
        {
            float interval = 1f / Mathf.Max(1f, rewindCharsPerSecond * 2f);
            for (int i = target.text.Length; i >= 0; i--)
            {
                target.text = target.text.Substring(0, i);
                yield return new WaitForSecondsRealtime(interval);
            }
        }

        IEnumerator FadeFromBlack()
        {
            group.alpha = 1f;
            background.enabled = true;
            wishText.text = string.Empty;
            infoText.text = string.Empty;
            hintText.enabled = false;
            line.sizeDelta = new Vector2(0f, line.sizeDelta.y);
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

        void RestoreTime()
        {
            if (Time.timeScale > 0f && Time.timeScale < 1f) Time.timeScale = 1f;
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

            background = UiBuilder.CreateImage(canvas.transform, "Background", Color.black);
            UiBuilder.Stretch(background.rectTransform, Vector2.zero, Vector2.one);


            wishText = UiBuilder.CreateText(canvas.transform, "Wish", font, wishFontSize, wishColor);
            UiBuilder.Stretch(wishText.rectTransform, new Vector2(0.06f, 0.5f), new Vector2(0.94f, 0.68f));
            wishRect = wishText.rectTransform;
            wishHome = wishRect.anchoredPosition;

            line = UiBuilder.CreateImage(canvas.transform, "Line", lineColor).rectTransform;
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.49f);
            line.sizeDelta = new Vector2(0f, 3f);

            infoText = UiBuilder.CreateText(canvas.transform, "Info", font, infoFontSize, infoColor);
            UiBuilder.Stretch(infoText.rectTransform, new Vector2(0.2f, 0.3f), new Vector2(0.8f, 0.46f));

            hintText = UiBuilder.CreateText(canvas.transform, "Hint", font, infoFontSize, infoColor);
            UiBuilder.Stretch(hintText.rectTransform, new Vector2(0.3f, 0.05f), new Vector2(0.7f, 0.12f));
        }
    }
}
