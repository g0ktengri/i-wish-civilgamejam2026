using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Minigames
{
    // Kilitle: ibre ray üzerinde gidip gelir; yeşil alandayken tıkla. Her isabette mandalın sürgüsü bir adım kapanır;
    // gereken isabet sayısında kilitlenir. Her isabette yeşil alan daralır, ibre hızlanır ve alan kaymaya başlar
    // (Berke'nin play testi: çok kolay ve hep aynıydı; MinigameVariant.LockDrift).
    // Iskalayınca bir isabet geri gider, sürgü geri kayar, ray kısa süre kırmızı yanar, yeşil alan yer değiştirir.
    // Play testi 8: alan ve hız birikimliydi (ıskalayınca geri gelmiyordu), birkaç denemede geçilemez oluyordu.
    // Artık hepsi o anki isabet sayısından hesaplanır: ıskalayınca bir önceki zorluğa döner.
    public class LockMinigame : Minigame
    {
        const float BarHeight = 24f;
        const float FlashTime = 0.25f;
        const float BoltTravel = 44f;

        static readonly Color PipOff = new Color(0.22f, 0.18f, 0.16f);
        static readonly Color PipOn = MinigamePixels.Accent;

        readonly float startSpeed;
        readonly float startZoneWidth;
        readonly int requiredHits;
        readonly float shrinkPerHit;
        readonly float speedUpPerHit;

        Image bar;
        RectTransform zone;
        RectTransform needle;
        RectTransform bolt;
        Vector2 boltOpen;
        Image[] pips;
        float barWidth;
        float time;
        float speed;
        float zoneWidth;
        float zoneCenter;
        float drift;
        float driftDirection = 1f;
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
            barWidth = PanelSize.x * 0.78f;
            speed = startSpeed;
            zoneWidth = startZoneWidth;
            var panelTex = TexSize(PanelSize);
            CreateBackground(Art.lockBackground, MinigamePixels.CabinetDoor(panelTex.x, panelTex.y));

            // Mandal: sağda karşılık, solda kapanan sürgü (sanatçı arka planı varsa sadece ışıklar ve ray)
            if (!Art.lockBackground)
            {
                var plateSize = new Vector2(32f, 44f);
                var plate = CreatePixel("LatchPlate", new Vector2(36f, 48f), plateSize, null,
                    MinigamePixels.LatchPlate(TexSize(plateSize).x, TexSize(plateSize).y));
                boltOpen = new Vector2(-34f, 48f);
                var boltSize = new Vector2(48f, 12f);
                bolt = CreatePixel("Bolt", boltOpen, boltSize, null, MinigamePixels.Bolt(TexSize(boltSize).x, TexSize(boltSize).y)).rectTransform;
                plate.transform.SetAsLastSibling();
            }

            pips = new Image[requiredHits];
            float spacing = 18f;
            for (int i = 0; i < requiredHits; i++)
            {
                float x = (i - (requiredHits - 1) * 0.5f) * spacing;
                pips[i] = CreatePixel($"Pip{i}", new Vector2(x, 4f), new Vector2(12f, 12f), null, MinigamePixels.ScrewHead(TexSize(new Vector2(12f, 12f)).x));
                pips[i].color = PipOff;
            }

            var trackSize = new Vector2(barWidth + 8f, BarHeight);
            bar = CreatePixel("Bar", new Vector2(0f, -56f), trackSize, null, MinigamePixels.Track(TexSize(trackSize).x, TexSize(trackSize).y));
            zone = CreateRect("Zone", Vector2.zero, new Vector2(barWidth * zoneWidth, BarHeight - 8f), Color.white, bar.transform).rectTransform;
            var needleSize = Art.lockNeedle ? new Vector2(8f, 32f) : new Vector2(14f, 36f);
            needle = CreatePixel("Needle", Vector2.zero, needleSize, Art.lockNeedle,
                MinigamePixels.Needle(TexSize(needleSize).x, TexSize(needleSize).y), bar.transform).rectTransform;
            MoveZone();
        }

        public override bool Tick(MinigameInput input)
        {
            time += input.DeltaTime * speed;
            float t = Mathf.PingPong(time, 1f);
            needle.anchoredPosition = new Vector2((t - 0.5f) * barWidth, 6f);

            // İsabetten sonra yeşil alan yavaşça kayar, kenarda döner
            if (drift > 0f)
            {
                zoneCenter += driftDirection * drift * input.DeltaTime;
                float half = zoneWidth * 0.5f;
                if (zoneCenter < half || zoneCenter > 1f - half)
                {
                    driftDirection = -driftDirection;
                    zoneCenter = Mathf.Clamp(zoneCenter, half, 1f - half);
                }
                zone.anchoredPosition = new Vector2((zoneCenter - 0.5f) * barWidth, 0f);
            }

            if (flash > 0f)
            {
                flash -= input.DeltaTime;
                if (flash <= 0f) bar.color = Color.white;
            }
            if (bolt)
            {
                // Sürgü hedefine doğru kayar
                var target = boltOpen + Vector2.right * BoltTravel * hits / requiredHits;
                bolt.anchoredPosition = Vector2.MoveTowards(bolt.anchoredPosition, target, 240f * input.DeltaTime);
            }

            if (!input.Pressed) return false;

            if (Mathf.Abs(t - zoneCenter) <= zoneWidth * 0.5f)
            {
                hits++;
                Audio.Sfx.Play("minigame.latch");
                if (hits >= requiredHits)
                {
                    UpdatePips();
                    if (bolt) bolt.anchoredPosition = boltOpen + Vector2.right * BoltTravel;
                    return true;
                }
                Flash(MinigamePixels.Safe);
            }
            else
            {
                hits = Mathf.Max(0, hits - 1);
                Audio.Sfx.Play("minigame.miss");
                Flash(MinigamePixels.Danger);
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
            drift = Variant.LockDrift * hits;
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
            zoneCenter = Range(zoneWidth * 0.5f, 1f - zoneWidth * 0.5f);
            driftDirection = Random.NextDouble() < 0.5 ? -1f : 1f;
            var size = new Vector2(barWidth * zoneWidth, BarHeight - 8f);
            zone.sizeDelta = size;
            zone.anchoredPosition = new Vector2((zoneCenter - 0.5f) * barWidth, 0f);
            var image = zone.GetComponent<Image>();
            image.sprite = MinigamePixels.Zone(TexSize(size).x, TexSize(size).y);
            image.preserveAspect = false;
            image.color = Color.white;
        }
    }
}
