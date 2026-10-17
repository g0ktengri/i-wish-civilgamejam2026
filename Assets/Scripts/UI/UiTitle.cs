using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Kabartma pixel başlık (arayüz turu): krem yazının arkasında aşağı-sağa kaymış iki katman (koyu kırmızı, koyu),
    // pixel ölçüsünde "derinlik"; en üstte koyu kontur. Menü başlıkları kullanır.
    public class UiTitle : MonoBehaviour
    {
        Text front;
        Text middle;
        Text back;

        public Text Front => front;

        public string Text
        {
            get => front.text;
            set { front.text = value; middle.text = value; back.text = value; }
        }

        public TextAnchor Alignment
        {
            set { front.alignment = value; middle.alignment = value; back.alignment = value; }
        }

        // depth: katman kayması (referans pikseli)
        public static UiTitle Create(Transform parent, string name, Font font, int size, float depth = 3f)
        {
            var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            UiBuilder.Stretch(root, Vector2.zero, Vector2.one);
            var title = root.gameObject.AddComponent<UiTitle>();
            title.back = Layer(root, "Back", font, size, new Color(0.1f, 0.06f, 0.06f), new Vector2(depth * 2f, -depth * 2f));
            title.middle = Layer(root, "Middle", font, size, new Color(0.55f, 0.16f, 0.12f), new Vector2(depth, -depth));
            title.front = UiBuilder.Outlined(Layer(root, "Front", font, size, new Color(0.96f, 0.92f, 0.84f), Vector2.zero));
            title.front.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f);
            return title;
        }

        static Text Layer(RectTransform parent, string name, Font font, int size, Color color, Vector2 offset)
        {
            var text = UiBuilder.CreateText(parent, name, font, size, color);
            UiBuilder.Stretch(text.rectTransform, Vector2.zero, Vector2.one);
            text.rectTransform.anchoredPosition = offset;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }
    }
}
