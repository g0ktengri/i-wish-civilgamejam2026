using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deprem.Bag;
using Deprem.Minigames;
using Deprem.Spirit;
using Deprem.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deprem.Loop.EditorTools
{
    // Berke'nin sistemlerini tek prefab'da toplar: Assets/Prefabs/Berke/BerkeSystems.prefab.
    // Mehmet'in BerkeSystemsSetup'ı bu klasördeki prefab'ları Main sahnesine otomatik ekler.
    // Kurulurken Assets/Art/UI altındaki görseller ve Assets/Art/Fonts altındaki ilk font
    // ilgili alanlara kendiliğinden bağlanır; olmayan dosya için düz renk kalır ve Console'a yazılır.
    // Batchmode: -executeMethod Deprem.Loop.EditorTools.BerkeSystemsPrefab.Build
    public static class BerkeSystemsPrefab
    {
        const string Folder = "Assets/Prefabs/Berke";
        const string PrefabPath = Folder + "/BerkeSystems.prefab";
        const string UiArtFolder = "Assets/Art/UI";
        const string FontFolder = "Assets/Art/Fonts";
        // HUD, menü, keşke ekranı Pixelify Sans; konuşma balonları Mehmet'in DepremBalon'u (play testi 13).
        // Klasörde iki font olduğu için adla seçilir (alfabetik ilk font DepremBalon'du).
        const string UiFontPath = FontFolder + "/PixelifySans.ttf";
        const string BubbleFontPath = FontFolder + "/DepremBalon-Regular.ttf";

        // (bileşen, alan yolu, Assets/Art/UI altındaki dosya adı)
        static readonly (Type component, string field, string file)[] Sprites =
        {
            (typeof(MinigameHost), "art.panel", "minigame_panel"),
            (typeof(MinigameHost), "art.screwBackground", "minigame_vida"),
            (typeof(MinigameHost), "art.screwPlate", "minigame_levha"),
            (typeof(MinigameHost), "art.screwHead", "minigame_vida_basi"),
            (typeof(MinigameHost), "art.screwdriver", "minigame_tornavida"),
            (typeof(MinigameHost), "art.screwDone", "minigame_l_demiri"),
            (typeof(MinigameHost), "art.carryBackground", "minigame_tasi"),
            (typeof(MinigameHost), "art.carryWall", "minigame_duvar"),
            (typeof(MinigameHost), "art.carryTarget", "minigame_guvenli_alan"),
            (typeof(MinigameHost), "art.carryItem", "minigame_esya"),
            (typeof(MinigameHost), "art.carryBedShelf", "minigame_yatak_rafi"),
            (typeof(MinigameHost), "art.carryMirror", "minigame_ayna"),
            (typeof(MinigameHost), "art.carryFrame", "minigame_cerceve"),
            (typeof(MinigameHost), "art.lockBackground", "minigame_dolap_kapagi"),
            (typeof(MinigameHost), "art.lockNeedle", "minigame_ibre"),
            (typeof(BagSystem), "firstAidIcon", "ikon_ilk_yardim"),
            (typeof(BagSystem), "flashlightIcon", "ikon_el_feneri"),
            (typeof(BagSystem), "waterIcon", "ikon_su"),
            (typeof(BagSystem), "whistleIcon", "ikon_duduk"),
            (typeof(BagSystem), "bagSprite", "deprem_cantasi"),
            (typeof(MainMenu), "backgroundSprite", "menu_arka_plan"),
            (typeof(MainMenu), "logoSprite", "logo"),
        };

        // "font" alanı olan bütün UI bileşenleri
        static readonly Type[] FontUsers =
        {
            typeof(WishScreen), typeof(SpiritHud), typeof(MinigameHost), typeof(PromptHud),
            typeof(FinalScreen), typeof(PauseMenu), typeof(MainMenu), typeof(ObjectiveHud), typeof(BagHud), typeof(SettingsMenu),
        };

        // Ortak UI stili (Assets/Resources/UiStyle.asset): alan → Assets/Art/UI dosyası
        const string StylePath = "Assets/Resources/UiStyle.asset";
        static readonly (string field, string file)[] StyleSprites =
        {
            ("bubble", "konusma_balonu"),
            ("bubbleTail", "konusma_balonu_kuyruk"),
            ("panel", "hud_ipucu"),
            ("arrow", "hud_ok"),
            ("keyCap", "hud_tus"),
        };

        [MenuItem("Tools/Deprem/Berke: Sistem Prefab'ını Kur")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);

            var root = new GameObject("BerkeSystems");
            try
            {
                root.AddComponent<LoopManager>();
                root.AddComponent<LoopDebugPanel>(); // sadece Editor ve Development Build'de görünür
                root.AddComponent<WishScreen>();
                root.AddComponent<SpiritPhase>();
                root.AddComponent<SpiritHighlighter>();
                root.AddComponent<SpiritHud>();
                root.AddComponent<MinigameHost>();
                BindBagModels(root.AddComponent<BagSystem>());
                root.AddComponent<PromptHud>();
                root.AddComponent<ObjectiveHud>();
                root.AddComponent<BagHud>();
                root.AddComponent<FinalScreen>();
                root.AddComponent<PauseMenu>();
                root.AddComponent<SettingsMenu>();
                root.AddComponent<MainMenu>();
                root.AddComponent<PixelScreen>();

                BindArt(root);
                BindSpiritFrames(root.GetComponent<SpiritPhase>());

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success) throw new IOException($"Prefab kaydedilemedi: {PrefabPath}");
                Debug.Log($"[BerkeSystemsPrefab] Kaydedildi: {PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
        }

        // M20: Mehmet'in çanta malzemesi modelleri (HouseBuilder.BuildMainSceneBatch üretir). Yoksa ikon / küp kalır.
        // M23: kabinin önündeki çanta EarthquakeBag.prefab. BagModelBinder bu klasöre bir prefab gelince
        // BerkeSystems prefab'ında ikisini de kendiliğinden yeniden bağlar.
        internal const string BagModelFolder = "Assets/Prefabs/Mehmet/BagItems";
        internal const string EarthquakeBagPath = BagModelFolder + "/EarthquakeBag.prefab";

        /// <summary>Kayıtlı BerkeSystems prefab'ındaki çanta modellerini yeniler; bir şey değiştiyse true</summary>
        internal static bool RebindBagModelsInPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var bag = prefab ? prefab.GetComponent<BagSystem>() : null;
            if (!bag) return false;
            if (!BindBagModels(bag)) return false;
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssetIfDirty(prefab);
            Debug.Log($"[BerkeSystemsPrefab] Çanta modelleri yeniden bağlandı: {PrefabPath}");
            return true;
        }

        static bool BindBagModels(BagSystem bag)
        {
            var so = new SerializedObject(bag);
            var bagModel = so.FindProperty("bagModel");
            bagModel.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(EarthquakeBagPath);
            var supplies = so.FindProperty("supplies");
            for (int i = 0; i < supplies.arraySize; i++)
            {
                var def = supplies.GetArrayElementAtIndex(i);
                var idProperty = def.FindPropertyRelative("id");
                string id = idProperty.enumNames[idProperty.enumValueIndex];
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{BagModelFolder}/BagItem_{id}.prefab");
                def.FindPropertyRelative("model").objectReferenceValue = model;
                if (!model) Debug.LogWarning($"[BerkeSystemsPrefab] Çanta modeli yok: BagItem_{id} (ikon / küp kullanılır)");
            }
            return so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Ruh animasyonu: Assets/Art/Characters/ruh_1..N.png (hareket) ve ruh_bekleme_1..N.png (M30; ekip çizimi, 128 x 128)
        static void BindSpiritFrames(SpiritPhase phase)
        {
            var so = new SerializedObject(phase);
            int move = SetFrames(so, "spiritFrames", LoadFrames("ruh"));
            int idle = SetFrames(so, "spiritIdleFrames", LoadFrames("ruh_bekleme"));
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[BerkeSystemsPrefab] Ruh kareleri: {move} hareket, {idle} bekleme");
        }

        static Texture2D[] LoadFrames(string prefix)
        {
            const string folder = "Assets/Art/Characters";
            return AssetDatabase.FindAssets(prefix + "_ t:Texture2D", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => (path, name: Path.GetFileNameWithoutExtension(path)))
                .Where(p => p.name.StartsWith(prefix + "_") && int.TryParse(p.name.Substring(prefix.Length + 1), out _))
                .OrderBy(p => int.Parse(p.name.Substring(prefix.Length + 1)))
                .Select(p => AssetDatabase.LoadAssetAtPath<Texture2D>(p.path))
                .Where(t => t)
                .ToArray();
        }

        static int SetFrames(SerializedObject so, string field, Texture2D[] frames)
        {
            var property = so.FindProperty(field);
            property.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            return frames.Length;
        }

        static void BindArt(GameObject root)
        {
            var bound = new List<string>();
            var missing = new List<string>();

            foreach (var (componentType, field, file) in Sprites)
            {
                string path = $"{UiArtFolder}/{file}.png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite) bound.Add(file);
                else missing.Add(file);
                SetReference(root.GetComponent(componentType), field, sprite);
            }

            var font = FindFont();
            foreach (var type in FontUsers) SetReference(root.GetComponent(type), "font", font);
            var bubbleFont = AssetDatabase.LoadAssetAtPath<Font>(BubbleFontPath);
            SetReference(root.GetComponent<SpiritPhase>(), "bubbleFont", bubbleFont ? bubbleFont : font);

            // Ortak stil: balon, panel ve font bütün oyunda (Mehmet'in SpeechBubble'ı dahil) buradan
            var style = AssetDatabase.LoadAssetAtPath<UiStyle>(StylePath);
            if (!style)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StylePath));
                style = ScriptableObject.CreateInstance<UiStyle>();
                AssetDatabase.CreateAsset(style, StylePath);
            }
            style.font = font;
            foreach (var (field, file) in StyleSprites)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArtFolder}/{file}.png");
                if (sprite) bound.Add(file);
                else missing.Add(file);
                var serialized = new SerializedObject(style);
                serialized.FindProperty(field).objectReferenceValue = sprite;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(style);

            Debug.Log($"[BerkeSystemsPrefab] Font: {(font ? AssetDatabase.GetAssetPath(font) : "yok (Unity'nin yerleşik fontu)")}");
            Debug.Log($"[BerkeSystemsPrefab] Bağlanan görseller ({bound.Count}): {string.Join(", ", bound)}");
            if (missing.Count > 0)
                Debug.Log($"[BerkeSystemsPrefab] {UiArtFolder} altında olmayan, düz renkle çizilenler ({missing.Count}): " +
                          string.Join(", ", missing));
        }

        static Font FindFont()
        {
            if (!AssetDatabase.IsValidFolder(FontFolder)) return null;
            var path = AssetDatabase.LoadAssetAtPath<Font>(UiFontPath) ? UiFontPath : AssetDatabase.FindAssets("t:Font", new[] { FontFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p != BubbleFontPath)
                .OrderBy(p => p)
                .FirstOrDefault();
            if (path == null) return null;

            // Pixel font keskin çizilsin: yumuşatmasız raster
            if (AssetImporter.GetAtPath(path) is TrueTypeFontImporter importer &&
                importer.fontRenderingMode != FontRenderingMode.HintedRaster)
            {
                importer.fontRenderingMode = FontRenderingMode.HintedRaster;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Font>(path);
        }

        static void SetReference(Component component, string field, Object value)
        {
            var serialized = new SerializedObject(component);
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[BerkeSystemsPrefab] Alan yok: {component.GetType().Name}.{field}");
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
