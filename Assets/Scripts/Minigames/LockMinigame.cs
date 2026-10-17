using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Minigames
{
    // Kilitle: ibre bar üzerinde gidip gelir; yeşil alandayken tıkla. Mandalın kilitlenmesi için
    // birkaç isabet gerekir; her isabette yeşil alan daralır ve ibre hızlanır.
    // Iskalayınca bir isabet geri gider, bar kısa süre kırmızı yanar, yeşil alan yer değiştirir.
    // Play testi 8: alan ve hız birikimliydi (ıskalayınca geri gelmiyordu), birkaç denemede geçilemez oluyordu.
    // Artık ikisi de o anki isabet sayısından hesaplanır: ıskalayınca bir önceki zorluğa döner.
    public class LockMinigame : Minigame
    {
        const float BarHeight = 18f;
        const float FlashTime = 0.25f;

        static readonly Color BarColor = new Color(0.15f, 0.15f, 0.18f);
        static readonly Color MissColor = new Color(0.6f, 0.15f, 0.15f);
        static readonly Color HitColor = new Color(0.2f, 0.45f, 0.25f);
        static readonly Color PipOff = new Color(0.3f, 0.3f, 0.35f);
        static readonly Color PipOn = new Color(0.45f, 0.85f, 0.5f);

        readonly float startSpeed;
        readonly float startZoneWidth;
        readonly int requiredHits;
        readonly float shrinkPerHit;
        readonly float speedUpPerHit;

        Image bar;
        RectTransform zone;
        RectTransform needle;
        Image[] pips;
        float barWidth;
        float time;
        float speed;
        float zoneWidth;
        float zoneCenter;
        float flash;
        int hits;

        public LockMinigame(float cyclesPerSecond, float zoneWidth, int requiredHits, float shrinkPerHit, float speedUpPerHit)
        {
            startSpeed = cyclesPerSecond;
            startZoneWidth = Mathf.Clamp(zoneWidth, 0.05f, 0.9f);
            this.requiredHits = Mathf.Max(1, requiredHits);
            this.shrinkPerHit = Mathf.Clamp(shrinkPerHit, 0.3f, 1f);
            this.speedUpPerHit = Mathf.Max(1f, speedUpPerHit);
        }

        protected override void OnBuild()
        {
            barWidth = PanelSize.x * 0.8f;
            speed = startSpeed;
            zoneWidth = startZoneWidth;

            if (!CreateBackground(Art.lockBackground))
                CreateRect("Latch", new Vector2(0f, 50f), new Vector2(70f, 40f), new Color(0.6f, 0.62f, 0.66f));

            pips = new Image[requiredHits];
            float spacing = 22f;
            for (int i = 0; i < requiredHits; i++)
            {
                float x = (i - (requiredHits - 1) * 0.5f) * spacing;
                pips[i] = CreateRect($"Pip{i}", new Vector2(x, 10f), new Vector2(14f, 14f), PipOff);
            }

            bar = CreateRect("Bar", new Vector2(0f, -40f), new Vector2(barWidth, BarHeight), BarColor);
            zone = CreateRect("Zone", Vector2.zero, new Vector2(barWidth * zoneWidth, BarHeight),
                new Color(0.45f, 0.85f, 0.5f), bar.transform).rectTransform;
            needle = CreateRect("Needle", Vector2.zero, new Vector2(Art.lockNeedle ? 8f : 4f, 32f), Color.white,
                bar.transform, Art.lockNeedle).rectTransform;
            MoveZone();
        }

        public override bool Tick(MinigameInput input)
        {
            time += input.DeltaTime * speed;
            float t = Mathf.PingPong(time, 1f);
            needle.anchoredPosition = new Vector2((t - 0.5f) * barWidth, 0f);

            if (flash > 0f)
            {
                flash -= input.DeltaTime;
                if (flash <= 0f) bar.color = BarColor;
            }

            if (!input.Pressed) return false;

            if (Mathf.Abs(t - zoneCenter) <= zoneWidth * 0.5f)
            {
                hits++;
                if (hits >= requiredHits)
                {
                    UpdatePips();
                    return true;
                }
                Flash(HitColor);
            }
            else
            {
                hits = Mathf.Max(0, hits - 1);
                Flash(MissColor);
            }

            ApplyDifficulty();
            UpdatePips();
            MoveZone();
            return false;
        }

        void ApplyDifficulty()
        {
            zoneWidth = startZoneWidth * Mathf.Pow(shrinkPerHit, hits);
            speed = startSpeed * Mathf.Pow(speedUpPerHit, hits);
        }

        void Flash(Color color)
        {
            flash = FlashTime;
            bar.color = color;
        }

        void UpdatePips()
        {
            for (int i = 0; i < pips.Length; i++) pips[i].color = i < hits ? PipOn : PipOff;
        }

        void MoveZone()
        {
            zoneCenter = Random.Range(zoneWidth * 0.5f, 1f - zoneWidth * 0.5f);
            zone.sizeDelta = new Vector2(barWidth * zoneWidth, BarHeight);
            zone.anchoredPosition = new Vector2((zoneCenter - 0.5f) * barWidth, 0f);
        }
    }
}
