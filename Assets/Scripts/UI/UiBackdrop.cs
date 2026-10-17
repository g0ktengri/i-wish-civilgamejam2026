using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Menü arka planı (Berke'nin arayüz turu: menüler temaya uymuyordu): arkadaki evin bulanık görüntüsü (kamera bir kez
    // küçük dokuya çizilir, küçültülür, bulanık büyütülür), ruh fazının soğuk mavisinde, üstünde karartma, kenarlarda
    // koyu vinyet; istenirse yavaşça düşen pixel toz zerreleri (deprem havası). Ana menü ve duraklatma kullanır.
    // Arayüz turu 2 (menü çok sade duruyordu): istenirse tavandan sarkan, hafifçe sallanan pixel lamba ve sıcak ışık
    // konisi (soğuk evde sıcak ışık); Quake() ile kısa sarsıntı: görüntü titrer, lamba savrulur ve titreşir, tavandan
    // pixel moloz düşer, toz hızlanır.
    public class UiBackdrop : MonoBehaviour
    {
        const int DustCount = 28;
        const int DebrisCount = 12;

        RawImage snapshot;
        Image dim;
        RenderTexture big;
        RenderTexture small;
        RectTransform canvasRect;
        readonly List<(RectTransform rect, float speed, float phase, float sway)> dust = new List<(RectTransform, float, float, float)>();
        Color tint;
        RectTransform root;
        RectTransform lamp;
        Image cone;
        Image bulb;
        float coneAlpha;
        float swing = 2.5f;
        float quakeTime = -10f;
        float quakeStrength;
        readonly List<(RectTransform rect, Vector2 velocity)> debris = new List<(RectTransform, Vector2)>();
        static Sprite vignetteSprite;
        static Sprite lampSprite;
        static Sprite coneSprite;

        // tint: oda görüntüsünün rengi; darkness: üstündeki karartma (0-1); lampX: lambanın yatay yeri (0-1, < 0 lamba yok)
        public static UiBackdrop Create(Transform canvas, Color tint, float darkness, bool withDust, float lampX = -1f)
        {
            var holder = new GameObject("Backdrop", typeof(RectTransform)).GetComponent<RectTransform>();
            holder.SetParent(canvas, false);
            UiBuilder.Stretch(holder, Vector2.zero, Vector2.one);
            var backdrop = holder.gameObject.AddComponent<UiBackdrop>();
            backdrop.canvasRect = (RectTransform)canvas;
            backdrop.tint = tint;

            // Sarsıntıda titreyen iç kök (kenarlar görünmesin diye biraz taşar)
            var root = new GameObject("Shake", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(holder, false);
            UiBuilder.Stretch(root, Vector2.zero, Vector2.one);
            root.offsetMin = new Vector2(-8f, -8f);
            root.offsetMax = new Vector2(8f, 8f);
            backdrop.root = root;

            var floor = UiBuilder.CreateImage(root, "Base", new Color(0.02f, 0.03f, 0.06f));
            UiBuilder.Stretch(floor.rectTransform, Vector2.zero, Vector2.one);
            backdrop.snapshot = UiBuilder.CreateChild<RawImage>(root, "Snapshot");
            backdrop.snapshot.raycastTarget = false;
            UiBuilder.Stretch(backdrop.snapshot.rectTransform, Vector2.zero, Vector2.one);
            backdrop.snapshot.enabled = false;

            backdrop.dim = UiBuilder.CreateImage(root, "Dim", new Color(0.01f, 0.02f, 0.05f, darkness));
            UiBuilder.Stretch(backdrop.dim.rectTransform, Vector2.zero, Vector2.one);

            if (lampX >= 0f) backdrop.BuildLamp(root, lampX);

            var vignette = UiBuilder.CreateImage(root, "Vignette", Color.white);
            vignette.sprite = VignetteSprite;
            UiBuilder.Stretch(vignette.rectTransform, Vector2.zero, Vector2.one);

            if (withDust) backdrop.BuildDust(root);
            backdrop.BuildDebris(root);
            return backdrop;
        }

        /// <summary>Oda görüntüsü taşıyan dokuyu dışarıdan ver (önizleme: kamera yok)</summary>
        public void SetTexture(Texture texture)
        {
            snapshot.texture = texture;
            snapshot.color = tint;
            snapshot.enabled = texture;
        }

        // Ana kamerayı bir kez çizer (oyun durmuşken de çalışır), bulanık görüntüyü gösterir
        public void Capture()
        {
            var cam = Camera.main;
            if (!cam)
            {
                snapshot.enabled = false;
                return;
            }
            if (!big)
            {
                big = new RenderTexture(384, 216, 24) { name = "MenuSnapshot" };
                small = new RenderTexture(96, 54, 0) { name = "MenuSnapshotBlur", filterMode = FilterMode.Bilinear };
            }
            var previous = cam.targetTexture;
            cam.targetTexture = big;
            cam.Render();
            cam.targetTexture = previous;
            Graphics.Blit(big, small);
            SetTexture(small);
        }

        /// <summary>Karartmanın üstüne, lambanın ve ışığının altına bir katman koy (ana menünün sol geçişi)</summary>
        public void InsertBelowLamp(Transform layer)
        {
            layer.SetParent(root, false);
            layer.SetSiblingIndex(lamp ? lamp.GetSiblingIndex() : dim.transform.GetSiblingIndex() + 1);
        }

        /// <summary>Kısa sarsıntı (0-1): görüntü titrer, lamba savrulur, moloz düşer, toz hızlanır</summary>
        public void Quake(float strength)
        {
            quakeTime = Time.unscaledTime;
            quakeStrength = Mathf.Clamp01(strength);
            swing = Mathf.Max(swing, 4f + 10f * quakeStrength);
            int count = Mathf.RoundToInt(DebrisCount * (0.4f + 0.6f * quakeStrength));
            for (int i = 0; i < debris.Count; i++)
            {
                var (rect, _) = debris[i];
                bool active = i < count;
                rect.gameObject.SetActive(active);
                if (!active) continue;
                rect.anchorMin = rect.anchorMax = new Vector2(Random.Range(0.05f, 0.95f), 1f);
                rect.anchoredPosition = new Vector2(0f, Random.Range(0f, 60f));
                debris[i] = (rect, new Vector2(Random.Range(-20f, 20f), -Random.Range(10f, 60f)));
            }
        }

        void BuildDust(RectTransform parent)
        {
            var random = new System.Random(41);
            for (int i = 0; i < DustCount; i++)
            {
                float size = random.Next(2, 4) * 2f; // 4-6 px: oyunun piksel ölçüsü
                var mote = UiBuilder.CreateImage(parent, "Dust", new Color(0.96f, 0.9f, 0.78f, 0.12f + (float)random.NextDouble() * 0.22f));
                var rect = mote.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
                rect.sizeDelta = Vector2.one * size;
                dust.Add((rect, 6f + (float)random.NextDouble() * 16f, (float)random.NextDouble() * 6.28f, 3f + (float)random.NextDouble() * 8f));
            }
        }

        // Tavandan ip, pixel abajur, ampul ve aşağı açılan sıcak ışık konisi; hepsi ipin tepesinden döner
        void BuildLamp(RectTransform parent, float x)
        {
            lamp = new GameObject("Lamp", typeof(RectTransform)).GetComponent<RectTransform>();
            lamp.SetParent(parent, false);
            lamp.anchorMin = lamp.anchorMax = new Vector2(x, 1f);
            lamp.pivot = new Vector2(0.5f, 1f);
            lamp.sizeDelta = Vector2.zero;
            lamp.anchoredPosition = new Vector2(0f, -8f);

            const float rope = 30f;
            cone = UiBuilder.CreateImage(lamp, "LightCone", new Color(1f, 0.82f, 0.52f, 0.2f));
            cone.sprite = ConeSprite;
            var coneRect = cone.rectTransform;
            coneRect.anchorMin = coneRect.anchorMax = new Vector2(0.5f, 1f);
            coneRect.pivot = new Vector2(0.5f, 1f);
            coneRect.sizeDelta = new Vector2(960f, 520f);
            coneRect.anchoredPosition = new Vector2(0f, -rope - 26f);
            coneAlpha = cone.color.a;

            var cord = UiBuilder.CreateImage(lamp, "Cord", new Color(0.06f, 0.05f, 0.05f));
            cord.rectTransform.anchorMin = cord.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            cord.rectTransform.pivot = new Vector2(0.5f, 1f);
            cord.rectTransform.sizeDelta = new Vector2(2f, rope);

            bulb = UiBuilder.CreateImage(lamp, "Bulb", new Color(1f, 0.92f, 0.6f));
            bulb.rectTransform.anchorMin = bulb.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            bulb.rectTransform.pivot = new Vector2(0.5f, 1f);
            bulb.rectTransform.sizeDelta = new Vector2(14f, 8f);
            bulb.rectTransform.anchoredPosition = new Vector2(0f, -rope - 22f);

            var shade = UiBuilder.CreateImage(lamp, "Shade", Color.white);
            shade.sprite = LampSprite;
            shade.rectTransform.anchorMin = shade.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            shade.rectTransform.pivot = new Vector2(0.5f, 1f);
            shade.rectTransform.sizeDelta = new Vector2(100f, 28f);
            shade.rectTransform.anchoredPosition = new Vector2(0f, -rope);
        }

        // Sarsıntıda tavandan düşen küçük koyu taşlar (kapalıyken gizli)
        void BuildDebris(RectTransform parent)
        {
            var random = new System.Random(19);
            for (int i = 0; i < DebrisCount; i++)
            {
                float size = random.Next(2, 5) * 2f;
                float shade = 0.18f + (float)random.NextDouble() * 0.15f;
                var stone = UiBuilder.CreateImage(parent, "Debris", new Color(shade, shade * 0.95f, shade * 0.9f));
                stone.rectTransform.sizeDelta = new Vector2(size, size * (random.Next(0, 2) == 0 ? 1f : 0.5f));
                var shadow = stone.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
                shadow.effectDistance = new Vector2(1f, -1f);
                stone.gameObject.SetActive(false);
                debris.Add((stone.rectTransform, Vector2.zero));
            }
        }

        // Hepsi oyun dururken de (timeScale 0, gerçek zaman) çalışır: toz yavaşça düşer ve sallanır, alttan çıkınca
        // üste döner; lamba sönümlü sarkaç gibi sallanır; sarsıntıda görüntü titrer, moloz yerçekimiyle düşer
        void Update()
        {
            if (!canvasRect) return;
            float height = Mathf.Max(1f, canvasRect.rect.height);
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f), t = Time.unscaledTime;
            float shaking = Mathf.Clamp01(1f - (t - quakeTime) / 0.6f) * quakeStrength;

            float dustSpeed = 1f + 5f * shaking;
            foreach (var (rect, speed, phase, sway) in dust)
            {
                var anchor = rect.anchorMin;
                anchor.y -= speed * dustSpeed * dt / height;
                if (anchor.y < -0.02f) anchor.y = 1.02f;
                rect.anchorMin = rect.anchorMax = anchor;
                rect.anchoredPosition = new Vector2(Mathf.Sin(t * 0.7f + phase) * sway, 0f);
            }

            root.anchoredPosition = shaking > 0f
                ? new Vector2(Mathf.Round(Random.Range(-4f, 4f) * shaking), Mathf.Round(Random.Range(-3f, 3f) * shaking))
                : Vector2.zero;

            if (lamp)
            {
                swing = Mathf.MoveTowards(swing, 2.5f, dt * 2.5f);
                lamp.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.6f) * swing);
                // Sarsıntıda ve ara ara ışık titrer (elektrik oynuyor)
                bool flicker = shaking > 0.05f
                    ? Random.value < 0.35f
                    : Mathf.Repeat(t, 7.3f) < 0.12f && Mathf.Repeat(t * 31f, 1f) < 0.5f;
                var color = cone.color;
                color.a = flicker ? coneAlpha * 0.35f : coneAlpha;
                cone.color = color;
                bulb.color = flicker ? new Color(0.5f, 0.42f, 0.3f) : new Color(1f, 0.92f, 0.6f);
            }

            for (int i = 0; i < debris.Count; i++)
            {
                var (rect, velocity) = debris[i];
                if (!rect.gameObject.activeSelf) continue;
                velocity.y -= 900f * dt;
                rect.anchoredPosition += velocity * dt;
                debris[i] = (rect, velocity);
                if (rect.anchoredPosition.y < -height - 20f) rect.gameObject.SetActive(false);
            }
        }

        void OnDestroy()
        {
            if (big) big.Release();
            if (small) small.Release();
        }

        // 25 x 7 pixel abajur (4 kat büyütülür): üstü dar, altı geniş yamuk; koyu kontur, içi koyu yeşil-gri metal, sol üstte açık
        // parıltı, alt kenarda sıcak ışık şeridi
        static Sprite LampSprite
        {
            get
            {
                if (lampSprite) return lampSprite;
                const int w = 25, h = 7;
                bool Inside(int x, int y)
                {
                    float half = Mathf.Lerp(12.5f, 5f, y / (h - 1f)); // y = 0 altta (geniş)
                    return y >= 0 && y < h && Mathf.Abs(x + 0.5f - w * 0.5f) <= half;
                }
                var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "UiMenuLamp",
                };
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = Color.clear;
                    if (Inside(x, y))
                    {
                        bool edge = !Inside(x - 1, y) || !Inside(x + 1, y) || y == h - 1;
                        if (edge) c = new Color(0.05f, 0.05f, 0.05f);
                        else if (y == 0) c = new Color(1f, 0.85f, 0.5f);
                        else if (y >= h - 3 && x < w / 2) c = new Color(0.42f, 0.5f, 0.44f);
                        else c = new Color(0.22f, 0.3f, 0.27f);
                    }
                    texture.SetPixel(x, y, c);
                }
                texture.Apply();
                lampSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 1f), 100f);
                return lampSprite;
            }
        }

        // 64 x 64 ışık konisi: tepede dar, aşağı genişleyen; merkezden kenara ve yukarıdan aşağı sönen beyaz (yumuşak)
        static Sprite ConeSprite
        {
            get
            {
                if (coneSprite) return coneSprite;
                const int n = 64;
                var texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "UiMenuLightCone",
                };
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float down = 1f - (y + 0.5f) / n; // 0 tepede, 1 altta
                    float half = Mathf.Lerp(0.055f, 0.5f, down);
                    float across = Mathf.Abs((x + 0.5f) / n - 0.5f) / half;
                    float a = Mathf.Clamp01(1f - across) * Mathf.Lerp(1f, 0.15f, down);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, a)));
                }
                texture.Apply();
                coneSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 1f), 100f);
                return coneSprite;
            }
        }

        // 64 x 64: ortası saydam, kenarlara doğru koyulaşan siyah (çift doğrusal, yumuşak)
        static Sprite VignetteSprite
        {
            get
            {
                if (vignetteSprite) return vignetteSprite;
                const int n = 64;
                var texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "UiVignette",
                };
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float d = Mathf.Sqrt(dx * dx * 1.2f + dy * dy * 1.6f) * 2f;
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 0.85f, Mathf.InverseLerp(0.45f, 1.15f, d))));
                }
                texture.Apply();
                vignetteSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return vignetteSprite;
            }
        }
    }
}
