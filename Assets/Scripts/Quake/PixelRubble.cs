using System.Collections.Generic;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Kodla üretilen pixel art moloz dokuları (Berke'nin play testi: düşen parçalar düz kare kutulardı).
    /// Düzensiz kenarlı parça, 1 piksel koyu kontur, üstü açık / altı koyu iki ton sıva ve birkaç çatlak pikseli.
    /// Sanatçı dokusu gelince (Assets/Art/Particles/siva.png) bunun yerine o kullanılabilir.
    /// </summary>
    public static class PixelRubble
    {
        public const int ChunkSize = 16;
        public const int AtlasTiles = 4;

        static readonly Color Outline = new Color(0.22f, 0.19f, 0.17f);
        static readonly Color Light = new Color(0.86f, 0.82f, 0.74f);
        static readonly Color Mid = new Color(0.72f, 0.68f, 0.6f);
        static readonly Color Dark = new Color(0.55f, 0.51f, 0.45f);

        static readonly List<Texture2D> chunks = new List<Texture2D>();
        static Texture2D atlas;
        static readonly Dictionary<int, Texture2D> slabs = new Dictionary<int, Texture2D>();

        /// <summary>Tek moloz parçası (16 x 16), değişkenler önbellekte</summary>
        public static Texture2D Chunk(int variant)
        {
            while (chunks.Count < AtlasTiles) chunks.Add(Generate(ChunkSize, ChunkSize, 1000 + chunks.Count * 37, 0.42f));
            return chunks[Mathf.Abs(variant) % AtlasTiles];
        }

        /// <summary>Parçacıklar için yan yana 4 parça (64 x 16): texture sheet animation ile rastgele seçilir</summary>
        public static Texture2D Atlas()
        {
            if (atlas != null) return atlas;
            atlas = NewTexture(ChunkSize * AtlasTiles, ChunkSize, "PixelRubbleAtlas");
            for (int i = 0; i < AtlasTiles; i++)
            {
                var chunk = Chunk(i);
                atlas.SetPixels(i * ChunkSize, 0, ChunkSize, ChunkSize, chunk.GetPixels());
            }
            atlas.Apply();
            return atlas;
        }

        /// <summary>Tavandan kopan geniş beton / sıva levhası (en x boy piksel; 64 px = 1 m)</summary>
        public static Texture2D Slab(int widthPx, int heightPx)
        {
            widthPx = Mathf.Clamp(widthPx, 8, 128);
            heightPx = Mathf.Clamp(heightPx, 4, 32);
            int key = widthPx * 1000 + heightPx;
            if (!slabs.TryGetValue(key, out var slab))
            {
                slab = Generate(widthPx, heightPx, key, 0.5f);
                slabs[key] = slab;
            }
            return slab;
        }

        static Texture2D Generate(int w, int h, int seed, float roughness)
        {
            var random = new System.Random(seed);
            var texture = NewTexture(w, h, "PixelRubble");
            var filled = new bool[w, h];

            // Düzensiz kenar: her sütun ve satırda kenardan rastgele 0-2 piksel içeri
            var top = new int[w];
            var bottom = new int[w];
            for (int x = 0; x < w; x++)
            {
                top[x] = random.NextDouble() < roughness ? random.Next(1, 3) : 0;
                bottom[x] = random.NextDouble() < roughness ? random.Next(1, 3) : 0;
            }
            var left = new int[h];
            var right = new int[h];
            for (int y = 0; y < h; y++)
            {
                left[y] = random.NextDouble() < roughness ? random.Next(1, 3) : 0;
                right[y] = random.NextDouble() < roughness ? random.Next(1, 3) : 0;
            }
            // Köşeler yuvarlansın
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                bool inside = y >= bottom[x] && y < h - top[x] && x >= left[y] && x < w - right[y];
                bool corner = (x < 2 || x > w - 3) && (y < 2 || y > h - 3) && (x + y) % 3 != 0;
                filled[x, y] = inside && !corner;
            }

            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (!filled[x, y])
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }
                bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1 ||
                            !filled[x - 1, y] || !filled[x + 1, y] || !filled[x, y - 1] || !filled[x, y + 1];
                Color color;
                if (edge) color = Outline;
                else if (y >= h * 0.6f) color = Light;      // üstü ışık alır
                else if (y <= h * 0.3f) color = Dark;       // altı gölgede
                else color = Mid;
                if (!edge && random.NextDouble() < 0.06) color = Dark; // çatlak / gözenek
                texture.SetPixel(x, y, color);
            }
            texture.Apply();
            return texture;
        }

        static Texture2D NewTexture(int w, int h, string name) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
            };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            chunks.Clear();
            slabs.Clear();
            atlas = null;
        }
    }
}
