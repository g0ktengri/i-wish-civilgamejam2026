using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Deprem.HouseBuilder
{
    /// <summary>Evi önden gören geçici bir kamerayla PNG önizleme çizer (kontrol amaçlı).</summary>
    internal static class HousePreview
    {
        public static void Render(string path, float ambient, Vector3 cameraPosition, int width, int height)
        {
            var previousAmbient = RenderSettings.ambientLight;
            RenderSettings.ambientLight = new Color(ambient, ambient, ambient * 1.1f);

            var go = new GameObject("PreviewCamera");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 25f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
            cam.transform.position = cameraPosition;

            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request))
                RenderPipeline.SubmitRenderRequest(cam, request);
            else
                cam.Render();

            var previousActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previousActive;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            RenderSettings.ambientLight = previousAmbient;
        }
    }
}
