using System.Collections.Generic;
using UnityEngine;

namespace Deprem.Minigames
{
    // Minigame'lerin kodla çizilen pixel art parçaları (sanatçı görseli yokken; Berke'nin play testi: minigame'ler
    // oyunun tarzına benzemiyordu). Oyunun paleti: sıcak ahşap, bej sıva, gri metal, koyu kontur.
    // 1 doku pikseli = 2 referans pikseli: çağıran Image boyutunu dokunun iki katı verir (Pixel(...) yardımcısı).
    // Aynı istek önbellekten döner.
    public static class MinigamePixels
    {
        public const float Scale = 2f;

        static readonly Color Outline = new Color(0.07f, 0.055f, 0.05f);
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static readonly Color Wood = new Color(0.52f, 0.36f, 0.23f);
        public static readonly Color WoodDark = new Color(0.36f, 0.24f, 0.16f);
        public static readonly Color Steel = new Color(0.62f, 0.64f, 0.68f);
        public static readonly Color Accent = new Color(1f, 0.82f, 0.4f);
        public static readonly Color Safe = new Color(0.5f, 0.86f, 0.52f);
        public static readonly Color Danger = new Color(0.86f, 0.3f, 0.24f);

        // ------------------------------------------------------------------ Yardımcılar

        static Color Shade(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);

        static Sprite Make(string key, int w, int h, System.Action<Color[], int, int, System.Random> draw, int seed = 1)
        {
            if (cache.TryGetValue(key, out var cached) && cached) return cached;
            w = Mathf.Max(2, w);
            h = Mathf.Max(2, h);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Minigame_" + key,
            };
            var pixels = new Color[w * h];
            draw(pixels, w, h, new System.Random(seed));
            texture.SetPixels(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            cache[key] = sprite;
            return sprite;
        }

        static void Set(Color[] p, int w, int h, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            p[y * w + x] = c;
        }

        static void Rect(Color[] p, int w, int h, int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++) Set(p, w, h, x, y, c);
        }

        static void Frame(Color[] p, int w, int h, int x0, int y0, int x1, int y1, Color c)
        {
            for (int x = x0; x <= x1; x++) { Set(p, w, h, x, y0, c); Set(p, w, h, x, y1, c); }
            for (int y = y0; y <= y1; y++) { Set(p, w, h, x0, y, c); Set(p, w, h, x1, y, c); }
        }

        // Kenarda 1 px koyu kontur, üst-solda ışık, alt-sağda gölge (oyunun mobilya dokularıyla aynı "cila")
        static void Bevel(Color[] p, int w, int h, int x0, int y0, int x1, int y1, Color baseColor)
        {
            for (int x = x0 + 1; x < x1; x++) { Set(p, w, h, x, y1 - 1, Shade(baseColor, 1.18f)); Set(p, w, h, x, y0 + 1, Shade(baseColor, 0.72f)); }
            for (int y = y0 + 1; y < y1; y++) { Set(p, w, h, x0 + 1, y, Shade(baseColor, 1.12f)); Set(p, w, h, x1 - 1, y, Shade(baseColor, 0.78f)); }
            Frame(p, w, h, x0, y0, x1, y1, Outline);
        }

        static void Noise(Color[] p, int w, int h, System.Random r, float amount)
        {
            for (int i = 0; i < p.Length; i++)
                if (p[i].a > 0f && r.NextDouble() < amount) p[i] = Shade(p[i], r.NextDouble() < 0.5 ? 0.93f : 1.06f);
        }

        // ------------------------------------------------------------------ Arka planlar

        // Dolabın arkası / dolap kapağı: yatay tahtalar, damar, tahta araları
        public static Sprite Boards(int w, int h, int seed) => Make($"boards_{w}x{h}_{seed}", w, h, (p, W, H, r) =>
        {
            int plank = 11;
            for (int y = 0; y < H; y++)
            {
                int row = y / plank;
                var tone = Shade(Wood, 0.9f + (row * 37 % 5) * 0.05f);
                for (int x = 0; x < W; x++) p[y * W + x] = y % plank == 0 ? WoodDark : tone;
            }
            for (int y = 3; y < H; y += r.Next(3, 6))
            {
                int x = r.Next(0, W);
                int length = r.Next(W / 5, W / 2);
                for (int i = 0; i < length; i++) Set(p, W, H, (x + i) % W, y, Shade(Wood, 0.75f));
            }
            Noise(p, W, H, r, 0.05f);
        }, seed);

        // Taşı-yerleştir zemini: bej sıva duvar, altta ahşap lambri ve süpürgelik
        public static Sprite Room(int w, int h) => Make($"room_{w}x{h}", w, h, (p, W, H, r) =>
        {
            var plaster = new Color(0.78f, 0.7f, 0.56f);
            int wainscot = H / 4;
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                Color c = y < wainscot ? (x % 12 == 0 ? WoodDark : Shade(Wood, 1.05f)) : plaster;
                if (y == wainscot || y == wainscot + 1) c = WoodDark;
                if (y < 3) c = Shade(WoodDark, 0.8f);
                p[y * W + x] = c;
            }
            // Sıvada hafif çatlak lekeleri
            for (int i = 0; i < 6; i++)
            {
                int x = r.Next(4, W - 4), y = r.Next(wainscot + 6, H - 4);
                for (int k = 0; k < 5; k++) Set(p, W, H, x + k, y + (k % 2), Shade(plaster, 0.86f));
            }
            Noise(p, W, H, r, 0.06f);
        });

        // Tuğla engel (taşı-yerleştir duvarı); boyuta göre üretilir
        public static Sprite Bricks(int w, int h) => Make($"bricks_{w}x{h}", w, h, (p, W, H, r) =>
        {
            var brick = new Color(0.62f, 0.34f, 0.26f);
            var mortar = new Color(0.72f, 0.66f, 0.58f);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int row = y / 4;
                int offset = row % 2 == 0 ? 0 : 3;
                bool joint = y % 4 == 0 || (x + offset) % 6 == 0;
                p[y * W + x] = joint ? mortar : Shade(brick, 0.9f + (row * 13 + (x + offset) / 6 * 7) % 4 * 0.05f);
            }
            Frame(p, W, H, 0, 0, W - 1, H - 1, Outline);
        });

        // ------------------------------------------------------------------ Vida

        // L demiri levhası: gri metal, kenar cilası, vida delikleri (doku pikseli konumları, merkezden)
        public static Sprite Plate(int w, int h, IList<Vector2Int> holes, string key) => Make($"plate_{w}x{h}_{key}", w, h, (p, W, H, r) =>
        {
            Rect(p, W, H, 0, 0, W - 1, H - 1, Steel);
            // Fırçalanmış metal: yatay ince çizgiler
            for (int y = 2; y < H - 2; y += 3) for (int x = 2; x < W - 2; x++) if (r.NextDouble() < 0.6) Set(p, W, H, x, y, Shade(Steel, 0.95f));
            Bevel(p, W, H, 0, 0, W - 1, H - 1, Steel);
            foreach (var hole in holes)
            {
                int cx = W / 2 + hole.x, cy = H / 2 + hole.y;
                for (int y = -5; y <= 5; y++)
                for (int x = -5; x <= 5; x++)
                    if (x * x + y * y <= 25) Set(p, W, H, cx + x, cy + y, Shade(Steel, x + y < 0 ? 0.55f : 0.75f));
            }
        });

        // Yıldız başlı vida (16 x 16): kontur, ışık, yıldız yuvası. Beyaz-gri; renk Image.color ile verilebilir
        public static Sprite ScrewHead() => Make("screw", 16, 16, (p, W, H, r) =>
        {
            var metal = new Color(0.86f, 0.87f, 0.9f);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float dx = x + 0.5f - 8f, dy = y + 0.5f - 8f, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 7.6f) continue;
                Color c = d > 6.6f ? Outline : (dx - dy < -4f ? Shade(metal, 1.1f) : dx - dy > 5f ? Shade(metal, 0.75f) : metal);
                p[y * W + x] = c;
            }
            var slot = new Color(0.25f, 0.26f, 0.28f);
            Rect(p, W, H, 4, 7, 11, 8, slot);
            Rect(p, W, H, 7, 4, 8, 11, slot);
        });

        // ------------------------------------------------------------------ Kilit

        // Ray: koyu içe gömük kanal, kenarda metal çerçeve
        public static Sprite Track(int w, int h) => Make($"track_{w}x{h}", w, h, (p, W, H, r) =>
        {
            Rect(p, W, H, 0, 0, W - 1, H - 1, new Color(0.16f, 0.13f, 0.12f));
            for (int x = 1; x < W - 1; x++) Set(p, W, H, x, H - 2, new Color(0.08f, 0.06f, 0.06f));
            Frame(p, W, H, 0, 0, W - 1, H - 1, Shade(Steel, 0.7f));
            for (int x = 4; x < W - 4; x += 8) Set(p, W, H, x, 1, Shade(Steel, 0.55f)); // çentikler
        });

        // Yeşil alan: parlak kenarlı, içi taramalı
        public static Sprite Zone(int w, int h) => Make($"zone_{w}x{h}", w, h, (p, W, H, r) =>
        {
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++) p[y * W + x] = (x + y) % 4 < 2 ? Safe : Shade(Safe, 0.82f);
            for (int y = 0; y < H; y++) { Set(p, W, H, 0, y, Shade(Safe, 1.25f)); Set(p, W, H, W - 1, y, Shade(Safe, 1.25f)); }
        });

        // İbre: aşağı bakan üçgen uç + ince gövde (6 x 18)
        public static Sprite Needle() => Make("needle", 7, 18, (p, W, H, r) =>
        {
            for (int y = 0; y < 14; y++) { Set(p, W, H, 3, y, Accent); }
            for (int y = 14; y < 18; y++)
            for (int x = 0; x < W; x++)
                if (Mathf.Abs(x - 3) <= y - 14) p[y * W + x] = Accent;
            for (int y = 0; y < 14; y++) { Set(p, W, H, 2, y, Outline); Set(p, W, H, 4, y, Outline); }
        });

        // Mandal gövdesi (karşılık) ve sürgü
        public static Sprite LatchPlate() => Make("latch_plate", 16, 22, (p, W, H, r) =>
        {
            Rect(p, W, H, 0, 0, W - 1, H - 1, Shade(Steel, 0.9f));
            Bevel(p, W, H, 0, 0, W - 1, H - 1, Steel);
            Rect(p, W, H, 4, 8, 11, 13, new Color(0.12f, 0.1f, 0.1f)); // sürgü yuvası
            Set(p, W, H, 3, 3, Outline); Set(p, W, H, 12, 3, Outline); Set(p, W, H, 3, 18, Outline); Set(p, W, H, 12, 18, Outline);
        });

        public static Sprite Bolt() => Make("bolt", 24, 6, (p, W, H, r) =>
        {
            Rect(p, W, H, 0, 0, W - 1, H - 1, Steel);
            Bevel(p, W, H, 0, 0, W - 1, H - 1, Steel);
            Rect(p, W, H, 3, 2, 6, 3, Accent); // tutamak
        });

        // Kapı yüzü: tahta + iç panel çerçevesi + topuz
        public static Sprite CabinetDoor(int w, int h) => Make($"door_{w}x{h}", w, h, (p, W, H, r) =>
        {
            var boards = Boards(W, H, 7).texture.GetPixels();
            System.Array.Copy(boards, p, p.Length);
            Frame(p, W, H, 8, 8, W - 9, H - 9, WoodDark);
            Frame(p, W, H, 9, 9, W - 10, H - 10, Shade(Wood, 1.2f));
            Bevel(p, W, H, 0, 0, W - 1, H - 1, Wood);
        });

        // ------------------------------------------------------------------ Taşı-yerleştir

        // Güvenli alan: kesikli çerçeve, köşelerde işaret, hafif dolgu
        public static Sprite SafeSpot(int size) => Make($"safe_{size}", size, size, (p, W, H, r) =>
        {
            var fill = new Color(Safe.r, Safe.g, Safe.b, 0.22f);
            Rect(p, W, H, 1, 1, W - 2, H - 2, fill);
            for (int i = 0; i < W; i++)
            {
                if (i / 3 % 2 != 0) continue;
                Set(p, W, H, i, 0, Safe); Set(p, W, H, i, H - 1, Safe); Set(p, W, H, 0, i, Safe); Set(p, W, H, W - 1, i, Safe);
            }
            foreach (var (cx, cy) in new[] { (0, 0), (W - 4, 0), (0, H - 4), (W - 4, H - 4) }) Rect(p, W, H, cx, cy, cx + 3, cy + 3, Shade(Safe, 1.2f));
        });

        // Taşınan eşyanın küçük çizimi (24 x 24): raf, ayna, çerçeve ya da kutu
        public static Sprite CarryItem(string kind) => Make("item_" + kind, 24, 24, (p, W, H, r) =>
        {
            switch (kind)
            {
                case "shelf": // iki katlı raf, kitaplar
                    Rect(p, W, H, 1, 3, 22, 20, Shade(Wood, 0.8f));
                    Bevel(p, W, H, 1, 3, 22, 20, Wood);
                    Rect(p, W, H, 2, 11, 21, 12, WoodDark);
                    var books = new[] { new Color(0.55f, 0.36f, 0.32f), new Color(0.36f, 0.45f, 0.43f), new Color(0.62f, 0.52f, 0.34f), new Color(0.37f, 0.4f, 0.48f) };
                    for (int x = 3, i = 0; x < 20; x += 3, i++) { Rect(p, W, H, x, 13, x + 1, 18 - i % 3, books[i % 4]); Rect(p, W, H, x, 5, x + 1, 9 - (i + 1) % 2, books[(i + 2) % 4]); }
                    break;
                case "mirror": // ahşap çerçeve, açık mavi cam, parıltı
                    Rect(p, W, H, 4, 1, 19, 22, Wood);
                    Bevel(p, W, H, 4, 1, 19, 22, Wood);
                    Rect(p, W, H, 7, 4, 16, 19, new Color(0.62f, 0.76f, 0.82f));
                    for (int i = 0; i < 6; i++) Set(p, W, H, 9 + i, 12 + i, new Color(0.92f, 0.97f, 1f));
                    break;
                case "frame": // resim çerçevesi: gökyüzü, tepe
                    Rect(p, W, H, 1, 4, 22, 19, new Color(0.7f, 0.56f, 0.3f));
                    Bevel(p, W, H, 1, 4, 22, 19, new Color(0.7f, 0.56f, 0.3f));
                    Rect(p, W, H, 4, 7, 19, 16, new Color(0.56f, 0.72f, 0.82f));
                    for (int x = 4; x <= 19; x++) Rect(p, W, H, x, 7, x, 7 + (int)(3 + 2 * Mathf.Sin(x * 0.5f)), new Color(0.45f, 0.58f, 0.36f));
                    Rect(p, W, H, 15, 13, 16, 14, new Color(1f, 0.9f, 0.5f));
                    break;
                default: // karton kutu
                    var card = new Color(0.72f, 0.56f, 0.36f);
                    Rect(p, W, H, 2, 3, 21, 20, card);
                    Bevel(p, W, H, 2, 3, 21, 20, card);
                    Rect(p, W, H, 10, 3, 13, 20, Shade(card, 1.15f));
                    break;
            }
            Noise(p, W, H, r, 0.04f);
        });
    }
}
