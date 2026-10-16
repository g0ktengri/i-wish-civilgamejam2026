using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Deprem.HouseBuilder
{
    /// <summary>
    /// Sanatçılar için boş, kılavuz çizgili şablon PNG'ler (GDD: "Claude her asset için doğru boyutta şablon üretebilir").
    /// Boyutlar ev üreticinin ölçülerinden gelir; ev değişince yeniden üretilir. Çıktı: proje kökünde Sablonlar/
    /// (Unity'nin dışında). Klasör düzeni Assets/Art ile aynı; çizilen dosya aynı yolla Assets/Art altına konur.
    /// </summary>
    internal static class ArtTemplates
    {
        private const int Ppu = 64;

        private static readonly Color Background = new Color(0.86f, 0.86f, 0.86f, 1f);
        private static readonly Color MinorLine = new Color(0.78f, 0.78f, 0.78f, 1f);
        private static readonly Color MeterLine = new Color(0.58f, 0.6f, 0.66f, 1f);
        private static readonly Color Border = new Color(0.85f, 0.25f, 0.25f, 1f);
        private static readonly Color NoDetail = new Color(0.95f, 0.45f, 0.4f, 1f);
        private static readonly Color Separate = new Color(0.45f, 0.6f, 0.95f, 1f);
        private static readonly Color Guide = new Color(0.2f, 0.65f, 0.3f, 1f);

        private static string root;
        private static readonly List<string> Written = new List<string>();

        [MenuItem("Tools/Deprem/Sanatçı Şablonlarını Üret")]
        public static void Generate()
        {
            root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Sablonlar"));
            Written.Clear();

            Rooms();
            Items();
            Characters();
            Others();

            Debug.Log($"[ArtTemplates] {Written.Count} şablon üretildi: {root}");
        }

        // ------------------------------------------------------------------ Odalar

        private static void Rooms()
        {
            foreach (var room in HouseLayout.Rooms)
            {
                string folder = $"Rooms/{room.Key}";
                int w = Px(room.Width), d = Px(room.Depth), h = Px(room.Height);

                Save($"{folder}/arka.png", w, h, tex =>
                {
                    if (room.Key == "salon") // pencere ayrı görsel olarak konur (HouseBuilder.BuildWindow)
                        Hatch(tex, Px(HouseDims.SalonWindowX - 0.75f - room.X0), Px(1f), Px(1.5f), Px(1f), Separate);
                });

                bool hasDoor = HouseLayout.HasNeighbor(room, false) || HouseLayout.HasNeighbor(room, true);
                Save($"{folder}/yan.png", d, h, tex =>
                {
                    // Sol kenar = odanın ön tarafı. Kapı kodla oyulur, buraya detay çizilmez.
                    if (hasDoor) Hatch(tex, Px(HouseDims.DoorZ0), 0, Px(HouseDims.DoorZ1 - HouseDims.DoorZ0), Px(HouseDims.DoorHeight), NoDetail);
                    VLine(tex, 0, Guide, 2);
                });

                // Zemin ve tavan: üst kenar = arka duvar, alt kenar = ön (kesit)
                Save($"{folder}/zemin.png", w, d, tex =>
                {
                    if (room.Key == "sahanlik") StairHole(tex, room);
                });
                Save($"{folder}/tavan.png", w, d, tex =>
                {
                    if (room.Key == "koridor") StairHole(tex, room);
                });
            }
        }

        private static void StairHole(Texture2D tex, RoomDef room)
        {
            Hatch(tex, Px(HouseDims.StairHoleX0 - room.X0), Px(HouseDims.StairZ0),
                Px(HouseDims.StairHoleX1 - HouseDims.StairHoleX0), Px(Mathf.Min(HouseDims.StairZ1, room.Depth) - HouseDims.StairZ0), NoDetail);
        }

        // ------------------------------------------------------------------ Sabitlenebilir eşyalar ve kabin

        private static void Items()
        {
            foreach (var item in HouseLayout.Items)
            {
                Box($"Items/{item.Key}", item.Size, !item.PlainSides);
                if (item.Key == "tv_unitesi") Box("Items/tv", new Vector3(1f, 0.625f, 0.125f), true);
                if (item.Key == "mutfak_dolabi") Save("Items/mutfak_kapagi_on.png", Px(0.75f), Px(0.75f));
            }
            Box("Items/kabin", HouseDims.CabinMax - HouseDims.CabinMin, true);
            Save("Items/l_demiri.png", 16, 16);
        }

        /// <summary>Kutu: ön, yan (arka ve yanlar), üst (üst ve alt)</summary>
        private static void Box(string prefix, Vector3 size, bool sides)
        {
            Save($"{prefix}_on.png", Px(size.x), Px(size.y));
            if (!sides) return;
            Save($"{prefix}_yan.png", Px(size.z), Px(size.y));
            Save($"{prefix}_ust.png", Px(size.x), Px(size.z));
        }

        // ------------------------------------------------------------------ Karakterler (GDD "Karakterler")

        private static void Characters()
        {
            var characters = new (string key, float height, (string anim, int frames)[] anims)[]
            {
                ("oyuncu", 1.7f, new[] { ("durma", 2), ("yurume", 6), ("ziplama", 3), ("egilme", 2), ("cok_kapan_tutun", 3), ("yatakta_uyuma", 2), ("kalkma", 3), ("etkilesim", 2) }),
                ("baba", 1.8f, new[] { ("durma", 2), ("yurume", 6), ("yerde_yarali", 2), ("topallama", 6), ("cok_kapan_tutun", 3) }),
                ("anne", 1.65f, new[] { ("durma", 2), ("yurume", 6), ("cok_kapan_tutun", 3) }),
                ("kardes", 1.1f, new[] { ("durma", 2), ("yurume", 6), ("korkmus_bekleme", 2), ("cok_kapan_tutun", 3) }),
            };
            foreach (var (key, height, anims) in characters)
            foreach (var (anim, frames) in anims)
            {
                Save($"Characters/{key}_{anim}.png", 128 * frames, 128, tex =>
                {
                    int top = Mathf.RoundToInt(height * Ppu);
                    for (int f = 0; f < frames; f++)
                    {
                        int x0 = f * 128;
                        if (f > 0) VLine(tex, x0, Border, 1);
                        VLine(tex, x0 + 64, Guide, 1);     // pivot: alt orta
                        HLine(tex, x0, x0 + 128, top, Guide); // boy
                    }
                    HLine(tex, 0, tex.width, 0, Guide);    // ayak hizası
                }, transparent: true);
            }
        }

        // ------------------------------------------------------------------ Dekor, decal, parçacık, UI (GDD tabloları)

        private static void Others()
        {
            var sizes = new (string path, int w, int h)[]
            {
                ("Decor/tek_kisilik_yatak.png", 128, 64), ("Decor/ebeveyn_yatagi.png", 128, 64), ("Decor/kanepe.png", 128, 64),
                ("Decor/tezgah.png", 128, 64), ("Decor/masa.png", 80, 48), ("Decor/buzdolabi.png", 48, 128),
                ("Decor/sandalye.png", 32, 64), ("Decor/kapi_cercevesi.png", 80, 144), ("Decor/pencere.png", 96, 64),
                ("Decor/pencere_emission.png", 96, 64), ("Decor/tavan_lambasi.png", 32, 32),
                ("Decor/poster.png", 32, 48), ("Decor/tablo.png", 64, 48),
                ("Decals/catlak_1.png", 64, 64), ("Decals/catlak_2.png", 128, 64), ("Decals/catlak_3.png", 128, 128),
                ("Decals/cam_kirigi.png", 64, 16), ("Decals/guvenli_nokta.png", 96, 48),
                ("Particles/toz_kucuk.png", 24, 8), ("Particles/toz_buyuk.png", 48, 16), ("Particles/siva.png", 16, 16),
                ("Particles/kitap.png", 16, 24), ("Particles/oyuncak.png", 16, 16), ("Particles/tabak.png", 16, 8),
                ("UI/ikon_el_feneri.png", 32, 32), ("UI/ikon_ilk_yardim.png", 32, 32), ("UI/ikon_su.png", 32, 32),
                ("UI/ikon_duduk.png", 32, 32), ("UI/deprem_cantasi.png", 48, 48),
                ("UI/tus_f.png", 32, 32), ("UI/tus_c.png", 32, 32), ("UI/tus_space.png", 32, 32), ("UI/tus_esc.png", 32, 32),
                ("UI/sarsinti_cubugu.png", 192, 16),
                ("UI/minigame_vida.png", 256, 256), ("UI/minigame_l_demiri.png", 256, 256), ("UI/minigame_dolap_kapagi.png", 256, 256),
                // Berke'nin UI yuvaları (ENTEGRASYON B9)
                ("UI/minigame_panel.png", 256, 256), ("UI/minigame_tasi.png", 256, 256),
                ("UI/minigame_duvar.png", 16, 256), ("UI/minigame_guvenli_alan.png", 64, 64),
                ("UI/minigame_yatak_rafi.png", 48, 48), ("UI/minigame_ayna.png", 48, 48), ("UI/minigame_cerceve.png", 48, 48),
                ("UI/minigame_esya.png", 48, 48), ("UI/minigame_ibre.png", 8, 32),
                ("UI/hud_ipucu.png", 192, 32), ("UI/hud_sayac.png", 64, 32),
                ("UI/menu_arka_plan.png", 960, 540), ("UI/logo.png", 320, 96),
                ("Tiles/kesit.png", 64, 64), ("Tiles/merdiven_basamak.png", 64, 64),
            };
            foreach (var (path, w, h) in sizes)
                Save(path, w, h, transparent: !path.StartsWith("Tiles/"));

            // Vida başı kodla döner: dönme merkezi tam ortada
            Save("UI/minigame_vida_basi.png", 48, 48, tex =>
            {
                VLine(tex, 24, Guide, 1);
                HLine(tex, 0, 48, 24, Guide);
            }, transparent: true);
        }

        // ------------------------------------------------------------------ Çizim

        private static int Px(float meters) => Mathf.RoundToInt(meters * Ppu);

        private static void Save(string relativePath, int w, int h, Action<Texture2D> draw = null, bool transparent = false)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var bg = transparent ? new Color(0f, 0f, 0f, 0f) : Background;
            var pixels = Enumerable.Repeat(bg, w * h).ToArray();
            tex.SetPixels(pixels);

            // Kılavuz: 16 px açık, 64 px (1 m) koyu; şeffaf şablonlarda sadece 1 m çizgileri yarı saydam
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                bool meter = x % Ppu == 0 || y % Ppu == 0;
                bool minor = x % 16 == 0 || y % 16 == 0;
                if (transparent)
                {
                    if (meter && w > 64 && h > 64) tex.SetPixel(x, y, new Color(MeterLine.r, MeterLine.g, MeterLine.b, 0.35f));
                }
                else if (meter) tex.SetPixel(x, y, MeterLine);
                else if (minor) tex.SetPixel(x, y, MinorLine);
            }

            draw?.Invoke(tex);
            for (int x = 0; x < w; x++)
            {
                tex.SetPixel(x, 0, Border);
                tex.SetPixel(x, h - 1, Border);
            }
            for (int y = 0; y < h; y++)
            {
                tex.SetPixel(0, y, Border);
                tex.SetPixel(w - 1, y, Border);
            }
            tex.Apply();

            string full = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            Written.Add(relativePath);
        }

        /// <summary>Taranmış alan: (x, y) sol alt köşe, piksel</summary>
        private static void Hatch(Texture2D tex, int x0, int y0, int w, int h, Color color)
        {
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(tex.width, x0 + w); x++)
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(tex.height, y0 + h); y++)
            {
                bool edge = x == x0 || y == y0 || x == x0 + w - 1 || y == y0 + h - 1;
                if (edge || (x + y) % 6 == 0) tex.SetPixel(x, y, color);
            }
        }

        private static void VLine(Texture2D tex, int x, Color color, int width)
        {
            for (int i = 0; i < width; i++)
                for (int y = 0; y < tex.height; y++)
                    if (x + i < tex.width) tex.SetPixel(x + i, y, color);
        }

        private static void HLine(Texture2D tex, int x0, int x1, int y, Color color)
        {
            if (y < 0 || y >= tex.height) return;
            for (int x = x0; x < Mathf.Min(x1, tex.width); x++) tex.SetPixel(x, y, color);
        }
    }
}
