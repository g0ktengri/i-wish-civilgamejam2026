using System.Reflection;
using Deprem.HouseBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deprem.Player
{
    /// <summary>
    /// Yataktan kalkma animasyonunu oyunu açmadan görmek için: Main sahnesinde oyuncuyu yatağa yatırır, kalkışın
    /// beş anını (yatıyor ... ayakta) yakın kamerayla çizer: Logs/getup_preview_0..4.png.
    /// Batchmode'da -nographics OLMADAN: -executeMethod Deprem.Player.GetUpPreview.Render
    /// </summary>
    public static class GetUpPreview
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        [MenuItem("Tools/Deprem/Yataktan Kalkma Önizlemesi")]
        public static void Render()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var player = Object.FindFirstObjectByType<PlayerController>();
            var visual = player.GetComponent<PlayerVisual>();
            // QuakeDirector.LieInBed gibi: şilte üstü (0,66), yatağın ön kenarı (0,75 önde)
            var bed = GameObject.Find("Marker_PlayerBed").transform.position + Vector3.up * 0.66f + Vector3.back * 0.75f;
            player.Initialize();
            Call(visual, "Awake");

            player.LieDown(bed);
            var stand = new Vector3(bed.x, 0f, 1f);
            player.GetUp(stand);
            float duration = (float)typeof(PlayerController).GetField("getUpDuration", Flags).GetValue(player);
            var timeField = typeof(PlayerController).GetField("getUpTime", Flags);

            float[] moments = { 0f, 0.3f, 0.45f, 0.6f, 0.8f, 0.99f };
            for (int i = 0; i < moments.Length; i++)
            {
                timeField.SetValue(player, moments[i] * duration);
                // Edit mode'da fizik adımı yok: kök elle konur (oyunda MovePosition)
                player.transform.position = player.GetUpPosition;
                Call(visual, "LateUpdate");
                HousePreview.Render($"Logs/getup_preview_{i}.png", 0.75f, new Vector3(1.9f, 1.3f, -4.5f), 960, 640);
            }
            EditorSceneManager.CloseScene(scene, false);
            Debug.Log("[GetUpPreview] Kaydedildi: Logs/getup_preview_0..4.png");
        }

        private static void Call(object target, string method) =>
            target.GetType().GetMethod(method, Flags).Invoke(target, null);
    }
}
