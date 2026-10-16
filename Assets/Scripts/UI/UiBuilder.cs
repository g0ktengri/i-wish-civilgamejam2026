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

        // Sprite'ı pixel art için ayarlar: kenarı varsa 9-slice, 1 sprite pikseli = 1 referans pikseli
        public static void ApplySprite(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            if (!sprite) return;
            image.color = Color.white;
            image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = sprite.pixelsPerUnit / CanvasPixelsPerUnit;
        }

        // HUD paneli (tuş ipucu, sayaç, hedef): UiStyle.panel ya da kenarlıklı yarı saydam kutu.
        // Boyutu ve yerini çağıran verir; yazı bunun çocuğu olur.
        public static Image CreatePanel(Transform parent, string name)
        {
            var style = UiStyle.Current;
            return CreateBox(parent, name, style ? style.panel : null, PanelFill, PanelBorder);
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
            var tailSprite = style ? style.bubbleTail : null;
            if (tailSprite)
            {
                var tail = CreateImage(rootRect, "Tail", Color.white);
                ApplySprite(tail, tailSprite);
                PlaceBottomCenter(tail.rectTransform, new Vector2(16f, tailHeight), 0f);
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

            var body = CreateBox(rootRect, "Body", style ? style.bubble : null, BubbleFill, BubbleBorder);
            var bodyRect = body.rectTransform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(0f, tailHeight);
            bodyRect.offsetMax = Vector2.zero;
            if (!tailSprite) body.transform.SetAsLastSibling(); // karonun üst yarısını örtsün

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
