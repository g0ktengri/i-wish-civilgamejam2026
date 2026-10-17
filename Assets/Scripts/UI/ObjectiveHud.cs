using System.Collections.Generic;
using Deprem.Core;
using Deprem.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Kurtarma fazının hedefi (ENTEGRASYON M11, M14), sade hali (Berke'nin play testi: çok kalabalıktı):
    // - Sol üstte tek satır: "Aileni kabine götür · 0/3" (kabine giren / toplam)
    // - Ekran dışında kalan, henüz bulunmamış aile üyesine ekran kenarında küçük, yarı saydam ok;
    //   herkes bulununca kabine ok (Marker_CabinEntrance)
    // Büyük "Ailen tehlikede" yazısı ve kabin açıklaması kalktı: oyuncunun balonu ve kabin tabelası söylüyor.
    public class ObjectiveHud : MonoBehaviour
    {
        static readonly (FamilyMemberId id, string marker)[] Family =
        {
            (FamilyMemberId.Father, "Marker_Father"),
            (FamilyMemberId.Mother, "Marker_Mother"),
            (FamilyMemberId.Sibling, "Marker_Sibling"),
        };
        const string CabinMarker = "Marker_CabinEntrance";

        [SerializeField] Font font;
        [SerializeField] int fontSize = 13;
        [SerializeField] int arrowFontSize = 10;
        [SerializeField] Color color = Color.white;
        [SerializeField] Color arrowColor = new Color(1f, 0.85f, 0.4f, 0.8f);
        [SerializeField] Color cabinArrowColor = new Color(0.45f, 0.9f, 0.55f, 0.8f);

        [Header("Oklar")]
        [Tooltip("Okun ekran kenarından uzaklığı (referans pikseli)")]
        [SerializeField] float arrowMargin = 26f;
        [SerializeField] float arrowSize = 18f;
        [SerializeField] int sortingOrder = 55;

        class Arrow
        {
            public FamilyMemberId Id; // None = kabin
            public Transform Target;
            public RectTransform Root;
            public RectTransform Head;
            public Text Label;
            public string LabelKey;
        }

        CanvasGroup group;
        RectTransform canvasRect;
        RectTransform panel;
        Text objectiveText;
        readonly List<Arrow> arrows = new List<Arrow>();
        readonly HashSet<FamilyMemberId> inCabin = new HashSet<FamilyMemberId>();
        bool rescue;

        void Awake()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "ObjectiveCanvas", sortingOrder);
            canvasRect = (RectTransform)canvas.transform;
            group = canvas.GetComponent<CanvasGroup>();

            panel = UiBuilder.CreatePanel(canvas.transform, "Objective").rectTransform;
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(12f, -12f);
            panel.sizeDelta = new Vector2(220f, 26f);
            objectiveText = UiBuilder.CreateText(panel, "Text", font, fontSize, color);
            UiBuilder.Stretch(objectiveText.rectTransform, Vector2.zero, Vector2.one);
            objectiveText.horizontalOverflow = HorizontalWrapMode.Overflow;

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
                foreach (var arrow in arrows) arrow.Root.gameObject.SetActive(false);
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
                UpdateArrow(arrow, cam, wanted);
            }
        }

        // Hedef ekran dışındaysa ok, ekranın kenarında hedefin yönünü gösterir
        void UpdateArrow(Arrow arrow, Camera cam, bool wanted)
        {
            if (!wanted || !cam || !arrow.Target)
            {
                arrow.Root.gameObject.SetActive(false);
                return;
            }

            Vector3 viewport = cam.WorldToViewportPoint(arrow.Target.position + Vector3.up);
            bool behind = viewport.z < 0f;
            if (behind) viewport = new Vector3(1f - viewport.x, 1f - viewport.y, -viewport.z);
            bool onScreen = !behind && viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f;
            arrow.Root.gameObject.SetActive(!onScreen);
            if (onScreen) return;

            Vector2 size = canvasRect.rect.size;
            Vector2 half = size * 0.5f - Vector2.one * arrowMargin;
            Vector2 direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            if (direction.sqrMagnitude < 1e-6f) direction = Vector2.up;
            // Yönü ekranın kenarındaki dikdörtgene kadar uzat
            float scale = Mathf.Min(half.x / Mathf.Max(Mathf.Abs(direction.x), 1e-4f),
                half.y / Mathf.Max(Mathf.Abs(direction.y), 1e-4f));
            arrow.Root.anchoredPosition = direction * scale;
            arrow.Head.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        void BuildArrows()
        {
            if (arrows.Count > 0) return;
            foreach (var (id, marker) in Family)
            {
                var target = GameObject.Find(marker);
                if (target) arrows.Add(CreateArrow(id, target.transform, $"family.{id}", arrowColor));
            }
            var cabin = GameObject.Find(CabinMarker);
            if (cabin) arrows.Add(CreateArrow(FamilyMemberId.None, cabin.transform, "family.cabin", cabinArrowColor));
        }

        Arrow CreateArrow(FamilyMemberId id, Transform target, string labelKey, Color tint)
        {
            var root = new GameObject($"Arrow_{labelKey}", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvasRect, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.one * arrowSize * 1.5f;

            // Ok: görseli UiStyle.arrow (sağa bakan), yoksa iki kareden küçük ok ucu
            var head = new GameObject("Head", typeof(RectTransform)).GetComponent<RectTransform>();
            head.SetParent(root, false);
            head.sizeDelta = Vector2.one * arrowSize;
            var style = UiStyle.Current;
            if (style && style.arrow)
            {
                var image = UiBuilder.CreateImage(head, "Sprite", tint);
                UiBuilder.ApplySprite(image, style.arrow);
                image.color = tint;
                UiBuilder.Stretch(image.rectTransform, Vector2.zero, Vector2.one);
            }
            else
            {
                var tip = UiBuilder.CreateImage(head, "Tip", tint);
                tip.rectTransform.sizeDelta = Vector2.one * arrowSize * 0.45f;
                tip.rectTransform.anchoredPosition = new Vector2(arrowSize * 0.18f, 0f);
                tip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var shaft = UiBuilder.CreateImage(head, "Shaft", tint);
                shaft.rectTransform.sizeDelta = new Vector2(arrowSize * 0.5f, arrowSize * 0.18f);
                shaft.rectTransform.anchoredPosition = new Vector2(-arrowSize * 0.18f, 0f);
            }

            var label = UiBuilder.CreateText(root, "Label", font, arrowFontSize, tint);
            label.rectTransform.sizeDelta = new Vector2(60f, 12f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -arrowSize * 0.85f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            root.gameObject.SetActive(false);
            return new Arrow { Id = id, Target = target, Root = root, Head = head, Label = label, LabelKey = labelKey };
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
            objectiveText.text = Texts.Format("objective.compact", inCabin.Count, FamilyTotal());
            panel.sizeDelta = new Vector2(Mathf.Max(160f, objectiveText.preferredWidth + 24f), panel.sizeDelta.y);
            foreach (var arrow in arrows) arrow.Label.text = Texts.Get(arrow.LabelKey);
        }
    }
}
