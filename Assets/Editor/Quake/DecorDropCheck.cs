using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Play testi 19: masadaki vazo düşüp kırılmıyordu. Main sahnesinde her dekoru düşürür, fiziği 3 sn elle işletir,
    /// nerede kaldığını ve kırılıp kırılmadığını yazar. Batchmode: -executeMethod Deprem.Quake.DecorDropCheck.Run
    /// </summary>
    public static class DecorDropCheck
    {
        [MenuItem("Tools/Deprem/Test: Dekor Düşme")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var previous = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var decors = Object.FindObjectsByType<FallingDecor>(FindObjectsSortMode.None);
            foreach (var d in decors) typeof(FallingDecor).GetMethod("Awake", flags).Invoke(d, null);
            var start = new Vector3[decors.Length];
            for (int i = 0; i < decors.Length; i++)
            {
                start[i] = decors[i].transform.position;
                decors[i].Drop();
            }
            // Edit mode'da FixedUpdate ve çarpışma mesajları gelmez: dekorun adımı elle çağrılır (yere oturunca kırılma)
            var fixedUpdate = typeof(FallingDecor).GetMethod("FixedUpdate", flags);
            for (int step = 0; step < 150; step++)
            {
                Physics.Simulate(0.02f);
                foreach (var d in decors) fixedUpdate.Invoke(d, null);
            }

            int stuck = 0;
            for (int i = 0; i < decors.Length; i++)
            {
                var d = decors[i];
                var p = d.transform.position;
                bool fell = start[i].y - p.y > 0.3f;
                if (!fell) stuck++;
                Debug.Log($"[DecorDrop] {d.name}: {start[i]} -> {p}  düştü={fell} kırıldı={d.IsBroken}");
            }
            Debug.Log($"[DecorDrop] SONUÇ: {decors.Length} dekor, {stuck} tanesi düşmedi");
            Physics.simulationMode = previous;
            EditorSceneManager.CloseScene(scene, false);
        }
    }
}
