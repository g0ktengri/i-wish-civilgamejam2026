using System.IO;
using Deprem.Bag;
using Deprem.Minigames;
using Deprem.Spirit;
using Deprem.UI;
using UnityEditor;
using UnityEngine;

namespace Deprem.Loop.EditorTools
{
    // Berke'nin sistemlerini tek prefab'da toplar: Assets/Prefabs/Berke/BerkeSystems.prefab.
    // Mehmet'in BerkeSystemsSetup'ı bu klasördeki prefab'ları Main sahnesine otomatik ekler.
    // Batchmode: -executeMethod Deprem.Loop.EditorTools.BerkeSystemsPrefab.Build
    public static class BerkeSystemsPrefab
    {
        const string Folder = "Assets/Prefabs/Berke";
        const string PrefabPath = Folder + "/BerkeSystems.prefab";

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
                root.AddComponent<BagSystem>();
                root.AddComponent<PromptHud>();
                root.AddComponent<FinalScreen>();
                root.AddComponent<PauseMenu>();
                root.AddComponent<MainMenu>();

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
    }
}
