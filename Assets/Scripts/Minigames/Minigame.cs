using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Minigames
{
    // Bir kare için fare girdisi; konum minigame panelinin yerel koordinatında (merkez 0,0).
    public struct MinigameInput
    {
        public Vector2 Pointer;
        public bool Held;
        public bool Pressed;
        public bool Released;
        public float DeltaTime;
    }

    public enum ScrewLayout { Center1, Horizontal2, Vertical2, Diagonal2, Triangle3 }
    public enum CarryMode { Single, Moving, Double }

    // Berke'nin play testi: minigame'ler çok kolay ve hep aynıydı. Her eşya kendi kurulumunu alır
    // (MinigameHost.VariantFor); Seed her açılışta değişir, yerleşim biraz oynar.
    public class MinigameVariant
    {
        public int Seed;
        public ScrewLayout Screws = ScrewLayout.Horizontal2;
        public CarryMode Carry = CarryMode.Single;
        [Tooltip("Taşınan şeyin çizimi yoksa: shelf, mirror, frame, box")]
        public string CarryKind = "box";
        [Tooltip("Kilit: her isabetten sonra yeşil alanın kayma hızı (bar eni / sn, isabet başına)")]
        public float LockDrift = 0.16f;
    }

    // Minigame türlerinin ortak tabanı. MinigameHost paneli kurar ve her kare Tick çağırır.
    public abstract class Minigame
    {
        protected RectTransform Panel { get; private set; }
        protected Vector2 PanelSize => Panel.rect.size;
        protected MinigameArt Art { get; private set; }
        protected MinigameVariant Variant { get; private set; }
        protected System.Random Random { get; private set; }

        public void Build(RectTransform panel, MinigameArt art, MinigameVariant variant = null)
        {
            Panel = panel;
            Art = art ?? new MinigameArt();
            Variant = variant ?? new MinigameVariant { Seed = UnityEngine.Random.Range(0, 100000) };
            Random = new System.Random(Variant.Seed);
            OnBuild();
        }

        protected abstract void OnBuild();

        // Bitince true döner
        public abstract bool Tick(MinigameInput input);

        /// <summary>
        /// M61 (Mehmet'in play testi 31): bu türe ilk girişte öğretici adımları: texts.csv anahtarı ve vurgulanacak alan
        /// (panelin ortasına göre, referans pikseli). Öğreticisi olmayan tür null döner.
        /// </summary>
        public virtual List<(string key, Rect area)> TutorialSteps() => null;

        protected static Rect Around(Vector2 center, Vector2 size) => new Rect(center - size * 0.5f, size);


        // Panelin ortasına göre konumlanan dikdörtgen görüntü: sprite varsa o, yoksa düz renk
        protected Image CreateRect(string name, Vector2 position, Vector2 size, Color color,
            Transform parent = null, Sprite sprite = null)
        {
            var image = UI.UiBuilder.CreateImage(parent ? parent : Panel, name, sprite ? Color.white : color);
            if (sprite)
            {
                image.sprite = sprite;
                image.preserveAspect = true;
            }
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return image;
        }

        // Sanatçı görseli varsa o, yoksa kodla çizilen pixel parça (MinigamePixels; 1 doku pikseli = 2 referans pikseli).
        // size referans pikseli; pixel parça bu boyutta, en-boy oranı korunmadan çizilir.
        protected Image CreatePixel(string name, Vector2 position, Vector2 size, Sprite art, Sprite pixel,
            Transform parent = null)
        {
            var image = CreateRect(name, position, size, Color.white, parent, art ? art : pixel);
            if (!art) image.preserveAspect = false;
            return image;
        }

        protected static Vector2Int TexSize(Vector2 size) =>
            new Vector2Int(Mathf.Max(2, Mathf.RoundToInt(size.x / MinigamePixels.Scale)), Mathf.Max(2, Mathf.RoundToInt(size.y / MinigamePixels.Scale)));

        // Sprite verilmişse paneli kaplayan arka plan; yoksa null
        protected Image CreateBackground(Sprite sprite) =>
            sprite ? CreateRect("Background", Vector2.zero, PanelSize, Color.white, null, sprite) : null;

        // Sanatçı arka planı yoksa pixel arka plan
        protected Image CreateBackground(Sprite art, Sprite pixel) =>
            CreatePixel("Background", Vector2.zero, PanelSize, art, pixel);

        protected float Range(float min, float max) => min + (float)Random.NextDouble() * (max - min);
    }
}
