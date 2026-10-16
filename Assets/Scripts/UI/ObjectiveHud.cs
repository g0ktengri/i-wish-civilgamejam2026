using System.Collections;
using Deprem.Core;
using Deprem.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Kurtarma fazının hedefi (ENTEGRASYON M11): faz Rescue olunca ekranın üstünde
    // "Aileni bul ve deprem kabinine götür" çıkar ve faz boyunca köşede kalır.
    // Altında kabinin ne olduğunu anlatan satır birkaç saniye görünüp kaybolur.
    public class ObjectiveHud : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] int fontSize = 14;
        [SerializeField] int infoFontSize = 12;
        [SerializeField] Color color = Color.white;
        [SerializeField] Color infoColor = new Color(0.8f, 0.85f, 0.9f);
        [Tooltip("Kabin açıklamasının ekranda kalma süresi (sn)")]
        [SerializeField] float infoSeconds = 7f;
        [SerializeField] float fadeSeconds = 0.6f;
        [SerializeField] int sortingOrder = 55;

        CanvasGroup group;
        Text objectiveText;
        CanvasGroup infoGroup;
        Text infoText;

        void Awake()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "ObjectiveCanvas", sortingOrder);
            group = canvas.GetComponent<CanvasGroup>();

            var panel = UiBuilder.CreatePanel(canvas.transform, "Objective");
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, -12f);
            rect.sizeDelta = new Vector2(300f, 32f);
            objectiveText = UiBuilder.CreateText(rect, "Text", font, fontSize, color);
            UiBuilder.Stretch(objectiveText.rectTransform, Vector2.zero, Vector2.one);

            var info = UiBuilder.CreatePanel(canvas.transform, "ShelterInfo");
            var infoRect = info.rectTransform;
            infoRect.anchorMin = infoRect.anchorMax = new Vector2(0f, 1f);
            infoRect.pivot = new Vector2(0f, 1f);
            infoRect.anchoredPosition = new Vector2(12f, -48f);
            infoRect.sizeDelta = new Vector2(300f, 44f);
            infoGroup = info.gameObject.AddComponent<CanvasGroup>();
            infoText = UiBuilder.CreateText(infoRect, "Text", font, infoFontSize, infoColor);
            UiBuilder.Stretch(infoText.rectTransform, Vector2.zero, Vector2.one);
            infoText.rectTransform.offsetMin = new Vector2(8f, 2f);
            infoText.rectTransform.offsetMax = new Vector2(-8f, -2f);

            group.alpha = 0f;
            infoGroup.alpha = 0f;
        }

        void OnEnable()
        {
            GameEvents.PhaseChanged += HandlePhaseChanged;
            Texts.LanguageChanged += Refresh;
        }

        void OnDisable()
        {
            GameEvents.PhaseChanged -= HandlePhaseChanged;
            Texts.LanguageChanged -= Refresh;
        }

        void HandlePhaseChanged(GamePhase phase)
        {
            StopAllCoroutines();
            bool rescue = phase == GamePhase.Rescue;
            group.alpha = rescue ? 1f : 0f;
            infoGroup.alpha = 0f;
            if (!rescue) return;
            Refresh();
            StartCoroutine(ShowInfo());
        }

        IEnumerator ShowInfo()
        {
            yield return Fade(infoGroup, 0f, 1f);
            yield return new WaitForSeconds(infoSeconds);
            yield return Fade(infoGroup, 1f, 0f);
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
            infoText.text = Texts.Get("objective.shelter_info");
        }
    }
}
