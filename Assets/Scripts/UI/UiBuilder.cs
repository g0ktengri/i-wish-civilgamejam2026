using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Prefab'sız UI kurulumu için ortak yardımcılar (GDD referans çözünürlüğü 960 x 540).
    public static class UiBuilder
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(960, 540);

        public static Font DefaultFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
