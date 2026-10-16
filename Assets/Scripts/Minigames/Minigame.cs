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

    // Minigame türlerinin ortak tabanı. MinigameHost paneli kurar ve her kare Tick çağırır.
    public abstract class Minigame
    {
        protected RectTransform Panel { get; private set; }
        protected Vector2 PanelSize => Panel.rect.size;
        protected MinigameArt Art { get; private set; }

        public void Build(RectTransform panel, MinigameArt art)
        {
            Panel = panel;
            Art = art ?? new MinigameArt();
            OnBuild();
        }

        protected abstract void OnBuild();

        // Bitince true döner
        public abstract bool Tick(MinigameInput input);

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

        // Sprite verilmişse paneli kaplayan arka plan; yoksa null
        protected Image CreateBackground(Sprite sprite) =>
            sprite ? CreateRect("Background", Vector2.zero, PanelSize, Color.white, null, sprite) : null;
    }
}
