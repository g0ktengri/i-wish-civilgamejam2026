using System.IO;
using System.Reflection;
using Deprem.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Quake
{
    /// <summary>
    /// Çök-kapan öğreticisinin ekran görüntüsü (Logs/duck_cover_tutorial_N.png): evin önizlemesinin üstüne karartma,
    /// çerçeve, adım paneli. Vurgu alanı elle verilir (dünya hizası oyunda denenir). Batch: -executeMethod
    /// Deprem.Quake.DuckCoverTutorialPreview.Render
    /// </summary>
    public static class DuckCoverTutorialPreview
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static void Render()
        {
            var areas = new[] { new Rect(-150f, -150f, 170f, 110f), new Rect(-150f, -150f, 290f, 160f), new Rect(60f, -150f, 80f, 190f) };
            for (int step = 0; step < areas.Length; step++) RenderStep(step, areas[step]);
        }

        private static void RenderStep(int step, Rect area)
        {
            const int Width = 1920, Height = 1080;
            var root = new GameObject("DuckCoverTutorialPreview");
            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            var target = new RenderTexture(Width, Height, 24);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.2f, 0.2f, 0.22f);
                camera.orthographic = true;
                camera.targetTexture = target;

                var background = UiBuilder.CreateCanvas(root.transform, "Background", -100);
                var image = UiBuilder.CreateChild<RawImage>(background.transform, "House");
                UiBuilder.Stretch(image.rectTransform, Vector2.zero, Vector2.one);
                const string house = "Logs/house_preview_game.png";
                if (File.Exists(house))
                {
                    var texture = new Texture2D(2, 2);
                    texture.LoadImage(File.ReadAllBytes(house));
                    image.texture = texture;
                }

                var tutorial = root.AddComponent<DuckCoverTutorial>();
                Call(tutorial, "Build");
                var canvas = (Canvas)typeof(DuckCoverTutorial).GetField("canvas", Flags).GetValue(tutorial);
                canvas.gameObject.SetActive(true);
                string[] keys = { "tutorial.duck_cover.table", "tutorial.duck_cover.walk", "tutorial.duck_cover.hold" };
                var text = (Text)typeof(DuckCoverTutorial).GetField("text", Flags).GetValue(tutorial);
                text.text = $"({step + 1}/3) {Localization.Texts.Get(keys[step])}";
                var next = (UiPrompt)typeof(DuckCoverTutorial).GetField("next", Flags).GetValue(tutorial);
                next.Set(Localization.Texts.Get("minigame.tutorial.continue"));

                foreach (var c in root.GetComponentsInChildren<Canvas>(true))
                {
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = camera;
                    c.planeDistance = 1f + c.sortingOrder * -0.001f;
                }
                Canvas.ForceUpdateCanvases();
                Call(tutorial, "Highlight", area);
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = target;
                var output = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                output.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                output.Apply();
                RenderTexture.active = null;
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes($"Logs/duck_cover_tutorial_{step + 1}.png", output.EncodeToPNG());
                Object.DestroyImmediate(output);
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(root);
            }
        }

        private static void Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Flags).Invoke(target, args);
    }
}
