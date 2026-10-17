using System.Collections.Generic;
using UnityEngine;

namespace Deprem.Minigames
{
    // Minigame'lerin kodla çizilen pixel art parçaları (sanatçı görseli yokken). Oyunun paleti: sıcak ahşap, bej sıva,
    // gri metal, koyu kontur. M26 (Mehmet'in play testi 17: "low-poly ve çirkin"): çözünürlük iki katı
    // (1 doku pikseli = 1 referans pikseli), her parça konturlu, en az üç tonlu (ışık / ana / gölge), ortam gölgeli.
    // M28: zeminler ve engeller tersine sade (Ground / Block, büyük pikseller); detay sadece eşyalarda ve mekanizmada.
    // Çağıran, Image boyutunu doku boyutunda verir (Minigame.TexSize). Aynı istek önbellekten döner.
    public static class MinigamePixels
    {
        public const float Scale = 1f;

        static readonly Color Outline = new Color(0.08f, 0.06f, 0.055f);
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static readonly Color Wood = new Color(0.52f, 0.36f, 0.23f);
        public static readonly Color WoodDark = new Color(0.34f, 0.22f, 0.15f);
        public static readonly Color Steel = new Color(0.64f, 0.66f, 0.7f);
        public static readonly Color Accent = new Color(1f, 0.82f, 0.4f);
        public static readonly Color Safe = new Color(0.5f, 0.86f, 0.52f);
        public static readonly Color Danger = new Color(0.86f, 0.3f, 0.24f);

        // ------------------------------------------------------------------ Yardımcılar

        public static Color Shade(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);

        delegate void Draw(Canvas c);

        // Basit çizim tuvali: sol alt (0,0)
        sealed class Canvas
        {
            public readonly int W, H;
            public readonly Color[] P;
            public readonly System.Random R;

            public Canvas(int w, int h, int seed)
            {
                W = w; H = h; P = new Color[w * h]; R = new System.Random(seed);
            }

            public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
            public void Set(int x, int y, Color c) { if (In(x, y)) P[y * W + x] = c; }
            public Color Get(int x, int y) => In(x, y) ? P[y * W + x] : Color.clear;

            public void Rect(int x0, int y0, int x1, int y1, Color c)
            {
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(x, y, c);
            }

            public void Frame(int x0, int y0, int x1, int y1, Color c)
            {
                for (int x = x0; x <= x1; x++) { Set(x, y0, c); Set(x, y1, c); }
                for (int y = y0; y <= y1; y++) { Set(x0, y, c); Set(x1, y, c); }
            }

            // Üç tonlu kutu: dolgu, üst-sol ışık, alt-sağ gölge (kalınlık b), dışta kontur; köşeler kırpık
            public void Box(int x0, int y0, int x1, int y1, Color baseColor, int b = 1, bool outline = true, bool round = true)
            {
                Rect(x0, y0, x1, y1, baseColor);
                for (int i = 1; i <= b; i++)
                {
                    for (int x = x0 + i; x <= x1 - i; x++) { Set(x, y1 - i, Shade(baseColor, 1.22f)); Set(x, y0 + i, Shade(baseColor, 0.68f)); }
                    for (int y = y0 + i; y <= y1 - i; y++) { Set(x0 + i, y, Shade(baseColor, 1.12f)); Set(x1 - i, y, Shade(baseColor, 0.78f)); }
                }
                if (outline) Frame(x0, y0, x1, y1, Outline);
                if (round) { Set(x0, y0, Color.clear); Set(x1, y0, Color.clear); Set(x0, y1, Color.clear); Set(x1, y1, Color.clear); }
            }

            // Silindir: yön boyunca bantlı gölge (yatayda yukarıdan aşağı ışık → koyu)
            public void Cylinder(int x0, int y0, int x1, int y1, Color baseColor, bool horizontal)
            {
                int span = horizontal ? y1 - y0 : x1 - x0;
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float t = span <= 0 ? 0.5f : (horizontal ? (float)(y - y0) / span : 1f - (float)(x - x0) / span);
                    float k = t > 0.78f ? 1.3f : t > 0.55f ? 1.1f : t > 0.25f ? 0.95f : 0.72f;
                    Set(x, y, Shade(baseColor, k));
                }
                Frame(x0, y0, x1, y1, Outline);
            }

            public void Disc(float cx, float cy, float r, Color baseColor, bool shaded = true)
            {
                for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
                for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > r) continue;
                    Color c = baseColor;
                    if (shaded) c = d > r - 1.2f ? Outline : (dx - dy) < -r * 0.6f ? Shade(baseColor, 1.25f) : (dx - dy) > r * 0.5f ? Shade(baseColor, 0.7f) : baseColor;
                    Set(x, y, c);
                }
            }

            public void Noise(float amount, float strength = 0.06f)
            {
                for (int i = 0; i < P.Length; i++)
                    if (P[i].a > 0f && R.NextDouble() < amount) P[i] = Shade(P[i], R.NextDouble() < 0.5 ? 1f - strength : 1f + strength);
            }

            // Kenarlara doğru koyulaşan ortam gölgesi (sahne çerçeve içinde derin dursun)
            public void Vignette(float strength)
            {
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float ex = Mathf.Min(x, W - 1 - x) / (W * 0.5f), ey = Mathf.Min(y, H - 1 - y) / (H * 0.5f);
                    float e = Mathf.Clamp01(Mathf.Min(ex, ey) * 3f);
                    int i = y * W + x;
                    if (P[i].a > 0f) P[i] = Shade(P[i], Mathf.Lerp(1f - strength, 1f, e));
                }
            }
        }

        static Sprite Make(string key, int w, int h, Draw draw, int seed = 1)
        {
            if (cache.TryGetValue(key, out var cached) && cached) return cached;
            w = Mathf.Max(2, w);
            h = Mathf.Max(2, h);
            var canvas = new Canvas(w, h, seed);
            draw(canvas);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Minigame_" + key,
            };
            texture.SetPixels(canvas.P);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            cache[key] = sprite;
            return sprite;
        }

        // ------------------------------------------------------------------ Arka planlar

        // M28 (Mehmet'in play testi 19: zeminler çok detaylı ve renkliydi, oyundan kopuktu): sade, koyu, iki tonlu düzlük,
        // büyük net pikseller (1 doku pikseli = BigPixel referans pikseli), gürültü yok. Eşya ve hedef öne çıkar.
        public const int BigPixel = 2;

        // kind: "wood" (vida: dolabın arkası, seyrek tahta araları), "door" (kilit: tek iç panel çizgisi ve topuz),
        // "room" (taşı-yerleştir: soğuk koyu duvar, altta zemin bandı). w, h referans pikseli
        public static Sprite Ground(int w, int h, string kind) => Make($"ground_{kind}_{w}x{h}", Mathf.Max(2, w / BigPixel), Mathf.Max(2, h / BigPixel), c =>
        {
            Color baseColor, line;
            switch (kind)
            {
                case "room": baseColor = new Color(0.13f, 0.14f, 0.18f); line = new Color(0.17f, 0.18f, 0.23f); break;
                default: baseColor = new Color(0.2f, 0.15f, 0.12f); line = new Color(0.25f, 0.19f, 0.15f); break;
            }
            c.Rect(0, 0, c.W - 1, c.H - 1, baseColor);
            switch (kind)
            {
                case "wood":
                    for (int y = 12; y < c.H; y += 14) for (int x = 0; x < c.W; x++) c.Set(x, y, line);
                    break;
                case "door":
                    c.Frame(8, 8, c.W - 9, c.H - 9, line);
                    c.Rect(c.W - 7, c.H / 2 - 1, c.W - 6, c.H / 2, new Color(0.45f, 0.37f, 0.22f));
                    break;
                case "room":
                    int floor = Mathf.Max(4, c.H / 9);
                    c.Rect(0, 0, c.W - 1, floor - 1, new Color(0.17f, 0.15f, 0.14f));
                    for (int x = 0; x < c.W; x++) c.Set(x, floor, line);
                    break;
            }
        });

        // Taşı-yerleştir engeli: düz koyu gri blok, solda bir büyük piksel ışık, sağda gölge, konturlu (w, h referans pikseli)
        public static Sprite Block(int w, int h) => Make($"block_{w}x{h}", Mathf.Max(2, w / BigPixel), Mathf.Max(2, h / BigPixel), c =>
        {
            var stone = new Color(0.36f, 0.34f, 0.38f);
            c.Rect(0, 0, c.W - 1, c.H - 1, stone);
            for (int y = 0; y < c.H; y++)
            {
                c.Set(1, y, Shade(stone, 1.25f));
                c.Set(c.W - 2, y, Shade(stone, 0.7f));
                c.Set(0, y, Outline);
                c.Set(c.W - 1, y, Outline);
            }
        });

        // ------------------------------------------------------------------ Vida

        // L demiri levhası: fırçalanmış metal, 2 px kenar cilası, köşelerde perçin, havşalı delikler (merkezden doku pikseli)
        public static Sprite Plate(int w, int h, IList<Vector2Int> holes, string key) => Make($"plate_{w}x{h}_{key}", w, h, c =>
        {
            c.Box(0, 0, c.W - 1, c.H - 1, Steel, 2);
            for (int y = 3; y < c.H - 3; y++)
                for (int x = 3; x < c.W - 3; x++)
                    if (c.R.NextDouble() < 0.35) c.Set(x, y, Shade(Steel, (y % 3 == 0) ? 0.94f : 1.03f));
            // Köşe perçinleri
            foreach (var (px, py) in new[] { (6, 6), (c.W - 7, 6), (6, c.H - 7), (c.W - 7, c.H - 7) }) c.Disc(px, py, 2.6f, Steel);
            foreach (var hole in holes)
            {
                float cx = c.W / 2f + hole.x, cy = c.H / 2f + hole.y;
                c.Disc(cx, cy, 13f, Shade(Steel, 0.85f), false);                   // havşa
                for (int y = -13; y <= 13; y++) for (int x = -13; x <= 13; x++)
                {
                    float d = Mathf.Sqrt(x * x + y * y);
                    if (d > 13f || d < 10.5f) continue;
                    c.Set((int)cx + x, (int)cy + y, Shade(Steel, x + y > 0 ? 1.2f : 0.6f));
                }
                c.Disc(cx, cy, 9f, new Color(0.14f, 0.12f, 0.12f), false);        // delik
            }
        });

        // Yıldız başlı vida: üç tonlu kubbe, kontur, iki piksel kalın yıldız yuva, yuvada ışık
        public static Sprite ScrewHead(int size) => Make($"screw_{size}", size, size, c =>
        {
            var metal = new Color(0.84f, 0.85f, 0.88f);
            float r = size * 0.5f - 0.5f;
            c.Disc(size * 0.5f, size * 0.5f, r, metal);
            // Kubbe parlaması
            c.Disc(size * 0.38f, size * 0.64f, size * 0.12f, Shade(metal, 1.2f), false);
            int m = size / 2, len = Mathf.RoundToInt(size * 0.3f), thick = Mathf.Max(1, size / 14);
            var slot = new Color(0.2f, 0.2f, 0.22f);
            c.Rect(m - len, m - thick, m + len - 1, m + thick - 1, slot);
            c.Rect(m - thick, m - len, m + thick - 1, m + len - 1, slot);
            for (int i = -len; i < len; i++) { c.Set(m + i, m - thick - 1, Shade(metal, 1.15f)); c.Set(m + thick, m + i, Shade(metal, 0.7f)); }
        });

        // ------------------------------------------------------------------ Kilit

        // Ray: metal çerçeve, içe gömük kanal (üstte gölge), çentikler
        public static Sprite Track(int w, int h) => Make($"track_{w}x{h}", w, h, c =>
        {
            c.Box(0, 0, c.W - 1, c.H - 1, Shade(Steel, 0.8f), 2);
            var groove = new Color(0.13f, 0.1f, 0.1f);
            c.Rect(3, 3, c.W - 4, c.H - 4, groove);
            for (int x = 3; x < c.W - 3; x++) { c.Set(x, c.H - 4, new Color(0.05f, 0.04f, 0.04f)); c.Set(x, 3, Shade(groove, 1.6f)); }
            for (int x = 8; x < c.W - 8; x += 12) { c.Set(x, 1, Shade(Steel, 0.45f)); c.Set(x, 2, Shade(Steel, 0.55f)); }
        });

        // Yeşil alan: çapraz tarama, parlak kenar, üstte ışık
        public static Sprite Zone(int w, int h) => Make($"zone_{w}x{h}", w, h, c =>
        {
            for (int y = 0; y < c.H; y++)
            for (int x = 0; x < c.W; x++) c.Set(x, y, (x + y) % 6 < 3 ? Safe : Shade(Safe, 0.8f));
            for (int x = 0; x < c.W; x++) { c.Set(x, c.H - 1, Shade(Safe, 1.3f)); c.Set(x, 0, Shade(Safe, 0.6f)); }
            for (int y = 0; y < c.H; y++) { c.Set(0, y, Shade(Safe, 1.35f)); c.Set(c.W - 1, y, Shade(Safe, 1.35f)); }
        });

        // İbre: aşağı bakan konturlu üçgen uç, iki tonlu gövde
        public static Sprite Needle(int w, int h) => Make($"needle_{w}x{h}", w, h, c =>
        {
            int m = c.W / 2, tip = Mathf.RoundToInt(c.H * 0.3f);
            for (int y = 0; y < c.H - tip; y++) { c.Set(m - 1, y, Accent); c.Set(m, y, Shade(Accent, 0.8f)); c.Set(m - 2, y, Outline); c.Set(m + 1, y, Outline); }
            for (int y = c.H - tip; y < c.H; y++)
            {
                int half = y - (c.H - tip);
                for (int x = m - 1 - half; x <= m + half; x++) c.Set(x, y, x < m ? Shade(Accent, 1.15f) : Accent);
                c.Set(m - 2 - half, y, Outline); c.Set(m + 1 + half, y, Outline);
            }
            for (int x = 0; x < c.W; x++) if (c.Get(x, c.H - 1).a > 0f) c.Set(x, c.H - 1, Outline);
        });

        // Mandal karşılığı: metal levha, dört vida, sürgü yuvası
        public static Sprite LatchPlate(int w, int h) => Make($"latch_{w}x{h}", w, h, c =>
        {
            c.Box(0, 0, c.W - 1, c.H - 1, Steel, 2);
            foreach (var (x, y) in new[] { (5, 5), (c.W - 6, 5), (5, c.H - 6), (c.W - 6, c.H - 6) }) c.Disc(x, y, 2.5f, Steel);
            int sy = c.H / 2 - 4;
            c.Rect(7, sy, c.W - 8, sy + 8, new Color(0.1f, 0.08f, 0.08f));
            for (int x = 7; x < c.W - 7; x++) c.Set(x, sy + 8, new Color(0.04f, 0.03f, 0.03f));
        });

        // Sürgü: yatay silindir, ucunda tutamak
        public static Sprite Bolt(int w, int h) => Make($"bolt_{w}x{h}", w, h, c =>
        {
            c.Cylinder(0, 1, c.W - 1, c.H - 2, Steel, true);
            int k = Mathf.RoundToInt(c.W * 0.18f);
            c.Box(k, 0, k + 6, c.H - 1, Accent, 1);
        });

        // ------------------------------------------------------------------ Taşı-yerleştir

        // Güvenli alan: kesikli 2 px çerçeve, köşe parantezleri, içe doğru solan yeşil
        public static Sprite SafeSpot(int size) => Make($"safe_{size}", size, size, c =>
        {
            for (int y = 2; y < size - 2; y++)
            for (int x = 2; x < size - 2; x++)
            {
                float e = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y)) / (size * 0.5f);
                c.Set(x, y, new Color(Safe.r, Safe.g, Safe.b, Mathf.Lerp(0.38f, 0.1f, e * 1.6f)));
            }
            for (int i = 0; i < size; i++)
            {
                if (i / 4 % 2 != 0) continue;
                for (int t = 0; t < 2; t++) { c.Set(i, t, Safe); c.Set(i, size - 1 - t, Safe); c.Set(t, i, Safe); c.Set(size - 1 - t, i, Safe); }
            }
            int k = size / 4;
            foreach (var (sx, sy) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
                for (int i = 0; i < k; i++)
                    for (int t = 0; t < 3; t++)
                    {
                        int x0 = sx == 0 ? i : size - 1 - i, y0 = sy == 0 ? t : size - 1 - t;
                        int x1 = sx == 0 ? t : size - 1 - t, y1 = sy == 0 ? i : size - 1 - i;
                        c.Set(x0, y0, Shade(Safe, 1.25f)); c.Set(x1, y1, Shade(Safe, 1.25f));
                    }
        });

        // Taşınan şeyin görünen boyutu (referans pikseli = doku pikseli); taşı-yerleştir çarpışması da bu dikdörtgen
        public static Vector2Int CarrySize(string kind)
        {
            switch (kind)
            {
                case "shelf": return new Vector2Int(50, 28);
                case "mirror": return new Vector2Int(30, 46);
                case "frame": return new Vector2Int(46, 36);
                case "firstaid": return new Vector2Int(42, 34);
                case "flashlight": return new Vector2Int(48, 16);
                case "water": return new Vector2Int(20, 44);
                case "whistle": return new Vector2Int(34, 22);
                default: return new Vector2Int(40, 40);
            }
        }

        // Taşınan şeyin çizimi, kendi boyutunda: kontur, üç ton, ayrıntı
        public static Sprite CarryItem(string kind)
        {
            var size = CarrySize(kind);
            return Make("item_" + kind, size.x, size.y, c =>
            {
                int W = c.W, H = c.H;
                switch (kind)
                {
                    case "shelf": // iki katlı ahşap raf, renkli kitap sırtları, bir kutu
                    {
                        c.Box(0, 0, W - 1, H - 1, Wood, 2);
                        c.Rect(3, 3, W - 4, H - 4, Shade(WoodDark, 0.8f));
                        c.Box(2, H / 2 - 1, W - 3, H / 2 + 1, Wood, 1, false, false);
                        var books = new[] { new Color(0.55f, 0.32f, 0.28f), new Color(0.34f, 0.46f, 0.44f), new Color(0.66f, 0.54f, 0.32f), new Color(0.36f, 0.4f, 0.52f), new Color(0.48f, 0.36f, 0.44f) };
                        for (int shelf = 0; shelf < 2; shelf++)
                        {
                            int y0 = shelf == 0 ? 4 : H / 2 + 2, y1 = shelf == 0 ? H / 2 - 2 : H - 5;
                            for (int x = 4, b = shelf * 2; x < W - 8; b++)
                            {
                                int bw = 3 + b % 2, top = y1 - (b * 7 % 3);
                                c.Box(x, y0, x + bw, top, books[b % books.Length], 1, true, false);
                                x += bw + 1;
                            }
                        }
                        c.Box(W - 10, 4, W - 4, H / 2 - 3, new Color(0.72f, 0.6f, 0.42f), 1);
                        break;
                    }
                    case "mirror": // ahşap çerçeve, mavi cam, iki parıltı çizgisi
                    {
                        c.Box(0, 0, W - 1, H - 1, Wood, 2);
                        var glass = new Color(0.58f, 0.74f, 0.82f);
                        for (int y = 5; y < H - 5; y++)
                            for (int x = 5; x < W - 5; x++) c.Set(x, y, Shade(glass, 0.85f + 0.3f * y / H));
                        c.Frame(4, 4, W - 5, H - 5, WoodDark);
                        for (int i = 0; i < 14; i++) { c.Set(8 + i / 2, H - 12 - i, new Color(0.95f, 0.98f, 1f)); c.Set(13 + i / 2, H - 12 - i, new Color(0.85f, 0.92f, 0.96f)); }
                        break;
                    }
                    case "frame": // yaldızlı çerçeve, gökyüzü, tepeler, güneş
                    {
                        var gold = new Color(0.78f, 0.62f, 0.3f);
                        c.Box(0, 0, W - 1, H - 1, gold, 3);
                        for (int y = 5; y < H - 5; y++)
                            for (int x = 5; x < W - 5; x++) c.Set(x, y, Color.Lerp(new Color(0.86f, 0.78f, 0.6f), new Color(0.5f, 0.68f, 0.84f), (float)(y - 5) / (H - 10)));
                        for (int x = 5; x < W - 5; x++)
                        {
                            int hill = 5 + (int)(7 + 4 * Mathf.Sin(x * 0.22f));
                            for (int y = 5; y < hill; y++) c.Set(x, y, y > hill - 2 ? new Color(0.52f, 0.66f, 0.4f) : new Color(0.4f, 0.54f, 0.32f));
                        }
                        c.Disc(W - 12, H - 11, 3.2f, new Color(1f, 0.88f, 0.5f), false);
                        c.Frame(4, 4, W - 5, H - 5, Shade(gold, 0.55f));
                        break;
                    }
                    case "firstaid": // kırmızı çanta, sap, kilitler, gölgeli beyaz haç
                    {
                        var red = new Color(0.8f, 0.2f, 0.17f);
                        c.Box(W / 2 - 8, H - 8, W / 2 + 7, H - 1, Shade(red, 0.7f), 1);
                        c.Rect(W / 2 - 5, H - 6, W / 2 + 4, H - 3, Color.clear);
                        c.Box(0, 0, W - 1, H - 6, red, 2);
                        foreach (int x in new[] { 6, W - 9 }) c.Box(x, H - 10, x + 3, H - 7, Steel, 1, true, false);
                        int cx = W / 2, cy = (H - 6) / 2;
                        c.Rect(cx - 2 + 1, cy - 8 - 1, cx + 2 + 1, cy + 8 - 1, Shade(red, 0.6f));
                        c.Rect(cx - 8 + 1, cy - 2 - 1, cx + 8 + 1, cy + 2 - 1, Shade(red, 0.6f));
                        c.Rect(cx - 2, cy - 8, cx + 2, cy + 8, Color.white);
                        c.Rect(cx - 8, cy - 2, cx + 8, cy + 2, Color.white);
                        break;
                    }
                    case "flashlight": // sarı gövde (silindir), tırtıklı tutma, düğme, gri kafa, parlayan cam
                    {
                        var yellow = new Color(0.96f, 0.76f, 0.22f);
                        c.Cylinder(0, 2, W - 15, H - 3, yellow, true);
                        for (int x = 4; x < W - 22; x += 3) for (int y = 3; y < H - 3; y++) c.Set(x, y, Shade(c.Get(x, y), 0.72f));
                        c.Box(W - 24, H - 4, W - 20, H - 1, Shade(Outline, 2.5f), 1, true, false);
                        c.Cylinder(W - 15, 0, W - 4, H - 1, new Color(0.6f, 0.61f, 0.64f), true);
                        c.Rect(W - 3, 1, W - 1, H - 2, new Color(1f, 0.96f, 0.72f));
                        c.Rect(W - 1, 1, W - 1, H - 2, Outline);
                        break;
                    }
                    case "water": // şişe: kapak, boyun, şeffaf mavi gövde, etiket, parlama
                    {
                        var blue = new Color(0.42f, 0.66f, 0.9f);
                        c.Box(W / 2 - 4, H - 6, W / 2 + 3, H - 1, Color.white, 1);
                        c.Box(W / 2 - 3, H - 11, W / 2 + 2, H - 6, Shade(blue, 1.1f), 1, true, false);
                        c.Cylinder(0, 0, W - 1, H - 11, blue, false);
                        c.Rect(1, H / 3, W - 2, H / 3 + 9, new Color(0.9f, 0.94f, 1f));
                        c.Rect(1, H / 3 + 4, W - 2, H / 3 + 5, new Color(0.36f, 0.56f, 0.82f));
                        for (int y = 3; y < H - 14; y++) c.Set(3, y, new Color(0.85f, 0.94f, 1f));
                        break;
                    }
                    case "whistle": // turuncu gövde, ağızlık, delik, halka
                    {
                        var orange = new Color(0.96f, 0.52f, 0.16f);
                        c.Disc(11f, 10f, 9.5f, orange);
                        c.Box(14, 8, W - 1, H - 4, orange, 1);
                        c.Rect(13, 9, 16, H - 6, orange);
                        c.Disc(10f, 11f, 2.6f, Outline, false);
                        c.Frame(1, H - 6, 6, H - 1, Shade(Steel, 0.8f));
                        break;
                    }
                    default: // karton kutu: kapak çizgisi, bant, ok işareti
                    {
                        var card = new Color(0.72f, 0.56f, 0.36f);
                        c.Box(0, 0, W - 1, H - 1, card, 2);
                        c.Rect(W / 2 - 3, 2, W / 2 + 2, H - 3, Shade(card, 1.18f));
                        for (int y = 2; y < H - 2; y++) c.Set(W / 2 + 3, y, Shade(card, 0.75f));
                        for (int x = 2; x < W - 2; x++) c.Set(x, H - 9, Shade(card, 0.8f));
                        for (int i = 0; i < 4; i++) { c.Set(8 + i, 10 + i, WoodDark); c.Set(8 - i, 10 + i, WoodDark); c.Set(8, 10 + i + 1, WoodDark); }
                        break;
                    }
                }
                c.Noise(0.04f, 0.05f);
            });
        }

        // Yumuşak gölge (eşyanın / levhanın altına; siyah, kenara doğru saydam)
        public static Sprite Shadow(int w, int h) => Make($"shadow_{w}x{h}", w, h, c =>
        {
            for (int y = 0; y < c.H; y++)
            for (int x = 0; x < c.W; x++)
            {
                float e = Mathf.Min(Mathf.Min(x, c.W - 1 - x), Mathf.Min(y, c.H - 1 - y)) / 4f;
                c.Set(x, y, new Color(0f, 0f, 0f, 0.35f * Mathf.Clamp01(e)));
            }
        });
    }
}
