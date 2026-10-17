using System;
using System.Collections.Generic;
using Deprem.Audio;
using Deprem.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deprem.UI
{
    // EventSystem'siz basit dikey menü: W/S ya da oklar ile seç, F/Enter/Space ile onayla,
    // fareyle üzerine gel ve tıkla. Satır metinleri texts.csv anahtarlarından gelir.
    // M25: seçim değişince ui.move, onayda ui.click (Deprem.Audio.Sfx).
    // M32: ayarlanabilir satır (AddAdjustable): A/D ya da sol/sağ ok değeri değiştirir, onay / tık bir adım ileri;
    // satır metni her yenilemede değerden yeniden yazılır.
    public class MenuList
    {
        class Entry
        {
            public Text Label;
            public string Key;
            public object[] Args;
            public Func<object[]> ArgsProvider;
            public Action Action;
            public Action<int> Adjust;
        }

        readonly RectTransform parent;
        readonly Font font;
        readonly int fontSize;
        readonly float rowHeight;
        readonly Color normal;
        readonly Color highlight;
        readonly List<Entry> entries = new List<Entry>();
        int selected;
        Vector2 lastMouse;

        public MenuList(RectTransform parent, Font font, int fontSize, float rowHeight, Color normal, Color highlight)
        {
            this.parent = parent;
            this.font = font;
            this.fontSize = fontSize;
            this.rowHeight = rowHeight;
            this.normal = normal;
            this.highlight = highlight;
        }

        public void Clear()
        {
            foreach (var entry in entries)
                if (entry.Label) UnityEngine.Object.Destroy(entry.Label.gameObject);
            entries.Clear();
            selected = 0;
        }

        public void Add(string key, Action action, params object[] args)
        {
            var label = UiBuilder.CreateText(parent, key, font, fontSize, normal);
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, rowHeight);
            rect.anchoredPosition = new Vector2(0f, -entries.Count * rowHeight);
            entries.Add(new Entry { Label = label, Key = key, Args = args, Action = action });
            Refresh();
        }

        // args: her yenilemede metne verilecek değerler (null = düz metin); adjust(-1 / +1)
        public void AddAdjustable(string key, Func<object[]> args, Action<int> adjust)
        {
            Add(key, null);
            var entry = entries[entries.Count - 1];
            entry.ArgsProvider = args;
            entry.Adjust = adjust;
            Refresh();
        }

        public void Refresh()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var args = entry.ArgsProvider != null ? entry.ArgsProvider() : entry.Args;
                entry.Label.text = args != null && args.Length > 0
                    ? Texts.Format(entry.Key, args)
                    : Texts.Get(entry.Key);
                entry.Label.color = i == selected ? highlight : normal;
            }
        }

        public void Tick()
        {
            if (entries.Count == 0) return;
            int previous = selected;

            if (UiInput.Up()) selected = (selected - 1 + entries.Count) % entries.Count;
            if (UiInput.Down()) selected = (selected + 1) % entries.Count;

            var mouse = Mouse.current;
            int hovered = -1;
            if (mouse != null)
            {
                Vector2 position = mouse.position.ReadValue();
                for (int i = 0; i < entries.Count; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(entries[i].Label.rectTransform, position, null))
                        hovered = i;
                // Sadece fare gerçekten hareket edince seçimi değiştir, klavyeyle çakışmasın
                if (hovered >= 0 && position != lastMouse) selected = hovered;
                lastMouse = position;
            }

            if (selected != previous)
            {
                Refresh();
                Sfx.Play("ui.move");
            }

            bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame && hovered >= 0;
            if (clicked) selected = hovered;
            var current = entries[selected];
            if (current.Adjust != null)
            {
                int direction = UiInput.Left() ? -1 : UiInput.Right() || clicked || UiInput.Confirm() ? 1 : 0;
                if (direction != 0)
                {
                    Sfx.Play("ui.move");
                    current.Adjust(direction);
                    Refresh();
                }
                return;
            }
            if (clicked || UiInput.Confirm())
            {
                Sfx.Play("ui.click");
                current.Action?.Invoke();
            }
        }
    }
}
