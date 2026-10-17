using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deprem.Quake
{
    /// <summary>
    /// Deprem sistemini prefab olarak (Assets/Prefabs/Mehmet/QuakeSystem.prefab) sahneye koyar.
    /// Sahnede döngü yöneticisi yokken denenebilsin diye sahnedeki kopyada devStartLoop açılır.
    /// </summary>
    internal static class QuakeSetup
    {
        public const string PrefabPath = "Assets/Prefabs/Mehmet/QuakeSystem.prefab";
        private const string SystemName = "QuakeSystem";

        [MenuItem("Tools/Deprem/Deprem Sistemini Kur (açık sahne)")]
        private static void SetupMenu()
        {
            var scene = SceneManager.GetActiveScene();
            SetupInScene(scene, 3);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[QuakeSetup] Deprem sistemi kuruldu: " + scene.name);
        }

        public static GameObject SetupInScene(Scene scene, int devStartLoop)
        {
            var prefab = CreateOrUpdatePrefab();
            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == SystemName).ToList())
                Object.DestroyImmediate(old);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = SystemName;
            var so = new SerializedObject(instance.GetComponent<QuakeDirector>());
            so.FindProperty("devStartLoop").intValue = devStartLoop;
            so.ApplyModifiedPropertiesWithoutUndo();
            return instance;
        }

        private static GameObject CreateOrUpdatePrefab()
        {
            string folder = Path.GetDirectoryName(PrefabPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
                throw new System.InvalidOperationException(folder + " yok; önce oyuncu kurulumu çalışmalı");

            var root = new GameObject(SystemName);
            var director = root.AddComponent<QuakeDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("debrisMaterial").objectReferenceValue = DebrisMaterial();
            so.ApplyModifiedPropertiesWithoutUndo();

            // Tavandan toz ve sıva: her odanın tavanının altı (ev ölçüleri HouseLayout'tan)
            var ceilings = Deprem.HouseBuilder.HouseLayout.Rooms.Select(room => new QuakeEffects.Ceiling
            {
                center = new Vector3((room.X0 + room.X1) * 0.5f, room.CeilingY - 0.05f, room.Depth * 0.45f),
                size = new Vector2(room.Width - 0.4f, room.Depth * 0.7f),
            }).ToArray();
            root.AddComponent<QuakeEffects>().Configure(ceilings, DustMaterial(), DebrisMaterial());

            // Süre çubuğu artık parçalı pixel ölçer (SegmentMeter, Berke'nin paneli); ayrı sprite kullanılmıyor
            root.AddComponent<RescueTimer>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Toz tanesi ve toz bulutu: URP Particles/Unlit, saydam; renk parçacıktan gelir.
        /// Sanatçı dokusu (Assets/Art/Particles/toz_kucuk.png) varsa onu kullanır.</summary>
        private static Material DustMaterial()
        {
            const string folder = "Assets/Generated/Quake";
            const string path = folder + "/toz.mat";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Generated", "Quake");
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            var art = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Particles/toz_kucuk.png");
            mat.SetTexture("_BaseMap", art != null ? art : PixelDustTexture());
            mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Sanatçı dokusu yokken toz: 16 x 16 pixel toz bulutu. Play testi 5: kare taneler çirkindi;
        /// play testi 9: yumuşak yuvarlak toz pixel stile uymuyordu. Keskin pikseller: merkez dolu, kenara doğru
        /// 4 x 4 Bayer düzeniyle seyrelir (yumuşak geçiş yok), biraz düzensiz kenar. Point filtre.
        /// Kodla üretilir: Assets/Generated/Quake/toz_pixel.png.
        /// </summary>
        private static Texture2D PixelDustTexture()
        {
            const string path = "Assets/Generated/Quake/toz_pixel.png";
            const int size = 16;
            int[,] bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                // Hafif düzensiz bulut: köşelere doğru biraz şişkin
                float d = Mathf.Sqrt(dx * dx + dy * dy) * (1f - 0.12f * Mathf.Sin(3f * Mathf.Atan2(dy, dx)));
                float density = Mathf.Clamp01((1f - d) * 1.6f);
                bool on = density * 16f > bayer[y % 4, x % 4];
                // İki ton: merkez daha opak, kenar pikselleri yarı saydam (pixel art'ta iki kademe)
                float alpha = on ? (density > 0.6f ? 1f : 0.55f) : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            const string oldPath = "Assets/Generated/Quake/toz_yumusak.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(oldPath) != null) AssetDatabase.DeleteAsset(oldPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Tavandan düşen sıva: sanatçı dokusu (Assets/Art/Particles/siva.png) varsa onu kullanır</summary>
        private static Material DebrisMaterial()
        {
            const string folder = "Assets/Generated/Quake";
            const string path = folder + "/siva.mat";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Generated", "Quake");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Particles/siva.png");
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", tex != null ? Color.white : new Color(0.82f, 0.78f, 0.7f));
            mat.SetFloat("_Smoothness", 0.05f);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
