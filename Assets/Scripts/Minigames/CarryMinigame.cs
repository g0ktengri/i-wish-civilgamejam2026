using UnityEngine;

namespace Deprem.Minigames
{
    // Taşı ve yerleştir: eşyayı fareyle tutup sürükle, güvenli alana bırak.
    public class CarryMinigame : Minigame
    {
        static readonly Vector2 ItemSize = new Vector2(56f, 56f);
        static readonly Vector2 TargetSize = new Vector2(76f, 76f);

        readonly float snapRadius;

        RectTransform item;
        RectTransform target;
        Vector2 grabOffset;
        bool dragging;

        public CarryMinigame(float snapRadius) => this.snapRadius = snapRadius;

        protected override void OnBuild()
        {
            // Eşya bir yanda, güvenli alan diğer yanda; taraf her açılışta rastgele
            float side = Random.value < 0.5f ? -1f : 1f;
            float x = PanelSize.x * 0.28f;
            float y = Random.Range(-PanelSize.y * 0.2f, PanelSize.y * 0.2f);

            target = CreateRect("SafeSpot", new Vector2(side * x, -y), TargetSize, new Color(0.45f, 0.85f, 0.5f, 0.35f)).rectTransform;
            item = CreateRect("Item", new Vector2(-side * x, y), ItemSize, new Color(0.85f, 0.65f, 0.4f)).rectTransform;
        }

        public override bool Tick(MinigameInput input)
        {
            if (input.Pressed && Contains(item, input.Pointer))
            {
                dragging = true;
                grabOffset = item.anchoredPosition - input.Pointer;
            }

            if (dragging && input.Held)
            {
                Vector2 half = (PanelSize - ItemSize) * 0.5f;
                Vector2 position = input.Pointer + grabOffset;
                item.anchoredPosition = new Vector2(Mathf.Clamp(position.x, -half.x, half.x),
                    Mathf.Clamp(position.y, -half.y, half.y));
            }

            if (dragging && (input.Released || !input.Held))
            {
                dragging = false;
                if (Vector2.Distance(item.anchoredPosition, target.anchoredPosition) <= snapRadius)
                {
                    item.anchoredPosition = target.anchoredPosition;
                    return true;
                }
            }
            return false;
        }

        static bool Contains(RectTransform rect, Vector2 point)
        {
            Vector2 half = rect.sizeDelta * 0.5f;
            Vector2 delta = point - rect.anchoredPosition;
            return Mathf.Abs(delta.x) <= half.x && Mathf.Abs(delta.y) <= half.y;
        }
    }
}
