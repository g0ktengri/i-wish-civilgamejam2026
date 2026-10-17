using System.Collections;
using System.Collections.Generic;
using Deprem.Core;
using Deprem.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Kurtarma fazının hedefi (ENTEGRASYON M11, M14):
    // - Faz Rescue olunca ortada 3 sn büyük yazı: "Ailen tehlikede! Onları bul ve kabine götür"
    // - Sol üstte hedef, altında "Aile: bulunan x/3 · kabinde y/3", altında kabin açıklaması (birkaç sn)
    // - Ekran dışında kalan, henüz bulunmamış aile üyelerine ekran kenarında ok (Marker_Father vb.);
    //   herkes bulununca kabine ok (Marker_CabinEntrance)
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
        [SerializeField] int fontSize = 14;
        [SerializeField] int infoFontSize = 12;
        [SerializeField] int bannerFontSize = 28;
        [SerializeField] Color color = Color.white;
        [SerializeField] Color infoColor = new Color(0.8f, 0.85f, 0.9f);
        [SerializeField] Color arrowColor = new Color(1f, 0.85f, 0.4f);
        [SerializeField] Color cabinArrowColor = new Color(0.45f, 0.9f, 0.55f);

        [Header("Süreler (sn)")]
        [SerializeField] float bannerSeconds = 3f;
        [Tooltip("Kabin açıklamasının ekranda kalma süresi")]
        [SerializeField] float infoSeconds = 7f;
        [SerializeField] float fadeSeconds = 0.6f;

        [Header("Oklar")]
        [Tooltip("Okun ekran kenarından uzaklığı (referans pikseli)")]
        [SerializeField] float arrowMargin = 36f;
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
        Text objectiveText;
        Text familyText;
        CanvasGroup infoGroup;
        Text infoText;
        CanvasGroup bannerGroup;
        Text bannerText;
        readonly List<Arrow> arrows = new List<Arrow>();
        readonly HashSet<FamilyMemberId> inCabin = new HashSet<FamilyMemberId>();
        bool rescue;

        void Awake()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "ObjectiveCanvas", sortingOrder);
            canvasRect = (RectTransform)canvas.transform;
            group = canvas.GetComponent<CanvasGroup>();

            var panel = CreateCornerPanel(canvas.transform, "Objective", -12f, 52f);
            objectiveText = UiBuilder.CreateText(panel, "Text", font, fontSize, color);
            UiBuilder.Stretch(objectiveText.rectTransform, new Vector2(0f, 0.5f), Vector2.one);
            familyText = UiBuilder.CreateText(panel, "Family", font, infoFontSize, infoColor);
            UiBuilder.Stretch(familyText.rectTransform, Vector2.zero, new Vector2(1f, 0.5f));

            var info = CreateCornerPanel(canvas.transform, "ShelterInfo", -68f, 44f);
            infoGroup = info.gameObject.AddComponent<CanvasGroup>();
            infoText = UiBuilder.CreateText(info, "Text", font, infoFontSize, infoColor);
            UiBuilder.Stretch(infoText.rectTransform, Vector2.zero, Vector2.one);
            infoText.rectTransform.offsetMin = new Vector2(8f, 2f);
            infoText.rectTransform.offsetMax = new Vector2(-8f, -2f);

            var banner = UiBuilder.CreatePanel(canvas.transform, "Banner");
            var bannerRect = banner.rectTransform;
            bannerRect.anchorMin = bannerRect.anchorMax = new Vector2(0.5f, 0.62f);
            bannerRect.sizeDelta = new Vector2(620f, 64f);
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerText = UiBuilder.CreateText(bannerRect, "Text", font, bannerFontSize, color);
            UiBuilder.Stretch(bannerText.rectTransform, Vector2.zero, Vector2.one);

            group.alpha = 0f;
            infoGroup.alpha = 0f;
            bannerGroup.alpha = 0f;
        }

        RectTransform CreateCornerPanel(Transform parent, string name, float y, float height)
        {
            var rect = UiBuilder.CreatePanel(parent, name).rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, y);
            rect.sizeDelta = new Vector2(320f, height);
            return rect;
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
            StopAllCoroutines();
            rescue = phase == GamePhase.Rescue;
            group.alpha = rescue ? 1f : 0f;
            infoGroup.alpha = 0f;
            bannerGroup.alpha = 0f;
            if (!rescue)
            {
                foreach (var arrow in arrows) arrow.Root.gameObject.SetActive(false);
                return;
            }

            inCabin.Clear();
            BuildArrows();
            Refresh();
            StartCoroutine(Show(bannerGroup, bannerSeconds));
            StartCoroutine(Show(infoGroup, infoSeconds));
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
            root.sizeDelta = new Vector2(48f, 48f);

            // Ok: görseli UiStyle.arrow (sağa bakan), yoksa iki kareden ok ucu
            var head = new GameObject("Head", typeof(RectTransform)).GetComponent<RectTransform>();
            head.SetParent(root, false);
            head.sizeDelta = new Vector2(32f, 32f);
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
                tip.rectTransform.sizeDelta = new Vector2(14f, 14f);
                tip.rectTransform.anchoredPosition = new Vector2(6f, 0f);
                tip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var shaft = UiBuilder.CreateImage(head, "Shaft", tint);
                shaft.rectTransform.sizeDelta = new Vector2(16f, 6f);
                shaft.rectTransform.anchoredPosition = new Vector2(-6f, 0f);
            }

            var label = UiBuilder.CreateText(root, "Label", font, infoFontSize, tint);
            label.rectTransform.sizeDelta = new Vector2(80f, 16f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -22f);
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

        IEnumerator Show(CanvasGroup target, float seconds)
        {
            yield return Fade(target, 0f, 1f);
            yield return new WaitForSeconds(seconds);
            yield return Fade(target, 1f, 0f);
        }

        IEnumerator Fade(CanvasGroup target, float from, float to)
        {
            for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
            {
                target.alpha = Mathf.Lerp(from, to, t / fadeSeconds);
                yield return null;
            }
            target.alpha = to;
        }

        void Refresh()
        {
            objectiveText.text = Texts.Get("objective.rescue");
            familyText.text = Texts.Format("objective.family", FoundCount(), FamilyTotal(), inCabin.Count);
            infoText.text = Texts.Get("objective.shelter_info");
            bannerText.text = Texts.Get("objective.banner");
            foreach (var arrow in arrows) arrow.Label.text = Texts.Get(arrow.LabelKey);
        }
    }
}
