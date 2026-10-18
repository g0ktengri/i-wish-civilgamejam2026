using System.IO;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Outro mezarlığının ekran görüntüleri (Logs/outro_*.png): ilk kadraj (yürürken), ikinci mezarda diz çökmüş,
    /// kamera sağa kaymış (dört gül). Balonlar ekran arayüzü olduğu için görüntüde yok.
    /// Batch: -executeMethod Deprem.Quake.OutroPreview.Render
    /// </summary>
    public static class OutroPreview
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void Render()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var outro = Object.FindFirstObjectByType<OutroCinematic>(FindObjectsInactive.Include);
            if (outro == null)
            {
                Debug.LogError("[OutroPreview] Sahnede OutroCinematic yok");
                return;
            }
            var actor = Get<Transform>(outro, "actor");
            var renderer = Get<Renderer>(outro, "actorRenderer");
            var graves = (OutroCinematic.Grave[])typeof(OutroCinematic).GetField("graves", Flags).GetValue(outro);
            var edges = Get<Vector2>(outro, "backdropEdges");
            float centerY = Get<float>(outro, "backdropCenterY");
            float halfHeight = Get<float>(outro, "backdropHalfHeight");
            float firstRight = Get<float>(outro, "firstFrameRight");
            float walkY = Get<float>(outro, "walkY");
            float standOffset = Get<float>(outro, "standOffset");
            var walk = Get<Texture2D[]>(outro, "walkFrames");
            var bend = Get<Texture2D[]>(outro, "bendFrames");
            var bendEmpty = Get<Texture2D[]>(outro, "bendEmptyFrames");

            const int width = 1920, height = 1080;
            float viewHalf = Mathf.Min(Get<float>(outro, "viewHalfHeight"), halfHeight);
            float viewY = Mathf.Clamp(Get<float>(outro, "viewCenterY"), centerY - halfHeight + viewHalf, centerY + halfHeight - viewHalf);
            float lead = Get<float>(outro, "followLead");
            float halfWidth = viewHalf * width / (float)height;
            float Clamp(float x) => Mathf.Clamp(x, edges.x + halfWidth, edges.y - halfWidth);
            float firstX = Clamp(firstRight - halfWidth), lastX = Clamp(graves[3].x - halfWidth * 0.2f);

            // Yürürken kamera karakteri izler (ilk kadraja varmadan, mezarlığın solunda)
            foreach (var g in graves) g.rose.SetActive(false);
            float walkX = edges.x + halfWidth * 1.2f;
            Place(actor, renderer, walkX, walkY, walk[2]);
            Shot("outro_1_yuruyus", Clamp(Mathf.Min(walkX + halfWidth * lead, firstX)), viewY, viewHalf, width, height);

            graves[0].rose.SetActive(true);
            Place(actor, renderer, graves[1].x - standOffset, walkY, bend[bend.Length - 1]);
            Shot("outro_2_egilme", firstX, viewY, viewHalf, width, height);

            graves[1].rose.SetActive(true);
            graves[2].rose.SetActive(true);
            Place(actor, renderer, graves[3].x - standOffset, walkY, bendEmpty[bendEmpty.Length - 1]);
            graves[3].rose.SetActive(true);
            Shot("outro_3_son_mezar", lastX, viewY, viewHalf, width, height);
            Debug.Log("[OutroPreview] Kaydedildi: Logs/outro_*.png");
        }

        private static void Place(Transform actor, Renderer renderer, float x, float y, Texture2D frame)
        {
            actor.gameObject.SetActive(true);
            actor.position = new Vector3(x, y, actor.position.z);
            var block = new MaterialPropertyBlock();
            block.SetTexture("_BaseMap", frame);
            block.SetColor("_BaseColor", new Color(0.8f, 0.82f, 0.95f, 1f));
            renderer.SetPropertyBlock(block);
        }

        private static void Shot(string name, float x, float y, float halfHeight, int width, int height)
        {
            var go = new GameObject("OutroPreviewCamera");
            var cam = go.AddComponent<Camera>();
            var target = new RenderTexture(width, height, 24);
            try
            {
                cam.orthographic = true;
                cam.orthographicSize = halfHeight;
                cam.transform.position = new Vector3(x, y, -10f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.targetTexture = target;
                cam.Render();
                RenderTexture.active = target;
                var output = new Texture2D(width, height, TextureFormat.RGB24, false);
                output.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                output.Apply();
                RenderTexture.active = null;
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes($"Logs/{name}.png", output.EncodeToPNG());
                Object.DestroyImmediate(output);
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(go);
            }
        }

        private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Flags).GetValue(target);
    }
}
