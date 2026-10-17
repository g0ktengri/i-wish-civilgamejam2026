using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Tuş ipucu (M24): "C / S: Parlayan masada Çök-Kapan-Tutun" → [C] [S] Parlayan masada Çök-Kapan-Tutun.
    // Tuşlar ayrı kapaklarda (UiBuilder.CreateKeyCap), yazı yanında; panel yazıya göre genişler.
    // ':' yoksa ya da tuş kısmı tuşa benzemiyorsa düz yazı. PromptHud ve SpiritHud kullanır.
    public class UiPrompt : MonoBehaviour
    {
        const float Height = 34f;
        const float Padding = 10f;
        const float Gap = 4f;
        const int MaxKeyLength = 6;

        Font font;
        int fontSize;
        Text message;
        readonly List<GameObject> caps = new List<GameObject>();
        readonly List<string> keys = new List<string>();
        string shown;

        public RectTransform Rect => (RectTransform)transform;

        public static UiPrompt Create(Transform parent, string name, Font font, int fontSize = 0)
        {
            var panel = UiBuilder.CreatePanel(parent, name);
            var prompt = panel.gameObject.AddComponent<UiPrompt>();
            prompt.font = font;
            prompt.fontSize = fontSize > 0 ? fontSize : UiBuilder.HudFontSize;
            prompt.message = UiBuilder.CreateText(panel.rectTransform, "Message", font, prompt.fontSize, UiBuilder.HudText,
                TextAnchor.MiddleLeft);
            var rect = prompt.message.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            prompt.message.horizontalOverflow = HorizontalWrapMode.Overflow;
            prompt.message.supportRichText = true;
            panel.rectTransform.sizeDelta = new Vector2(Padding * 2f, Height);
            panel.gameObject.SetActive(false);
            return prompt;
        }

        // Boş yazı ipucunu gizler
        public void Set(string text)
        {
            if (text == shown) return;
            shown = text;
            foreach (var cap in caps)
                if (cap)
                {
                    if (Application.isPlaying) Destroy(cap);
                    else DestroyImmediate(cap);
                }
            caps.Clear();

            bool visible = !string.IsNullOrEmpty(text);
            gameObject.SetActive(visible);
            if (!visible) return;

            Split(text, keys, out string body);
            float x = Padding;
            foreach (var key in keys)
            {
                var cap = UiBuilder.CreateKeyCap(transform, key, font, Mathf.Max(10, fontSize - 1));
                cap.anchoredPosition = new Vector2(x, 0f);
                caps.Add(cap.gameObject);
                x += cap.sizeDelta.x + Gap;
            }
            if (keys.Count > 0) x += Gap;

            message.text = body;
            float width = message.preferredWidth;
            message.rectTransform.anchoredPosition = new Vector2(x, 0f);
            message.rectTransform.sizeDelta = new Vector2(width, 0f);
            x += width + Padding + 2f;
            Rect.sizeDelta = new Vector2(x, Height);
        }

        // "C / S: Çök" → C, S + "Çök". "F (basılı tut): Sar" → F + "(basılı tut) Sar". "Burada olmaz!" → düz.
        public static void Split(string text, List<string> keys, out string body)
        {
            keys.Clear();
            body = text ?? string.Empty;
            int colon = body.IndexOf(':');
            if (colon <= 0 || colon > 24) return;

            string keyPart = body.Substring(0, colon);
            string extra = string.Empty;
            int paren = keyPart.IndexOf('(');
            if (paren >= 0)
            {
                extra = keyPart.Substring(paren).Trim();
                keyPart = keyPart.Substring(0, paren);
            }

            var found = new List<string>();
            foreach (var token in keyPart.Split('/'))
            {
                string key = token.Trim();
                if (key.Length == 0 || key.Length > MaxKeyLength || key.Contains(" ")) return;
                found.Add(key);
            }
            if (found.Count == 0) return;

            keys.AddRange(found);
            string rest = body.Substring(colon + 1).Trim();
            body = extra.Length > 0 ? extra + " " + rest : rest;
        }
    }
}
