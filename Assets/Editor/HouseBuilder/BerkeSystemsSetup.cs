using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deprem.HouseBuilder
{
    /// <summary>
    /// Bootstrap yerine: Assets/Prefabs/Berke altındaki her prefab Main sahnesine kökte bir kopya olarak eklenir
    /// (aynı adlı eski kopya silinir). Berke prefab'ını klasöre koyar; Main kurulurken otomatik gelir.
    /// Sahne her kayıpta yeniden yüklendiği için çalışma anında ayrı bir yükleyiciye gerek yok.
    /// </summary>
    internal static class BerkeSystemsSetup
    {
        private const string Folder = "Assets/Prefabs/Berke";

        [MenuItem("Tools/Deprem/Berke'nin Sistemlerini Sahneye Ekle (açık sahne)")]
        private static void SetupMenu()
        {
            var scene = SceneManager.GetActiveScene();
            int count = SetupInScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[BerkeSystemsSetup] {count} prefab eklendi: {scene.name}");
        }

        public static int SetupInScene(Scene scene)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) return 0;
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { Folder });
            foreach (var guid in guids)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var old in scene.GetRootGameObjects().Where(g => g.name == prefab.name).ToList())
                    Object.DestroyImmediate(old);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = prefab.name;
            }
            return guids.Length;
        }
    }
}
