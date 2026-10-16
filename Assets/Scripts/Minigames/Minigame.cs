using UnityEngine;

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

        public void Build(RectTransform panel)
        {
            Panel = panel;
            OnBuild();
        }

        protected abstract void OnBuild();

        // Bitince true döner
        public abstract bool Tick(MinigameInput input);

        // Panelin ortasına göre konumlanan dikdörtgen bir görüntü (sprite gelene kadar düz renk)
        protected UnityEngine.UI.Image CreateRect(string name, Vector2 position, Vector2 size, Color color,
            Transform parent = null)
        {
            var image = Deprem.UI.UiBuilder.CreateImage(parent ? parent : Panel, name, color);
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return image;
        }
    }
}
