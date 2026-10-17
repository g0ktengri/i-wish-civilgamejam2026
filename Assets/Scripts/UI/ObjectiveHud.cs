using System.Collections.Generic;
using Deprem.Core;
using Deprem.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Kurtarma fazının hedefi (ENTEGRASYON M11, M14), sade hali (Berke'nin play testi: çok kalabalıktı):
    // - Sol üstte tek satır: "Aileni kabine götür  0/3" (kabine giren / toplam; M24 ortak HUD paneli)
    // - Henüz bulunmamış her aile üyesine işaret; herkes bulununca kabine (Marker_CabinEntrance)
    // M27 (Mehmet'in play testi 19: oklar çok yavandı):
    // - Hedef ekran dışındaysa ekran kenarına yapışık, kalın, konturlu pixel ok; nabız gibi atar ve hedefe doğru "iter".
    // - Üye başına renk ve baş harf rozeti (B / A / K), kurtarılınca söner. Uzaklık arttıkça ok biraz küçülür.
    // - Hedef ekrandaysa üyenin başının üstünde aşağı bakan küçük işaret (sallanır).
    public class ObjectiveHud : MonoBehaviour
    {
        static readonly (FamilyMemberId id, string marker, string member)[] Family =
        {
            (FamilyMemberId.Father, "Marker_Father", "Father"),
            (FamilyMemberId.Mother, "Marker_Mother", "Mother"),
            (FamilyMemberId.Sibling, "Marker_Sibling", "Sibling"),
        };
        const string CabinMarker = "Marker_CabinEntrance";
        const int ArrowTexture = 16; // pixel ok dokusu (16 x 16), 2 referans pikseli
        const int MarkerTexture = 9;

        [SerializeField] Font font;
        [Tooltip("0: UiStyle.hudFontSize")]
        [SerializeField] int fontSize;

        [Header("İşaretler (M27)")]
        [SerializeField] Color fatherColor = new Color(1f, 0.82f, 0.4f);   // HUD vurgu sarısı
        [SerializeField] Color motherColor = new Color(1f, 0.6f, 0.52f);   // sıcak gül
        [SerializeField] Color siblingColor = new Color(0.58f, 0.88f, 0.6f); // güvenli yeşil
        [SerializeField] Color cabinColor = new Color(0.96f, 0.92f, 0.84f); // HUD krem
        [Tooltip("Okun ekran kenarından uzaklığı (referans pikseli)")]
        [SerializeField] float arrowMargin = 34f;
        [SerializeField] float arrowSize = 44f;
        [Tooltip("Nabız: büyüme oranı ve hızı; it: hedefe doğru kayma (referans pikseli)")]
        [SerializeField] float pulseScale = 0.12f;
        [SerializeField] float pulseSpeed = 5f;
        [SerializeField] float pushDistance = 6f;
        [Tooltip("Oyuncuya bu uzaklıktan (m) sonra ok küçülmeye başlar; en uzakta bu orana iner")]
        [SerializeField] float nearDistance = 4f;
        [SerializeField] float farDistance = 18f;
        [SerializeField] float farScale = 0.7f;
        [Tooltip("Ekrandaki hedefin başının üstündeki işaretin yüksekliği (m, görselin tepesinden)")]
        [SerializeField] float markerLift = 0.35f;
        [SerializeField] int sortingOrder = 55;

        class Arrow
        {
            public FamilyMemberId Id; // None = kabin
            public Transform Target;  // işaret noktası (ekran dışı yön)
            public Transform Member;  // aile üyesinin objesi (başının üstündeki işaret), yoksa Target
            public RectTransform Root;
            public RectTransform Head;
            public Image HeadImage;
            public RectTransform Badge;
            public Text BadgeText;
            public RectTransform Marker;
            public Color Color;
            public string LabelKey;
        }

        CanvasGroup group;
        RectTransform canvasRect;
        RectTransform panel;
        Text objectiveText;
        readonly List<Arrow> arrows = new List<Arrow>();
        readonly HashSet<FamilyMemberId> inCabin = new HashSet<FamilyMemberId>();
        bool rescue;
        Transform player;
        static Sprite arrowSprite;
        static Sprite markerSprite;

        void Awake()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "ObjectiveCanvas", sortingOrder);
            canvasRect = (RectTransform)canvas.transform;
            group = canvas.GetComponent<CanvasGroup>();

            float margin = UiBuilder.HudMargin;
            panel = UiBuilder.CreatePanel(canvas.transform, "Objective").rectTransform;
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(margin, -margin);
            panel.sizeDelta = new Vector2(260f, 46f);
            objectiveText = UiBuilder.CreateText(panel, "Text", font, fontSize > 0 ? fontSize : UiBuilder.HudFontSize,
                UiBuilder.HudText, TextAnchor.MiddleLeft);
            UiBuilder.Stretch(objectiveText.rectTransform, Vector2.zero, Vector2.one);
            objectiveText.rectTransform.offsetMin = new Vector2(16f, 0f);
            objectiveText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiBuilder.Outlined(objectiveText);
            objectiveText.supportRichText = true;

            group.alpha = 0f;
        }

        void OnEnable()
        {
            GameEvents.PhaseChanged += HandlePhaseChanged;
            GameEvents.FamilyMemberRescued += HandleFamilyChanged;
            GameEvents.FamilyMemberEnteredCabin += HandleEnteredCabin;
            Texts.LanguageChanged += Refresh;
        }

        void OnDisable()
        {
            GameEvents.PhaseChanged -= HandlePhaseChanged;
            GameEvents.FamilyMemberRescued -= HandleFamilyChanged;
            GameEvents.FamilyMemberEnteredCabin -= HandleEnteredCabin;
            Texts.LanguageChanged -= Refresh;
        }

        void HandlePhaseChanged(GamePhase phase)
        {
            rescue = phase == GamePhase.Rescue;
            group.alpha = rescue ? 1f : 0f;
            if (!rescue)
            {
                foreach (var arrow in arrows) Hide(arrow);
                return;
            }
            inCabin.Clear();
            BuildArrows();
            Refresh();
        }

        void HandleFamilyChanged(FamilyMemberId id) => Refresh();

        void HandleEnteredCabin(FamilyMemberId id)
        {
            inCabin.Add(id);
            Refresh();
        }

        void LateUpdate()
        {
            if (!rescue) return;
            var cam = Camera.main;
            bool everyoneFound = FoundCount() >= FamilyTotal();
            foreach (var arrow in arrows)
            {
                bool wanted = arrow.Id == FamilyMemberId.None ? everyoneFound : !GameState.IsRescued(arrow.Id);
                UpdateArrow(arrow, cam, wanted, Time.unscaledTime);
            }
        }

        static void Hide(Arrow arrow)
        {
            arrow.Root.gameObject.SetActive(false);
            arrow.Marker.gameObject.SetActive(false);
        }

        // Hedef ekran dışındaysa kenarda ok, ekrandaysa başının üstünde işaret
        void UpdateArrow(Arrow arrow, Camera cam, bool wanted, float time)
        {
            if (!wanted || !cam || !arrow.Target)
            {
                Hide(arrow);
                return;
            }

            var focus = arrow.Member ? arrow.Member : arrow.Target;
            Vector3 viewport = cam.WorldToViewportPoint(arrow.Target.position + Vector3.up);
            bool behind = viewport.z < 0f;
            if (behind) viewport = new Vector3(1f - viewport.x, 1f - viewport.y, -viewport.z);
            bool onScreen = !behind && viewport.x > 0.04f && viewport.x < 0.96f && viewport.y > 0.06f && viewport.y < 0.94f;
            Vector2 size = canvasRect.rect.size;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * pulseSpeed);

            arrow.Root.gameObject.SetActive(!onScreen);
            arrow.Marker.gameObject.SetActive(onScreen);

            if (onScreen)
            {
                // Başın üstünde aşağı bakan işaret, aşağı yukarı sallanır
                Vector3 head = HeadPosition(focus) + Vector3.up * markerLift;
                var v = cam.WorldToViewportPoint(head);
                arrow.Marker.anchoredPosition = new Vector2((v.x - 0.5f) * size.x, (v.y - 0.5f) * size.y + pulse * 4f);
                return;
            }

            Vector2 half = size * 0.5f - Vector2.one * arrowMargin;
            Vector2 direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            if (direction.sqrMagnitude < 1e-6f) direction = Vector2.up;
            // Yönü ekranın kenarındaki dikdörtgene kadar uzat
            float scale = Mathf.Min(half.x / Mathf.Max(Mathf.Abs(direction.x), 1e-4f),
                half.y / Mathf.Max(Mathf.Abs(direction.y), 1e-4f));
            Vector2 unit = direction.normalized;
            arrow.Root.anchoredPosition = direction * scale + unit * pushDistance * pulse; // hedefe doğru "it"
            arrow.Head.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(unit.y, unit.x) * Mathf.Rad2Deg);
            // Rozet okun arkasında (kenardan içeride)
            arrow.Badge.anchoredPosition = -unit * arrowSize * 0.85f;

            if (!player)
            {
                var playerObject = GameObject.Find("Player");
                if (playerObject) player = playerObject.transform;
            }
            float distance = player ? Vector2.Distance(player.position, arrow.Target.position) : nearDistance;
            float far = Mathf.Lerp(1f, farScale, Mathf.InverseLerp(nearDistance, farDistance, distance));
            arrow.Root.localScale = Vector3.one * far * (1f + pulseScale * pulse);
        }

        // Görselin tepesi (yoksa nokta + 1,8 m)
        static Vector3 HeadPosition(Transform member)
        {
            var renderers = member.GetComponentsInChildren<Renderer>();
            float top = float.MinValue;
            foreach (var r in renderers)
                if (r.enabled && !(r is ParticleSystemRenderer)) top = Mathf.Max(top, r.bounds.max.y);
            var p = member.position;
            return new Vector3(p.x, top > float.MinValue ? top : p.y + 1.8f, p.z);
        }

        void BuildArrows()
        {
            if (arrows.Count > 0) return;
            foreach (var (id, marker, member) in Family)
            {
                var target = GameObject.Find(marker);
                var memberObject = GameObject.Find(member);
                if (target) arrows.Add(CreateArrow(id, target.transform, memberObject ? memberObject.transform : null,
                    $"family.{id}", ColorFor(id)));
            }
            var cabin = GameObject.Find(CabinMarker);
            if (cabin) arrows.Add(CreateArrow(FamilyMemberId.None, cabin.transform, null, "family.cabin", cabinColor));
        }

        Color ColorFor(FamilyMemberId id) =>
            id == FamilyMemberId.Father ? fatherColor : id == FamilyMemberId.Mother ? motherColor : siblingColor;

        Arrow CreateArrow(FamilyMemberId id, Transform target, Transform member, string labelKey, Color tint)
        {
            var root = new GameObject($"Arrow_{labelKey}", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvasRect, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.one * arrowSize;

            // Ok: görseli UiStyle.arrow (sağa bakan, beyaz) ya da kodla çizilen kalın pixel ok; renk üyeden
            var head = new GameObject("Head", typeof(RectTransform)).GetComponent<RectTransform>();
            head.SetParent(root, false);
            head.sizeDelta = Vector2.one * arrowSize;
            var style = UiStyle.Current;
            var headImage = UiBuilder.CreateImage(head, "Sprite", Color.white);
            headImage.sprite = style && style.arrow ? style.arrow : ArrowSprite;
            headImage.color = tint;
            UiBuilder.Stretch(headImage.rectTransform, Vector2.zero, Vector2.one);

            // Rozet: üyenin baş harfi (Baba → B), kabin için ev işareti yerine "K"/"S" (Kabin/Shelter)
            var badge = UiBuilder.CreatePanel(root, "Badge").rectTransform;
            badge.sizeDelta = new Vector2(30f, 30f);
            var badgeText = UiBuilder.CreateText(badge, "Letter", font, 18, tint);
            UiBuilder.Stretch(badgeText.rectTransform, Vector2.zero, Vector2.one);
            badgeText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Ekrandaki hedefin başının üstündeki işaret: aşağı bakan küçük ok
            var marker = UiBuilder.CreateImage(canvasRect, $"Marker_{labelKey}", tint);
            marker.sprite = MarkerSprite;
            marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            marker.rectTransform.pivot = new Vector2(0.5f, 0f);
            marker.rectTransform.sizeDelta = Vector2.one * MarkerTexture * 3f;

            root.gameObject.SetActive(false);
            marker.gameObject.SetActive(false);
            return new Arrow
            {
                Id = id, Target = target, Member = member, Root = root, Head = head, HeadImage = headImage,
                Badge = badge, BadgeText = badgeText, Marker = marker.rectTransform, Color = tint, LabelKey = labelKey,
            };
        }

        // 16 x 16, sağa bakan kalın ok: beyaz gövde (renk Image.color'dan), 1 px koyu kontur, üstte ışık, altta gölge
        static Sprite ArrowSprite
        {
            get
            {
                if (arrowSprite) return arrowSprite;
                const int n = ArrowTexture;
                bool Inside(int x, int y)
                {
                    float cy = (n - 1) * 0.5f;
                    if (x >= 7) return Mathf.Abs(y - cy) <= (n - 1 - x) * 0.95f + 0.5f; // uç (üçgen)
                    return x >= 2 && Mathf.Abs(y - cy) <= 2.5f;                          // gövde
                }
                arrowSprite = PixelSprite(n, n, "UiArrow", (x, y) =>
                {
                    if (!Inside(x, y)) return Color.clear;
                    bool edge = !Inside(x - 1, y) || !Inside(x + 1, y) || !Inside(x, y - 1) || !Inside(x, y + 1);
                    if (edge) return new Color(0.08f, 0.06f, 0.05f);
                    if (!Inside(x, y + 2)) return new Color(1f, 1f, 1f);       // üst kenarda ışık
                    if (!Inside(x, y - 2)) return new Color(0.68f, 0.68f, 0.68f); // alt kenarda gölge
                    return new Color(0.9f, 0.9f, 0.9f);
                });
                return arrowSprite;
            }
        }

        // 9 x 9, aşağı bakan küçük ok (başın üstündeki işaret)
        static Sprite MarkerSprite
        {
            get
            {
                if (markerSprite) return markerSprite;
                const int n = MarkerTexture;
                bool Inside(int x, int y) => y <= 5 ? Mathf.Abs(x - 4) <= y * 0.8f + 0.2f : Mathf.Abs(x - 4) <= 1;
                markerSprite = PixelSprite(n, n, "UiMarker", (x, y) =>
                {
                    if (!Inside(x, y)) return Color.clear;
                    bool edge = !Inside(x - 1, y) || !Inside(x + 1, y) || !Inside(x, y - 1) || !Inside(x, y + 1);
                    return edge ? new Color(0.08f, 0.06f, 0.05f) : x < 4 ? Color.white : new Color(0.8f, 0.8f, 0.8f);
                });
                return markerSprite;
            }
        }

        static Sprite PixelSprite(int w, int h, string name, System.Func<int, int, Color> pixel)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
            };
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) texture.SetPixel(x, y, pixel(x, y));
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        // Evde işareti bulunan aile üyesi sayısı (bulunamazsa GDD'deki 3)
        int FamilyTotal()
        {
            int total = 0;
            foreach (var arrow in arrows)
                if (arrow.Id != FamilyMemberId.None) total++;
            return total > 0 ? total : Family.Length;
        }

        int FoundCount()
        {
            int count = 0;
            foreach (var arrow in arrows)
                if (arrow.Id != FamilyMemberId.None && GameState.IsRescued(arrow.Id)) count++;
            return count;
        }

        void Refresh()
        {
            objectiveText.text = $"{Texts.Get("objective.short")}   " +
                                 $"<color={UiBuilder.Hex(UiBuilder.HudAccent)}>{inCabin.Count}/{FamilyTotal()}</color>";
            panel.sizeDelta = new Vector2(Mathf.Max(200f, objectiveText.preferredWidth + 34f), panel.sizeDelta.y);
            var culture = new System.Globalization.CultureInfo(Texts.Current == Language.Turkish ? "tr-TR" : "en-US");
            foreach (var arrow in arrows)
            {
                string label = Texts.Get(arrow.LabelKey);
                arrow.BadgeText.text = string.IsNullOrEmpty(label) ? "?" : char.ToUpper(label[0], culture).ToString();
            }
        }
    }
}
