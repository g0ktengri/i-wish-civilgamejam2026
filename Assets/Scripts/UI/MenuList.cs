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
    // Arayüz turu: satırlar HUD'un pixel panelinde, sola dayalı; solda pixel simge (SetIcon), seçili satırda vurgu
    // rengi, yarı saydam şerit, solda nabız gibi parlayan dikey çizgi ve sağa kayan yazı; satırlar arasında noktalı ayraç.
    // Ayarlanabilir satırda "Ad: < değer >" metni ikiye bölünür: ad solda, değer sağda pixel oklar arasında;
    // AddSlider satırında değerin solunda 10 dilimli pixel çubuk. Panel satır sayısına göre uzar (parent'ın üstünden).
    public class MenuList
    {
        const float Padding = 12f;
        const float IconSize = 26f;
        const float SlideDistance = 6f;
        const int SliderSegments = 10;

        class Entry
        {
            public RectTransform Row;
            public Image Bar;
            public Image Marker;
            public Image Icon;
            public Text Label;
            public Text Value;
            public Image ArrowLeft;
            public Image ArrowRight;
            public Image[] Segments;
            public bool Disabled;
            public bool ShowValue;
            public Func<float> SliderValue;
            public Action<float> SliderSet;
            public float Slide;
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
        readonly RectTransform panel;
        int selected;
        Vector2 lastMouse;
        static Sprite markerSprite;
        static Sprite dotSprite;

        public MenuList(RectTransform parent, Font font, int fontSize, float rowHeight, Color normal, Color highlight)
        {
            this.parent = parent;
            this.font = font;
            this.fontSize = fontSize;
            this.rowHeight = rowHeight;
            this.normal = normal;
            this.highlight = highlight;

            panel = UiBuilder.CreatePanel(parent, "MenuPanel").rectTransform;
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = Vector2.zero;
            panel.gameObject.SetActive(false);
        }

        public void Clear()
        {
            foreach (var entry in entries)
                if (entry.Row)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(entry.Row.gameObject);
                    else UnityEngine.Object.DestroyImmediate(entry.Row.gameObject);
                }
            entries.Clear();
            selected = 0;
            Resize();
        }

        public void Add(string key, Action action, params object[] args)
        {
            var row = new GameObject(key, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(panel, false);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(-Padding * 2f, rowHeight);
            row.anchoredPosition = new Vector2(0f, -Padding - entries.Count * rowHeight);

            // Üstteki satırla arasında noktalı ayraç (ilk satırda yok)
            if (entries.Count > 0)
            {
                var divider = UiBuilder.CreateImage(row, "Divider", new Color(0.04f, 0.03f, 0.03f, 0.55f));
                divider.sprite = DotSprite;
                divider.type = Image.Type.Tiled;
                var rect = divider.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(-16f, 2f);
                rect.anchoredPosition = Vector2.zero;
            }

            // Seçim şeridi: satırı kaplayan yarı saydam vurgu (seçili değilken görünmez)
            var bar = UiBuilder.CreateImage(row, "Bar", Color.clear);
            UiBuilder.Stretch(bar.rectTransform, Vector2.zero, Vector2.one);
            bar.rectTransform.offsetMin = new Vector2(0f, 3f);
            bar.rectTransform.offsetMax = new Vector2(0f, -3f);

            // Seçili satırın solunda dikey vurgu çizgisi
            var marker = UiBuilder.CreateImage(row, "Marker", highlight);
            marker.rectTransform.anchorMin = new Vector2(0f, 0f);
            marker.rectTransform.anchorMax = new Vector2(0f, 1f);
            marker.rectTransform.pivot = new Vector2(0f, 0.5f);
            marker.rectTransform.sizeDelta = new Vector2(4f, -6f);
            marker.rectTransform.anchoredPosition = Vector2.zero;
            marker.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1f, -1f);

            var icon = UiBuilder.CreateImage(row, "Icon", normal);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            icon.rectTransform.sizeDelta = Vector2.one * IconSize;
            icon.enabled = false;

            var label = UiBuilder.Outlined(UiBuilder.CreateText(row, "Label", font, fontSize, normal, TextAnchor.MiddleLeft));
            UiBuilder.Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            var value = UiBuilder.Outlined(UiBuilder.CreateText(row, "Value", font, fontSize, normal, TextAnchor.MiddleRight));
            UiBuilder.Stretch(value.rectTransform, Vector2.zero, Vector2.one);
            value.rectTransform.offsetMax = new Vector2(-22f, 0f);
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            value.enabled = false;

            var entry = new Entry { Row = row, Bar = bar, Marker = marker, Icon = icon, Label = label, Value = value, Key = key, Args = args, Action = action };
            entries.Add(entry);
            Layout(entry);
            Resize();
            Refresh();
        }

        /// <summary>Son eklenen satırın soldaki simgesi (UiIcons)</summary>
        public void SetIcon(Sprite sprite)
        {
            if (entries.Count == 0) return;
            var entry = entries[entries.Count - 1];
            entry.Icon.sprite = sprite;
            entry.Icon.enabled = sprite;
            Layout(entry);
        }

        /// <summary>M42: son eklenen satır seçilebilir mi (kapalıysa soluk, W/S ve fare atlar)</summary>
        public void SetEnabled(bool enabled)
        {
            if (entries.Count == 0) return;
            entries[entries.Count - 1].Disabled = !enabled;
            if (entries[selected].Disabled) selected = Step(selected, 1);
            Refresh();
        }

        // from'dan direction yönünde ilk seçilebilir satır (hepsi kapalıysa from)
        int Step(int from, int direction)
        {
            for (int i = 1; i <= entries.Count; i++)
            {
                int index = ((from + direction * i) % entries.Count + entries.Count) % entries.Count;
                if (!entries[index].Disabled) return index;
            }
            return from;
        }

        // M40: "Ad: değer" satırı, değer sağda (oksuz); onay / tık action'ı çağırır (tuş değiştirme)
        public void AddValue(string key, Func<object[]> args, Action action)
        {
            Add(key, action);
            var entry = entries[entries.Count - 1];
            entry.ArgsProvider = args;
            entry.ShowValue = true;
            entry.Value.enabled = true;
            Refresh();
        }

        // args: her yenilemede metne verilecek değerler (null = düz metin); adjust(-1 / +1)
        public void AddAdjustable(string key, Func<object[]> args, Action<int> adjust)
        {
            Add(key, null);
            var entry = entries[entries.Count - 1];
            entry.ArgsProvider = args;
            entry.Adjust = adjust;
            entry.Value.enabled = true;
            entry.ArrowLeft = Arrow(entry.Row, "ArrowLeft", true);
            entry.ArrowRight = Arrow(entry.Row, "ArrowRight", false);
            Refresh();
        }

        // Ses düzeyi gibi 0-1 değerler: metindeki değer (yüzde) ve solunda 10 dilimli pixel çubuk.
        // M39: sol / sağ %10 azaltır / artırır, %0 ve %100'de durur (başa sarmaz); bir kareye tıklamak değeri o kareye ayarlar
        public void AddSlider(string key, Func<float> value, Action<float> set)
        {
            AddAdjustable(key, () => new object[] { Mathf.RoundToInt(value() * 100f) },
                direction => set(Mathf.Clamp01(Mathf.Round(value() * SliderSegments + direction) / SliderSegments)));
            var entry = entries[entries.Count - 1];
            entry.SliderValue = value;
            entry.SliderSet = set;
            entry.Segments = new Image[SliderSegments];
            const float segment = 7f, gap = 2f;
            for (int i = 0; i < SliderSegments; i++)
            {
                var image = UiBuilder.CreateImage(entry.Row, "Segment", Color.white);
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(segment, 12f);
                // Değer yazısının ("%100", ~56 px) solunda
                rect.anchoredPosition = new Vector2(-96f - (SliderSegments - i) * (segment + gap), 0f);
                var shadow = image.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0.04f, 0.03f, 0.03f, 0.9f);
                shadow.effectDistance = new Vector2(1f, -1f);
                entry.Segments[i] = image;
            }
            Refresh();
        }

        Image Arrow(RectTransform row, string name, bool left)
        {
            var arrow = UiBuilder.CreateImage(row, name, highlight);
            arrow.sprite = MarkerSprite;
            var rect = arrow.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(8f, 12f);
            rect.localScale = new Vector3(left ? -1f : 1f, 1f, 1f);
            rect.anchoredPosition = new Vector2(left ? -84f : -10f, 0f);
            return arrow;
        }

        // Simge varsa yazı simgenin sağında başlar; seçiliyken sağa kayar (Slide)
        void Layout(Entry entry)
        {
            float left = 16f + entry.Slide;
            if (entry.Icon.enabled)
            {
                entry.Icon.rectTransform.anchoredPosition = new Vector2(left, 0f);
                left += IconSize + 8f;
            }
            entry.Label.rectTransform.offsetMin = new Vector2(left, 0f);
            entry.Label.rectTransform.offsetMax = new Vector2(-12f, 0f);
        }

        void Resize()
        {
            panel.gameObject.SetActive(entries.Count > 0);
            panel.sizeDelta = new Vector2(0f, entries.Count * rowHeight + Padding * 2f);
        }

        public void Refresh()
        {
            // Seçili satır kapalıysa (kapalı satırdan sonra eklenenler) ilk açık satıra geç
            if (entries.Count > 0 && entries[selected].Disabled) selected = Step(selected, 1);
            var bar = new Color(highlight.r, highlight.g, highlight.b, 0.1f);
            var dim = new Color(normal.r * 0.35f, normal.g * 0.35f, normal.b * 0.35f, 1f);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var args = entry.ArgsProvider != null ? entry.ArgsProvider() : entry.Args;
                string text = args != null && args.Length > 0
                    ? Texts.Format(entry.Key, args)
                    : Texts.Get(entry.Key);
                bool on = i == selected && !entry.Disabled;
                var color = on ? highlight : entry.Disabled ? new Color(normal.r, normal.g, normal.b, 0.35f) : normal;

                // Ayarlanabilir: "Ad: < değer >" → ad solda, değer sağda (oklar pixel)
                int colon = entry.Adjust != null || entry.ShowValue ? text.IndexOf(':') : -1;
                if (colon >= 0)
                {
                    entry.Label.text = text.Substring(0, colon);
                    entry.Value.text = text.Substring(colon + 1).Replace("<", string.Empty).Replace(">", string.Empty).Trim();
                }
                else
                {
                    entry.Label.text = text;
                    if (entry.Value) entry.Value.text = string.Empty;
                }
                entry.Label.color = color;
                entry.Value.color = color;
                entry.Value.rectTransform.offsetMax = new Vector2(entry.ArrowLeft ? -22f : -12f, 0f);
                if (entry.ArrowLeft)
                {
                    // Sol ok değer yazısının hemen solunda
                    float width = Mathf.Max(24f, entry.Value.preferredWidth);
                    entry.ArrowLeft.rectTransform.anchoredPosition = new Vector2(-22f - width - 10f, 0f);
                    entry.ArrowLeft.color = entry.ArrowRight.color = on ? highlight : dim;
                }
                if (entry.Segments != null)
                {
                    int filled = Mathf.RoundToInt(Mathf.Clamp01(entry.SliderValue()) * SliderSegments);
                    for (int s = 0; s < entry.Segments.Length; s++)
                        entry.Segments[s].color = s < filled ? color : dim;
                }
                entry.Icon.color = color;
                entry.Bar.color = on ? bar : Color.clear;
                entry.Marker.enabled = on;
            }
        }

        public void Tick()
        {
            if (entries.Count == 0) return;
            int previous = selected;

            // Seçili satırın vurgu çizgisi nabız gibi parlar; seçili satırın yazısı sağa kayar, diğerleri geri döner
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f);
            entries[selected].Marker.color = new Color(highlight.r, highlight.g, highlight.b, pulse);
            float step = Time.unscaledDeltaTime * 60f;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                float target = i == selected ? SlideDistance : 0f;
                if (Mathf.Approximately(entry.Slide, target)) continue;
                entry.Slide = Mathf.MoveTowards(entry.Slide, target, step);
                Layout(entry);
            }

            if (UiInput.Up()) selected = Step(selected, -1);
            if (UiInput.Down()) selected = Step(selected, 1);

            var mouse = Mouse.current;
            int hovered = -1;
            if (mouse != null)
            {
                Vector2 position = mouse.position.ReadValue();
                for (int i = 0; i < entries.Count; i++)
                    if (!entries[i].Disabled && RectTransformUtility.RectangleContainsScreenPoint(entries[i].Row, position, null))
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
                int direction = UiInput.Left() ? -1 : UiInput.Right() || UiInput.Confirm() ? 1 : 0;
                if (clicked && ClickAdjust(current, mouse.position.ReadValue())) direction = 0;
                else if (clicked) direction = 1;
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

        // M39: satırın içindeki tıklama: ◂ azaltır, ▸ artırır, çubuğun bir karesi değeri o kareye ayarlar (ilk kare
        // zaten seçiliyse %0). Bir şey yaptıysa true; satırın başka yerine tıklamak bir adım ileri (çağıran yapar).
        bool ClickAdjust(Entry entry, Vector2 screen)
        {
            int direction = 0;
            if (Hit(entry.ArrowLeft.rectTransform, screen, 8f)) direction = -1;
            else if (Hit(entry.ArrowRight.rectTransform, screen, 8f)) direction = 1;
            if (direction != 0)
            {
                Sfx.Play("ui.move");
                entry.Adjust(direction);
                Refresh();
                return true;
            }
            if (entry.Segments == null) return false;
            for (int i = 0; i < entry.Segments.Length; i++)
            {
                if (!Hit(entry.Segments[i].rectTransform, screen, 4f)) continue;
                float value = (i + 1f) / SliderSegments;
                if (i == 0 && Mathf.Approximately(entry.SliderValue(), value)) value = 0f;
                Sfx.Play("ui.move");
                entry.SliderSet(value);
                Refresh();
                return true;
            }
            return false;
        }

        // Ekran noktası dikdörtgenin içinde mi (pad: dört yana referans pikseli pay; satır boyunca dikeyde de geniş)
        bool Hit(RectTransform rect, Vector2 screen, float pad)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out var local)) return false;
            var r = rect.rect;
            float vertical = Mathf.Max(pad, (rowHeight - r.height) * 0.5f);
            return local.x >= r.xMin - pad && local.x <= r.xMax + pad && local.y >= r.yMin - vertical && local.y <= r.yMax + vertical;
        }

        // 6 x 9, sağa bakan küçük pixel ok (beyaz, renk Image.color'dan), koyu kontur
        static Sprite MarkerSprite
        {
            get
            {
                if (markerSprite) return markerSprite;
                const int w = 6, h = 9;
                bool Inside(int x, int y) => x >= 0 && x < w && Mathf.Abs(y - 4) <= (w - 1 - x) * 0.8f + 0.3f;
                var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "UiMenuMarker",
                };
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool inside = Inside(x, y);
                    bool edge = inside && (!Inside(x - 1, y) || !Inside(x + 1, y) || !Inside(x, y - 1) || !Inside(x, y + 1));
                    texture.SetPixel(x, y, !inside ? Color.clear : edge ? new Color(0.08f, 0.06f, 0.05f) : Color.white);
                }
                texture.Apply();
                markerSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
                return markerSprite;
            }
        }

        // 4 x 2 döşeme: 2 px dolu, 2 px boş (noktalı ayraç)
        static Sprite DotSprite
        {
            get
            {
                if (dotSprite) return dotSprite;
                var texture = new Texture2D(4, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Repeat,
                    name = "UiMenuDots",
                };
                for (int y = 0; y < 2; y++)
                for (int x = 0; x < 4; x++)
                    texture.SetPixel(x, y, x < 2 ? Color.white : Color.clear);
                texture.Apply();
                // pixelsPerUnit 100: döşemenin referans pikselindeki boyu Canvas'ın referencePixelsPerUnit'ine göre
                dotSprite = Sprite.Create(texture, new Rect(0, 0, 4, 2), new Vector2(0.5f, 0.5f), 100f);
                return dotSprite;
            }
        }
    }
}
