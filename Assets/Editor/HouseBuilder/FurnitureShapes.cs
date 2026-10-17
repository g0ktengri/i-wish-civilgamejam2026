using System.Collections.Generic;
using UnityEngine;

namespace Deprem.HouseBuilder
{
    /// <summary>
    /// Parçanın rengi (doku yokken). Body = eşyanın kendi rengi ve dokusu; diğerleri detay: kulp, yastık, cam,
    /// ekran, renkli kitap / oyuncak. Eşyanın sanatçı dokusu varsa hepsi Body gibi çizilir (detay çizimde).
    /// </summary>
    internal enum PartTone
    {
        Body,
        Metal,
        Soft,
        Glass,
        Dark,
        AccentA,
        AccentB,
        AccentC,
        /// <summary>Kitap sırtları: eşyanın boyutunda üretilen pixel doku (PixelFurnitureTextures.Books)</summary>
        Books,
        /// <summary>Yastık kılıfı: kırık beyaz (şilte bejinden ayrılsın)</summary>
        Linen,
        /// <summary>Oyuncak renkleri (pastel; ahşap mobilyalardan ayrı, Play testi 11: oyuncak dolabı belli olmuyordu)</summary>
        ToyRed,
        ToyBlue,
        ToyYellow,
        ToyGreen,
    }

    /// <summary>Şeklin bir kutu parçası: eşyanın sol-alt-ön köşesine göre metre</summary>
    internal readonly struct ShapePart
    {
        public readonly Vector3 Min;
        public readonly Vector3 Max;
        public readonly PartTone Tone;

        public ShapePart(Vector3 min, Vector3 max, PartTone tone)
        {
            Min = min;
            Max = max;
            Tone = tone;
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
                case "yatak_rafi": // yatağın üstünde iki katlı, kitap ve kutularla dolu ağır duvar rafı
                    s.Add(0f, 0f, 0f, 0.04f, h, d);
                    s.Add(w - 0.04f, 0f, 0f, w, h, d);
                    s.Add(0.04f, 0f, 0f, w - 0.04f, 0.04f, d);
                    s.Add(0.04f, 0.34f, 0f, w - 0.04f, 0.38f, d);
                    s.Add(0f, h - 0.04f, 0f, w, h, d);
                    s.Add(0.04f, 0.04f, d - 0.02f, w - 0.04f, h - 0.04f, d);
                    Books(s, 0.04f, d, new[] { (0.08f, 0.5f, 0.27f), (0.55f, 0.7f, 0.22f), (0.78f, 1.3f, 0.28f), (1.35f, 1.52f, 0.2f) });
                    Books(s, 0.38f, d, new[] { (0.1f, 0.42f, 0.24f), (0.5f, 0.95f, 0.26f), (1.05f, 1.5f, 0.23f) });
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
                    s.Add(0.62f, 0.12f, 0.1f, w - 0.62f, 0.2f, d - 0.1f, PartTone.Dark); // oyun konsolu / modem
                    break;

                case "tv": // ayak, boyun, ekran
                    s.Add(0.3f, 0f, 0f, w - 0.3f, 0.02f, d, PartTone.Metal);
                    s.Add(w * 0.5f - 0.04f, 0.02f, 0.05f, w * 0.5f + 0.04f, 0.1f, 0.08f, PartTone.Metal);
                    s.Add(0f, 0.1f, 0.03f, w, h, 0.09f);
                    s.Add(0.03f, 0.13f, 0.02f, w - 0.03f, h - 0.03f, 0.03f, PartTone.Dark); // ekran
                    break;

                case "mutfak_dolabi": // gövde, iki kapak, kulplar
                    s.Add(0f, 0f, 0.05f, w, h, d);
                    s.Add(0.02f, 0.02f, 0.02f, w * 0.5f - 0.01f, h - 0.02f, 0.05f);
                    s.Add(w * 0.5f + 0.01f, 0.02f, 0.02f, w - 0.02f, h - 0.02f, 0.05f);
                    s.Add(w * 0.5f - 0.09f, 0.08f, 0f, w * 0.5f - 0.06f, 0.24f, 0.02f, PartTone.Metal);
                    s.Add(w * 0.5f + 0.06f, 0.08f, 0f, w * 0.5f + 0.09f, 0.24f, 0.02f, PartTone.Metal);
                    break;

                case "ayakkabilik": // açık raf ve ayakkabılar
                    OpenCase(s, size, 0.05f, 0.05f, new[] { 0.35f, 0.65f }, 0.03f, 0f);
                    Books(s, 0.05f, d, new[] { (0.1f, 0.3f, 0.1f), (0.55f, 0.75f, 0.22f) }, false); // ayakkabılar
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
                    s.Add(f, f, 0.025f, w - f, h - f, d, key == "ayna" ? PartTone.Glass : PartTone.AccentB);
                    break;
                }

                case "oyuncak_rafi": // iki sütunlu, renkli arkalıklı bölmeler ve oyuncaklar (Play testi 11)
                {
                    OpenCase(s, size, 0.05f, 0.08f, new[] { 0.5f, 0.97f }, 0.04f);
                    s.Add(w * 0.5f - 0.02f, 0.08f, 0f, w * 0.5f + 0.02f, h - 0.05f, d - 0.02f);
                    // Bölmelerin arkası pastel renkli: dolap uzaktan "çocuk rafı" diye okunsun
                    var backs = new[] { PartTone.ToyBlue, PartTone.ToyYellow, PartTone.ToyGreen, PartTone.ToyRed, PartTone.ToyYellow, PartTone.ToyBlue };
                    float[] rowBottom = { 0.12f, 0.54f, 1.01f }, rowTop = { 0.5f, 0.97f, 1.45f };
                    for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 2; col++)
                    {
                        float x0 = col == 0 ? 0.05f : w * 0.5f + 0.02f, x1 = col == 0 ? w * 0.5f - 0.02f : w - 0.05f;
                        s.Add(x0, rowBottom[row], d - 0.05f, x1, rowTop[row], d - 0.03f, backs[row * 2 + col]);
                    }
                    // Alt sol: kırmızı top (basamaklı daire)
                    s.Add(0.17f, 0.12f, 0.1f, 0.35f, 0.34f, 0.28f, PartTone.ToyRed);
                    s.Add(0.14f, 0.15f, 0.1f, 0.38f, 0.31f, 0.28f, PartTone.ToyRed);
                    s.Add(0.14f, 0.21f, 0.09f, 0.38f, 0.24f, 0.29f, PartTone.Linen);   // topun beyaz şeridi
                    // Alt sağ: üst üste renkli küpler
                    s.Add(0.57f, 0.12f, 0.08f, 0.69f, 0.24f, 0.22f, PartTone.ToyBlue);
                    s.Add(0.71f, 0.12f, 0.08f, 0.83f, 0.24f, 0.22f, PartTone.ToyYellow);
                    s.Add(0.64f, 0.24f, 0.08f, 0.76f, 0.36f, 0.22f, PartTone.ToyGreen);
                    // Orta sol: oturan ayıcık (gövde, baş, kulaklar, kollar, burun)
                    s.Add(0.15f, 0.54f, 0.1f, 0.35f, 0.74f, 0.28f, PartTone.AccentC);
                    s.Add(0.18f, 0.74f, 0.1f, 0.32f, 0.87f, 0.27f, PartTone.AccentC);
                    s.Add(0.16f, 0.85f, 0.12f, 0.2f, 0.9f, 0.24f, PartTone.AccentC);
                    s.Add(0.3f, 0.85f, 0.12f, 0.34f, 0.9f, 0.24f, PartTone.AccentC);
                    s.Add(0.11f, 0.6f, 0.12f, 0.15f, 0.7f, 0.24f, PartTone.AccentC);
                    s.Add(0.35f, 0.6f, 0.12f, 0.39f, 0.7f, 0.24f, PartTone.AccentC);
                    s.Add(0.23f, 0.77f, 0.08f, 0.27f, 0.8f, 0.1f, PartTone.Dark);      // burun
                    // Orta sağ: araba (gövde, kabin, cam, tekerlekler)
                    s.Add(0.57f, 0.58f, 0.1f, 0.91f, 0.67f, 0.26f, PartTone.ToyRed);
                    s.Add(0.63f, 0.67f, 0.1f, 0.83f, 0.76f, 0.26f, PartTone.ToyRed);
                    s.Add(0.66f, 0.68f, 0.09f, 0.8f, 0.74f, 0.1f, PartTone.Glass);
                    s.Add(0.61f, 0.54f, 0.09f, 0.68f, 0.6f, 0.27f, PartTone.Dark);
                    s.Add(0.8f, 0.54f, 0.09f, 0.87f, 0.6f, 0.27f, PartTone.Dark);
                    // Üst sol: kapaklı oyuncak kutusu
                    s.Add(0.1f, 1.01f, 0.08f, 0.43f, 1.21f, 0.3f, PartTone.ToyYellow);
                    s.Add(0.08f, 1.21f, 0.07f, 0.45f, 1.26f, 0.31f, PartTone.ToyBlue);
                    // Üst sağ: renkli masal kitapları ve küçük top
                    s.Add(0.57f, 1.01f, 0.06f, 0.63f, 1.29f, 0.3f, PartTone.ToyGreen);
                    s.Add(0.64f, 1.01f, 0.06f, 0.69f, 1.25f, 0.3f, PartTone.ToyRed);
                    s.Add(0.7f, 1.01f, 0.06f, 0.76f, 1.31f, 0.3f, PartTone.ToyBlue);
                    s.Add(0.8f, 1.01f, 0.1f, 0.91f, 1.12f, 0.26f, PartTone.ToyYellow);
                    break;
                }

                case "gardirop": // ayaklar, gövde, taç, iki kapak, kulplar
                    Legs(s, size, 0.1f, 0.08f, 0.05f, 0.06f);
                    s.Add(0.03f, 0.1f, 0.04f, w - 0.03f, h - 0.1f, d);
                    s.Add(0f, 0.1f, 0.02f, w, 0.16f, d);
                    s.Add(0f, h - 0.1f, 0f, w, h, d);
                    s.Add(0.05f, 0.18f, 0.02f, w * 0.5f - 0.005f, h - 0.12f, 0.04f);
                    s.Add(w * 0.5f + 0.005f, 0.18f, 0.02f, w - 0.05f, h - 0.12f, 0.04f);
                    s.Add(w * 0.5f - 0.07f, 0.85f, 0f, w * 0.5f - 0.04f, 1.15f, 0.02f, PartTone.Metal);
                    s.Add(w * 0.5f + 0.04f, 0.85f, 0f, w * 0.5f + 0.07f, 1.15f, 0.02f, PartTone.Metal);
                    break;

                case "kabin":
                {
                    // Play testi 24: kabin çirkindi. Koyu çelik çerçeve ve ince dikmeler, turuncu gövde, altta ve üstte
                    // sarı-siyah uyarı şeridi, çerçeveli ve takviyeli kapı, ızgaralı küçük pencere, yeşil tabela levhası
                    // (yazısı HouseBuilder.BuildCabin'de). Tabela y 1,5-1,72
                    s.Add(0f, 0f, 0f, w, 0.05f, d, PartTone.Dark);
                    s.Add(0f, h - 0.08f, 0f, w, h, d, PartTone.Dark);
                    foreach (float x in new[] { 0f, w - 0.06f })
                    foreach (float z in new[] { 0f, d - 0.06f })
                        s.Add(x, 0f, z, x + 0.06f, h, z + 0.06f, PartTone.Dark);
                    s.Add(0.03f, 0.05f, 0.03f, w - 0.03f, h - 0.08f, d);
                    // Uyarı şeritleri
                    int n = 0;
                    for (float x = 0.06f; x < w - 0.07f; x += 0.1f, n++)
                    {
                        float x1 = Mathf.Min(x + 0.1f, w - 0.06f);
                        var tone = n % 2 == 0 ? PartTone.ToyYellow : PartTone.Dark;
                        s.Add(x, 0.05f, 0.015f, x1, 0.15f, 0.03f, tone);
                        s.Add(x, h - 0.16f, 0.015f, x1, h - 0.08f, 0.03f, tone);
                    }
                    // Kapı: çelik kasa, gövde renginde kanat, iki takviye laması, yatay kulp
                    s.Add(0.25f, 0.15f, 0.01f, 0.28f, 1.45f, 0.035f, PartTone.Metal);
                    s.Add(w - 0.28f, 0.15f, 0.01f, w - 0.25f, 1.45f, 0.035f, PartTone.Metal);
                    s.Add(0.25f, 1.42f, 0.01f, w - 0.25f, 1.45f, 0.035f, PartTone.Metal);
                    s.Add(0.28f, 0.15f, 0.018f, w - 0.28f, 1.42f, 0.03f);
                    s.Add(0.31f, 0.38f, 0.008f, w - 0.31f, 0.42f, 0.018f, PartTone.Metal);
                    s.Add(0.31f, 0.66f, 0.008f, w - 0.31f, 0.7f, 0.018f, PartTone.Metal);
                    s.Add(w - 0.44f, 0.8f, 0f, w - 0.34f, 0.84f, 0.018f, PartTone.Metal);
                    // Pencere ve ızgarası
                    s.Add(0.45f, 0.98f, 0.01f, w - 0.45f, 1.3f, 0.018f, PartTone.Glass);
                    s.Add(w * 0.5f - 0.012f, 0.98f, 0.004f, w * 0.5f + 0.012f, 1.3f, 0.012f, PartTone.Metal);
                    s.Add(0.45f, 1.13f, 0.004f, w - 0.45f, 1.155f, 0.012f, PartTone.Metal);
                    // Tabela levhası
                    s.Add(0.12f, 1.5f, 0.006f, w - 0.12f, 1.72f, 0.03f, PartTone.ToyGreen);
                    break;
                }

                case "portmanto": // intro: giriş kapısının yanındaki uzun dolap; taç ve baza. Play testi: sola, mutfağa
                                  // baksın: iki kapak ve kulplar sol yüzde (X = 0), kameraya yan yüzü döner
                    s.Add(0f, 0f, 0f, w, 0.08f, d, PartTone.Dark);
                    s.Add(0.02f, 0.08f, 0.01f, w, h - 0.1f, d - 0.01f);
                    s.Add(0f, h - 0.1f, 0f, w, h, d, PartTone.AccentC);
                    s.Add(0f, 0.14f, 0.05f, 0.02f, h - 0.16f, d * 0.5f - 0.01f, PartTone.AccentA);
                    s.Add(0f, 0.14f, d * 0.5f + 0.01f, 0.02f, h - 0.16f, d - 0.05f, PartTone.AccentA);
                    s.Add(-0.012f, h * 0.5f - 0.08f, d * 0.5f - 0.06f, 0f, h * 0.5f + 0.08f, d * 0.5f - 0.035f, PartTone.Metal);
                    s.Add(-0.012f, h * 0.5f - 0.08f, d * 0.5f + 0.035f, 0f, h * 0.5f + 0.08f, d * 0.5f + 0.06f, PartTone.Metal);
                    break;

                case "kapi": // kapalıyken kameraya yan döner; açılınca X yüzü (_yan) görünür. Kulplar serbest kenarda (Z = 0)
                    s.Add(0.03f, 0f, 0f, w - 0.03f, h, d);
                    s.Add(0f, h * 0.48f, 0.06f, 0.03f, h * 0.53f, 0.14f, PartTone.Metal);
                    s.Add(w - 0.03f, h * 0.48f, 0.06f, w, h * 0.53f, 0.14f, PartTone.Metal);
                    break;

                // ---------------------------------------------------------- Mobilyalar (Decor)
                case "tek_kisilik_yatak":
                case "ebeveyn_yatagi": // yan görünüş: başlık solda; yastıklar yatan karakterin arkasında kalır
                {
                    // Play testi 9: yataklar karaktere göre küçük ve oyuncak gibiydi; şilte üstü 0,66 m
                    // (QuakeDirector.bedHeight), kalın başlık, beyaz şilte üstünde örtü, kalın yastık
                    bool dbl = key == "ebeveyn_yatagi";
                    const float frameTop = 0.4f, mattressTop = 0.66f;
                    Legs(s, size, 0.14f, 0.1f, 0.06f, 0.06f);
                    s.Add(0f, 0.14f, 0f, w, frameTop, d);
                    s.Add(0.12f, frameTop, 0.04f, w - 0.1f, mattressTop - 0.02f, d - 0.04f, PartTone.Soft); // şilte
                    s.Add(w * 0.4f, frameTop, 0.02f, w - 0.1f, mattressTop + 0.02f, d - 0.02f, PartTone.AccentA); // örtü
                    s.Add(0f, 0.14f, 0f, 0.12f, h - 0.06f, d);                // başlık
                    s.Add(-0.02f, h - 0.06f, -0.02f, 0.14f, h, d + 0.02f);    // başlık kapağı
                    s.Add(w - 0.1f, 0.14f, 0f, w, 0.8f, d);                   // ayak ucu
                    if (dbl)
                    {
                        Pillow(s, mattressTop, 0.1f, 0.85f);
                        Pillow(s, mattressTop, d - 0.85f, d - 0.1f);
                    }
                    else
                    {
                        Pillow(s, mattressTop, 0.75f, d - 0.06f);
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
                    s.Add(0.22f, 0.36f, 0.02f, w * 0.5f, 0.48f, d - 0.28f, PartTone.Soft);
                    s.Add(w * 0.5f, 0.36f, 0.02f, w - 0.22f, 0.48f, d - 0.28f, PartTone.Soft);
                    break;

                case "tezgah": // süpürgelik, dolaplar, tezgah üstü, üç kapak
                    s.Add(0f, 0f, 0.08f, w, 0.1f, d);
                    s.Add(0f, 0.1f, 0.03f, w, h - 0.04f, d);
                    s.Add(0f, h - 0.04f, 0f, w, h, d, PartTone.Metal); // tezgah üstü
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
                    s.Add(w - 0.13f, 1.3f, 0f, w - 0.09f, 1.5f, 0.01f, PartTone.Metal);
                    s.Add(w - 0.13f, 0.9f, 0f, w - 0.09f, 1.15f, 0.01f, PartTone.Metal);
                    break;

                case "masa": // tabla, kuşak, dört ayak
                    Legs(s, size, h - 0.12f, 0.07f, 0.05f, 0.05f);
                    s.Add(0.05f, h - 0.12f, 0.05f, w - 0.05f, h - 0.04f, d - 0.05f);
                    s.Add(0f, h - 0.04f, 0f, w, h, d);
                    break;

                case "calisma_masasi": // sağlam çalışma masası: tabla, solda çekmeceli ayak, sağda iki ayak
                    s.Add(0f, h - 0.05f, 0f, w, h, d);
                    s.Add(0.02f, 0f, 0.02f, 0.42f, h - 0.05f, d - 0.02f);
                    s.Add(0.05f, h - 0.25f, 0f, 0.39f, h - 0.08f, 0.02f, PartTone.AccentC); // çekmeceler
                    s.Add(0.05f, 0.08f, 0f, 0.39f, h - 0.28f, 0.02f, PartTone.AccentC);
                    s.Add(w - 0.08f, 0f, 0.03f, w - 0.02f, h - 0.05f, 0.09f);
                    s.Add(w - 0.08f, 0f, d - 0.09f, w - 0.02f, h - 0.05f, d - 0.03f);
                    s.Add(0.42f, h - 0.15f, d - 0.05f, w - 0.02f, h - 0.05f, d - 0.02f); // arka kuşak
                    break;

                case "konsol": // sağlam konsol masa: tabla, iki yan panel, altta raf
                    s.Add(0f, h - 0.05f, 0f, w, h, d);
                    s.Add(0.03f, 0f, 0.02f, 0.09f, h - 0.05f, d - 0.02f);
                    s.Add(w - 0.09f, 0f, 0.02f, w - 0.03f, h - 0.05f, d - 0.02f);
                    s.Add(0.09f, 0.12f, 0.04f, w - 0.09f, 0.16f, d - 0.04f);
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
        // spines: kitap sırtı dokusu (Berke'nin play testi: tek renk geniş kutular kitap gibi durmuyordu); değilse renk blokları
        private static void Books(Shape s, float baseY, float depth, (float x0, float x1, float height)[] blocks, bool spines = true)
        {
            foreach (var (x0, x1, height) in blocks)
                s.Add(x0, baseY, 0.05f, x1, baseY + height, depth - 0.06f, spines ? PartTone.Books : s.NextAccent());
        }

        /// <summary>
        /// Yastık (Play testi 9: kutu gibi duruyordu): yandan yuvarlak görünsün diye üç katman, ortası en geniş;
        /// başlığa yaslı, şiltenin üstünde, kırık beyaz kılıf
        /// </summary>
        private static void Pillow(Shape s, float top, float z0, float z1)
        {
            s.Add(0.17f, top - 0.02f, z0, 0.66f, top + 0.02f, z1, PartTone.Linen);
            s.Add(0.14f, top + 0.02f, z0, 0.7f, top + 0.09f, z1, PartTone.Linen);
            s.Add(0.19f, top + 0.09f, z0, 0.64f, top + 0.12f, z1, PartTone.Linen);
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
            public void Add(float x0, float y0, float z0, float x1, float y1, float z1, PartTone tone = PartTone.Body)
            {
                var min = Vector3.Max(Vector3.zero, new Vector3(x0, y0, z0));
                var max = Vector3.Min(size, new Vector3(x1, y1, z1));
                if (max.x - min.x < 0.001f || max.y - min.y < 0.001f || max.z - min.z < 0.001f) return;
                Parts.Add(new ShapePart(min, max, tone));
            }

            private int accent;

            /// <summary>Sıradaki renkli detay tonu (kitaplar, oyuncaklar, ayakkabılar sırayla farklı renk)</summary>
            public PartTone NextAccent() => (PartTone)((int)PartTone.AccentA + accent++ % 3);
        }
    }
}
