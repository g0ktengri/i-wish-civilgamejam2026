using UnityEngine;

namespace Deprem.Minigames
{
    // Vida sıkma: fareyle vidanın etrafında saat yönünde daire çiz; bar dolunca L demiri takılır.
    public class ScrewMinigame : Minigame
    {
        static readonly Vector2 ScrewCenter = new Vector2(0f, 10f);

        readonly float requiredTurns;
        readonly bool clockwiseOnly;
        readonly float minRadius;

        RectTransform head;
        RectTransform fill;
        float barWidth;
        float progress;
        float lastAngle;
        bool hasAngle;

        public ScrewMinigame(float requiredTurns, bool clockwiseOnly, float minRadius)
        {
            this.requiredTurns = Mathf.Max(0.25f, requiredTurns);
            this.clockwiseOnly = clockwiseOnly;
            this.minRadius = minRadius;
        }

        protected override void OnBuild()
        {
            CreateRect("Bracket", ScrewCenter, new Vector2(120f, 120f), new Color(0.45f, 0.47f, 0.5f));
            head = CreateRect("ScrewHead", ScrewCenter, new Vector2(56f, 56f), new Color(0.75f, 0.77f, 0.8f)).rectTransform;
            CreateRect("Slot", Vector2.zero, new Vector2(44f, 8f), new Color(0.25f, 0.26f, 0.28f), head);

            barWidth = PanelSize.x * 0.75f;
            float barY = -PanelSize.y * 0.38f;
            CreateRect("BarBack", new Vector2(0f, barY), new Vector2(barWidth, 14f), new Color(0.15f, 0.15f, 0.18f));
            fill = CreateRect("BarFill", new Vector2(-barWidth * 0.5f, barY), new Vector2(0f, 14f),
                new Color(0.45f, 0.85f, 0.5f)).rectTransform;
            fill.pivot = new Vector2(0f, 0.5f);
        }

        public override bool Tick(MinigameInput input)
        {
            Vector2 offset = input.Pointer - ScrewCenter;
            if (offset.magnitude < minRadius)
            {
                hasAngle = false;
                return false;
            }

            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            if (hasAngle)
            {
                float delta = Mathf.DeltaAngle(lastAngle, angle);
                // Saat yönü = açının azalması (y yukarı)
                float turn = clockwiseOnly ? Mathf.Max(0f, -delta) : Mathf.Abs(delta);
                progress = Mathf.Min(1f, progress + turn / 360f / requiredTurns);
                head.localRotation = Quaternion.Euler(0f, 0f, -progress * requiredTurns * 360f);
                fill.sizeDelta = new Vector2(barWidth * progress, fill.sizeDelta.y);
            }
            lastAngle = angle;
            hasAngle = true;
            return progress >= 1f;
        }
    }
}
