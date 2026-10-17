using System.IO;
using Deprem.Bag;
using Deprem.Core;
using Deprem.Minigames;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// M37 önizlemesi: minigame'in arka planı (eşyanın odası) ve taşınan şeyin görüntüsü oyundaki nesnelerden nasıl
    /// çekiliyor. Logs/minigame_closeup_*.png. Batchmode'da -nographics OLMADAN:
    /// -executeMethod Deprem.Quake.MinigameCloseUpPreview.Render
    /// </summary>
    public static class MinigameCloseUpPreview
    {
        [MenuItem("Tools/Deprem/Minigame Yakın Çekim Önizlemesi")]
        public static void Render()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            Directory.CreateDirectory("Logs");
            var panel = new Vector2(340f, 300f);
            foreach (var item in Object.FindObjectsByType<SecurableItem>(FindObjectsSortMode.None))
            {
                if (item.Id != ItemId.Bookcase && item.Id != ItemId.BedShelf && item.Id != ItemId.KitchenCabinets) continue;
                bool carry = item.Minigame == MinigameType.CarryAndPlace;
                Save(MinigameCloseUp.RenderScene(MinigameCloseUp.BoundsOf(item.gameObject), panel, 2f, 1.9f,
                    carry ? item.gameObject : null), $"Logs/minigame_closeup_{item.Id}.png");
                if (carry) SaveSprite(MinigameCloseUp.RenderObject(item.gameObject, new Vector2(50f, 28f), 1f), $"Logs/minigame_item_{item.Id}.png");
            }
            foreach (var supply in Object.FindObjectsByType<BagSupply>(FindObjectsSortMode.None))
            {
                if (supply.Id != BagItemId.FirstAidKit && supply.Id != BagItemId.Flashlight) continue;
                Save(MinigameCloseUp.RenderScene(MinigameCloseUp.BoundsOf(supply.gameObject), panel, 2f, 1.9f, supply.gameObject),
                    $"Logs/minigame_closeup_{supply.Id}.png");
                SaveSprite(MinigameCloseUp.RenderObject(supply.gameObject, new Vector2(42f, 34f), 1f), $"Logs/minigame_item_{supply.Id}.png");
            }
            EditorSceneManager.CloseScene(scene, false);
            Debug.Log("[CloseUpPreview] Kaydedildi: Logs/minigame_closeup_*.png, Logs/minigame_item_*.png");
        }

        private static void Save(RenderTexture rt, string path)
        {
            if (!rt) return;
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = previous;
            File.WriteAllBytes(path, Upscale(tex, 3).EncodeToPNG());
            rt.Release();
        }

        private static void SaveSprite(Sprite sprite, string path)
        {
            if (!sprite) return;
            File.WriteAllBytes(path, Upscale(sprite.texture, 6).EncodeToPNG());
        }

        // Point büyütme: pikseller görünsün
        private static Texture2D Upscale(Texture2D src, int k)
        {
            var dst = new Texture2D(src.width * k, src.height * k, TextureFormat.RGBA32, false);
            for (int y = 0; y < dst.height; y++)
                for (int x = 0; x < dst.width; x++)
                    dst.SetPixel(x, y, src.GetPixel(x / k, y / k));
            dst.Apply();
            return dst;
        }
    }
}
