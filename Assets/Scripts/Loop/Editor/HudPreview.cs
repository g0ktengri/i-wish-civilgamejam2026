using System.IO;
using System.Reflection;
using Deprem.Bag;
using Deprem.Core;
using Deprem.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Loop.EditorTools
{
    // M24: HUD'ların görünüşünü oyunu açmadan görmek için. HUD bileşenlerini gerçek kodlarıyla kurar, örnek
    // durumla doldurur ve evin önizlemesinin (Logs/house_preview_game.png) üstüne 1920 x 1080 çizer:
    // Logs/hud_preview_rescue.png (hedef, ipucu, çanta, eklendi yazısı) ve Logs/hud_preview_spirit.png.
    // Batchmode'da -nographics OLMADAN çalışır:
    //   Unity.exe -batchmode -quit -projectPath . -executeMethod Deprem.Loop.EditorTools.HudPreview.Render
    public static class HudPreview
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        [MenuItem("Tools/Deprem/Berke: HUD Önizlemesi")]
        public static void Render()
        {
            RenderScene("rescue", root =>
            {
                var prompt = root.AddComponent<PromptHud>();
                Call(prompt, "Awake");
                Call(prompt, "HandlePromptChanged", "prompt.duck_cover");

                var objective = root.AddComponent<ObjectiveHud>();
                Call(objective, "Awake");
                Get<CanvasGroup>(objective, "group").alpha = 1f;
                Call(objective, "Refresh");

                GameState.AddBagItem(BagItemId.FirstAidKit);
                GameState.AddBagItem(BagItemId.Flashlight);
                var bag = root.AddComponent<BagHud>();
                Call(bag, "Awake"); // Awake çantadaki malzemelerle kurar
                Get<CanvasGroup>(bag, "group").alpha = 1f;
                var toast = Get<Text>(bag, "toastText");
                toast.text = Deprem.Localization.Texts.Format("bag.added",
                    $"<color={UiBuilder.Hex(UiBuilder.HudAccent)}>{Deprem.Localization.Texts.Get("bag.Flashlight")}</color>");
                ((RectTransform)toast.rectTransform.parent).sizeDelta = new Vector2(toast.preferredWidth + 32f, 36f);
                Get<CanvasGroup>(bag, "toastGroup").alpha = 1f;
            });

            RenderScene("spirit", root =>
            {
                var spirit = root.AddComponent<SpiritHud>();
                Call(spirit, "Awake");
                Get<CanvasGroup>(spirit, "group").alpha = 1f;
                Get<Text>(spirit, "timerText").text = "0:42";
                typeof(SpiritHud).GetField("shownPrompt", Flags).SetValue(spirit, "prompt.secure");
                typeof(SpiritHud).GetField("shownCarry", Flags).SetValue(spirit, BagItemId.Flashlight);
                Call(spirit, "RefreshStaticTexts");

                GameState.AddBagItem(BagItemId.FirstAidKit);
                var bag = root.AddComponent<BagHud>();
                Call(bag, "Awake"); // Awake çantadaki malzemelerle kurar
                Get<CanvasGroup>(bag, "group").alpha = 1f;
            });
        }

        static void RenderScene(string name, System.Action<GameObject> build)
        {
            const int Width = 1920, Height = 1080;
            GameState.ResetAll();
            var root = new GameObject("HudPreview");
            var camera = new GameObject("HudPreviewCamera").AddComponent<Camera>();
            var target = new RenderTexture(Width, Height, 24);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.2f, 0.2f, 0.22f);
                camera.orthographic = true;
                camera.targetTexture = target;

                // Arka plan: evin önizlemesi (yoksa düz gri)
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
                else image.color = camera.backgroundColor;

                build(root);

                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1f + canvas.sortingOrder * -0.001f;
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = target;
                var output = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                output.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                output.Apply();
                RenderTexture.active = null;
                Directory.CreateDirectory("Logs");
                string path = $"Logs/hud_preview_{name}.png";
                File.WriteAllBytes(path, output.EncodeToPNG());
                Object.DestroyImmediate(output);
                Debug.Log($"[HudPreview] Kaydedildi: {path}");
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(root);
                GameState.ResetAll();
            }
        }

        static void Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Flags).Invoke(target, args);

        static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Flags).GetValue(target);
    }
}
