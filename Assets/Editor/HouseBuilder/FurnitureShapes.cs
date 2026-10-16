using System.Collections.Generic;
using UnityEngine;

namespace Deprem.HouseBuilder
{
    /// <summary>Şeklin bir kutu parçası: eşyanın sol-alt-ön köşesine göre metre</summary>
    internal readonly struct ShapePart
    {
        public readonly Vector3 Min;
        public readonly Vector3 Max;

        public ShapePart(Vector3 min, Vector3 max)
        {
            Min = min;
            Max = max;
        }
    }

    /// <summary>
    /// Eşya ve mobilya şekilleri: kutu parçalarından silüet (kitaplıkta raflar ve kitaplar, yatakta ayaklar ve yastık...).
    /// Koordinatlar eşyanın sol-alt-ön köşesinden metre: X sağa, Y yukarı, Z arkaya (Z = 0 ön yüz).
    /// Parçalar eşyanın dış kutusunun (Size) içinde kalır; çarpışma ve devrilme dış kutuyla çalışır.
    /// Dokular dış kutudan yansıtılır: bütün ön yüzler {key}_on.png'nin aynı yerini görür, şablon da
    /// aynı parçalardan çizilir (ArtTemplates). Şekil değişince şablonlar yeniden üretilir.
    /// </summary>
    internal static class FurnitureShapes
    {
        /// <summary>Tanımı olmayan anahtar (kabin) tek kutu döner</summary>
        public static List<ShapePart> For(string key, Vector3 size)
        {
            var s = new Shape(size);
            float w = size.x, h = size.y, d = size.z;
            switch (key)
            {
                // ---------------------------------------------------------- Sabitlenebilir eşyalar
                case "yatak_rafi": // duvar rafı, iki konsol, üstünde devrilecek eşyalar
                    s.Add(0f, 0.3f, 0f, w, 0.36f, d);
                    foreach (float x in new[] { 0.12f, w - 0.17f })
                    {
                        s.Add(x, 0.05f, d - 0.05f, x + 0.05f, 0.3f, d);
                        s.Add(x, 0.26f, 0.02f, x + 0.05f, 0.3f, d - 0.05f);
                    }
                    s.Add(0.08f, 0.36f, 0.04f, 0.34f, 0.5f, 0.22f);  // kutu
                    s.Add(0.46f, 0.36f, 0.06f, 0.52f, 0.5f, 0.22f);  // kitaplar
                    s.Add(0.53f, 0.36f, 0.06f, 0.58f, 0.48f, 0.22f);
                    s.Add(0.59f, 0.36f, 0.06f, 0.66f, 0.46f, 0.22f);
                    s.Add(0.76f, 0.36f, 0.08f, 0.9f, 0.47f, 0.2f);   // kavanoz
                    break;

                case "kitaplik":
                    OpenCase(s, size, 0.06f, 0.1f, new[] { 0.55f, 1f, 1.45f }, 0.04f);
                    Books(s, 0.14f, d, new[] { (0.1f, 0.48f, 0.32f), (0.7f, 0.9f, 0.2f) });
                    Books(s, 0.59f, d, new[] { (0.1f, 0.3f, 0.28f), (0.34f, 0.62f, 0.34f) });
                    Books(s, 1.04f, d, new[] { (0.55f, 0.9f, 0.3f) });
                    Books(s, 1.49f, d, new[] { (0.1f, 0.4f, 0.25f), (0.66f, 0.86f, 0.15f) });
                    break;

                case "tv_unitesi": // ayaklı alçak dolap, ortada açık bölme
                    Legs(s, size, 0.08f, 0.08f, 0.06f, 0.04f);
                    s.Add(0f, 0.46f, 0f, w, h, d);
                    s.Add(0f, 0.08f, 0f, w, 0.12f, d);
                    s.Add(0f, 0.12f, 0f, 0.55f, 0.46f, d);
                    s.Add(w - 0.55f, 0.12f, 0f, w, 0.46f, d);
                    s.Add(0.55f, 0.12f, d - 0.04f, w - 0.55f, 0.46f, d);
                    s.Add(0.62f, 0.12f, 0.1f, w - 0.62f, 0.2f, d - 0.1f); // oyun konsolu / modem
                    break;

                case "tv": // ayak, boyun, ekran
                    s.Add(0.3f, 0f, 0f, w - 0.3f, 0.02f, d);
                    s.Add(w * 0.5f - 0.04f, 0.02f, 0.05f, w * 0.5f + 0.04f, 0.1f, 0.08f);
                    s.Add(0f, 0.1f, 0.03f, w, h, 0.09f);
                    break;

                case "mutfak_dolabi": // gövde, iki kapak, kulplar
                    s.Add(0f, 0f, 0.05f, w, h, d);
                    s.Add(0.02f, 0.02f, 0.02f, w * 0.5f - 0.01f, h - 0.02f, 0.05f);
                    s.Add(w * 0.5f + 0.01f, 0.02f, 0.02f, w - 0.02f, h - 0.02f, 0.05f);
                    s.Add(w * 0.5f - 0.09f, 0.08f, 0f, w * 0.5f - 0.06f, 0.24f, 0.02f);
                    s.Add(w * 0.5f + 0.06f, 0.08f, 0f, w * 0.5f + 0.09f, 0.24f, 0.02f);
                    break;

                case "ayakkabilik": // açık raf ve ayakkabılar
                    OpenCase(s, size, 0.05f, 0.05f, new[] { 0.35f, 0.65f }, 0.03f, 0f);
                    Books(s, 0.05f, d, new[] { (0.1f, 0.3f, 0.1f), (0.55f, 0.75f, 0.22f) });
                    Books(s, 0.38f, d, new[] { (0.12f, 0.32f, 0.1f), (0.4f, 0.6f, 0.12f), (0.68f, 0.88f, 0.1f) });
                    Books(s, 0.68f, d, new[] { (0.2f, 0.4f, 0.11f), (0.5f, 0.7f, 0.1f) });
                    break;

                case "ayna":
                case "cerceve": // çerçeve ve içeride cam / resim
                {
                    float f = key == "ayna" ? 0.06f : 0.05f;
                    s.Add(0f, 0f, 0f, f, h, d);
                    s.Add(w - f, 0f, 0f, w, h, d);
                    s.Add(f, 0f, 0f, w - f, f, d);
                    s.Add(f, h - f, 0f, w - f, h, d);
                    s.Add(f, f, 0.025f, w - f, h - f, d);
                    break;
                }

                case "oyuncak_rafi": // iki sütunlu bölmeli raf ve oyuncaklar
                    OpenCase(s, size, 0.05f, 0.08f, new[] { 0.5f, 0.97f }, 0.04f);
                    s.Add(w * 0.5f - 0.02f, 0.08f, 0f, w * 0.5f + 0.02f, h - 0.05f, d - 0.02f);
                    s.Add(0.12f, 0.12f, 0.08f, 0.34f, 0.34f, 0.3f);  // top
                    s.Add(0.58f, 0.12f, 0.08f, 0.72f, 0.26f, 0.24f); // küpler
                    s.Add(0.74f, 0.12f, 0.1f, 0.86f, 0.18f, 0.22f);
                    s.Add(0.75f, 0.18f, 0.1f, 0.85f, 0.28f, 0.22f);
                    s.Add(0.1f, 0.54f, 0.08f, 0.36f, 0.8f, 0.3f);    // ayıcık
                    s.Add(0.15f, 0.8f, 0.1f, 0.31f, 0.92f, 0.28f);
                    s.Add(0.6f, 0.54f, 0.06f, 0.68f, 0.84f, 0.3f);   // masal kitapları
                    s.Add(0.69f, 0.54f, 0.06f, 0.76f, 0.8f, 0.3f);
                    s.Add(0.1f, 1.01f, 0.08f, 0.4f, 1.11f, 0.28f);   // araba
                    s.Add(0.6f, 1.01f, 0.06f, 0.88f, 1.25f, 0.3f);   // oyuncak kutusu
                    break;

                case "gardirop": // ayaklar, gövde, taç, iki kapak, kulplar
                    Legs(s, size, 0.1f, 0.08f, 0.05f, 0.06f);
                    s.Add(0.03f, 0.1f, 0.04f, w - 0.03f, h - 0.1f, d);
                    s.Add(0f, 0.1f, 0.02f, w, 0.16f, d);
                    s.Add(0f, h - 0.1f, 0f, w, h, d);
                    s.Add(0.05f, 0.18f, 0.02f, w * 0.5f - 0.005f, h - 0.12f, 0.04f);
                    s.Add(w * 0.5f + 0.005f, 0.18f, 0.02f, w - 0.05f, h - 0.12f, 0.04f);
                    s.Add(w * 0.5f - 0.07f, 0.85f, 0f, w * 0.5f - 0.04f, 1.15f, 0.02f);
                    s.Add(w * 0.5f + 0.04f, 0.85f, 0f, w * 0.5f + 0.07f, 1.15f, 0.02f);
                    break;

                // ---------------------------------------------------------- Mobilyalar (Decor)
                case "tek_kisilik_yatak":
                case "ebeveyn_yatagi": // yan görünüş: başlık solda; yastıklar karakterin arkasında kalır
                {
                    bool dbl = key == "ebeveyn_yatagi";
                    Legs(s, size, 0.1f, 0.08f, 0.05f, 0.05f);
                    s.Add(0f, 0.1f, 0f, w, 0.3f, d);
                    s.Add(0.08f, 0.3f, 0.03f, w - 0.08f, 0.5f, d - 0.03f);
                    s.Add(0f, 0.1f, 0f, dbl ? 0.1f : 0.08f, h, d);
                    s.Add(w - 0.08f, 0.1f, 0f, w, 0.58f, d);
                    if (dbl)
                    {
                        s.Add(0.14f, 0.5f, 0.1f, 0.55f, 0.62f, 0.8f);
                        s.Add(0.14f, 0.5f, d - 0.8f, 0.55f, 0.62f, d - 0.1f);
                    }
                    else
                    {
                        s.Add(0.12f, 0.5f, 0.5f, 0.55f, 0.62f, d - 0.08f);
                    }
                    break;
                }

                case "komodin": // ayaklı, iki çekmece
                    Legs(s, size, 0.06f, 0.06f, 0.03f, 0.03f);
                    s.Add(0f, 0.06f, 0.02f, w, h, d);
                    s.Add(0.04f, 0.32f, 0f, w - 0.04f, h - 0.04f, 0.02f);
                    s.Add(0.04f, 0.1f, 0f, w - 0.04f, 0.29f, 0.02f);
                    break;

                case "kanepe": // kolçaklar, oturma, sırtlık, iki minder
                    Legs(s, size, 0.08f, 0.08f, 0.08f, 0.08f);
                    s.Add(0f, 0.08f, 0f, 0.22f, 0.62f, d);
                    s.Add(w - 0.22f, 0.08f, 0f, w, 0.62f, d);
                    s.Add(0.22f, 0.08f, 0f, w - 0.22f, 0.36f, d);
                    s.Add(0.22f, 0.36f, d - 0.28f, w - 0.22f, h, d);
                    s.Add(0.22f, 0.36f, 0.02f, w * 0.5f, 0.48f, d - 0.28f);
                    s.Add(w * 0.5f, 0.36f, 0.02f, w - 0.22f, 0.48f, d - 0.28f);
                    break;

                case "tezgah": // süpürgelik, dolaplar, tezgah üstü, üç kapak
                    s.Add(0f, 0f, 0.08f, w, 0.1f, d);
                    s.Add(0f, 0.1f, 0.03f, w, h - 0.04f, d);
                    s.Add(0f, h - 0.04f, 0f, w, h, d);
                    for (int i = 0; i < 3; i++)
                    {
                        float x0 = i * w / 3f;
                        s.Add(x0 + 0.02f, 0.12f, 0.01f, x0 + w / 3f - 0.02f, h - 0.06f, 0.03f);
                    }
                    break;

                case "buzdolabi": // gövde, dondurucu ve soğutucu kapağı, kulplar
                    s.Add(0.04f, 0f, 0.08f, w - 0.04f, 0.03f, d - 0.04f);
                    s.Add(0f, 0.03f, 0.04f, w, h, d);
                    s.Add(0.01f, 1.24f, 0.01f, w - 0.01f, h - 0.01f, 0.04f);
                    s.Add(0.01f, 0.04f, 0.01f, w - 0.01f, 1.22f, 0.04f);
                    s.Add(w - 0.13f, 1.3f, 0f, w - 0.09f, 1.5f, 0.01f);
                    s.Add(w - 0.13f, 0.9f, 0f, w - 0.09f, 1.15f, 0.01f);
                    break;

                case "masa": // tabla, kuşak, dört ayak
                    Legs(s, size, h - 0.12f, 0.07f, 0.05f, 0.05f);
                    s.Add(0.05f, h - 0.12f, 0.05f, w - 0.05f, h - 0.04f, d - 0.05f);
                    s.Add(0f, h - 0.04f, 0f, w, h, d);
                    break;

                case "sandalye": // oturak, ayaklar, sırtlık sağda (masaya bakar)
                    Legs(s, size, 0.42f, 0.04f, 0.02f, 0.02f);
                    s.Add(0f, 0.42f, 0f, w, 0.47f, d);
                    s.Add(w - 0.06f, 0.47f, 0.02f, w - 0.02f, h, 0.06f);
                    s.Add(w - 0.06f, 0.47f, d - 0.06f, w - 0.02f, h, d - 0.02f);
                    s.Add(w - 0.06f, h - 0.2f, 0.02f, w - 0.02f, h - 0.04f, d - 0.02f);
                    break;

                default:
                    s.Add(0f, 0f, 0f, w, h, d);
                    break;
            }
            return s.Parts;
        }

        /// <summary>Önü açık dolap: yan paneller, tepe, taban, arkalık ve raflar (rafların alt yüksekliği)</summary>
        private static void OpenCase(Shape s, Vector3 size, float side, float plinth, float[] shelves, float shelf,
            float plinthInset = 0.02f)
        {
            float w = size.x, h = size.y, d = size.z;
            s.Add(0f, 0f, 0f, side, h, d);
            s.Add(w - side, 0f, 0f, w, h, d);
            s.Add(0f, h - side, 0f, w, h, d);
            s.Add(side, 0f, plinthInset, w - side, plinth, d);
            s.Add(side, plinth, d - 0.03f, w - side, h - side, d);
            foreach (float y in shelves)
                s.Add(side, y, 0f, w - side, y + shelf, d - 0.03f);
            if (plinth > 0.05f) s.Add(side, plinth, 0f, w - side, plinth + shelf, d - 0.03f);
        }

        /// <summary>Raf üstünde dizili bloklar (kitap, ayakkabı): (x0, x1, yükseklik)</summary>
        private static void Books(Shape s, float baseY, float depth, (float x0, float x1, float height)[] blocks)
        {
            foreach (var (x0, x1, height) in blocks)
                s.Add(x0, baseY, 0.05f, x1, baseY + height, depth - 0.06f);
        }

        /// <summary>Dört köşede kare ayak: kenardan (inX, inZ) içeride</summary>
        private static void Legs(Shape s, Vector3 size, float height, float thickness, float inX, float inZ)
        {
            foreach (float x in new[] { inX, size.x - inX - thickness })
            foreach (float z in new[] { inZ, size.z - inZ - thickness })
                s.Add(x, 0f, z, x + thickness, height, z + thickness);
        }

        private sealed class Shape
        {
            public readonly List<ShapePart> Parts = new List<ShapePart>();
            private readonly Vector3 size;

            public Shape(Vector3 size) => this.size = size;

            /// <summary>Dış kutuya kırpılmış parça; boş kalanlar atlanır</summary>
            public void Add(float x0, float y0, float z0, float x1, float y1, float z1)
            {
                var min = Vector3.Max(Vector3.zero, new Vector3(x0, y0, z0));
                var max = Vector3.Min(size, new Vector3(x1, y1, z1));
                if (max.x - min.x < 0.001f || max.y - min.y < 0.001f || max.z - min.z < 0.001f) return;
                Parts.Add(new ShapePart(min, max));
            }
        }
    }
}
