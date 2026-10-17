using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Minigames
{
    // Taşı ve yerleştir: eşyayı fareyle tutup sürükle, güvenli alana bırak. Aradaki tuğla duvarın boşluğundan geçmeli;
    // duvara çarparsa eşya düşer ve başa döner. Kurulum eşyaya göre (MinigameVariant.Carry; Berke'nin play testi:
    // çok kolay ve hep aynıydı): tek duvar, yukarı aşağı kayan boşluk ya da iki duvarlı zikzak. Taraflar ve
    // boşlukların yeri her açılışta rastgele.
    // M26 (Mehmet'in play testi 17: el fenerinin çarpışması kare gibiydi): eşyanın boyu ve çarpışma dikdörtgeni
    // görünen biçimi (MinigamePixels.CarrySize: el feneri ince uzun, ayna dar uzun, raf geniş alçak).
    public class CarryMinigame : Minigame
    {
        Vector2 ItemSize;
        static readonly Vector2 TargetSize = new Vector2(64f, 64f);
        const float WallWidth = 16f;
        const float HitFlashTime = 0.25f;
        const float DoubleWallX = 42f;
        const float MovingSpeed = 1.1f; // rad/sn

        class Wall
        {
            public float X;
            public float GapY;
            public float GapHeight;
            public float BaseY;
            public float Amplitude;
            public float Phase;
            public RectTransform Top;
            public RectTransform Bottom;
            public readonly List<Image> Images = new List<Image>();
        }

        readonly float snapRadius;
        readonly float gapHeight;
        readonly Sprite itemSprite;

        readonly List<Wall> walls = new List<Wall>();
        static readonly Vector2 ShadowOffset = new Vector2(4f, -5f);
        RectTransform item;
        RectTransform itemShadow;
        RectTransform target;
        Image targetImage;
        Vector2 start;
        Vector2 grabOffset;
        bool dragging;
        float hitFlash;
        float time;

        // itemSprite: taşınan şeyin görseli (eşya ya da çanta malzemesi); boşsa Art.carryItem ya da pixel çizim
        public CarryMinigame(float snapRadius, float gapHeight, Sprite itemSprite)
        {
            this.snapRadius = snapRadius;
            this.gapHeight = gapHeight;
            this.itemSprite = itemSprite;
        }

        protected override void OnBuild()
        {
            CreateBackground(Art.carryBackground, MinigamePixels.Ground((int)PanelSize.x, (int)PanelSize.y, "room"));
            ItemSize = (Vector2)MinigamePixels.CarrySize(Variant.CarryKind) * MinigamePixels.Scale;

            // Eşya bir yanda, güvenli alan diğer yanda
            float side = Random.NextDouble() < 0.5 ? -1f : 1f;
            float x = PanelSize.x * 0.5f - ItemSize.x * 0.5f - 6f;
            float targetX = PanelSize.x * 0.5f - TargetSize.x * 0.5f - 4f; // güvenli alan kenarda kırpılmasın
            float halfHeight = PanelSize.y * 0.5f;
            start = new Vector2(-side * x, Range(-halfHeight * 0.45f, halfHeight * 0.45f));
            var targetPosition = new Vector2(side * targetX, Range(-halfHeight * 0.45f, halfHeight * 0.45f));

            BuildWalls(halfHeight);

            targetImage = CreatePixel("SafeSpot", targetPosition, TargetSize, Art.carryTarget, MinigamePixels.SafeSpot(TexSize(TargetSize).x));
            target = targetImage.rectTransform;

            // Gölge eşyayla birlikte gider (eşyanın çocuğu değil: önce o çizilsin)
            var shadowTex = TexSize(ItemSize + Vector2.one * 6f);
            itemShadow = CreatePixel("ItemShadow", start + ShadowOffset, ItemSize + Vector2.one * 6f, null,
                MinigamePixels.Shadow(shadowTex.x, shadowTex.y)).rectTransform;
            var sprite = itemSprite ? itemSprite : Art.carryItem;
            item = CreatePixel("Item", start, ItemSize, sprite, MinigamePixels.CarryItem(Variant.CarryKind)).rectTransform;
        }

        void BuildWalls(float halfHeight)
        {
            float margin = 10f;
            switch (Variant.Carry)
            {
                case CarryMode.Moving:
                {
                    float gap = gapHeight + 10f;
                    float amplitude = Mathf.Min(48f, halfHeight - gap * 0.5f - margin);
                    AddWall(0f, gap, 0f, amplitude, Range(0f, Mathf.PI * 2f));
                    break;
                }
                case CarryMode.Double:
                {
                    // Zikzak: bir boşluk üstte, diğeri altta
                    float limit = halfHeight - gapHeight * 0.5f - margin;
                    float upper = Range(limit * 0.35f, limit);
                    float lower = -Range(limit * 0.35f, limit);
                    bool flip = Random.NextDouble() < 0.5;
                    AddWall(-DoubleWallX, gapHeight, flip ? upper : lower, 0f, 0f);
                    AddWall(DoubleWallX, gapHeight, flip ? lower : upper, 0f, 0f);
                    break;
                }
                default:
                {
                    float limit = halfHeight - gapHeight * 0.5f - margin;
                    AddWall(0f, gapHeight, Range(-limit, limit), 0f, 0f);
                    break;
                }
            }
            foreach (var wall in walls) LayoutWall(wall);
        }

        void AddWall(float x, float gap, float baseY, float amplitude, float phase)
        {
            var wall = new Wall { X = x, GapHeight = gap, BaseY = baseY, GapY = baseY, Amplitude = amplitude, Phase = phase };
            int index = walls.Count;
            wall.Top = WallPiece($"Wall{index}Top", wall);
            wall.Bottom = WallPiece($"Wall{index}Bottom", wall);
            walls.Add(wall);
        }

        // Maskeli parça: içindeki tuğla dokusu panelde sabit durur, boşluk kayınca doku kaymaz
        RectTransform WallPiece(string name, Wall wall)
        {
            var piece = new GameObject(name, typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            piece.SetParent(Panel, false);
            piece.anchorMin = piece.anchorMax = new Vector2(0.5f, 0.5f);
            var size = new Vector2(WallWidth, PanelSize.y);
            var bricks = CreatePixel("Bricks", Vector2.zero, size, Art.carryWall, MinigamePixels.Block((int)size.x, (int)size.y), piece);
            if (Art.carryWall)
            {
                bricks.type = Image.Type.Tiled;
                bricks.preserveAspect = false;
            }
            wall.Images.Add(bricks);
            return piece;
        }

        void LayoutWall(Wall wall)
        {
            float half = PanelSize.y * 0.5f;
            float gapTop = wall.GapY + wall.GapHeight * 0.5f;
            float gapBottom = wall.GapY - wall.GapHeight * 0.5f;
            Place(wall.Top, wall.X, gapTop, half);
            Place(wall.Bottom, wall.X, -half, gapBottom);
        }

        static void Place(RectTransform piece, float x, float y0, float y1)
        {
            float height = Mathf.Max(0f, y1 - y0);
            float center = (y0 + y1) * 0.5f;
            piece.sizeDelta = new Vector2(WallWidth, height);
            piece.anchoredPosition = new Vector2(x, center);
            // Tuğla panelin ortasında sabit kalsın
            var bricks = (RectTransform)piece.GetChild(0);
            bricks.anchoredPosition = new Vector2(0f, -center);
        }

        public override bool Tick(MinigameInput input)
        {
            time += input.DeltaTime;
            foreach (var wall in walls)
            {
                if (wall.Amplitude <= 0f) continue;
                wall.GapY = wall.BaseY + Mathf.Sin(time * MovingSpeed + wall.Phase) * wall.Amplitude;
                LayoutWall(wall);
            }

            if (hitFlash > 0f)
            {
                hitFlash -= input.DeltaTime;
                foreach (var wall in walls)
                foreach (var image in wall.Images)
                    image.color = hitFlash > 0f ? MinigamePixels.Danger : Color.white;
            }
            // Gölge eşyayı izler; tutarken biraz daha uzakta (eşya kalkmış gibi)
            itemShadow.anchoredPosition = item.anchoredPosition + ShadowOffset * (dragging ? 1.6f : 1f);
            // Güvenli alan hafifçe nabız gibi parlar
            targetImage.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(time * 4f));

            // İnce eşyalar (el feneri) de rahat tutulsun: tutma alanı biraz büyük; çarpışma görünen dikdörtgen
            if (input.Pressed && Contains(item.anchoredPosition, item.sizeDelta + Vector2.one * 12f, input.Pointer))
            {
                dragging = true;
                grabOffset = item.anchoredPosition - input.Pointer;
                Audio.Sfx.Play("minigame.pickup");
            }

            if (dragging && input.Held)
            {
                Vector2 half = (PanelSize - ItemSize) * 0.5f;
                Vector2 previous = item.anchoredPosition;
                Vector2 position = input.Pointer + grabOffset;
                item.anchoredPosition = new Vector2(Mathf.Clamp(position.x, -half.x, half.x),
                    Mathf.Clamp(position.y, -half.y, half.y));

                if (HitsWall(previous, item.anchoredPosition))
                {
                    // Duvara çarptı: eşya düşer, başa döner
                    dragging = false;
                    item.anchoredPosition = start;
                    hitFlash = HitFlashTime;
                    Audio.Sfx.Play("minigame.miss");
                    return false;
                }
            }

            if (dragging && (input.Released || !input.Held))
            {
                dragging = false;
                Audio.Sfx.Play("minigame.drop");
                // M36 (Mehmet'in play testi 22): yeşil alanın içine bırakılan eşya, merkezi 14 px'ten uzaksa kabul
                // edilmiyor, olduğu yerde kalıyordu. Eşyanın merkezi yeşil alanın içindeyse yeter
                if (Contains(target.anchoredPosition, TargetSize + Vector2.one * 8f, item.anchoredPosition) ||
                    Vector2.Distance(item.anchoredPosition, target.anchoredPosition) <= snapRadius)
                {
                    item.anchoredPosition = target.anchoredPosition;
                    return true;
                }
            }
            return false;
        }

        bool HitsWall(Vector2 from, Vector2 to)
        {
            foreach (var wall in walls)
            {
                if (Overlaps(item, wall.Top) || Overlaps(item, wall.Bottom)) return true;
                // Fare tek karede duvarın öbür yanına sıçradıysa: geçiş noktası boşluğun içinde mi?
                float a = from.x - wall.X, b = to.x - wall.X;
                if (Mathf.Sign(a) == Mathf.Sign(b) || Mathf.Approximately(a, b)) continue;
                float t = Mathf.InverseLerp(from.x, to.x, wall.X);
                float crossY = Mathf.Lerp(from.y, to.y, t);
                if (Mathf.Abs(crossY - wall.GapY) > (wall.GapHeight - ItemSize.y) * 0.5f) return true;
            }
            return false;
        }

        static bool Contains(Vector2 center, Vector2 size, Vector2 point)
        {
            Vector2 delta = point - center;
            return Mathf.Abs(delta.x) <= size.x * 0.5f && Mathf.Abs(delta.y) <= size.y * 0.5f;
        }

        static bool Overlaps(RectTransform a, RectTransform b)
        {
            if (b.sizeDelta.y <= 0f) return false;
            Vector2 delta = a.anchoredPosition - b.anchoredPosition;
            Vector2 reach = (a.sizeDelta + b.sizeDelta) * 0.5f;
            return Mathf.Abs(delta.x) < reach.x && Mathf.Abs(delta.y) < reach.y;
        }
    }
}
