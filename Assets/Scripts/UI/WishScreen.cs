using System.Collections;
using System.Collections.Generic;
using Deprem.Audio;
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
    //      Aile üyesi kaybedildiyse (ezilme, eksik malzeme) önce o gösterilir: ekran üyenin çevresindeki daire dışında
    //      kararır, üyeye güçlü sıcak bir ışık vurur, kısa bir an öyle kalır (play testi: karanlıkta ölü bulunan
    //      anne / baba görünmüyordu).
    //   2. M21: önce tek başına, büyük "KEŞKE..." daktilo gibi gelir (her harfte vuruş), titreyerek bekler,
    //      küçülüp yukarı kayar; sonra cümlenin devamı ("...KİTAPLIĞI SABİTLESEYDİM") ayrıca, sarsılarak yazılır.
    //   3. M22: altında kırmızı bir çatlak ortadan iki yana yarılır (ekran kısa kırmızı parlar ve sarsılır),
    //      beklerken kalp atışı gibi atar.
    //   4. Kısa bir sessizlik, sonra bilgi satırı yazılır.
    //   5. F (ya da Space/Enter/tık): yazılar geri sarılarak silinir, çatlak kapanır ("zaman geri sarılıyor"),
    //      döngü yöneticisi zamanı geri sarar. Yeni denemede siyahtan açılır. Canvas'ı kendisi kurar.
    // M25 sesleri: wish.note (KEŞKE gelirken), wish.type (her harf, çok kısık), wish.shatter (çatlağın yarıldığı an),
    // wish.rewind (geri sarma).
    public class WishScreen : MonoBehaviour
    {
        [Header("Kayıp anı")]
        [Tooltip("Ekranın kararma süresi (gerçek sn)")]
        [SerializeField] float fadeToBlack = 1.6f;
        [Tooltip("M60: aynı keşke ikinci kez geliyorsa yazı, bekleme, çatlak ve geri sarma bu kadar kat hızlı")]
        [SerializeField] float repeatPace = 3f;
        [Tooltip("Jüri temposu: ilk kez görülen keşke de bu kat hızlı akar (1 = eski hız)")]
        [SerializeField] float firstPace = 1.5f;
        [Tooltip("M52: ölüm siyahının üstüne keşke ekranının siyahı bu sürede gelir (ekran zaten kararmışsa fark edilmez)")]
        [SerializeField] float blackTakeover = 0.5f;
        [Tooltip("M52: yazılar ve çatlak siyahın içinden bu sürede belirir")]
        [SerializeField] float contentFadeIn = 0.7f;


        [Header("Kayıp anı: aile üyesi")]
        [Tooltip("Dairenin dışının kararma süresi")]
        [SerializeField] float focusInSeconds = 0.7f;
        [Tooltip("Üye ışıkta gösterilirken bekleme")]
        [SerializeField] float focusHold = 1.8f;
        [Tooltip("Dairenin dışındaki karartma (0-1; doğrusal renk uzayında 0,96 bile gri kalıyordu, 1 = tam siyah)")]
        [SerializeField] float focusDarkness = 1f;
        [Tooltip("Dairenin yarıçapı (referans pikseli; yumuşak kenar bunun ~1,5 katına kadar)")]
        [SerializeField] float focusRadius = 120f;
        [SerializeField] Color focusLightColor = new Color(1f, 0.9f, 0.78f);
        [SerializeField] float focusLightIntensity = 4f;
        [SerializeField] float focusLightRange = 3.5f;

        [Header("KEŞKE... (sn)")]
        [SerializeField] float wishDelay = 0.9f;
        [Tooltip("Tek başına KEŞKE'nin daktilo hızı (harf/sn)")]
        [SerializeField] float headCharsPerSecond = 6f;
        [Tooltip("KEŞKE yazıldıktan sonra titreyerek bekleme")]
        [SerializeField] float headHold = 1.2f;
        [Tooltip("KEŞKE'nin küçülüp yukarı kayma süresi")]
        [SerializeField] float headMoveSeconds = 0.45f;
        [SerializeField] float headEndScale = 0.6f;
        [SerializeField] float headEndRise = 92f; // büyüyen cümlenin (iki satır olabilir) üstünde kalsın

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
        [Tooltip("Berke'nin play testi: çatlak her keşkede büyür. İlk keşke en küçük, bu kadarıncı keşkede en büyük")]
        [SerializeField] int crackFullAtWish = 5;
        [Tooltip("İlk keşkede çatlağın eni (cümle enine oran) ve kalınlığı; en büyükte en ve kalınlık")]
        [SerializeField] Vector2 crackWidthRange = new Vector2(0.3f, 1.15f);
        [SerializeField] Vector2 crackThicknessRange = new Vector2(2f, 5f);

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
        [Tooltip("Mehmet'in play testi: keşkeler daha büyük olsun (76 → 96)")]
        [SerializeField] int headFontSize = 96;
        [Tooltip("Mehmet'in play testi: keşke cümlesi daha büyük (40 → 54; uzun cümle iki satıra iner)")]
        [SerializeField] int wishFontSize = 54;
        [Tooltip("M33: bilgi satırı ve ipucu 15'ten 19'a (okunmuyordu); Mehmet'in play testi: 23")]
        [SerializeField] int infoFontSize = 23;
        [SerializeField] Color wishColor = new Color(1f, 0.95f, 0.88f);
        [SerializeField] Color infoColor = new Color(0.72f, 0.72f, 0.75f);
        [SerializeField] Color lineColor = new Color(0.9f, 0.22f, 0.16f);
        [SerializeField] Color flashColor = new Color(0.28f, 0.02f, 0.02f);
        [SerializeField] int sortingOrder = 100;

        [Header("Ses (M25)")]
        [Tooltip("Harf başına daktilo sesinin şiddeti (0: kapalı)")]
        [SerializeField] float typeVolume = 0.25f;

        CanvasGroup group;
        CanvasGroup content;
        float pace = 1f;

        Image background;
        Image focus;
        Light focusLight;
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
            // M60 (Mehmet'in play testi 31): aynı keşke daha önce gösterildiyse ekran hızlı akar
            pace = WishLines.WasShown(reason, item) ? Mathf.Max(firstPace, repeatPace) : Mathf.Max(1f, firstPace);
            WishLines.Record(reason, item); // M47: final ekranı için
            StartCoroutine(Show(WishLines.Wish(reason, item), WishLines.Info(reason)));
        }

        IEnumerator Show(string wish, string info)
        {
            canContinue = false;
            rewinding = false;
            ClearTexts();
            background.enabled = true;
            var lost = LostFamilyMember(GameState.LastLoseReason);
            if (lost)
            {
                // Önce kaybedilen üye: arka plan saydam başlar, daire dışı kararır, sonra siyaha geçilir
                background.color = new Color(0f, 0f, 0f, 0f);
                group.alpha = 1f;
                yield return FocusOn(lost);
                yield return FadeBackground(fadeToBlack / pace);
                EndFocus();
            }
            // M52 (Mehmet'in play testi 27): ölümde ekran zaten yavaşça siyaha iniyor; keşke ekranı onun üstüne kısa
            // bir geçişle siyahı devralır, yazılar sonra siyahın içinden yumuşakça belirir
            else yield return Fade(group.alpha, 1f, blackTakeover);

            SplitWish(wish, out string head, out string rest);
            content.alpha = 0f;
            StartCoroutine(FadeContent(contentFadeIn));
            yield return new WaitForSecondsRealtime(wishDelay / pace);
            Sfx.Play("wish.note");
            if (!string.IsNullOrEmpty(head))
            {
                yield return TypeHead(head);
                yield return HoldHead();
                yield return MoveHead();
            }
            yield return TypeWish(rest);
            yield return Crack();
            yield return new WaitForSecondsRealtime(infoDelay / pace);
            yield return Type(infoText, info, infoCharsPerSecond * pace);
            yield return new WaitForSecondsRealtime(hintDelay / pace);
            hintText.text = Texts.Get("wish.continue"); // M59: herhangi bir tuş
            hintText.enabled = true;
            canContinue = true;
        }

        // Kayıp bir aile üyesiyse oyuncuya en yakın üye (Family sistemi: Father / Mother / Sibling objeleri);
        // oyuncu kayıp anında onun yanındadır. Başka sebeplerde null.
        static Transform LostFamilyMember(LoseReason reason)
        {
            if (reason != LoseReason.FamilyHitByItem && reason != LoseReason.MissingBagItem) return null;
            var player = GameObject.Find("Player");
            // M72 (Mehmet'in play testi 32): oyuncu öldüyse ekran zaten ölüm karartmasıyla siyah; odaklanmak ~4 sn boş
            // siyah ekran bırakıyordu (kardeş aramasında tavan çöküyor). Odak atlanır.
            var controller = player ? player.GetComponent<Player.PlayerController>() : null;
            if (controller && controller.IsDead) return null;
            Transform best = null;
            float bestDistance = float.MaxValue;
            foreach (var name in new[] { "Father", "Mother", "Sibling" })
            {
                var member = GameObject.Find(name);
                if (!member) continue;
                // Karanlıkta saklanan (görünmeyen) üyeye odaklanılmaz
                var family = member.GetComponent<Family.FamilyMember>();
                if (family && family.Hidden) continue;

                float distance = player ? Vector3.Distance(player.transform.position, member.transform.position) : 0f;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = member.transform;
                }
            }
            return best;
        }

        // Üyenin görsel merkezi (yatan görsel pivotun yanında uzanır)
        static Vector3 VisualCenter(Transform member)
        {
            var renderers = member.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return member.position + Vector3.up * 0.4f;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
                if (!(r is ParticleSystemRenderer)) bounds.Encapsulate(r.bounds);
            return bounds.center;
        }

        IEnumerator FocusOn(Transform member)
        {
            var center = VisualCenter(member);
            focusLight = new GameObject("WishFocusLight").AddComponent<Light>();
            focusLight.type = LightType.Point;
            focusLight.color = focusLightColor;
            focusLight.range = focusLightRange;
            focusLight.shadows = LightShadows.None;
            focusLight.intensity = 0f;

            focus.enabled = true;
            float total = (focusInSeconds + focusHold) / pace;
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                if (member) center = VisualCenter(member);
                focusLight.transform.position = center + new Vector3(0f, 0.8f, -1.2f);
                float k = Mathf.Clamp01(t * pace / focusInSeconds);
                k = k * k * (3f - 2f * k);
                focusLight.intensity = focusLightIntensity * k;
                focus.color = new Color(0f, 0f, 0f, focusDarkness * k);
                PlaceFocus(center);
                yield return null;
            }
        }

        void PlaceFocus(Vector3 world)
        {
            var cam = Camera.main;
            if (!cam) return;
            var viewport = cam.WorldToViewportPoint(world);
            var canvasRect = (RectTransform)focus.canvas.transform;
            focus.rectTransform.anchoredPosition = new Vector2((viewport.x - 0.5f) * canvasRect.rect.width,
                (viewport.y - 0.5f) * canvasRect.rect.height);
        }

        // Daire karartması kalır, arka plan siyaha gelir
        IEnumerator FadeBackground(float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                background.color = new Color(0f, 0f, 0f, t / duration);
                yield return null;
            }
            background.color = Color.black;
        }

        void EndFocus()
        {
            if (focus) focus.enabled = false;
            if (focusLight) Destroy(focusLight.gameObject);
            focusLight = null;
        }

        // Ortası saydam, kenara doğru yumuşakça kararan daire; dışı siyah. Image çok büyük çizilir ki daire ekranın
        // neresinde olursa olsun ekranın kalanını örtsün.
        Sprite FocusSprite(float imageSize)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "WishFocus",
            };
            float inner = focusRadius / imageSize * size;
            float outer = inner * 1.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, outer, d));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
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
            float interval = 1f / Mathf.Max(1f, headCharsPerSecond * pace);
            for (int i = 1; i <= text.Length; i++)
            {
                headText.text = text.Substring(0, i);
                TypeSound(text[i - 1]);
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
            for (float t = 0f; t < headHold / pace; t += Time.unscaledDeltaTime)
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
            float moveSeconds = headMoveSeconds / pace;
            for (float t = 0f; t < moveSeconds; t += Time.unscaledDeltaTime)
            {
                float k = 1f - (1f - t / moveSeconds) * (1f - t / moveSeconds);
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
            float interval = 1f / Mathf.Max(1f, wishCharsPerSecond * pace);
            for (int i = 1; i <= text.Length; i++)
            {
                wishText.text = text.Substring(0, i);
                char c = text[i - 1];
                TypeSound(c);
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
        // 0 = ilk keşke (küçük, ince), 1 = crackFullAtWish'inci ve sonrası (geniş, kalın, çok dallı)
        float CrackGrowth => Mathf.Clamp01((GameState.FailedAttempts - 1) / (float)Mathf.Max(1, crackFullAtWish - 1));

        IEnumerator Crack()
        {
            float growth = CrackGrowth;
            float canvasWidth = ((RectTransform)wishText.canvas.transform).rect.width;
            float textWidth = Mathf.Clamp(wishText.preferredWidth, 220f, canvasWidth * 0.75f);
            float width = Mathf.Min(textWidth * Mathf.Lerp(crackWidthRange.x, crackWidthRange.y, growth), canvasWidth * 0.92f);
            BuildCrack(width, Mathf.Lerp(crackThicknessRange.x, crackThicknessRange.y, growth), Mathf.Lerp(0.55f, 1.5f, growth), growth);
            Sfx.Play("wish.shatter");
            var shakeHead = headRect.anchoredPosition;
            float seconds = crackSeconds * Mathf.Lerp(0.6f, 1.25f, growth) / pace;
            float shakeStrength = crackShake * Mathf.Lerp(0.35f, 1.3f, growth);
            var flash = Color.Lerp(Color.Lerp(flashColor, Color.black, 0.55f), flashColor * 1.3f, growth);
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = t / seconds;
                ShowCrack(Mathf.CeilToInt(crackCore.Count * k));
                float shake = shakeStrength * (1f - k);
                var offset = Random.insideUnitCircle * shake;
                wishRect.anchoredPosition = wishHome + offset;
                headRect.anchoredPosition = shakeHead + offset * 0.6f;
                background.color = Color.Lerp(flash, Color.black, k);
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

        // Kırık çizgi: crackStep aralıklı, dikeyde jag kadar sapan noktalar; her parça döndürülmüş ince bir Image
        // (çekirdek, arkasında üç kat kalın yarı saydam ışıma). Dallar: büyüdükçe sıklaşır ve uzar. Pikseller tam sayıda.
        // thickness: çekirdek kalınlığı, jagScale: kırıklık, growth: 0 ilk keşke .. 1 en büyük
        void BuildCrack(float width, float thickness, float jagScale, float growth)
        {
            ClearCrack();
            float jag = crackJag * jagScale;
            var points = new List<Vector2>();
            float half = width * 0.5f;
            float y = 0f;
            for (float x = -half; x <= half + 0.01f; x += crackStep)
            {
                points.Add(new Vector2(Mathf.Round(x), Mathf.Round(y)));
                y = Mathf.Clamp(y + Random.Range(-jag, jag), -jag * 1.6f, jag * 1.6f);
            }

            float core = Mathf.Round(thickness);
            float branch = Mathf.Max(1f, Mathf.Round(thickness * 0.65f));
            var segments = new List<(Vector2 a, Vector2 b, float thickness)>();
            for (int i = 0; i + 1 < points.Count; i++) segments.Add((points[i], points[i + 1], core));
            // Dallar: ana çizgiden yukarı ya da aşağı; büyüdükçe daha sık, daha uzun (üç parçaya kadar)
            int minGap = Mathf.RoundToInt(Mathf.Lerp(6f, 2f, growth)), maxGap = Mathf.RoundToInt(Mathf.Lerp(9f, 4f, growth));
            int pieces = growth > 0.6f ? 3 : 2;
            for (int i = 2; i + 2 < points.Count; i += Random.Range(minGap, maxGap + 1))
            {
                var from = points[i];
                float dir = Random.value < 0.5f ? -1f : 1f;
                for (int piece = 0; piece < pieces; piece++)
                {
                    float shrink = 1f - piece * 0.3f;
                    var to = Round(from + new Vector2(Random.Range(-crackStep, crackStep) * shrink,
                        dir * Random.Range(jag * 0.6f, jag * 1.8f) * shrink));
                    segments.Add((from, to, Mathf.Max(1f, branch - piece)));
                    from = to;
                }
            }
            // Ortadan kenara doğru açılsın
            segments.Sort((s1, s2) => Mathf.Abs((s1.a.x + s1.b.x) * 0.5f).CompareTo(Mathf.Abs((s2.a.x + s2.b.x) * 0.5f)));

            var glowColor = new Color(lineColor.r, lineColor.g, lineColor.b, 0.28f);
            foreach (var (a, b, size) in segments)
            {
                crackGlow.Add(Segment(a, b, size * 3f, glowColor));
                crackCore.Add(Segment(a, b, size, lineColor));
            }
            // Işıma çekirdeğin arkasında kalsın
            foreach (var glow in crackGlow) glow.transform.SetAsFirstSibling();
            ShowCrack(0);
        }

        void TypeSound(char c)
        {
            if (typeVolume > 0f && !char.IsWhiteSpace(c) && c != '.') Sfx.Play("wish.type", typeVolume);
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
            Sfx.Play("wish.rewind");
            crackRoot.localScale = Vector3.one;
            hintText.text = Texts.Get("wish.rewind");
            hintText.enabled = true;
            yield return Erase(infoText);
            int crackStart = crackShown;
            int wishLength = wishText.text.Length;
            float interval = 1f / Mathf.Max(1f, rewindCharsPerSecond * pace);
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
            float interval = 1f / Mathf.Max(1f, rewindCharsPerSecond * 2f * pace);
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
            EndFocus();
        }

        IEnumerator FadeContent(float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                content.alpha = k * k * (3f - 2f * k);
                yield return null;
            }
            content.alpha = 1f;
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

        // M59 (Mehmet'in play testi 31): herhangi bir tuş ya da fare tıkı geçer; Esc hariç (duraklatma)
        static bool ContinuePressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame && !keyboard.escapeKey.wasPressedThisFrame)
                return true;
            var mouse = Mouse.current;
            return mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
        }

        void BuildCanvas()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "WishCanvas", sortingOrder);
            group = canvas.GetComponent<CanvasGroup>();

            background = UiBuilder.CreateImage(canvas.transform, "Background", Color.black);
            UiBuilder.Stretch(background.rectTransform, Vector2.zero, Vector2.one);

            // Kayıp anı dairesi: ekranın iki katından büyük, siyah dışı ekranın kalanını örter
            const float focusSize = 2400f;
            focus = UiBuilder.CreateImage(canvas.transform, "Focus", Color.clear);
            focus.sprite = FocusSprite(focusSize);
            focus.rectTransform.anchorMin = focus.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            focus.rectTransform.sizeDelta = Vector2.one * focusSize;
            focus.enabled = false;

            // M52: yazılar ve çatlak tek bir grupta; siyahın üstünde yumuşakça belirir
            var contentRoot = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            contentRoot.SetParent(canvas.transform, false);
            UiBuilder.Stretch(contentRoot, Vector2.zero, Vector2.one);
            content = contentRoot.GetComponent<CanvasGroup>();
            content.interactable = false;
            content.blocksRaycasts = false;

            // Tek başına KEŞKE ekranın ortasında; küçülünce cümlenin üstüne kayar
            headText = UiBuilder.CreateText(contentRoot, "WishHead", font, headFontSize, wishColor);
            UiBuilder.Stretch(headText.rectTransform, new Vector2(0.04f, 0.46f), new Vector2(0.96f, 0.74f));
            headRect = headText.rectTransform;
            headHome = headRect.anchoredPosition;

            wishText = UiBuilder.CreateText(contentRoot, "Wish", font, wishFontSize, wishColor);
            UiBuilder.Stretch(wishText.rectTransform, new Vector2(0.03f, 0.46f), new Vector2(0.97f, 0.7f));
            wishRect = wishText.rectTransform;
            wishHome = wishRect.anchoredPosition;

            crackRoot = new GameObject("CrackRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            crackRoot.SetParent(contentRoot, false);
            crackRoot.anchorMin = crackRoot.anchorMax = new Vector2(0.5f, 0.43f);
            crackRoot.sizeDelta = Vector2.zero;

            infoText = UiBuilder.CreateText(contentRoot, "Info", font, infoFontSize, infoColor);
            UiBuilder.Stretch(infoText.rectTransform, new Vector2(0.15f, 0.22f), new Vector2(0.85f, 0.4f));

            hintText = UiBuilder.CreateText(contentRoot, "Hint", font, infoFontSize, infoColor);
            UiBuilder.Stretch(hintText.rectTransform, new Vector2(0.3f, 0.05f), new Vector2(0.7f, 0.12f));
        }
    }
}
