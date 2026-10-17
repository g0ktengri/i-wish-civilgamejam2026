using System.Collections.Generic;
using UnityEngine;

namespace Deprem.UI
{
    // Menü satırlarının 11 x 11 pixel simgeleri (arayüz turu): '#' dolu piksel, çevresine 1 px koyu kontur eklenir
    // (13 x 13 doku). Beyaz çizilir; renk Image.color'dan (seçili satırda vurgu rengi).
    public static class UiIcons
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        static readonly Dictionary<string, string[]> maps = new Dictionary<string, string[]>
        {
            ["house"] = new[]
            {
                ".....#.....", "....###....", "...#####...", "..#######..", ".#########.", "..#######..",
                "..##...##..", "..##.#.##..", "..##.#.##..", "..##.#.##..", "..#######..",
            },
            ["clock"] = new[]
            {
                "...#####...", "..#.....#..", ".#...#...#.", "#....#....#", "#....#....#", "#....###..#",
                "#.........#", "#.........#", ".#.......#.", "..#.....#..", "...#####...",
            },
            ["gear"] = new[]
            {
                ".....#.....", "..#.###.#..", "...#####...", ".###...###.", "..##...##..", "###.....###",
                "..##...##..", ".###...###.", "...#####...", "..#.###.#..", ".....#.....",
            },
            ["key"] = new[]
            {
                "...........", ".#########.", ".#.......#.", ".#.#.#.#.#.", ".#.......#.", ".#.#####.#.",
                ".#.......#.", ".#########.", "...........", "...........", "...........",
            },
            ["heart"] = new[]
            {
                "...........", "..##...##..", ".####.####.", "###########", "###########", ".#########.",
                "..#######..", "...#####...", "....###....", ".....#.....", "...........",
            },
            ["globe"] = new[]
            {
                "...#####...", "..#..#..#..", ".#...#...#.", "#.#######.#", "#....#....#", "#....#....#",
                "#.#######.#", ".#...#...#.", "..#..#..#..", "...#####...", "...........",
            },
            ["door"] = new[]
            {
                "..#######..", "..#.....#..", "..#.....#..", "..#.....#..", "..#...#.#..", "..#.....#..",
                "..#.....#..", "..#.....#..", "..#######..", "...........", "...........",
            },
            ["play"] = new[]
            {
                "...........", "..#........", "..###......", "..#####....", "..#######..", "..#####....",
                "..###......", "..#........", "...........", "...........", "...........",
            },
            ["back"] = new[]
            {
                "...........", "....#......", "...##......", "..########.", ".#########.", "..########.",
                "...##......", "....#......", "...........", "...........", "...........",
            },
            ["speaker"] = new[]
            {
                "...........", "....#......", "...##..#...", "####.#..#..", "####.#.#.#.", "####.#.#.#.",
                "####.#..#..", "...##..#...", "....#......", "...........", "...........",
            },
            ["music"] = new[]
            {
                "....#####..", "....#...#..", "....#...#..", "....#...#..", "....#...#..", "..###.###..",
                ".####.####.", "..##...##..", "...........", "...........", "...........",
            },
            ["shake"] = new[]
            {
                "...........", "#..#..#..#.", ".#..#..#..#", "#..#..#..#.", "...........", "#..#..#..#.",
                ".#..#..#..#", "#..#..#..#.", "...........", "...........", "...........",
            },
            ["screen"] = new[]
            {
                "###########", "#.........#", "#.........#", "#.........#", "#.........#", "###########",
                "....###....", "..#######..", "...........", "...........", "...........",
            },
        };

        public static Sprite House => Get("house");
        public static Sprite Clock => Get("clock");
        public static Sprite Gear => Get("gear");
        public static Sprite Key => Get("key");
        public static Sprite Heart => Get("heart");
        public static Sprite Globe => Get("globe");
        public static Sprite Door => Get("door");
        public static Sprite Play => Get("play");
        public static Sprite Back => Get("back");
        public static Sprite Speaker => Get("speaker");
        public static Sprite Music => Get("music");
        public static Sprite Shake => Get("shake");
        public static Sprite Screen => Get("screen");

        static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var cached) && cached) return cached;
            var map = maps[name];
            const int n = 11, size = n + 2;
            bool Filled(int x, int y)
            {
                // Haritada satır 0 üstte; dokuda y = 0 altta
                int row = n - 1 - y;
                return x >= 0 && x < n && row >= 0 && row < n && map[row][x] == '#';
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "UiIcon_" + name,
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int mx = x - 1, my = y - 1;
                Color c = Color.clear;
                if (Filled(mx, my)) c = Color.white;
                else
                    for (int dy = -1; dy <= 1 && c.a == 0f; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (Filled(mx + dx, my + dy)) { c = new Color(0.08f, 0.06f, 0.05f, 0.9f); break; }
                texture.SetPixel(x, y, c);
            }
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            cache[name] = sprite;
            return sprite;
        }
    }
}
