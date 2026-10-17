using System.Collections.Generic;
using UnityEngine;

namespace Deprem.UI
{
    // Alt kenarda tuş kapaklı ipucu satırı: [W]/[S] Seç · [F] Onayla · [Esc] Geri (UiPrompt'lar yan yana, ortalı)
    public class UiPromptRow : MonoBehaviour
    {
        const float Gap = 12f;
        readonly List<UiPrompt> prompts = new List<UiPrompt>();
        Font font;
        int fontSize;

        public static UiPromptRow Create(Transform parent, Font font, int fontSize, float bottom)
        {
            var root = new GameObject("PromptRow", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, bottom);
            var row = root.gameObject.AddComponent<UiPromptRow>();
            row.font = font;
            row.fontSize = fontSize;
            return row;
        }

        // Her biri "Tuş: yazı" biçiminde metin (texts.csv'den)
        public void Set(params string[] texts)
        {
            while (prompts.Count < texts.Length)
            {
                var prompt = UiPrompt.Create(transform, "Prompt", font, fontSize);
                prompt.Animate = false;
                prompts.Add(prompt);
            }
            float total = 0f;
            for (int i = 0; i < prompts.Count; i++)
            {
                prompts[i].Set(i < texts.Length ? texts[i] : string.Empty);
                if (i < texts.Length) total += prompts[i].Rect.sizeDelta.x + (i > 0 ? Gap : 0f);
            }
            float x = -total * 0.5f;
            for (int i = 0; i < texts.Length; i++)
            {
                var rect = prompts[i].Rect;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0f, 0f);
                rect.anchoredPosition = new Vector2(x, 0f);
                x += rect.sizeDelta.x + Gap;
            }
        }
    }
}
