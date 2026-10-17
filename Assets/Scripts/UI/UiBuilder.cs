using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Prefab'sız UI kurulumu için ortak yardımcılar (GDD referans çözünürlüğü 960 x 540).
    // Görünüş UiStyle'dan gelir (font, balon, panel); görsel yoksa düz renkli kenarlıklı kutu.
    public static class UiBuilder
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(960, 540);
        const float CanvasPixelsPerUnit = 100f; // CanvasScaler varsayılanı
        const float FallbackBorder = 2f;

        public static readonly Color BubbleFill = new Color(1f, 1f, 1f, 0.94f);
        public static readonly Color BubbleBorder = new Color(0.08f, 0.08f, 0.1f);
        public static readonly Color BubbleText = new Color(0.08f, 0.08f, 0.1f);
        public static readonly Color PanelFill = new Color(0.03f, 0.04f, 0.07f, 0.75f);
        public static readonly Color PanelBorder = new Color(1f, 1f, 1f, 0.35f);

        public static Font DefaultFont
        {
            get
            {
                var style = UiStyle.Current;
                return style && style.font ? style.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }

        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            var canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(parent, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            var group = canvasObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            return canvas;
        }

        public static Text CreateText(Transform parent, string name, Font font, int size, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var text = CreateChild<Text>(parent, name);
            text.font = font ? font : DefaultFont;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Image CreateImage(Transform parent, string name, Color color)
        {
            var image = CreateChild<Image>(parent, name);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Sprite circle;
        static Sprite pixelBubble;
        static Sprite pixelTail;

        // Görseli yokken konuşma balonu: 16 x 16 pixel art, yuvarlak köşeli, 1 px koyu kontur, 9-slice (kenar 5)
        public static Sprite PixelBubbleSprite
        {
            get
            {
                if (pixelBubble) return pixelBubble;
                const int size = 16;
                var texture = PixelTexture(size, size, "UiPixelBubble");
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Köşelerden 2 px kırpılır (pixel art yuvarlak köşe)
                    int dx = Mathf.Min(x, size - 1 - x), dy = Mathf.Min(y, size - 1 - y);
                    bool outside = dx + dy < 2;
                    bool edge = !outside && (dx == 0 || dy == 0 || dx + dy == 2);
                    bool shade = !outside && !edge && dy == 1 && y < size / 2; // altta 1 px gölge
                    texture.SetPixel(x, y, outside ? Color.clear : edge ? BubbleBorder : shade ? new Color(0.82f, 0.82f, 0.86f) : Color.white);
                }
                texture.Apply();
                pixelBubble = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f, 0,
                    SpriteMeshType.FullRect, new Vector4(5, 5, 5, 5));
                return pixelBubble;
            }
        }

        // Görseli yokken balon kuyruğu: 10 x 7, ucu aşağıda, konturlu
        public static Sprite PixelTailSprite
        {
            get
            {
                if (pixelTail) return pixelTail;
                const int w = 10, h = 7;
                var texture = PixelTexture(w, h, "UiPixelTail");
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Üst kenar geniş, aşağı doğru daralır (y = 0 uç)
                    float half = (y + 1) * (w * 0.5f) / h;
                    float d = Mathf.Abs(x + 0.5f - w * 0.5f);
                    bool inside = d <= half;
                    bool edge = inside && (d > half - 1.2f || y == 0);
                    texture.SetPixel(x, y, !inside ? Color.clear : edge && y < h - 1 ? BubbleBorder : Color.white);
                }
                texture.Apply();
                pixelTail = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 1f), 64f);
                return pixelTail;
            }
        }

        static Texture2D PixelTexture(int w, int h, string name) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
            };

        // Görseli olmayan yuvarlak parçalar için kodla üretilen piksel daire (32 x 32, beyaz)
        public static Sprite CircleSprite
        {
            get
            {
                if (circle) return circle;
                const int size = 32;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "UiCircle",
                };
                float radius = size * 0.5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius, dy = y + 0.5f - radius;
                    texture.SetPixel(x, y, dx * dx + dy * dy <= radius * radius ? Color.white : Color.clear);
                }
                texture.Apply();
                circle = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
                return circle;
            }
        }

        // Sprite'ı pixel art için ayarlar: kenarı varsa 9-slice, 1 sprite pikseli = 1 referans pikseli
        public static void ApplySprite(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            if (!sprite) return;
            image.color = Color.white;
            image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = sprite.pixelsPerUnit / CanvasPixelsPerUnit;
        }

        // ------------------------------------------------------------------ HUD (M24: tek stil)
        // Mehmet'in play testi 12: "UI çok kötü". Bütün HUD'lar aynı pixel panel, tuş kapağı ve renkleri kullanır.
        // Sanatçı görseli (hud_ipucu / hud_tus) yoksa kodla üretilen pixel sprite'lar; renkler UiStyle'da.

        const float HudPixel = 2f; // 1 sprite pikseli = 2 referans pikseli (dünyanın 360p pikseline yakın)
        static readonly Color HudFill = new Color(0.11f, 0.09f, 0.085f, 0.92f);
        static readonly Color HudOutline = new Color(0.03f, 0.025f, 0.03f, 1f);
        static readonly Color HudHighlight = new Color(0.86f, 0.78f, 0.62f, 1f);
        static readonly Color HudEdge = new Color(0.5f, 0.43f, 0.34f, 1f);

        public static Color HudText => UiStyle.Current ? UiStyle.Current.hudText : new Color(0.96f, 0.92f, 0.84f);
        public static Color HudAccent => UiStyle.Current ? UiStyle.Current.hudAccent : new Color(1f, 0.82f, 0.4f);
        public static Color HudMuted => UiStyle.Current ? UiStyle.Current.hudMuted : new Color(0.74f, 0.69f, 0.6f);
        public static Color HudWarning => UiStyle.Current ? UiStyle.Current.hudWarning : new Color(1f, 0.42f, 0.36f);
        public static Color KeyText => UiStyle.Current ? UiStyle.Current.keyText : new Color(0.13f, 0.11f, 0.1f);
        public static int HudFontSize => UiStyle.Current ? UiStyle.Current.hudFontSize : 22;
        public static float HudMargin => UiStyle.Current ? UiStyle.Current.hudMargin : 20f;
        // M33: HUD yazıları okunaklı balon fontuyla (DepremBalon); yoksa ortak font
        public static Font HudFont => UiStyle.Current && UiStyle.Current.hudFont ? UiStyle.Current.hudFont : DefaultFont;

        // M33: HUD yazısına 1 px koyu kontur (krem yazı her zeminde okunsun)
        public static Text Outlined(Text text)
        {
            var outline = text.GetComponent<Outline>();
            if (!outline) outline = text.gameObject.AddComponent<Outline>(); // Unity null: ?? kullanılmaz
            outline.effectColor = new Color(0.04f, 0.03f, 0.03f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        static Sprite hudPanel;
        static Sprite keyCap;

        // 12 x 12, 9-slice (kenar 3): köşeleri kırpık 1 px koyu kontur, içte üst-solda açık, alt-sağda koyu kenar,
        // sıcak koyu dolgu
        public static Sprite HudPanelSprite
        {
            get
            {
                if (hudPanel) return hudPanel;
                const int size = 12;
                var texture = PixelTexture(size, size, "UiHudPanel");
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Min(x, size - 1 - x), dy = Mathf.Min(y, size - 1 - y);
                    Color c;
                    if (dx == 0 && dy == 0) c = Color.clear;
                    else if (dx == 0 || dy == 0) c = HudOutline;
                    else if (dx == 1 || dy == 1) c = y == size - 2 || x == 1 ? HudHighlight : HudEdge;
                    else c = HudFill;
                    texture.SetPixel(x, y, c);
                }
                texture.Apply();
                hudPanel = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f, 0,
                    SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3));
                return hudPanel;
            }
        }

        // 10 x 12, 9-slice: krem tuş yüzü, üstte ışık, altta 2 px koyu kalınlık (basılabilir tuş), koyu kontur
        public static Sprite KeyCapSprite
        {
            get
            {
                if (keyCap) return keyCap;
                const int w = 10, h = 12;
                var texture = PixelTexture(w, h, "UiKeyCap");
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int dx = Mathf.Min(x, w - 1 - x), dy = Mathf.Min(y, h - 1 - y);
                    Color c;
                    if (dx == 0 && dy == 0) c = Color.clear;
                    else if (dx == 0 || dy == 0) c = HudOutline;
                    else if (y <= 2) c = new Color(0.58f, 0.52f, 0.43f);
                    else if (y == h - 2) c = new Color(1f, 0.98f, 0.92f);
                    else c = new Color(0.9f, 0.86f, 0.76f);
                    texture.SetPixel(x, y, c);
                }
                texture.Apply();
                keyCap = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64f, 0,
                    SpriteMeshType.FullRect, new Vector4(3, 4, 3, 3));
                return keyCap;
            }
        }

        // HUD sprite'ı: 9-slice, 1 sprite pikseli = HudPixel referans pikseli (kenarlar her ekranda aynı kalınlıkta)
        public static void ApplyHudSprite(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            if (!sprite) return;
            image.color = Color.white;
            image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = CanvasPixelsPerUnit / (sprite.pixelsPerUnit * HudPixel);
        }

        // HUD paneli (tuş ipucu, sayaç, hedef, çanta; Mehmet'in çubukları da): UiStyle.panel (hud_ipucu) ya da
        // kodla üretilen pixel panel, sağ altta yumuşak gölge. Boyutu ve yerini çağıran verir; yazı bunun çocuğu olur.
        public static Image CreatePanel(Transform parent, string name)
        {
            var style = UiStyle.Current;
            var image = CreateImage(parent, name, Color.white);
            ApplyHudSprite(image, style && style.panel ? style.panel : HudPanelSprite);
            var shadow = image.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(3f, -3f);
            return image;
        }

        // Tuş kapağı: "F", "C", "Esc". Kök sol-orta pivotlu; boyu yazı boyuna göre (M33: 22 punto → 34 px), eni yazıya
        // göre ama en az boyu kadar (kare kapak), harf ortalı
        public static RectTransform CreateKeyCap(Transform parent, string key, Font font, int fontSize)
        {
            var style = UiStyle.Current;
            var cap = CreateImage(parent, "Key_" + key, Color.white);
            ApplyHudSprite(cap, style && style.keyCap ? style.keyCap : KeyCapSprite);
            var label = CreateText(cap.rectTransform, "Text", font, fontSize, KeyText);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            // Yüz alttaki kalınlığın üstünde; fontun üst boşluğu yüzünden harf biraz aşağı alınır
            float height = Mathf.Round(fontSize * 1.55f);
            float lift = Mathf.Round(height * 0.09f);
            label.rectTransform.offsetMin = new Vector2(0f, lift);
            label.rectTransform.offsetMax = new Vector2(0f, -lift);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = key;
            var rect = cap.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(Mathf.Max(height, label.preferredWidth + height * 0.5f), height);
            return rect;
        }

        public static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);

        // Pixel çatlak (menü başlığı, duvar çatlakları): parent'ın ortasından, start yönünde length uzunluğunda kırık çizgi.
        // thickness çekirdek, glowAlpha > 0 ise arkasında üç kat kalın yarı saydam ışıma. Seed aynıysa aynı çatlak.
        public static void CreateCrack(RectTransform parent, Vector2 start, Vector2 direction, float length, int seed,
            Color color, float thickness = 2f, float glowAlpha = 0f, float step = 14f, float jag = 3f)
        {
            var random = new System.Random(seed);
            var dir = direction.normalized;
            var normal = new Vector2(-dir.y, dir.x);
            var points = new System.Collections.Generic.List<Vector2>();
            float side = 0f;
            for (float d = 0f; d <= length + 0.01f; d += step)
            {
                var p = start + dir * d + normal * side;
                points.Add(new Vector2(Mathf.Round(p.x), Mathf.Round(p.y)));
                side = Mathf.Clamp(side + (float)(random.NextDouble() * 2 - 1) * jag, -jag * 1.5f, jag * 1.5f);
            }
            for (int pass = glowAlpha > 0f ? 0 : 1; pass < 2; pass++)
                for (int i = 0; i + 1 < points.Count; i++)
                {
                    var a = points[i];
                    var b = points[i + 1];
                    var segment = CreateImage(parent, "Crack", pass == 0 ? new Color(color.r, color.g, color.b, glowAlpha) : color);
                    var rect = segment.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0f, 0.5f);
                    float size = pass == 0 ? thickness * 3f : thickness;
                    var delta = b - a;
                    rect.sizeDelta = new Vector2(delta.magnitude + size * 0.5f, size);
                    rect.anchoredPosition = a;
                    rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                }
        }

        static readonly System.Collections.Generic.Dictionary<Deprem.Core.BagItemId, Sprite> bagIcons =
            new System.Collections.Generic.Dictionary<Deprem.Core.BagItemId, Sprite>();

        // Çanta malzemesi ikonu görseli yokken (ikon_*.png): 16 x 16 pixel art, koyu konturlu. HUD, minigame ve
        // dünyada düz renkli kare yerine. Sanatçı ikonu gelince BagSystem onu verir.
        public static Sprite PixelBagIcon(Deprem.Core.BagItemId id)
        {
            if (bagIcons.TryGetValue(id, out var cached) && cached) return cached;
            const int size = 16;
            var texture = PixelTexture(size, size, "UiBagIcon_" + id);
            texture.SetPixels(new Color[size * size]); // saydam
            var outline = HudOutline;
            void Fill(int x0, int y0, int x1, int y1, Color c)
            {
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++) texture.SetPixel(x, y, c);
            }
            void Box(int x0, int y0, int x1, int y1, Color c)
            {
                Fill(x0, y0, x1, y1, outline);
                Fill(x0 + 1, y0 + 1, x1 - 1, y1 - 1, c);
            }
            switch (id)
            {
                case Deprem.Core.BagItemId.FirstAidKit: // kırmızı çanta, sap, beyaz haç
                {
                    var red = new Color(0.8f, 0.2f, 0.17f);
                    Box(5, 10, 10, 14, Color.clear);
                    Fill(6, 11, 9, 13, Color.clear);
                    Box(1, 2, 14, 11, red);
                    Fill(2, 3, 13, 3, new Color(0.58f, 0.13f, 0.12f));
                    Fill(2, 10, 13, 10, new Color(0.92f, 0.4f, 0.34f));
                    Fill(7, 4, 8, 9, Color.white);
                    Fill(5, 6, 10, 7, Color.white);
                    break;
                }
                case Deprem.Core.BagItemId.Flashlight: // sarı gövde, gri kafa, parlayan cam (sağa bakar)
                {
                    Box(1, 5, 10, 10, new Color(0.96f, 0.76f, 0.22f));
                    Fill(2, 9, 9, 9, new Color(1f, 0.9f, 0.5f));
                    Fill(5, 10, 6, 11, outline); // düğme
                    Box(10, 3, 14, 12, new Color(0.62f, 0.62f, 0.64f));
                    Fill(14, 4, 14, 11, new Color(1f, 0.96f, 0.72f));
                    break;
                }
                case Deprem.Core.BagItemId.Water: // mavi şişe, beyaz kapak, etiket
                {
                    Box(4, 1, 11, 11, new Color(0.36f, 0.62f, 0.9f));
                    Box(6, 10, 9, 13, new Color(0.36f, 0.62f, 0.9f));
                    Fill(6, 13, 9, 14, Color.white);
                    Fill(5, 5, 10, 7, new Color(0.88f, 0.94f, 1f));
                    Fill(5, 2, 5, 9, new Color(0.62f, 0.82f, 1f));
                    break;
                }
                default: // düdük: turuncu gövde, ağızlık, delik, halka
                {
                    Box(2, 3, 10, 9, new Color(0.96f, 0.52f, 0.16f));
                    Box(9, 6, 14, 9, new Color(0.96f, 0.52f, 0.16f));
                    Fill(4, 6, 6, 7, outline);
                    Box(1, 9, 4, 12, Color.clear);
                    Fill(2, 10, 3, 11, Color.clear);
                    break;
                }
            }
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
            bagIcons[id] = sprite;
            return sprite;
        }

        // Konuşma balonu: gövde (9-slice) + altta kuyruk, yazıya göre büyür.
        // Kökün pivotu kuyruğun ucu (alt orta); karakterin başının üstüne bu noktayla konur.
        public static UiBubble CreateSpeechBubble(Transform parent, Font font, int fontSize = 14)
        {
            var style = UiStyle.Current;
            var root = CreateChild<UiBubble>(parent, "SpeechBubble");
            var rootRect = root.Root;
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0f);

            const float tailHeight = 12f;
            // Sanatçı görseli yoksa kodla üretilen pixel art balon (düz kutu çok çirkin duruyordu)
            var tailSprite = style && style.bubbleTail ? style.bubbleTail : PixelTailSprite;
            Image tail = null;
            if (tailSprite)
            {
                tail = CreateImage(rootRect, "Tail", Color.white);
                ApplySprite(tail, tailSprite);
                // Kuyruk gövdenin alt konturuna 2 px biner (beyaz üstü konturu örter: tek parça balon)
                PlaceBottomCenter(tail.rectTransform, new Vector2(16f, tailHeight + 2f), 0f);
            }
            else
            {
                // Kenarlıklı karo: üst yarısı gövdenin arkasında kalır, alt yarısı kuyruk olur
                var tailBorder = CreateImage(rootRect, "TailBorder", BubbleBorder);
                // 14'lük karonun yarım köşegeni ~10: merkez 10'da olunca alt ucu kökün dibine değer
                PlaceBottomCenter(tailBorder.rectTransform, Vector2.one * 14f, 10f);
                tailBorder.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var tailFill = CreateImage(rootRect, "TailFill", BubbleFill);
                PlaceBottomCenter(tailFill.rectTransform, Vector2.one * 10f, 10f);
                tailFill.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            var body = CreateBox(rootRect, "Body", style && style.bubble ? style.bubble : PixelBubbleSprite, BubbleFill, BubbleBorder);
            var bodyRect = body.rectTransform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(0f, tailHeight);
            bodyRect.offsetMax = Vector2.zero;
            if (!tailSprite) body.transform.SetAsLastSibling(); // karonun üst yarısını örtsün
            else if (tail != null) tail.transform.SetAsLastSibling();

            var label = CreateText(bodyRect, "Text", font, fontSize, BubbleText);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            root.Setup(bodyRect, label, tailHeight);
            return root;
        }

        static Image CreateBox(Transform parent, string name, Sprite sprite, Color fill, Color border)
        {
            if (sprite)
            {
                var image = CreateImage(parent, name, Color.white);
                ApplySprite(image, sprite);
                return image;
            }
            var outer = CreateImage(parent, name, border);
            var inner = CreateImage(outer.rectTransform, "Fill", fill);
            Stretch(inner.rectTransform, Vector2.zero, Vector2.one);
            inner.rectTransform.offsetMin = Vector2.one * FallbackBorder;
            inner.rectTransform.offsetMax = -Vector2.one * FallbackBorder;
            return outer;
        }

        static void PlaceBottomCenter(RectTransform rect, Vector2 size, float y)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, y > 0f ? 0.5f : 0f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(0f, y);
        }

        public static T CreateChild<T>(Transform parent, string name) where T : Component
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(T));
            child.transform.SetParent(parent, false);
            return child.GetComponent<T>();
        }

        // Anchor'ları ekranın oranı olarak verir (0-1), kenar boşluğu sıfır
        public static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
