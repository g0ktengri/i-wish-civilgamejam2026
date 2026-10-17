using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Pixel art görünüm (Berke'nin play testi: 3D modeller pixel oyuna uymuyordu): ana kamera düşük çözünürlüklü
    // bir dokuya çizer (varsayılan 360 satır; 64 px = 1 m ile ekranda ~10 m), doku nokta filtreyle tam ekrana
    // büyütülür. 3D duvarlar, mobilyalar, ışık ve gölgeler karakter sprite'larıyla aynı piksel ızgarasına oturur.
    // HUD, balonlar ve menüler (Screen Space Overlay) tam çözünürlükte kalır; yazılar okunur.
    // Not: kamera bir dokuya çizdiği için ekran noktası gerekirse WorldToViewportPoint kullanılmalı
    // (WorldToScreenPoint dokunun piksellerini verir).
    // Ekrana hiçbir kamera çizmezse Unity "No cameras rendering" yazar: hiçbir şey çizmeyen, sadece siyaha
    // temizleyen bir çıkış kamerası ekrana bağlı kalır.
    public class PixelScreen : MonoBehaviour
    {
        [Tooltip("Düşük çözünürlüğün satır sayısı (en, ekranın oranından)")]
        [SerializeField] int targetHeight = 360;
        [Tooltip("Kapalıysa kamera doğrudan ekrana çizer (karşılaştırmak için)")]
        [SerializeField] bool pixelate = true;
        [Tooltip("Bütün HUD'ların altında")]
        [SerializeField] int sortingOrder = -100;

        Camera target;
        RenderTexture texture;
        RawImage screen;
        Canvas canvas;
        Camera output;
        int lastWidth, lastHeight;

        void LateUpdate()
        {
            if (!pixelate)
            {
                Release();
                return;
            }

            if (target == null) target = Camera.main;
            if (target == null) return;
            if (texture == null || Screen.width != lastWidth || Screen.height != lastHeight) Rebuild();
            if (target.targetTexture != texture) target.targetTexture = texture;
        }

        void Rebuild()
        {
            Release();
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            int height = Mathf.Max(90, targetHeight);
            int width = Mathf.Max(160, Mathf.RoundToInt(height * (float)lastWidth / Mathf.Max(1, lastHeight)));

            texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Point,
                name = "PixelScreen",
            };
            texture.Create();
            target.targetTexture = texture;

            if (canvas == null)
            {
                canvas = UiBuilder.CreateCanvas(transform, "PixelScreenCanvas", sortingOrder);
                screen = UiBuilder.CreateChild<RawImage>(canvas.transform, "Screen");
                screen.raycastTarget = false;
                UiBuilder.Stretch(screen.rectTransform, Vector2.zero, Vector2.one);
            }
            screen.texture = texture;
            canvas.gameObject.SetActive(true);

            if (output == null)
            {
                var go = new GameObject("PixelScreenOutput");
                go.transform.SetParent(transform, false);
                output = go.AddComponent<Camera>();
                output.clearFlags = CameraClearFlags.SolidColor;
                output.backgroundColor = Color.black;
                output.cullingMask = 0;
                output.depth = -100f;
                output.allowHDR = false;
                output.allowMSAA = false;
            }
            output.enabled = true;
        }

        void Release()
        {
            if (target != null && target.targetTexture == texture) target.targetTexture = null;
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
                texture = null;
            }
            if (canvas != null) canvas.gameObject.SetActive(false);
            if (output != null) output.enabled = false;
        }

        void OnDisable() => Release();
    }
}
