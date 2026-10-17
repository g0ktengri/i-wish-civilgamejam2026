using System.Collections;
using System.Collections.Generic;
using Deprem.Bag;
using Deprem.Core;
using Deprem.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Deprem çantasının içeriği (ENTEGRASYON M13): ruh fazında, ana depremde ve kurtarmada sol altta
    // çantadaki malzemelerin ikonları. Malzeme çantaya konunca ortada kısa bir yazı
    // ("El feneri deprem çantasında") ve yeni ikon büyüyüp yerine oturur. Sadece konanlar görünür;
    // eksikler gösterilmez (oyuncu neyin gerektiğini kendisi çıkarır).
    public class BagHud : MonoBehaviour
    {
        const float IconSize = 32f;
        const float IconGap = 6f;

        [SerializeField] Font font;
        [SerializeField] int fontSize = 12;
        [SerializeField] int toastFontSize = 16;
        [SerializeField] float toastSeconds = 2f;
        [SerializeField] float fadeSeconds = 0.3f;
        [SerializeField] float popScale = 1.6f;
        [SerializeField] float popSeconds = 0.35f;
        [SerializeField] int sortingOrder = 52;

        CanvasGroup group;
        RectTransform panel;
        Text title;
        CanvasGroup toastGroup;
        Text toastText;
        readonly List<RectTransform> icons = new List<RectTransform>();
        Coroutine toastRoutine;

        void Awake()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "BagHudCanvas", sortingOrder);
            group = canvas.GetComponent<CanvasGroup>();

            panel = UiBuilder.CreatePanel(canvas.transform, "Bag").rectTransform;
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 0f);
            panel.pivot = new Vector2(0f, 0f);
            panel.anchoredPosition = new Vector2(12f, 44f);

            title = UiBuilder.CreateText(panel, "Title", font, fontSize, Color.white, TextAnchor.MiddleLeft);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0f, 0.5f);
            titleRect.anchoredPosition = new Vector2(8f, 0f);
            titleRect.sizeDelta = new Vector2(48f, 0f);

            var toast = UiBuilder.CreatePanel(canvas.transform, "Toast");
            var toastRect = toast.rectTransform;
            toastRect.anchorMin = toastRect.anchorMax = new Vector2(0.5f, 0.72f);
            toastRect.sizeDelta = new Vector2(280f, 34f);
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastText = UiBuilder.CreateText(toastRect, "Text", font, toastFontSize, Color.white);
            UiBuilder.Stretch(toastText.rectTransform, Vector2.zero, Vector2.one);
            toastGroup.alpha = 0f;

            Rebuild();
        }

        void OnEnable()
        {
            GameEvents.BagItemCollected += HandleBagItemCollected;
            GameEvents.PhaseChanged += HandlePhaseChanged;
            Texts.LanguageChanged += RefreshTitle;
        }

        void OnDisable()
        {
            GameEvents.BagItemCollected -= HandleBagItemCollected;
            GameEvents.PhaseChanged -= HandlePhaseChanged;
            Texts.LanguageChanged -= RefreshTitle;
        }

        void Start() => HandlePhaseChanged(GameState.Phase);

        void HandlePhaseChanged(GamePhase phase)
        {
            group.alpha = phase == GamePhase.Spirit || phase == GamePhase.MainQuake || phase == GamePhase.Rescue ? 1f : 0f;
            Rebuild();
        }

        void HandleBagItemCollected(BagItemId id)
        {
            Rebuild();
            var added = icons.Find(icon => icon.name == $"Icon_{id}");
            if (added) StartCoroutine(Pop(added));

            toastText.text = Texts.Format("bag.added", Texts.Get($"bag.{id}"));
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ShowToast());
        }

        // Çantadaki malzemelerin ikonları
        void Rebuild()
        {
            foreach (var icon in icons)
                if (icon) Destroy(icon.gameObject);
            icons.Clear();

            var bag = BagSystem.Instance;
            float x = 60f;
            foreach (var id in GameState.BagItems)
            {
                var image = UiBuilder.CreateImage(panel, $"Icon_{id}", bag ? bag.ColorFor(id) : Color.white);
                var sprite = bag ? bag.IconFor(id) : null;
                if (sprite) UiBuilder.ApplySprite(image, sprite);
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = Vector2.one * IconSize;
                rect.anchoredPosition = new Vector2(x, 0f);
                icons.Add(rect);
                x += IconSize + IconGap;
            }

            panel.sizeDelta = new Vector2(x + 2f, IconSize + 12f);
            panel.gameObject.SetActive(icons.Count > 0);
            RefreshTitle();
        }

        void RefreshTitle() => title.text = Texts.Get("hud.bag");

        IEnumerator Pop(RectTransform icon)
        {
            for (float t = 0f; t < popSeconds && icon; t += Time.deltaTime)
            {
                icon.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, t / popSeconds);
                yield return null;
            }
            if (icon) icon.localScale = Vector3.one;
        }

        IEnumerator ShowToast()
        {
            yield return FadeToast(0f, 1f);
            yield return new WaitForSeconds(toastSeconds);
            yield return FadeToast(1f, 0f);
            toastRoutine = null;
        }

        IEnumerator FadeToast(float from, float to)
        {
            for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
            {
                toastGroup.alpha = Mathf.Lerp(from, to, t / fadeSeconds);
                yield return null;
            }
            toastGroup.alpha = to;
        }
    }
}
