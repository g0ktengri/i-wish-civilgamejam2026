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

            var timer = root.AddComponent<RescueTimer>();
            var bar = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/sarsinti_cubugu.png");
            if (bar != null)
            {
                var timerSo = new SerializedObject(timer);
                timerSo.FindProperty("barSprite").objectReferenceValue = bar;
                timerSo.ApplyModifiedPropertiesWithoutUndo();
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
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
