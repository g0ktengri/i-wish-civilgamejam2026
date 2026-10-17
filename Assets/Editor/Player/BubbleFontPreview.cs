using System.IO;
using Deprem.Localization;
using Deprem.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Player
{
    /// <summary>
    /// Play testi 13: balon yazısı okunmuyordu. Konuşma balonunu aday fontlar ve boylarla oyundaki gibi
    /// (1920 x 1080, referans 960 x 540) yan yana çizer: Logs/bubble_font_preview.png.
    /// Batchmode'da -nographics OLMADAN: -executeMethod Deprem.Player.BubbleFontPreview.Render
    /// </summary>
    public static class BubbleFontPreview
    {
        private static readonly (string label, string path, int size)[] Candidates =
        {
            ("Pixelify Sans 18 (şimdiki)", "Assets/Art/Fonts/PixelifySans.ttf", 18),
            ("Yeni e/E 18 (oyunda)", PlayerSetup.BubbleFontPath, 18),
            ("Yeni e/E 22", PlayerSetup.BubbleFontPath, 22),
        };

        private static readonly string[] Lines =
        {
            "bubble.deja_vu", "bubble.door_jammed", "bubble.family_missing.firstaidkit", "bubble.panic_run",
        };

        [MenuItem("Tools/Deprem/Balon Fontu Önizlemesi")]
        public static void Render()
        {
            const int Width = 1920, Height = 1080;
            var root = new GameObject("BubblePreview");
            var camera = new GameObject("BubblePreviewCamera").AddComponent<Camera>();
            var target = new RenderTexture(Width, Height, 24);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.16f, 0.15f, 0.17f);
                camera.orthographic = true;
                camera.targetTexture = target;

                var canvas = UiBuilder.CreateCanvas(root.transform, "Canvas", 0);
                float columnWidth = UiBuilder.ReferenceResolution.x / Candidates.Length;
                for (int c = 0; c < Candidates.Length; c++)
                {
                    var (label, path, size) = Candidates[c];
                    var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                    float x = -UiBuilder.ReferenceResolution.x / 2f + columnWidth * (c + 0.5f);

                    var title = UiBuilder.CreateText(canvas.transform, "Title", null, 14, Color.white);
                    title.rectTransform.sizeDelta = new Vector2(columnWidth, 24f);
                    title.rectTransform.anchoredPosition = new Vector2(x, 240f);
                    title.text = font ? label : label + " (yok)";

                    for (int i = 0; i < Lines.Length; i++)
                    {
                        var bubble = UiBuilder.CreateSpeechBubble(canvas.transform, font, size);
                        bubble.SetText(Texts.Get(Lines[i]), columnWidth - 30f);
                        bubble.Root.anchoredPosition = new Vector2(x, 110f - i * 120f);
                    }
                }

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = target;
                var output = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                output.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                output.Apply();
                RenderTexture.active = null;
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/bubble_font_preview.png", output.EncodeToPNG());
                Object.DestroyImmediate(output);
                Debug.Log("[BubbleFontPreview] Kaydedildi: Logs/bubble_font_preview.png");
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(root);
            }
        }
    }
}
