using System.Collections.Generic;
using Deprem.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Tuş ipucu (M24): "C / S: Parlayan masada Çök-Kapan-Tutun" → [C] [S] Parlayan masada Çök-Kapan-Tutun.
    // Tuşlar ayrı kapaklarda (UiBuilder.CreateKeyCap), yazı yanında; panel yazıya göre genişler.
    // ':' yoksa ya da tuş kısmı tuşa benzemiyorsa düz yazı. PromptHud ve SpiritHud kullanır.
    // M33 (Mehmet'in play testi 21: okunmuyordu): ölçüler yazı boyundan (22 punto → 46 px yükseklik), geniş iç boşluk,
    // krem yazıda koyu kontur; yeni ipucu gelince kısa büyüme ve sarı parlama (fark edilsin).
    public class UiPrompt : MonoBehaviour
    {
        const int MaxKeyLength = 6;
        const float PopSeconds = 0.3f;
        const float PopScale = 0.18f;

        Font font;
        int fontSize;
        float height;
        float padding;
        float gap;
        Image panel;
        Text message;
        readonly List<GameObject> caps = new List<GameObject>();
        readonly List<string> keys = new List<string>();
        string shown;
        float pop = -1f;

        public RectTransform Rect => (RectTransform)transform;
        /// <summary>Yeni ipucunda büyüme ve parlama (menü alt satırında kapalı)</summary>
        public bool Animate { get; set; } = true;
        /// <summary>M40: varsayılan tuş harfleri güncel atamayla gösterilsin (menü alt satırında kapalı: menü tuşları sabit)</summary>
        public bool RemapKeys { get; set; } = true;

        void Awake() => KeyBindings.Changed += HandleBindingsChanged;
        void OnDestroy() => KeyBindings.Changed -= HandleBindingsChanged;

        // Tuş değişince aynı ipucu yeni tuşla yeniden kurulsun
        void HandleBindingsChanged()
        {
            if (!this || !RemapKeys || string.IsNullOrEmpty(shown)) return;
            string text = shown;
            shown = null;
            Set(text);
        }

        public static UiPrompt Create(Transform parent, string name, Font font, int fontSize = 0)
        {
            var panel = UiBuilder.CreatePanel(parent, name);
            var prompt = panel.gameObject.AddComponent<UiPrompt>();
            prompt.panel = panel;
            prompt.font = font ? font : UiBuilder.HudFont;
            prompt.fontSize = fontSize > 0 ? fontSize : UiBuilder.HudFontSize;
            prompt.height = Mathf.Round(prompt.fontSize * 2.1f);
            prompt.padding = Mathf.Round(prompt.fontSize * 0.75f);
            prompt.gap = Mathf.Round(prompt.fontSize * 0.3f);
            prompt.message = UiBuilder.Outlined(UiBuilder.CreateText(panel.rectTransform, "Message", prompt.font,
                prompt.fontSize, UiBuilder.HudText, TextAnchor.MiddleLeft));
            var rect = prompt.message.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            prompt.message.horizontalOverflow = HorizontalWrapMode.Overflow;
            prompt.message.supportRichText = true;
            panel.rectTransform.sizeDelta = new Vector2(prompt.padding * 2f, prompt.height);
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
            if (RemapKeys) Remap(keys);
            float x = padding;
            foreach (var key in keys)
            {
                var cap = UiBuilder.CreateKeyCap(transform, key, font, fontSize);
                cap.anchoredPosition = new Vector2(x, 0f);
                caps.Add(cap.gameObject);
                x += cap.sizeDelta.x + gap;
            }
            if (keys.Count > 0) x += gap;

            message.text = body;
            float width = message.preferredWidth;
            message.rectTransform.anchoredPosition = new Vector2(x, 0f);
            message.rectTransform.sizeDelta = new Vector2(width, 0f);
            x += width + padding;
            Rect.sizeDelta = new Vector2(x, height);
            if (Animate) pop = 0f; // yeni ipucu: büyüme ve parlama
        }

        void Update()
        {
            if (pop < 0f) return;
            pop += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(pop / PopSeconds);
            float ease = 1f - (1f - k) * (1f - k);
            transform.localScale = Vector3.one * (1f + PopScale * (1f - ease));
            panel.color = Color.Lerp(new Color(1f, 0.88f, 0.55f), Color.white, ease);
            if (k >= 1f)
            {
                pop = -1f;
                transform.localScale = Vector3.one;
                panel.color = Color.white;
            }
        }

        // M40: texts.csv'deki varsayılan harfler → güncel tuşun adı. "W/A/S/D" ruh uçuşu; tek tek F etkileşim, S eğilme,
        // Space zıplama, A / D yürüme. Diğerleri (Esc...) olduğu gibi.
        static void Remap(List<string> keys)
        {
            bool fly = keys.Count == 4 && keys[0] == "W" && keys[1] == "A" && keys[2] == "S" && keys[3] == "D";
            for (int i = 0; i < keys.Count; i++)
            {
                if (fly)
                {
                    var spirit = i == 0 ? KeyBindings.SpiritUp : i == 1 ? KeyBindings.SpiritLeft : i == 2 ? KeyBindings.SpiritDown : KeyBindings.SpiritRight;
                    keys[i] = KeyBindings.Label(spirit);
                    continue;
                }
                switch (keys[i])
                {
                    case "F": keys[i] = KeyBindings.Label(KeyBindings.Interact); break;
                    case "S": keys[i] = KeyBindings.Label(KeyBindings.Crouch); break;
                    case "Space": keys[i] = KeyBindings.Label(KeyBindings.Jump); break;
                    case "A": keys[i] = KeyBindings.Label(KeyBindings.Left); break;
                    case "D": keys[i] = KeyBindings.Label(KeyBindings.Right); break;
                }
            }
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
