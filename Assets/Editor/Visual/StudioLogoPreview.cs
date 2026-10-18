using System.IO;
using UnityEngine;

namespace Deprem.Visual
{
    /// <summary>
    /// Stüdyo logosunun karelerini batch modda çizer: Logs/studio_logo/frame_NNNN.png (30 fps, 1920x1080).
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod Deprem.Visual.StudioLogoPreview.Render
    /// </summary>
    public static class StudioLogoPreview
    {
        const int Width = 1920, Height = 1080, Fps = 30;

        public static void Render()
        {
            const string folder = "Logs/studio_logo";
            Directory.CreateDirectory(folder);
            foreach (var old in Directory.GetFiles(folder, "*.png")) File.Delete(old);

            var camera = new GameObject("StudioLogoPreviewCamera").AddComponent<Camera>();
            var target = new RenderTexture(Width, Height, 24);
            var logo = new GameObject("StudioLogo").AddComponent<StudioLogo>();
            var output = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.1f, 0.1f); // kapanış ekranının yerine
                camera.orthographic = true;
                camera.targetTexture = target;
                logo.Build();
                foreach (var canvas in logo.GetComponentsInChildren<Canvas>(true))
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1f;
                }

                int frames = Mathf.CeilToInt(logo.Length * Fps);
                for (int i = 0; i <= frames; i++)
                {
                    logo.Evaluate(i / (float)Fps);
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    RenderTexture.active = target;
                    output.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    output.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes($"{folder}/frame_{i:0000}.png", output.EncodeToPNG());
                }
                Debug.Log($"[StudioLogoPreview] {frames + 1} kare: {folder}");
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(output);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(logo.gameObject);
            }
        }
    }
}
