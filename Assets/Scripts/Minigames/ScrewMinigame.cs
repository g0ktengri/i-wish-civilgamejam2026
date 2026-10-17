using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Minigames
{
    // Vida sıkma: L demiri levhası dolabın arkasına vidalanır. Vida sayısı ve düzeni eşyaya göre (MinigameVariant.Screws:
    // ağır kitaplık / gardırop üç vida, TV ünitesi yatay, oyuncak rafı dikey, ayakkabılık çapraz); her açılışta biraz oynar.
    // Fare tornavidanın sapını tutar; ucu bir vidanın üstüne getirip basılı tut: uç vidaya oturur. Sapı vidanın
    // etrafında saat yönünde çevir; ters yönde çevirmek gevşetir. maxTurnsPerSecond > 0 ise çok hızlı çevirince uç
    // vidadan kayar (play testi 16'dan beri kapalı). Tornavida sabit boydadır: fare uzaklaşınca uzamaz, sadece döner.
    // Bırakınca tornavida kalkar, o vidanın ilerlemesi kalır. Bütün vidalar sıkılınca biter.
    public class ScrewMinigame : Minigame
    {
        static readonly Vector2 PlateCenter = new Vector2(0f, 14f);
        const float ScrewSize = 32f;
        const float GrabRadius = 20f;
        const float SlipFlashTime = 0.3f;

        // Tornavida parçaları (uçtan sapa, +x yönünde)
        const float TipLength = 8f;
        const float ShaftThickness = 4f;
        const float FerruleLength = 6f;
        const float HandleLength = 40f;
        const float HandleThickness = 16f;
        const float IdleShaft = 46f;
        const float IdleAngle = 35f;
        static readonly Vector2 ShadowOffset = new Vector2(3f, -4f);
        // Görsel (minigame_tornavida.png) varsa: 96 x 16, ucu solda; sap ortası uçtan bu kadar uzakta
        static readonly Vector2 ArtDriverSize = new Vector2(96f, 16f);
        const float ArtHandleCenter = 76f;

        static readonly Color ScrewLoose = Color.white;
        static readonly Color ScrewTight = new Color(0.72f, 1f, 0.72f);
        static readonly Color TipColor = new Color(0.3f, 0.3f, 0.33f);
        static readonly Color ShaftColor = new Color(0.8f, 0.81f, 0.84f);
        static readonly Color FerruleColor = new Color(0.6f, 0.61f, 0.64f);
        static readonly Color HandleColor = new Color(0.78f, 0.3f, 0.2f);
        static readonly Color GripColor = new Color(0.22f, 0.15f, 0.12f);
        static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.35f);

        class Screw
        {
            public Vector2 Position;
            public RectTransform Head;
            public Image Image;
            public float Progress;
            public float Slip;
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
        readonly float maxTurnsPerSecond;

        readonly List<Screw> screws = new List<Screw>();
        Driver driver;
        Driver shadow;
        RectTransform fill;
        Image done;
        float barWidth;
        Screw engaged;
        float lastAngle;
        bool hasAngle;

        // maxTurnsPerSecond: bundan hızlı çevirince uç kayar (0 = kapalı)
        public ScrewMinigame(float turnsPerScrew, float reversePenalty, float minRadius, float maxTurnsPerSecond = 0f)
        {
            this.turnsPerScrew = Mathf.Max(0.25f, turnsPerScrew);
            this.reversePenalty = Mathf.Max(0f, reversePenalty);
            this.minRadius = minRadius;
            this.maxTurnsPerSecond = maxTurnsPerSecond;
        }

        // Düzene göre levha boyutu ve vida konumları (levha merkezine göre, referans pikseli)
        static (Vector2 plate, Vector2[] screws) LayoutFor(ScrewLayout layout)
        {
            switch (layout)
            {
                case ScrewLayout.Center1: return (new Vector2(120f, 80f), new[] { Vector2.zero });
                case ScrewLayout.Vertical2: return (new Vector2(84f, 160f), new[] { new Vector2(0f, 46f), new Vector2(0f, -46f) });
                case ScrewLayout.Diagonal2: return (new Vector2(176f, 104f), new[] { new Vector2(-58f, 24f), new Vector2(58f, -24f) });
                case ScrewLayout.Triangle3: return (new Vector2(176f, 120f), new[] { new Vector2(-60f, -30f), new Vector2(60f, -30f), new Vector2(0f, 32f) });
                default: return (new Vector2(176f, 84f), new[] { new Vector2(-58f, 0f), new Vector2(58f, 0f) });
            }
        }

        protected override void OnBuild()
        {
            var panelTex = TexSize(PanelSize);
            CreateBackground(Art.screwBackground, MinigamePixels.Boards(panelTex.x, panelTex.y, Variant.Seed % 5));

            var (plateSize, layout) = LayoutFor(Variant.Screws);
            bool mirror = Random.NextDouble() < 0.5;
            var holes = new List<Vector2Int>();
            var positions = new List<Vector2>();
            foreach (var p in layout)
            {
                // Her açılışta vida yerleri biraz oynar; çapraz düzen yarı yarıya aynalanır
                var local = new Vector2(mirror && Variant.Screws == ScrewLayout.Diagonal2 ? -p.x : p.x, p.y)
                            + new Vector2(Range(-6f, 6f), Range(-5f, 5f));
                local = new Vector2(Mathf.Round(local.x / 2f) * 2f, Mathf.Round(local.y / 2f) * 2f);
                positions.Add(local);
                holes.Add(new Vector2Int(Mathf.RoundToInt(local.x / MinigamePixels.Scale), Mathf.RoundToInt(local.y / MinigamePixels.Scale)));
            }
            var tex = TexSize(plateSize);
            // Levhanın altında yumuşak gölge: tahtanın üstünde dursun
            if (!Art.screwPlate)
                CreatePixel("PlateShadow", PlateCenter + new Vector2(4f, -5f), plateSize + Vector2.one * 6f, null,
                    MinigamePixels.Shadow(tex.x + 6, tex.y + 6));
            CreatePixel("Plate", PlateCenter, plateSize, Art.screwPlate,
                MinigamePixels.Plate(tex.x, tex.y, holes, string.Join("_", holes)));

            foreach (var local in positions)
            {
                var position = PlateCenter + local;
                var image = CreatePixel("Screw", position, Vector2.one * ScrewSize, Art.screwHead,
                    MinigamePixels.ScrewHead(TexSize(Vector2.one * ScrewSize).x));
                image.color = ScrewLoose;
                screws.Add(new Screw { Position = position, Head = image.rectTransform, Image = image });
            }

            barWidth = PanelSize.x * 0.72f;
            float barY = -PanelSize.y * 0.4f;
            var trackSize = new Vector2(barWidth + 8f, 16f);
            CreatePixel("BarBack", new Vector2(0f, barY), trackSize, null, MinigamePixels.Track(TexSize(trackSize).x, TexSize(trackSize).y));
            fill = CreateRect("BarFill", new Vector2(-barWidth * 0.5f, barY), new Vector2(0f, 8f), MinigamePixels.Accent).rectTransform;
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

            if (engaged != null) Turn(engaged, input.Pointer, input.DeltaTime);
            else
            {
                PlaceIdle(input.Pointer);
                hasAngle = false;
            }

            float total = 0f;
            foreach (var screw in screws)
            {
                total += screw.Progress;
                if (screw.Slip > 0f)
                {
                    screw.Slip -= input.DeltaTime;
                    screw.Image.color = screw.Slip > 0f ? MinigamePixels.Danger : screw.Done ? ScrewTight : ScrewLoose;
                }
            }
            fill.sizeDelta = new Vector2(barWidth * total / screws.Count, fill.sizeDelta.y);

            if (total < screws.Count) return false;
            Audio.Sfx.Play("minigame.bracket"); // L demiri takıldı
            driver.Root.gameObject.SetActive(false);
            shadow.Root.gameObject.SetActive(false);
            if (done) done.enabled = true;
            return true;
        }

        void Turn(Screw screw, Vector2 pointer, float dt)
        {
            Vector2 offset = pointer - screw.Position;
            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;

            // Uç vidada, sap fareye doğru; boy sabit (play testi 16: çekiştirince tornavida uzuyordu)
            PlaceDriver(screw.Position, angle, IdleShaft);

            if (offset.magnitude < minRadius)
            {
                hasAngle = false;
                return;
            }

            if (hasAngle)
            {
                // Saat yönü = açının azalması (y yukarı)
                float delta = -Mathf.DeltaAngle(lastAngle, angle) / 360f;

                // Çok hızlı çevirince uç vidadan kayar: oyuncu kontrollü çevirmeli
                if (maxTurnsPerSecond > 0f && dt > 0f && Mathf.Abs(delta) / dt > maxTurnsPerSecond)
                {
                    screw.Slip = SlipFlashTime;
                    screw.Image.color = MinigamePixels.Danger;
                    Audio.Sfx.Play("minigame.miss");
                    engaged = null;
                    hasAngle = false;
                    return;
                }

                float turned = delta >= 0f ? delta : delta * reversePenalty;
                int quarters = Mathf.FloorToInt(screw.Progress * turnsPerScrew * 4f);
                screw.Progress = Mathf.Clamp01(screw.Progress + turned / turnsPerScrew);
                // M25: her çeyrek turda tıkırtı
                if (Mathf.FloorToInt(screw.Progress * turnsPerScrew * 4f) > quarters) Audio.Sfx.Play("minigame.screw");
                screw.Head.localRotation = Quaternion.Euler(0f, 0f, -screw.Progress * turnsPerScrew * 360f);
                // Sıkıldıkça vida başı hafifçe küçülür (levhaya gömülür)
                screw.Head.localScale = Vector3.one * Mathf.Lerp(1f, 0.82f, screw.Progress);
                if (screw.Done)
                {
                    screw.Image.color = ScrewTight;
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
