using System.IO;
using UnityEditor;
using UnityEngine;

namespace Deprem.HouseBuilder
{
    /// <summary>
    /// Sanatçı dokusu olmayan mobilya ve eşya yüzleri için kodla üretilen pixel art dokular (Berke'nin play testi:
    /// düz renkli kutular salonun pixel art duvarlarının yanında sırıtıyordu). 64 px = 1 m, yani yüzün gerçek
    /// boyutunda; nokta filtre. Tür anahtardan seçilir: ahşap (yatay damar), kumaş (dokuma), metal/plastik (panel).
    /// Kenarda 1 px koyu çerçeve, üstte 1 px ışık, altta 2 px gölge: salonun çizimleriyle aynı "cila".
    /// Assets/Generated/House/Textures altına PNG olarak yazılır (HouseBuilder her kurulumda yeniler).
    /// </summary>
    internal static class PixelFurnitureTextures
    {
        private const string Folder = "Assets/Generated/House/Textures";
        private const int PixelsPerMeter = 64;
        private const int MaxSize = 256;

        private enum Style { Wood, Fabric, Smooth }

        /// <summary>Yüz için doku (yoksa üretir). face: "on", "yan", "ust"</summary>
        public static Texture2D Get(string key, string face, Vector2 sizeMeters, Color baseColor)
        {
            int w = Mathf.Clamp(Mathf.RoundToInt(sizeMeters.x * PixelsPerMeter), 4, MaxSize);
            int h = Mathf.Clamp(Mathf.RoundToInt(sizeMeters.y * PixelsPerMeter), 4, MaxSize);
            string path = $"{Folder}/{key}_{face}.png";
            EnsureFolder(Folder);

            var style = StyleFor(key);
            // Berke'nin play testi: renkler hâlâ absürttü. Yer tutucu renk yerine sınırlı, sönük bir paletten seçilir
            // (salonun bej duvarı ve koyu ahşap zeminiyle uyumlu). Kabin ayırt edilsin diye kendi renginde kalır.
            baseColor = key.Contains("kabin")
                ? Color.Lerp(baseColor, PaletteTint(), 0.25f)
                : PaletteFor(style)[Mathf.Abs(StableHash(key)) % PaletteFor(style).Length];
            var texture = Generate(w, h, style, baseColor, StableHash(key + face), face == "ust");
            return Save(path, texture);
        }

        // Kitap sırtları için sönük ama çeşitli renkler: aynı değer aralığında, birbirinden fazla kopmaz
        private static readonly Color[] BookPalette =
        {
            new Color(0.55f, 0.36f, 0.32f), // kiremit
            new Color(0.36f, 0.45f, 0.43f), // petrol
            new Color(0.62f, 0.52f, 0.34f), // aşı
            new Color(0.45f, 0.47f, 0.34f), // zeytin
            new Color(0.37f, 0.4f, 0.48f),  // gece mavisi
            new Color(0.7f, 0.65f, 0.54f),  // krem
            new Color(0.47f, 0.37f, 0.42f), // mürdüm
            new Color(0.5f, 0.38f, 0.28f),  // deri
        };

        /// <summary>
        /// Kitap sırtları: dikey 3–7 px'lik sırtlar, aralarında 1 px koyu boşluk, solda 1 px ışık, bazı sırtlarda
        /// etiket bandı. Eşyanın ön yüzü boyutunda (64 px/m); kitap kutuları dokunun kendi bölgesini gösterir.
        /// </summary>
        public static Texture2D Books(string key, Vector2 sizeMeters)
        {
            int w = Mathf.Clamp(Mathf.RoundToInt(sizeMeters.x * PixelsPerMeter), 4, MaxSize);
            int h = Mathf.Clamp(Mathf.RoundToInt(sizeMeters.y * PixelsPerMeter), 4, MaxSize);
            string path = $"{Folder}/{key}_kitap.png";
            EnsureFolder(Folder);

            var random = new System.Random(StableHash(key + "kitap"));
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            int x = 0;
            while (x < w)
            {
                int width = random.Next(3, 8);
                var color = BookPalette[random.Next(BookPalette.Length)];
                color = Shade(color, 0.92f + (float)random.NextDouble() * 0.12f);
                // Etiket bandı: sırtın 1/3'ünde, yüksekliği dokunun her ~0,45 m'sinde bir (rafların kitap boyu)
                bool label = random.NextDouble() < 0.45;
                int labelOffset = random.Next(6, 14);
                for (int i = 0; i < width && x + i < w; i++)
                for (int y = 0; y < h; y++)
                {
                    Color c = color;
                    if (i == 0) c = Shade(color, 0.6f);             // kitaplar arası boşluk
                    else if (i == 1) c = Shade(color, 1.1f);        // sırtın ışık alan kenarı
                    if (label && i > 1 && i < width - 1 && (y + labelOffset) % 29 < 3) c = Shade(color, 1.22f);
                    if (random.NextDouble() < 0.03) c = Shade(c, 0.94f);
                    c.a = 1f;
                    pixels[y * w + x + i] = c;
                }
                x += width;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Save(path, texture);
        }

        private static Texture2D Save(string path, Texture2D texture)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static readonly Color[] WoodPalette =
        {
            new Color(0.36f, 0.24f, 0.17f), // ceviz
            new Color(0.52f, 0.36f, 0.23f), // meşe
            new Color(0.6f, 0.46f, 0.32f),  // çam
        };
        private static readonly Color[] FabricPalette =
        {
            // Play testi 7: hardal ve gül kurusu hâlâ sırıtıyordu; hepsi bej-kahve ailesinde, düşük doygunluk
            new Color(0.56f, 0.5f, 0.42f),  // vizon
            new Color(0.5f, 0.47f, 0.4f),   // haki gri
            new Color(0.53f, 0.45f, 0.39f), // mantar
        };
        private static readonly Color[] SmoothPalette =
        {
            new Color(0.76f, 0.74f, 0.68f), // kırık beyaz
            new Color(0.5f, 0.48f, 0.45f),  // sıcak gri
        };

        private static Color[] PaletteFor(Style style) =>
            style == Style.Wood ? WoodPalette : style == Style.Fabric ? FabricPalette : SmoothPalette;

        private static Color? paletteTint;

        // Salon duvar çiziminin ortalama renginden koyu, sıcak bir ahşap tonu (çizim yoksa sabit kahve)
        private static Color PaletteTint()
        {
            if (paletteTint.HasValue) return paletteTint.Value;
            var tint = new Color(0.5f, 0.34f, 0.22f);
            const string wall = "Assets/Art/Rooms/salon/duvar.png";
            var importer = AssetImporter.GetAtPath(wall) as TextureImporter;
            if (importer != null)
            {
                bool readable = importer.isReadable;
                if (!readable) { importer.isReadable = true; importer.SaveAndReimport(); }
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(wall);
                var pixels = texture.GetPixels32();
                long r = 0, g = 0, b = 0;
                for (int i = 0; i < pixels.Length; i += 7) { r += pixels[i].r; g += pixels[i].g; b += pixels[i].b; }
                int n = (pixels.Length + 6) / 7;
                var average = new Color(r / (255f * n), g / (255f * n), b / (255f * n));
                tint = new Color(average.r * 0.62f, average.g * 0.5f, average.b * 0.42f);
                if (!readable) { importer.isReadable = false; importer.SaveAndReimport(); }
            }
            paletteTint = tint;
            return tint;
        }

        // string.GetHashCode her çalıştırmada aynı olmayabilir: dokular her kurulumda aynı çıksın
        private static int StableHash(string text)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in text) hash = hash * 31 + c;
                return hash;
            }
        }

        private static Style StyleFor(string key)
        {
            if (key.Contains("kanepe")) return Style.Fabric; // yatakların çerçevesi ahşap, şilte detay tonu
            if (key.Contains("buzdolabi") || key == "tv" || key.Contains("kabin") || key.Contains("ayna")) return Style.Smooth;
            return Style.Wood;
        }

        private static Texture2D Generate(int w, int h, Style style, Color baseColor, int seed, bool top)
        {
            var random = new System.Random(seed);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];

            Color dark = Shade(baseColor, 0.72f);
            Color darker = Shade(baseColor, 0.55f);
            Color light = Shade(baseColor, 1.12f);

            // Ahşap damarları: her satır grubu için rastgele uzunlukta koyu çizgiler
            var grain = new bool[w, h];
            if (style == Style.Wood)
            {
                for (int y = 3; y < h - 3; y += random.Next(3, 6))
                {
                    int x = random.Next(0, w);
                    while (x < w)
                    {
                        int length = random.Next(w / 4 + 2, w / 2 + 4);
                        for (int i = 0; i < length && x + i < w; i++) grain[x + i, y] = true;
                        x += length + random.Next(4, 12);
                    }
                }
            }

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = baseColor;
                switch (style)
                {
                    case Style.Wood:
                        if (grain[x, y]) c = dark;
                        break;
                    case Style.Fabric:
                        // Dokuma: dama tahtası gibi hafif ton farkı
                        if (((x / 2) + (y / 2)) % 2 == 0) c = Shade(baseColor, 0.94f);
                        break;
                    case Style.Smooth:
                        // Yatay panel çizgisi ortada
                        if (h > 24 && y == h / 2) c = dark;
                        break;
                }
                // Hafif pixel gürültüsü
                if (random.NextDouble() < 0.05) c = Shade(c, random.NextDouble() < 0.5 ? 0.92f : 1.06f);

                // Cila: altta 2 px gölge, üstte 1 px ışık, kenarda 1 px koyu çerçeve
                if (!top && y <= 1) c = Shade(c, 0.8f);
                if (y == h - 2) c = light;
                if (x == 0 || y == 0 || x == w - 1 || y == h - 1) c = darker;

                c.a = 1f;
                pixels[y * w + x] = c;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Color Shade(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
