using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Minigames
{
    // Vida sıkma: L demiri levhası 4 köşesinden vidalanır.
    // Bir vidanın üstüne tıklayıp basılı tut: tornavida vidaya oturur. Fareyi vidanın etrafında
    // saat yönünde çevir; tornavida da döner. Ters yönde çevirmek gevşetir. Bırakınca tornavida kalkar,
    // o vidanın ilerlemesi kalır. Dört vida da sıkılınca biter.
    public class ScrewMinigame : Minigame
    {
        static readonly Vector2 PlateCenter = new Vector2(0f, 12f);
        static readonly Vector2 PlateSize = new Vector2(168f, 112f);
        static readonly Vector2 DriverSize = new Vector2(96f, 16f);
        const float ScrewSize = 32f;
        const float CornerInset = 24f;
        const float GrabRadius = 20f;

        static readonly Color PlateColor = new Color(0.55f, 0.57f, 0.6f);
        static readonly Color ScrewLoose = new Color(0.78f, 0.79f, 0.82f);
        static readonly Color ScrewTight = new Color(0.45f, 0.85f, 0.5f);
        static readonly Color SlotColor = new Color(0.25f, 0.26f, 0.28f);
        static readonly Color DriverColor = new Color(0.9f, 0.55f, 0.2f);

        class Screw
        {
            public Vector2 Position;
            public RectTransform Head;
            public Image Image;
            public float Progress;
            public bool Done => Progress >= 1f;
        }

        readonly float turnsPerScrew;
        readonly float reversePenalty;
        readonly float minRadius;

        readonly List<Screw> screws = new List<Screw>();
        RectTransform driver;
        RectTransform fill;
        Image done;
        float barWidth;
        Screw engaged;
        float lastAngle;
        bool hasAngle;

        public ScrewMinigame(float turnsPerScrew, float reversePenalty, float minRadius)
        {
            this.turnsPerScrew = Mathf.Max(0.25f, turnsPerScrew);
            this.reversePenalty = Mathf.Max(0f, reversePenalty);
            this.minRadius = minRadius;
        }

        protected override void OnBuild()
        {
            CreateBackground(Art.screwBackground);
            CreateRect("Plate", PlateCenter, PlateSize, PlateColor, null, Art.screwPlate);

            Vector2 half = PlateSize * 0.5f - Vector2.one * CornerInset;
            foreach (var corner in new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, -1), new Vector2(1, -1) })
            {
                var position = PlateCenter + Vector2.Scale(half, corner);
                var image = CreateRect("Screw", position, Vector2.one * ScrewSize, ScrewLoose, null,
                    Art.screwHead ? Art.screwHead : UI.UiBuilder.CircleSprite);
                if (!Art.screwHead)
                {
                    image.color = ScrewLoose; // kodla üretilen beyaz daire renklendirilir
                    CreateRect("Slot", Vector2.zero, new Vector2(ScrewSize * 0.75f, 4f), SlotColor, image.rectTransform);
                }
                screws.Add(new Screw { Position = position, Head = image.rectTransform, Image = image });
            }

            // Tornavida: ucu sol ortada (pivot), sapı fareye doğru uzanır
            driver = CreateRect("Screwdriver", Vector2.zero, DriverSize, DriverColor, null, Art.screwdriver).rectTransform;
            driver.pivot = new Vector2(0f, 0.5f);
            if (!Art.screwdriver)
            {
                // Sap turuncu, uç (sol yarı) metal
                CreateRect("Tip", new Vector2(-DriverSize.x * 0.25f, 0f), new Vector2(DriverSize.x * 0.5f, 4f),
                    new Color(0.75f, 0.77f, 0.8f), driver);
            }

            barWidth = PanelSize.x * 0.75f;
            float barY = -PanelSize.y * 0.42f;
            CreateRect("BarBack", new Vector2(0f, barY), new Vector2(barWidth, 10f), new Color(0.15f, 0.15f, 0.18f));
            fill = CreateRect("BarFill", new Vector2(-barWidth * 0.5f, barY), new Vector2(0f, 10f),
                new Color(0.45f, 0.85f, 0.5f)).rectTransform;
            fill.pivot = new Vector2(0f, 0.5f);

            // Sabitlenince L demiri (görseli varsa, arka planı saydam)
            if (Art.screwDone)
            {
                done = CreateRect("Done", Vector2.zero, PanelSize, Color.white, null, Art.screwDone);
                done.enabled = false;
            }
        }

        public override bool Tick(MinigameInput input)
        {
            if (input.Pressed) engaged = ScrewAt(input.Pointer);
            if (!input.Held) engaged = null;

            if (engaged != null) Turn(engaged, input.Pointer);
            else
            {
                // Boşta tornavida fareyi takip eder (ucu imleçte)
                driver.anchoredPosition = input.Pointer;
                driver.localRotation = Quaternion.Euler(0f, 0f, 35f);
                hasAngle = false;
            }

            float total = 0f;
            foreach (var screw in screws) total += screw.Progress;
            fill.sizeDelta = new Vector2(barWidth * total / screws.Count, fill.sizeDelta.y);

            if (total < screws.Count) return false;
            driver.gameObject.SetActive(false);
            if (done) done.enabled = true;
            return true;
        }

        void Turn(Screw screw, Vector2 pointer)
        {
            Vector2 offset = pointer - screw.Position;
            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;

            // Tornavidanın ucu vidada, sapı fareye bakar
            driver.anchoredPosition = screw.Position;
            if (offset.sqrMagnitude > 1f) driver.localRotation = Quaternion.Euler(0f, 0f, angle);

            if (offset.magnitude < minRadius)
            {
                hasAngle = false;
                return;
            }

            if (hasAngle)
            {
                // Saat yönü = açının azalması (y yukarı)
                float delta = -Mathf.DeltaAngle(lastAngle, angle) / 360f;
                float turned = delta >= 0f ? delta : delta * reversePenalty;
                screw.Progress = Mathf.Clamp01(screw.Progress + turned / turnsPerScrew);
                screw.Head.localRotation = Quaternion.Euler(0f, 0f, -screw.Progress * turnsPerScrew * 360f);
                if (!Art.screwHead || screw.Done)
                    screw.Image.color = screw.Done ? ScrewTight : Color.Lerp(ScrewLoose, Color.white, screw.Progress * 0.3f);
                if (screw.Done)
                {
                    engaged = null;
                    hasAngle = false;
                    return;
                }
            }
            lastAngle = angle;
            hasAngle = true;
        }

        Screw ScrewAt(Vector2 pointer)
        {
            Screw best = null;
            float bestDistance = GrabRadius;
            foreach (var screw in screws)
            {
                if (screw.Done) continue;
                float distance = Vector2.Distance(pointer, screw.Position);
                if (distance > bestDistance) continue;
                best = screw;
                bestDistance = distance;
            }
            hasAngle = false;
            return best;
        }
    }
}
