using System.Collections;
using System.Collections.Generic;
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
    //   2. M21: önce tek başına, büyük "KEŞKE..." daktilo gibi gelir (her harfte vuruş), titreyerek bekler,
    //      küçülüp yukarı kayar; sonra cümlenin devamı ("...KİTAPLIĞI SABİTLESEYDİM") ayrıca, sarsılarak yazılır.
    //   3. M22: altında kırmızı bir çatlak ortadan iki yana yarılır (ekran kısa kırmızı parlar ve sarsılır),
    //      beklerken kalp atışı gibi atar.
    //   4. Kısa bir sessizlik, sonra bilgi satırı yazılır.
    //   5. F (ya da Space/Enter/tık): yazılar geri sarılarak silinir, çatlak kapanır ("zaman geri sarılıyor"),
    //      döngü yöneticisi zamanı geri sarar. Yeni denemede siyahtan açılır. Canvas'ı kendisi kurar.
    public class WishScreen : MonoBehaviour
    {
        [Header("Kayıp anı")]
        [Tooltip("Ekranın kararma süresi (gerçek sn)")]
        [SerializeField] float fadeToBlack = 1.6f;

        [Header("KEŞKE... (sn)")]
        [SerializeField] float wishDelay = 0.9f;
        [Tooltip("Tek başına KEŞKE'nin daktilo hızı (harf/sn)")]
        [SerializeField] float headCharsPerSecond = 6f;
        [Tooltip("KEŞKE yazıldıktan sonra titreyerek bekleme")]
        [SerializeField] float headHold = 1.2f;
        [Tooltip("KEŞKE'nin küçülüp yukarı kayma süresi")]
        [SerializeField] float headMoveSeconds = 0.45f;
        [SerializeField] float headEndScale = 0.6f;
        [SerializeField] float headEndRise = 62f;

        [Header("Cümlenin devamı (sn)")]
        [Tooltip("Cümlenin devamının yazılma hızı (harf/sn)")]
        [SerializeField] float wishCharsPerSecond = 12f;
        [Tooltip("Her harfte yazının sarsılma miktarı (referans pikseli)")]
        [SerializeField] float letterShake = 2f;

        [Header("Çatlak (M22)")]
        [Tooltip("Çatlağın ortadan kenarlara yarılma süresi")]
        [SerializeField] float crackSeconds = 0.4f;
        [Tooltip("Çatlak parçasının boyu ve dikey kırılma miktarı (referans pikseli)")]
        [SerializeField] float crackStep = 14f;
        [SerializeField] float crackJag = 5f;
        [SerializeField] float crackShake = 7f;
        [Tooltip("Beklerken kalp atışı: dakikada vuruş")]
        [SerializeField] float heartbeatBpm = 62f;

        [Header("Bilgi ve devam (sn)")]
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
        [SerializeField] int headFontSize = 76;
        [SerializeField] int wishFontSize = 40;
        [SerializeField] int infoFontSize = 15;
        [SerializeField] Color wishColor = new Color(1f, 0.95f, 0.88f);
        [SerializeField] Color infoColor = new Color(0.72f, 0.72f, 0.75f);
        [SerializeField] Color lineColor = new Color(0.9f, 0.22f, 0.16f);
        [SerializeField] Color flashColor = new Color(0.28f, 0.02f, 0.02f);
        [SerializeField] int sortingOrder = 100;

        CanvasGroup group;
        Image background;
        Text headText;
        Text wishText;
        Text infoText;
        Text hintText;
        RectTransform headRect;
        RectTransform wishRect;
        RectTransform crackRoot;
        Vector2 headHome;
        Vector2 wishHome;
        readonly List<Image> crackCore = new List<Image>();
        readonly List<Image> crackGlow = new List<Image>();
        int crackShown;
        bool heartbeat;
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
            if (heartbeat) Heartbeat();
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
            ClearTexts();
            background.enabled = true;
            yield return Fade(group.alpha, 1f, fadeToBlack);

            SplitWish(wish, out string head, out string rest);
            yield return new WaitForSecondsRealtime(wishDelay);
            if (!string.IsNullOrEmpty(head))
            {
                yield return TypeHead(head);
                yield return HoldHead();
                yield return MoveHead();
            }
            yield return TypeWish(rest);
            yield return Crack();
            yield return new WaitForSecondsRealtime(infoDelay);
            yield return Type(infoText, info, infoCharsPerSecond);
            yield return new WaitForSecondsRealtime(hintDelay);
            hintText.text = Texts.Get("prompt.continue");
            hintText.enabled = true;
            canContinue = true;
        }

        // "Keşke kitaplığı sabitleseydim" → "KEŞKE..." + "...KİTAPLIĞI SABİTLESEYDİM". Ön ek wish.prefix'ten
        // (TR "Keşke", EN "I wish"); cümle onunla başlamıyorsa tamamı tek parça yazılır.
        static void SplitWish(string wish, out string head, out string rest)
        {
            var culture = new System.Globalization.CultureInfo(Texts.Current == Language.Turkish ? "tr-TR" : "en-US");
            head = string.Empty;
            rest = string.IsNullOrEmpty(wish) ? string.Empty : wish.ToUpper(culture);
            string prefix = Texts.Get("wish.prefix");
            if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(wish)) return;
            prefix = prefix.ToUpper(culture);
            if (!rest.StartsWith(prefix) || rest.Length <= prefix.Length) return;
            head = prefix + "...";
            rest = "..." + rest.Substring(prefix.Length).TrimStart();
        }

        // Daktilo: her harf büyükten oturur (vuruş), yazı hafif sarsılır; noktalar daha yavaş
        IEnumerator TypeHead(string text)
        {
            float interval = 1f / Mathf.Max(1f, headCharsPerSecond);
            for (int i = 1; i <= text.Length; i++)
            {
                headText.text = text.Substring(0, i);
                float wait = text[i - 1] == '.' ? interval * 1.6f : interval;
                for (float t = 0f; t < wait; t += Time.unscaledDeltaTime)
                {
                    float k = Mathf.Clamp01(t / (interval * 0.5f));
                    headRect.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, k);
                    headRect.anchoredPosition = headHome + Random.insideUnitCircle * letterShake * 1.5f * (1f - k);
                    if (ContinuePressed())
                    {
                        headText.text = text;
                        headRect.localScale = Vector3.one;
                        headRect.anchoredPosition = headHome;
                        yield break;
                    }
                    yield return null;
                }
            }
            headRect.localScale = Vector3.one;
            headRect.anchoredPosition = headHome;
        }

        // Tek başına, titreyerek: oyuncu sindirsin
        IEnumerator HoldHead()
        {
            for (float t = 0f; t < headHold; t += Time.unscaledDeltaTime)
            {
                headRect.anchoredPosition = headHome + new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));
                if (ContinuePressed()) break;
                yield return null;
            }
            headRect.anchoredPosition = headHome;
        }

        IEnumerator MoveHead()
        {
            var end = headHome + new Vector2(0f, headEndRise);
            for (float t = 0f; t < headMoveSeconds; t += Time.unscaledDeltaTime)
            {
                float k = 1f - (1f - t / headMoveSeconds) * (1f - t / headMoveSeconds);
                headRect.anchoredPosition = Vector2.Lerp(headHome, end, k);
                headRect.localScale = Vector3.one * Mathf.Lerp(1f, headEndScale, k);
                yield return null;
            }
            headRect.anchoredPosition = end;
            headRect.localScale = Vector3.one * headEndScale;
        }

        // Cümlenin devamı: her harfte hafif sarsıntı; noktalamada durur; sonunda kısa bir vuruş. Tıklayınca tamamlanır.
        IEnumerator TypeWish(string text)
        {
            wishText.text = string.Empty;
            if (string.IsNullOrEmpty(text)) yield break;
            float interval = 1f / Mathf.Max(1f, wishCharsPerSecond);
            for (int i = 1; i <= text.Length; i++)
            {
                wishText.text = text.Substring(0, i);
                char c = text[i - 1];
                bool openingDots = c == '.' && i <= 3;
                float wait = openingDots ? interval * 2f
                    : c == ',' || c == ';' ? interval * 5f : c == '.' || c == '!' || c == '?' ? interval * 8f : interval;
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

        // M22: cümlenin altını kırmızı bir çatlak yarar. Ortadan iki yana hızla ilerler; o anda ekran koyu kırmızı
        // parlar ve yazılar sarsılır. Sonra çatlak kalp atışı gibi atar (Heartbeat).
        IEnumerator Crack()
        {
            float canvasWidth = ((RectTransform)wishText.canvas.transform).rect.width;
            float width = Mathf.Clamp(wishText.preferredWidth, 220f, canvasWidth * 0.75f);
            BuildCrack(width);
            var shakeHead = headRect.anchoredPosition;
            for (float t = 0f; t < crackSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / crackSeconds;
                ShowCrack(Mathf.CeilToInt(crackCore.Count * k));
                float shake = crackShake * (1f - k);
                var offset = Random.insideUnitCircle * shake;
                wishRect.anchoredPosition = wishHome + offset;
                headRect.anchoredPosition = shakeHead + offset * 0.6f;
                background.color = Color.Lerp(flashColor, Color.black, k);
                yield return null;
            }
            ShowCrack(crackCore.Count);
            wishRect.anchoredPosition = wishHome;
            headRect.anchoredPosition = shakeHead;
            // Parlama yavaşça söner
            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
            {
                background.color = Color.Lerp(Color.Lerp(flashColor, Color.black, 0.6f), Color.black, t / 0.5f);
                yield return null;
            }
            background.color = Color.black;
            heartbeat = true;
        }

        // Kırık çizgi: crackStep aralıklı, dikeyde crackJag kadar sapan noktalar; her parça döndürülmüş ince bir
        // Image (çekirdek 3 px, arkasında yarı saydam 9 px ışıma). Birkaç kısa dal. Pikseller tam sayıya oturur.
        void BuildCrack(float width)
        {
            ClearCrack();
            var points = new List<Vector2>();
            float half = width * 0.5f;
            float y = 0f;
            for (float x = -half; x <= half + 0.01f; x += crackStep)
            {
                points.Add(new Vector2(Mathf.Round(x), Mathf.Round(y)));
                y = Mathf.Clamp(y + Random.Range(-crackJag, crackJag), -crackJag * 1.6f, crackJag * 1.6f);
            }

            var segments = new List<(Vector2 a, Vector2 b, float thickness)>();
            for (int i = 0; i + 1 < points.Count; i++) segments.Add((points[i], points[i + 1], 3f));
            // Dallar: ana çizgiden yukarı ya da aşağı 2 kısa parça
            for (int i = 2; i + 2 < points.Count; i += Random.Range(3, 6))
            {
                var start = points[i];
                float dir = Random.value < 0.5f ? -1f : 1f;
                var mid = start + new Vector2(Random.Range(-crackStep, crackStep), dir * Random.Range(crackJag, crackJag * 2f));
                var end = mid + new Vector2(Random.Range(-crackStep, crackStep) * 0.7f, dir * Random.Range(crackJag * 0.5f, crackJag * 1.5f));
                segments.Add((start, Round(mid), 2f));
                segments.Add((Round(mid), Round(end), 2f));
            }
            // Ortadan kenara doğru açılsın
            segments.Sort((s1, s2) => Mathf.Abs((s1.a.x + s1.b.x) * 0.5f).CompareTo(Mathf.Abs((s2.a.x + s2.b.x) * 0.5f)));

            var glowColor = new Color(lineColor.r, lineColor.g, lineColor.b, 0.28f);
            foreach (var (a, b, thickness) in segments)
            {
                crackGlow.Add(Segment(a, b, thickness * 3f, glowColor));
                crackCore.Add(Segment(a, b, thickness, lineColor));
            }
            // Işıma çekirdeğin arkasında kalsın
            foreach (var glow in crackGlow) glow.transform.SetAsFirstSibling();
            ShowCrack(0);
        }

        static Vector2 Round(Vector2 v) => new Vector2(Mathf.Round(v.x), Mathf.Round(v.y));

        Image Segment(Vector2 a, Vector2 b, float thickness, Color color)
        {
            var image = UiBuilder.CreateImage(crackRoot, "Crack", color);
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            var delta = b - a;
            rect.sizeDelta = new Vector2(delta.magnitude + thickness * 0.5f, thickness);
            rect.anchoredPosition = a;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            return image;
        }

        void ShowCrack(int count)
        {
            crackShown = Mathf.Clamp(count, 0, crackCore.Count);
            for (int i = 0; i < crackCore.Count; i++)
            {
                bool on = i < crackShown;
                crackCore[i].enabled = on;
                crackGlow[i].enabled = on;
            }
        }

        void ClearCrack()
        {
            heartbeat = false;
            foreach (var image in crackCore) if (image) Destroy(image.gameObject);
            foreach (var image in crackGlow) if (image) Destroy(image.gameObject);
            crackCore.Clear();
            crackGlow.Clear();
            crackShown = 0;
        }

        // İki vuruşlu kalp atışı (güm-güm ... güm-güm): ışıma kalınlaşır ve parlar
        void Heartbeat()
        {
            float period = 60f / Mathf.Max(20f, heartbeatBpm);
            float t = Time.unscaledTime % period / period;
            float beat = Mathf.Max(Pulse(t, 0f), Pulse(t, 0.22f) * 0.7f);
            var glowColor = new Color(lineColor.r, lineColor.g, lineColor.b, 0.22f + 0.4f * beat);
            var coreColor = Color.Lerp(lineColor, new Color(1f, 0.55f, 0.45f), beat * 0.6f);
            for (int i = 0; i < crackShown; i++)
            {
                crackGlow[i].color = glowColor;
                crackCore[i].color = coreColor;
            }
            crackRoot.localScale = new Vector3(1f, 1f + beat * 0.25f, 1f);
        }

        static float Pulse(float t, float at)
        {
            float d = t - at;
            return d < 0f || d > 0.16f ? 0f : 1f - d / 0.16f;
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

        // Devam: önce bilgi satırı, sonra cümlenin devamı sondan başa hızla silinir ve çatlak kenarlardan kapanır,
        // en son KEŞKE silinir; zaman geri sarılır
        IEnumerator RewindAndContinue()
        {
            rewinding = true;
            heartbeat = false;
            crackRoot.localScale = Vector3.one;
            hintText.text = Texts.Get("wish.rewind");
            hintText.enabled = true;
            yield return Erase(infoText);
            int crackStart = crackShown;
            int wishLength = wishText.text.Length;
            float interval = 1f / Mathf.Max(1f, rewindCharsPerSecond);
            for (int i = wishLength; i >= 0; i--)
            {
                wishText.text = wishText.text.Substring(0, i);
                ShowCrack(wishLength > 0 ? crackStart * i / wishLength : 0);
                wishRect.anchoredPosition = wishHome + new Vector2(Random.Range(-1f, 1f), 0f);
                yield return new WaitForSecondsRealtime(interval);
            }
            wishRect.anchoredPosition = wishHome;
            ShowCrack(0);
            yield return Erase(headText);
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
            ClearTexts();
            yield return Fade(1f, 0f, fadeFromBlack);
        }

        void ClearTexts()
        {
            headText.text = string.Empty;
            wishText.text = string.Empty;
            infoText.text = string.Empty;
            hintText.enabled = false;
            headRect.anchoredPosition = headHome;
            headRect.localScale = Vector3.one;
            wishRect.anchoredPosition = wishHome;
            background.color = Color.black;
            crackRoot.localScale = Vector3.one;
            ClearCrack();
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

            // Tek başına KEŞKE ekranın ortasında; küçülünce cümlenin üstüne kayar
            headText = UiBuilder.CreateText(canvas.transform, "WishHead", font, headFontSize, wishColor);
            UiBuilder.Stretch(headText.rectTransform, new Vector2(0.04f, 0.46f), new Vector2(0.96f, 0.74f));
            headRect = headText.rectTransform;
            headHome = headRect.anchoredPosition;

            wishText = UiBuilder.CreateText(canvas.transform, "Wish", font, wishFontSize, wishColor);
            UiBuilder.Stretch(wishText.rectTransform, new Vector2(0.06f, 0.5f), new Vector2(0.94f, 0.66f));
            wishRect = wishText.rectTransform;
            wishHome = wishRect.anchoredPosition;

            crackRoot = new GameObject("CrackRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            crackRoot.SetParent(canvas.transform, false);
            crackRoot.anchorMin = crackRoot.anchorMax = new Vector2(0.5f, 0.48f);
            crackRoot.sizeDelta = Vector2.zero;

            infoText = UiBuilder.CreateText(canvas.transform, "Info", font, infoFontSize, infoColor);
            UiBuilder.Stretch(infoText.rectTransform, new Vector2(0.2f, 0.28f), new Vector2(0.8f, 0.44f));

            hintText = UiBuilder.CreateText(canvas.transform, "Hint", font, infoFontSize, infoColor);
            UiBuilder.Stretch(hintText.rectTransform, new Vector2(0.3f, 0.05f), new Vector2(0.7f, 0.12f));
        }
    }
}
