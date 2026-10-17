using UnityEditor;

namespace Deprem.HouseBuilder
{
    /// <summary>
    /// Assets/Art altına gelen her PNG'yi GDD'deki pixel art ayarlarıyla içe aktarır:
    /// 64 PPU, Point filtre, sıkıştırma ve mipmap kapalı, boyut değiştirilmez.
    /// Tiles klasöründekiler tekrar eder (Repeat), diğerleri etmez (Clamp).
    /// UI klasöründekiler Sprite olur; geri kalanı Quad + URP Lit materyalde kullanılan doku olarak kalır.
    /// Konuşma balonu ve HUD paneli 9-slice (8 px kenar), tuş kapağı 9-slice (4 px kenar).
    /// </summary>
    internal sealed class ArtTextureImporter : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/Art/";
        private const int PixelsPerUnit = 64;

        /// <summary>9-slice kenarı (px, dört yön): köşeler gerilmez (ENTEGRASYON M10). Şablonlardaki yeşil çizgiler.</summary>
        public const int SliceBorder = 8;
        /// <summary>Tuş kapağının 9-slice kenarı (px): küçük sprite, 12 x 14 (ENTEGRASYON B12)</summary>
        public const int KeyCapSliceBorder = 4;
        private static readonly (string path, int border)[] SlicedSprites =
        {
            ("UI/konusma_balonu.png", SliceBorder), ("UI/hud_ipucu.png", SliceBorder), ("UI/hud_tus.png", KeyCapSliceBorder),
        };

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;

            var importer = (TextureImporter)assetImporter;
            bool isUi = assetPath.StartsWith(ArtRoot + "UI/");
            bool isTile = assetPath.Contains("/Tiles/");
            bool isNormalMap = assetPath.EndsWith("_normal.png");

            importer.textureType = isUi ? TextureImporterType.Sprite
                : isNormalMap ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            if (isUi) importer.spriteImportMode = SpriteImportMode.Single;
            foreach (var (path, border) in SlicedSprites)
                if (assetPath == ArtRoot + path)
                    importer.spriteBorder = new UnityEngine.Vector4(border, border, border, border);

            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = UnityEngine.FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = !isNormalMap;
            importer.wrapMode = isTile ? UnityEngine.TextureWrapMode.Repeat : UnityEngine.TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
        }
    }
}
