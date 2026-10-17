using System.Linq;
using UnityEditor;

namespace Deprem.Loop.EditorTools
{
    // M20 / M23: Mehmet çanta modellerini (Prefabs/Mehmet/BagItems: BagItem_*, EarthquakeBag) üretince ya da
    // pull ile gelince BerkeSystems prefab'ındaki model alanları kendiliğinden yeniden bağlanır; Berke'nin
    // prefab'ı elle yeniden kurmasına gerek kalmaz. Main'deki kopya prefab'dan alır.
    sealed class BagModelBinder : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            string folder = BerkeSystemsPrefab.BagModelFolder + "/";
            bool touched = imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                .Any(path => path.StartsWith(folder) && path.EndsWith(".prefab"));
            if (touched) BerkeSystemsPrefab.RebindBagModelsInPrefab();
        }
    }
}
