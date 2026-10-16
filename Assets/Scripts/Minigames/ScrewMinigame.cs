using UnityEngine;

namespace Deprem.Minigames
{
    // Vida sıkma: fareyle vidanın etrafındaki halkada saat yönünde daire çiz; bar dolunca L demiri takılır.
    // Ters yönde dönmek vidayı gevşetir; halkanın dışında (çok yakın ya da çok uzak) ilerleme olmaz.
    // Hiç dönülmezse vida yavaşça geri gevşer.
    public class ScrewMinigame : Minigame
    {
        static readonly Vector2 ScrewCenter = new Vector2(0f, 10f);

        readonly float requiredTurns;
        readonly float reversePenalty;
        readonly float minRadius;
        readonly float maxRadius;
        readonly float idleLoosen;

        RectTransform head;
        RectTransform fill;
        UnityEngine.UI.Image done;
        float barWidth;
        float progress;
        float lastAngle;
        bool hasAngle;

        public ScrewMinigame(float requiredTurns, float reversePenalty, float minRadius, float maxRadius, float idleLoosen)
        {
            this.requiredTurns = Mathf.Max(0.25f, requiredTurns);
            this.reversePenalty = Mathf.Max(0f, reversePenalty);
            this.minRadius = minRadius;
            this.maxRadius = Mathf.Max(minRadius + 10f, maxRadius);
            this.idleLoosen = Mathf.Max(0f, idleLoosen);
        }

        protected override void OnBuild()
        {
            if (!CreateBackground(Art.screwBackground))
                CreateRect("Bracket", ScrewCenter, new Vector2(96f, 96f), new Color(0.45f, 0.47f, 0.5f));
            // Halka: farenin dönmesi gereken alan
            CreateRect("Ring", ScrewCenter, Vector2.one * maxRadius * 2f, new Color(1f, 1f, 1f, 0.06f));
            head = CreateRect("ScrewHead", ScrewCenter, new Vector2(48f, 48f), new Color(0.75f, 0.77f, 0.8f),
                null, Art.screwHead).rectTransform;
            if (!Art.screwHead)
                CreateRect("Slot", Vector2.zero, new Vector2(38f, 7f), new Color(0.25f, 0.26f, 0.28f), head);

            // Sabitlenince L demiri (görseli varsa)
            if (Art.screwDone)
            {
                done = CreateRect("Done", Vector2.zero, PanelSize, Color.white, null, Art.screwDone);
                done.enabled = false;
            }

            barWidth = PanelSize.x * 0.75f;
            float barY = -PanelSize.y * 0.42f;
            CreateRect("BarBack", new Vector2(0f, barY), new Vector2(barWidth, 12f), new Color(0.15f, 0.15f, 0.18f));
            fill = CreateRect("BarFill", new Vector2(-barWidth * 0.5f, barY), new Vector2(0f, 12f),
                new Color(0.45f, 0.85f, 0.5f)).rectTransform;
            fill.pivot = new Vector2(0f, 0.5f);
        }

        public override bool Tick(MinigameInput input)
        {
            float turned = 0f; // tur cinsinden; + sıkma, - gevşetme
            Vector2 offset = input.Pointer - ScrewCenter;
            float radius = offset.magnitude;

            if (radius < minRadius || radius > maxRadius)
            {
                hasAngle = false;
            }
            else
            {
                float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                if (hasAngle)
                {
                    // Saat yönü = açının azalması (y yukarı)
                    float delta = -Mathf.DeltaAngle(lastAngle, angle) / 360f;
                    turned = delta >= 0f ? delta : delta * reversePenalty;
                }
                lastAngle = angle;
                hasAngle = true;
            }

            if (Mathf.Approximately(turned, 0f)) turned = -idleLoosen * input.DeltaTime;
            progress = Mathf.Clamp01(progress + turned / requiredTurns);

            head.localRotation = Quaternion.Euler(0f, 0f, -progress * requiredTurns * 360f);
            fill.sizeDelta = new Vector2(barWidth * progress, fill.sizeDelta.y);
            if (progress < 1f) return false;
            if (done) done.enabled = true;
            return true;
        }
    }
}
