using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Minigames
{
    // Vida sıkma: L demiri levhası vidalanır (1 vida ortada ya da 4 vida köşelerde; MinigameHost.screwCount).
    // Fare tornavidanın sapını tutar; ucu bir vidanın üstüne getirip basılı tut: uç vidaya oturur.
    // Sapı vidanın etrafında saat yönünde çevir; ters yönde çevirmek gevşetir. Bırakınca tornavida kalkar,
    // o vidanın ilerlemesi kalır. Bütün vidalar sıkılınca biter.
    public class ScrewMinigame : Minigame
    {
        static readonly Vector2 PlateCenter = new Vector2(0f, 12f);
        static readonly Vector2 PlateSize = new Vector2(168f, 112f);
        const float ScrewSize = 32f;
        const float CornerInset = 24f;
        const float GrabRadius = 20f;

        // Tornavida parçaları (uçtan sapa, +x yönünde)
        const float TipLength = 8f;
        const float ShaftThickness = 4f;
        const float FerruleLength = 6f;
        const float HandleLength = 40f;
        const float HandleThickness = 16f;
        const float IdleShaft = 46f;
        const float MinShaft = 18f;
        const float MaxShaft = 130f;
        const float IdleAngle = 35f;
        static readonly Vector2 ShadowOffset = new Vector2(3f, -4f);
        // Görsel (minigame_tornavida.png) varsa: 96 x 16, ucu solda; sap ortası uçtan bu kadar uzakta
        static readonly Vector2 ArtDriverSize = new Vector2(96f, 16f);
        const float ArtHandleCenter = 76f;

        static readonly Color PlateColor = new Color(0.55f, 0.57f, 0.6f);
        static readonly Color ScrewLoose = new Color(0.78f, 0.79f, 0.82f);
        static readonly Color ScrewTight = new Color(0.45f, 0.85f, 0.5f);
        static readonly Color SlotColor = new Color(0.25f, 0.26f, 0.28f);
        static readonly Color TipColor = new Color(0.32f, 0.33f, 0.36f);
        static readonly Color ShaftColor = new Color(0.78f, 0.8f, 0.84f);
        static readonly Color FerruleColor = new Color(0.62f, 0.64f, 0.68f);
        static readonly Color HandleColor = new Color(0.86f, 0.3f, 0.18f);
        static readonly Color GripColor = new Color(0.2f, 0.16f, 0.14f);
        static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.35f);

        class Screw
        {
            public Vector2 Position;
            public RectTransform Head;
            public Image Image;
            public float Progress;
            public bool Done => Progress >= 1f;
        }

        // Kodla çizilen tornavida: kök ucunda (pivot), parçalar +x yönünde dizilir
        class Driver
        {
            public RectTransform Root;
            public RectTransform Shaft;
            public RectTransform Ferrule;
            public RectTransform Handle;
            public bool FromArt;

            public float HandleCenter(float shaft) =>
                FromArt ? ArtHandleCenter : TipLength + shaft + FerruleLength + HandleLength * 0.5f;

            public void Layout(float shaft)
            {
                if (FromArt) return;
                Shaft.anchoredPosition = new Vector2(TipLength, 0f);
                Shaft.sizeDelta = new Vector2(shaft, ShaftThickness);
                Ferrule.anchoredPosition = new Vector2(TipLength + shaft, 0f);
                Handle.anchoredPosition = new Vector2(TipLength + shaft + FerruleLength, 0f);
            }
        }

        readonly float turnsPerScrew;
        readonly float reversePenalty;
        readonly float minRadius;
        readonly int screwCount;

        readonly List<Screw> screws = new List<Screw>();
        Driver driver;
        Driver shadow;
        RectTransform fill;
        Image done;
        float barWidth;
        Screw engaged;
        float lastAngle;
        bool hasAngle;

        public ScrewMinigame(float turnsPerScrew, float reversePenalty, float minRadius, int screwCount = 1)
        {
            this.turnsPerScrew = Mathf.Max(0.25f, turnsPerScrew);
            this.reversePenalty = Mathf.Max(0f, reversePenalty);
            this.minRadius = minRadius;
            this.screwCount = screwCount >= 4 ? 4 : 1;
        }

        protected override void OnBuild()
        {
            CreateBackground(Art.screwBackground);
            CreateRect("Plate", PlateCenter, PlateSize, PlateColor, null, Art.screwPlate);

            Vector2 half = PlateSize * 0.5f - Vector2.one * CornerInset;
            var layout = screwCount == 1
                ? new[] { Vector2.zero }
                : new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, -1), new Vector2(1, -1) };
            foreach (var corner in layout)
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

            barWidth = PanelSize.x * 0.75f;
            float barY = -PanelSize.y * 0.42f;
            CreateRect("BarBack", new Vector2(0f, barY), new Vector2(barWidth, 10f), new Color(0.15f, 0.15f, 0.18f));
            fill = CreateRect("BarFill", new Vector2(-barWidth * 0.5f, barY), new Vector2(0f, 10f),
                new Color(0.45f, 0.85f, 0.5f)).rectTransform;
            fill.pivot = new Vector2(0f, 0.5f);

            // Tornavida en üstte; gölgesi hemen altında
            shadow = CreateDriver("ScrewdriverShadow", true);
            driver = CreateDriver("Screwdriver", false);
            PlaceIdle(Vector2.zero);

            // Sabitlenince L demiri (görseli varsa, arka planı saydam)
            if (Art.screwDone)
            {
                done = CreateRect("Done", Vector2.zero, PanelSize, Color.white, null, Art.screwDone);
                done.enabled = false;
            }
        }

        public override bool Tick(MinigameInput input)
        {
            // Tutulan vida: tornavidanın (boştaki) ucunun altındaki vida; fare sapta
            if (input.Pressed) engaged = ScrewAt(IdleTip(input.Pointer));
            if (!input.Held) engaged = null;

            if (engaged != null) Turn(engaged, input.Pointer);
            else
            {
                PlaceIdle(input.Pointer);
                hasAngle = false;
            }

            float total = 0f;
            foreach (var screw in screws) total += screw.Progress;
            fill.sizeDelta = new Vector2(barWidth * total / screws.Count, fill.sizeDelta.y);

            if (total < screws.Count) return false;
            driver.Root.gameObject.SetActive(false);
            shadow.Root.gameObject.SetActive(false);
            if (done) done.enabled = true;
            return true;
        }

        void Turn(Screw screw, Vector2 pointer)
        {
            Vector2 offset = pointer - screw.Position;
            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;

            // Uç vidada, sap farede: şaft aradaki mesafeye göre uzar
            float shaft = Mathf.Clamp(offset.magnitude - TipLength - FerruleLength - HandleLength * 0.5f, MinShaft, MaxShaft);
            PlaceDriver(screw.Position, angle, shaft);

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

        // Boşta: fare sapın ortasında, uç aşağı-solda
        void PlaceIdle(Vector2 pointer) => PlaceDriver(IdleTip(pointer), IdleAngle, IdleShaft);

        Vector2 IdleTip(Vector2 pointer)
        {
            var direction = new Vector2(Mathf.Cos(IdleAngle * Mathf.Deg2Rad), Mathf.Sin(IdleAngle * Mathf.Deg2Rad));
            return pointer - direction * driver.HandleCenter(IdleShaft);
        }

        void PlaceDriver(Vector2 tip, float angle, float shaft)
        {
            foreach (var (d, offset) in new[] { (shadow, ShadowOffset), (driver, Vector2.zero) })
            {
                d.Layout(shaft);
                d.Root.anchoredPosition = tip + offset;
                d.Root.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        Driver CreateDriver(string name, bool isShadow)
        {
            var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(Panel, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0f, 0.5f);
            root.sizeDelta = Vector2.zero;

            Color Tint(Color c) => isShadow ? ShadowColor : c;

            if (Art.screwdriver)
            {
                var image = CreateRect("Sprite", Vector2.zero, ArtDriverSize, Color.white, root, Art.screwdriver);
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                image.preserveAspect = false;
                if (isShadow) image.color = ShadowColor;
                return new Driver { Root = root, FromArt = true };
            }

            var tip = Part(root, "Tip", new Vector2(TipLength, ShaftThickness - 1f), Tint(TipColor));
            tip.anchoredPosition = Vector2.zero;
            var shaftRect = Part(root, "Shaft", new Vector2(IdleShaft, ShaftThickness), Tint(ShaftColor));
            var ferrule = Part(root, "Ferrule", new Vector2(FerruleLength, HandleThickness - 4f), Tint(FerruleColor));

            // Sap: yuvarlak uçlu gövde ve iki koyu tutma bandı
            var handle = new GameObject("Handle", typeof(RectTransform)).GetComponent<RectTransform>();
            handle.SetParent(root, false);
            handle.anchorMin = handle.anchorMax = new Vector2(0f, 0.5f);
            handle.pivot = new Vector2(0f, 0.5f);
            handle.sizeDelta = new Vector2(HandleLength, HandleThickness);
            var body = Part(handle, "Body", new Vector2(HandleLength - HandleThickness, HandleThickness), Tint(HandleColor));
            body.anchoredPosition = new Vector2(HandleThickness * 0.5f, 0f);
            foreach (var x in new[] { HandleThickness * 0.5f, HandleLength - HandleThickness * 0.5f })
            {
                var cap = Part(handle, "Cap", Vector2.one * HandleThickness, Tint(HandleColor), UI.UiBuilder.CircleSprite);
                cap.pivot = new Vector2(0.5f, 0.5f);
                cap.anchoredPosition = new Vector2(x, 0f);
            }
            if (!isShadow)
                foreach (var x in new[] { HandleLength * 0.4f, HandleLength * 0.62f })
                {
                    var grip = Part(handle, "Grip", new Vector2(3f, HandleThickness - 2f), GripColor);
                    grip.pivot = new Vector2(0.5f, 0.5f);
                    grip.anchoredPosition = new Vector2(x, 0f);
                }

            var result = new Driver { Root = root, Shaft = shaftRect, Ferrule = ferrule, Handle = handle };
            result.Layout(IdleShaft);
            return result;
        }

        // Kökün soluna yaslı (pivot sol orta) dikdörtgen parça
        static RectTransform Part(RectTransform parent, string name, Vector2 size, Color color, Sprite sprite = null)
        {
            var image = UI.UiBuilder.CreateImage(parent, name, color);
            if (sprite) image.sprite = sprite;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = size;
            return rect;
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
