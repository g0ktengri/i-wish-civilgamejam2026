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
    //   1. Kayıp anında ekran donar ve karakterin olduğu noktadan cam gibi çatlar; parçalar dökülüp siyaha açılır.
    //   2. Siyahın üstünde keşke cümlesi büyük harflerle, her harfte hafif sarsıntıyla yazılır; altında çizgi uzar.
    //   3. Kısa bir sessizlik, sonra bilgi satırı yazılır.
    //   4. F (ya da Space/Enter/tık): yazılar geri sarılarak silinir ("zaman geri sarılıyor"), döngü yöneticisi
    //      zamanı geri sarar. Yeni denemede siyahtan açılır. Canvas'ı kendisi kurar.
    public class WishScreen : MonoBehaviour
    {
        [Header("Kayıp anı: cam kırılması")]
        [Tooltip("Çatlak ekranda asılı kalma süresi (gerçek sn)")]
        [SerializeField] float crackHoldSeconds = 0.55f;
        [Tooltip("Parçaların dökülme süresi")]
        [SerializeField] float shardFallSeconds = 1.3f;
        [Tooltip("Çatlaktan çıkan ışın sayısı")]
        [SerializeField] int crackRays = 11;
        [SerializeField] float crackShake = 6f;
        [SerializeField] Color crackColor = new Color(1f, 1f, 1f, 0.85f);
        [Tooltip("Ekran görüntüsü alınamazsa yavaş kararma süresi")]
        [SerializeField] float fadeToBlack = 1.6f;
        [SerializeField] string playerObjectName = "Player";

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
        RectTransform shatterRoot;
        Image flash;
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

            yield return Shatter();

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

        // Ekran donar, karakterin olduğu noktadan çatlar (beyaz çatlak çizgileri, sarsıntı, kısa beyaz parlama),
        // sonra cam parçaları dönerek dökülür ve arkadaki siyah görünür.
        IEnumerator Shatter()
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = null;
            try { shot = ScreenCapture.CaptureScreenshotAsTexture(); }
            catch (System.Exception) { shot = null; }

            group.alpha = 1f;
            background.enabled = true;
            if (shot == null)
            {
                yield return Fade(0f, 1f, fadeToBlack);
                yield break;
            }

            float previousScale = Time.timeScale;
            Time.timeScale = 0f; // donma
            shot.filterMode = FilterMode.Point;
            shot.wrapMode = TextureWrapMode.Clamp;

            var canvasRect = (RectTransform)shatterRoot.parent;
            Vector2 size = canvasRect.rect.size;
            Vector2 impact = ImpactPoint(size);
            var shards = BuildShards(shot, size, impact, out var cracks);

            // Çatlama anı: sarsıntı ve beyaz parlama
            for (float t = 0f; t < crackHoldSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / crackHoldSeconds;
                shatterRoot.anchoredPosition = Random.insideUnitCircle * crackShake * Mathf.Max(0f, 1f - k * 3f);
                flash.color = new Color(1f, 1f, 1f, Mathf.Max(0f, 0.55f - k * 4f));
                yield return null;
            }
            shatterRoot.anchoredPosition = Vector2.zero;
            flash.color = Color.clear;
            foreach (var crack in cracks) crack.gameObject.SetActive(false);

            // Dökülme: parçalar çarpma noktasından dışa ve aşağı savrulur, döner, solar
            var velocity = new Vector2[shards.Count];
            var spin = new float[shards.Count];
            for (int i = 0; i < shards.Count; i++)
            {
                Vector2 away = shards[i].rectTransform.anchoredPosition - impact;
                velocity[i] = away.normalized * Random.Range(40f, 160f) + new Vector2(0f, Random.Range(20f, 90f));
                spin[i] = Random.Range(-120f, 120f);
            }
            for (float t = 0f; t < shardFallSeconds; t += Time.unscaledDeltaTime)
            {
                float dt = Time.unscaledDeltaTime;
                float k = t / shardFallSeconds;
                for (int i = 0; i < shards.Count; i++)
                {
                    var rect = shards[i].rectTransform;
                    velocity[i] += new Vector2(0f, -900f) * dt;
                    rect.anchoredPosition += velocity[i] * dt;
                    rect.localRotation = Quaternion.Euler(0f, 0f, rect.localEulerAngles.z + spin[i] * dt);
                    var c = shards[i].color;
                    shards[i].color = new Color(c.r, c.g, c.b, Mathf.Clamp01(1.4f - k * 1.6f));
                }
                yield return null;
            }

            foreach (Transform child in shatterRoot) Destroy(child.gameObject);
            Destroy(shot);
            Time.timeScale = previousScale > 0f ? 1f : previousScale;
        }

        // Çarpma noktası: karakterin ekrandaki yeri (yoksa ekranın ortası), canvas merkezine göre
        Vector2 ImpactPoint(Vector2 size)
        {
            var cam = Camera.main;
            var player = GameObject.Find(playerObjectName);
            if (cam == null || player == null) return Vector2.zero;
            Vector3 viewport = cam.WorldToViewportPoint(player.transform.position + Vector3.up * 1.1f);
            return new Vector2((viewport.x - 0.5f) * size.x, (viewport.y - 0.5f) * size.y);
        }

        // Çarpma noktasından ışınlar ve halkalarla bölünmüş cam parçaları + çatlak çizgileri
        List<ShardGraphic> BuildShards(Texture shot, Vector2 size, Vector2 impact, out List<RectTransform> cracks)
        {
            int rays = Mathf.Max(5, crackRays);
            var angles = new float[rays];
            for (int i = 0; i < rays; i++)
                angles[i] = (i + Random.Range(-0.35f, 0.35f)) * Mathf.PI * 2f / rays;
            float far = size.magnitude * 1.2f;
            float[] rings = { Random.Range(28f, 45f), Random.Range(90f, 130f), Random.Range(190f, 260f), far };

            // ring + 1 halka (0 = merkez) x ray köşe noktası, halkalarda hafif sapma
            var pts = new Vector2[rings.Length + 1, rays];
            for (int r = 0; r <= rings.Length; r++)
            for (int i = 0; i < rays; i++)
            {
                if (r == 0) { pts[r, i] = impact; continue; }
                float radius = rings[r - 1] * (r == rings.Length ? 1f : Random.Range(0.85f, 1.15f));
                pts[r, i] = impact + new Vector2(Mathf.Cos(angles[i]), Mathf.Sin(angles[i])) * radius;
            }

            var shards = new List<ShardGraphic>();
            for (int r = 0; r < rings.Length; r++)
            for (int i = 0; i < rays; i++)
            {
                int j = (i + 1) % rays;
                var polygon = r == 0
                    ? new[] { pts[0, i], pts[1, i], pts[1, j] }
                    : new[] { pts[r, i], pts[r + 1, i], pts[r + 1, j], pts[r, j] };
                shards.Add(CreateShard(shot, size, polygon));
            }

            cracks = new List<RectTransform>();
            for (int i = 0; i < rays; i++)
            {
                for (int r = 0; r < rings.Length - 1; r++)
                {
                    cracks.Add(CreateCrack(pts[r, i], pts[r + 1, i], r == 0 ? 2f : 1.5f));
                    if (r > 0) cracks.Add(CreateCrack(pts[r, i], pts[r, (i + 1) % rays], 1f));
                }
            }
            return shards;
        }

        ShardGraphic CreateShard(Texture shot, Vector2 size, Vector2[] polygon)
        {
            Vector2 centroid = Vector2.zero;
            foreach (var point in polygon) centroid += point;
            centroid /= polygon.Length;

            var shard = UiBuilder.CreateChild<ShardGraphic>(shatterRoot, "Shard");
            var rect = shard.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = centroid;

            var local = new Vector2[polygon.Length];
            var uv = new Vector2[polygon.Length];
            for (int i = 0; i < polygon.Length; i++)
            {
                local[i] = polygon[i] - centroid;
                uv[i] = new Vector2(polygon[i].x / size.x + 0.5f, polygon[i].y / size.y + 0.5f);
            }
            shard.color = Color.white;
            shard.Set(shot, local, uv);
            return shard;
        }

        RectTransform CreateCrack(Vector2 from, Vector2 to, float width)
        {
            var image = UiBuilder.CreateImage(shatterRoot, "Crack", crackColor);
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            Vector2 delta = to - from;
            rect.sizeDelta = new Vector2(delta.magnitude, width);
            rect.anchoredPosition = from;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            return rect;
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

            // Cam kırılması parçaları ve çatlakları bunun altında (canvas merkezine göre)
            shatterRoot = new GameObject("Shatter", typeof(RectTransform)).GetComponent<RectTransform>();
            shatterRoot.SetParent(canvas.transform, false);
            shatterRoot.anchorMin = shatterRoot.anchorMax = new Vector2(0.5f, 0.5f);
            shatterRoot.sizeDelta = Vector2.zero;

            flash = UiBuilder.CreateImage(canvas.transform, "Flash", Color.clear);
            UiBuilder.Stretch(flash.rectTransform, Vector2.zero, Vector2.one);

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
