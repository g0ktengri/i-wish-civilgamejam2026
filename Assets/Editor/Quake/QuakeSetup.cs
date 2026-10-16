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
            root.AddComponent<QuakeDirector>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
