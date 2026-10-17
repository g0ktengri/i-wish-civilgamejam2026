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
            // Salonun paletine yaklaştır: yer tutucu eşya renkleri (yeşil, mavi...) sıcak duvarların yanında sırıtıyordu
            float blend = style == Style.Wood ? 0.5f : style == Style.Fabric ? 0.3f : 0.12f;
            baseColor = Color.Lerp(baseColor, PaletteTint(), blend);
            var texture = Generate(w, h, style, baseColor, StableHash(key + face), face == "ust");
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
            if (key.Contains("kanepe") || key.Contains("yatak") || key.Contains("yatagi")) return Style.Fabric;
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
