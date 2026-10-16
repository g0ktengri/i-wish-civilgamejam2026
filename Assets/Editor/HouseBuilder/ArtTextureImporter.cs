using UnityEditor;

namespace Deprem.HouseBuilder
{
    /// <summary>
    /// Assets/Art altına gelen her PNG'yi GDD'deki pixel art ayarlarıyla içe aktarır:
    /// 64 PPU, Point filtre, sıkıştırma ve mipmap kapalı, boyut değiştirilmez.
    /// Tiles klasöründekiler tekrar eder (Repeat), diğerleri etmez (Clamp).
    /// UI klasöründekiler Sprite olur; geri kalanı Quad + URP Lit materyalde kullanılan doku olarak kalır.
    /// Konuşma balonu ve HUD paneli 9-slice (8 px kenar).
    /// </summary>
    internal sealed class ArtTextureImporter : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/Art/";
        private const int PixelsPerUnit = 64;

        /// <summary>9-slice kenarı (px, dört yön): köşeler gerilmez (ENTEGRASYON M10). Şablonlardaki yeşil çizgiler.</summary>
        public const int SliceBorder = 8;
        private static readonly string[] SlicedSprites = { "UI/konusma_balonu.png", "UI/hud_ipucu.png" };

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
            if (System.Array.Exists(SlicedSprites, s => assetPath == ArtRoot + s))
                importer.spriteBorder = new UnityEngine.Vector4(SliceBorder, SliceBorder, SliceBorder, SliceBorder);

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
