using UnityEngine;

namespace Deprem.Minigames
{
    // Taşı ve yerleştir: eşyayı fareyle tutup sürükle, güvenli alana bırak.
    // Arada bir duvar var; eşya duvardaki boşluktan geçmeli. Duvara çarparsa eşya düşer ve başa döner.
    public class CarryMinigame : Minigame
    {
        static readonly Vector2 ItemSize = new Vector2(48f, 48f);
        static readonly Vector2 TargetSize = new Vector2(64f, 64f);
        static readonly Color WallColor = new Color(0.35f, 0.37f, 0.42f);
        static readonly Color HitColor = new Color(0.7f, 0.2f, 0.2f);
        const float WallWidth = 16f;
        const float HitFlashTime = 0.25f;

        readonly float snapRadius;
        readonly float gapHeight;
        readonly Sprite itemSprite;

        RectTransform item;
        RectTransform target;
        RectTransform wallTop;
        RectTransform wallBottom;
        UnityEngine.UI.Image[] wallImages;
        Vector2 start;
        float gapY;
        Vector2 grabOffset;
        bool dragging;
        float hitFlash;
        Color baseWallColor;

        // itemSprite: taşınan şeyin görseli (eşya ya da çanta malzemesi); boşsa Art.carryItem
        public CarryMinigame(float snapRadius, float gapHeight, Sprite itemSprite)
        {
            this.snapRadius = snapRadius;
            this.gapHeight = gapHeight;
            this.itemSprite = itemSprite;
        }

        protected override void OnBuild()
        {
            CreateBackground(Art.carryBackground);

            // Eşya bir yanda, güvenli alan diğer yanda; taraf ve boşluğun yeri her açılışta rastgele
            float side = Random.value < 0.5f ? -1f : 1f;
            float x = PanelSize.x * 0.34f;
            float halfHeight = PanelSize.y * 0.5f;

            start = new Vector2(-side * x, Random.Range(-halfHeight * 0.5f, halfHeight * 0.5f));
            var targetPosition = new Vector2(side * x, Random.Range(-halfHeight * 0.5f, halfHeight * 0.5f));
            target = CreateRect("SafeSpot", targetPosition, TargetSize, new Color(0.45f, 0.85f, 0.5f, 0.35f),
                null, Art.carryTarget).rectTransform;

            gapY = Random.Range(-halfHeight + gapHeight, halfHeight - gapHeight);
            float topHeight = halfHeight - (gapY + gapHeight * 0.5f);
            float bottomHeight = (gapY - gapHeight * 0.5f) + halfHeight;
            var top = CreateRect("WallTop", new Vector2(0f, halfHeight - topHeight * 0.5f), new Vector2(WallWidth, topHeight), WallColor, null, Art.carryWall);
            var bottom = CreateRect("WallBottom", new Vector2(0f, -halfHeight + bottomHeight * 0.5f), new Vector2(WallWidth, bottomHeight), WallColor, null, Art.carryWall);
            if (Art.carryWall)
            {
                // Duvar dikey döşenir, boşluk kodla açılır
                foreach (var wall in new[] { top, bottom })
                {
                    wall.type = UnityEngine.UI.Image.Type.Tiled;
                    wall.preserveAspect = false;
                }
            }
            wallTop = top.rectTransform;
            wallBottom = bottom.rectTransform;
            wallImages = new[] { top, bottom };
            baseWallColor = top.color;

            var sprite = itemSprite ? itemSprite : Art.carryItem;
            item = CreateRect("Item", start, ItemSize, new Color(0.85f, 0.65f, 0.4f), null, sprite).rectTransform;
        }

        public override bool Tick(MinigameInput input)
        {
            if (hitFlash > 0f)
            {
                hitFlash -= input.DeltaTime;
                foreach (var image in wallImages) image.color = hitFlash > 0f ? HitColor : baseWallColor;
            }

            if (input.Pressed && Contains(item.anchoredPosition, item.sizeDelta, input.Pointer))
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

                if (Overlaps(item, wallTop) || Overlaps(item, wallBottom) || JumpedWall(previous, item.anchoredPosition))
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
                if (Vector2.Distance(item.anchoredPosition, target.anchoredPosition) <= snapRadius)
                {
                    item.anchoredPosition = target.anchoredPosition;
                    return true;
                }
            }
            return false;
        }

        // Fare tek karede duvarın öbür yanına sıçradıysa: geçiş noktası boşluğun içinde mi?
        bool JumpedWall(Vector2 from, Vector2 to)
        {
            if (Mathf.Sign(from.x) == Mathf.Sign(to.x) || Mathf.Approximately(from.x, to.x)) return false;
            float t = Mathf.InverseLerp(from.x, to.x, 0f);
            float crossY = Mathf.Lerp(from.y, to.y, t);
            return Mathf.Abs(crossY - gapY) > (gapHeight - ItemSize.y) * 0.5f;
        }

        static bool Contains(Vector2 center, Vector2 size, Vector2 point)
        {
            Vector2 delta = point - center;
            return Mathf.Abs(delta.x) <= size.x * 0.5f && Mathf.Abs(delta.y) <= size.y * 0.5f;
        }

        static bool Overlaps(RectTransform a, RectTransform b)
        {
            Vector2 delta = a.anchoredPosition - b.anchoredPosition;
            Vector2 reach = (a.sizeDelta + b.sizeDelta) * 0.5f;
            return Mathf.Abs(delta.x) < reach.x && Mathf.Abs(delta.y) < reach.y;
        }
    }
}
