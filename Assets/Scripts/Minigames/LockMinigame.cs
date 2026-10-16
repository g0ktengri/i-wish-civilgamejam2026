using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Minigames
{
    // Kilitle: ibre bar üzerinde gidip gelir; yeşil alandayken tıkla, mandal kilitlensin.
    // Iskalarsa bar kısa süre kırmızı yanar, yeşil alan yer değiştirir.
    public class LockMinigame : Minigame
    {
        const float BarHeight = 18f;
        const float MissFlashTime = 0.25f;

        static readonly Color BarColor = new Color(0.15f, 0.15f, 0.18f);
        static readonly Color MissColor = new Color(0.6f, 0.15f, 0.15f);

        readonly float cyclesPerSecond;
        readonly float zoneWidth;

        Image bar;
        RectTransform zone;
        RectTransform needle;
        float barWidth;
        float time;
        float zoneCenter;
        float missFlash;

        public LockMinigame(float cyclesPerSecond, float zoneWidth)
        {
            this.cyclesPerSecond = cyclesPerSecond;
            this.zoneWidth = Mathf.Clamp(zoneWidth, 0.05f, 0.9f);
        }

        protected override void OnBuild()
        {
            barWidth = PanelSize.x * 0.8f;
            CreateRect("Latch", new Vector2(0f, 40f), new Vector2(70f, 40f), new Color(0.6f, 0.62f, 0.66f));
            bar = CreateRect("Bar", new Vector2(0f, -40f), new Vector2(barWidth, BarHeight), BarColor);
            zone = CreateRect("Zone", Vector2.zero, new Vector2(barWidth * zoneWidth, BarHeight),
                new Color(0.45f, 0.85f, 0.5f), bar.transform).rectTransform;
            needle = CreateRect("Needle", Vector2.zero, new Vector2(4f, BarHeight + 14f), Color.white,
                bar.transform).rectTransform;
            MoveZone();
        }

        public override bool Tick(MinigameInput input)
        {
            time += input.DeltaTime * cyclesPerSecond;
            float t = Mathf.PingPong(time, 1f);
            needle.anchoredPosition = new Vector2((t - 0.5f) * barWidth, 0f);

            if (missFlash > 0f)
            {
                missFlash -= input.DeltaTime;
                bar.color = missFlash > 0f ? MissColor : BarColor;
            }

            if (!input.Pressed) return false;
            if (Mathf.Abs(t - zoneCenter) <= zoneWidth * 0.5f) return true;

            missFlash = MissFlashTime;
            MoveZone();
            return false;
        }

        void MoveZone()
        {
            zoneCenter = Random.Range(zoneWidth * 0.5f, 1f - zoneWidth * 0.5f);
            zone.anchoredPosition = new Vector2((zoneCenter - 0.5f) * barWidth, 0f);
        }
    }
}
