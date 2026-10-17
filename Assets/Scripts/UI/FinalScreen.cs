using System.Collections;
using System.Collections.Generic;
using Deprem.Core;
using Deprem.Localization;
using Deprem.Loop;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Final ekranı: herkes kabine girince kararır, keşke cümleleri tek tek "yaptım"a döner
    // ve bir deprem hazırlık kontrol listesi çıkar. Altında çağrı ve AFAD yönlendirmesi.
    // F ile ana menüye dönülür. Canvas'ı kendisi kurar.
    public class FinalScreen : MonoBehaviour
    {
        // GDD "Eğitim içeriği" sırası
        static readonly string[] Lessons = { "ignored_foreshock", "duck_cover", "secure_items", "bag", "aftershock" };

        [Header("Zamanlama (sn)")]
        [Tooltip("Kabin kapanma anı oynansın diye kararmadan önceki bekleme")]
        [SerializeField] float startDelay = 2f;
        [SerializeField] float fadeTime = 1f;
        [SerializeField] float rowDelay = 0.7f;
        [SerializeField] float checkDelay = 0.6f;

        [Header("Görünüm")]
        [SerializeField] Font font;
        [SerializeField] int titleFontSize = 26;
        [SerializeField] int rowFontSize = 16;
        [SerializeField] float rowHeight = 30f;
        [SerializeField] Color wishColor = new Color(0.55f, 0.55f, 0.6f);
        [SerializeField] Color doneColor = Color.white;
        [SerializeField] Color boxColor = new Color(0.3f, 0.3f, 0.35f);
        [SerializeField] Color checkColor = new Color(0.45f, 0.85f, 0.5f);
        [SerializeField] int sortingOrder = 110;

        CanvasGroup group;
        RectTransform list;
        Text titleText;
        Text callText;
        Text afadText;
        Text hintText;
        readonly List<GameObject> rows = new List<GameObject>();
        bool canLeave;

        void Awake()
        {
            BuildCanvas();
            group.alpha = 0f;
        }

        void OnEnable() => GameEvents.GameWon += HandleGameWon;
        void OnDisable() => GameEvents.GameWon -= HandleGameWon;

        void HandleGameWon()
        {
            StopAllCoroutines();
            StartCoroutine(Play());
        }

        void Update()
        {
            if (!canLeave || !UiInput.ConfirmOrClick()) return;
            canLeave = false;
            if (LoopManager.Instance) LoopManager.Instance.ReturnToMenu();
        }

        IEnumerator Play()
        {
            canLeave = false;
            foreach (var row in rows) Destroy(row);
            rows.Clear();
            titleText.text = string.Empty;
            callText.text = string.Empty;
            afadText.text = string.Empty;
            hintText.text = string.Empty;

            Audio.Sfx.PlayMusic("music.final"); // M25
            yield return new WaitForSecondsRealtime(startDelay);
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                group.alpha = t / fadeTime;
                yield return null;
            }
            group.alpha = 1f;

            titleText.text = Texts.Get("final.title");
            yield return new WaitForSecondsRealtime(rowDelay);

            foreach (var lesson in Lessons)
            {
                var (label, check) = AddRow(Texts.Get($"wish.{lesson}"));
                yield return new WaitForSecondsRealtime(checkDelay);
                label.text = Texts.Get($"final.{lesson}");
                label.color = doneColor;
                check.enabled = true;
                Audio.Sfx.Play("final.check");
                yield return new WaitForSecondsRealtime(rowDelay);
            }

            // Bonus maddeler: su ve düdük çantadaysa
            if (GameState.HasBagItem(BagItemId.Water)) AddDoneRow(Texts.Get("final.bonus_water"));
            if (GameState.HasBagItem(BagItemId.Whistle)) AddDoneRow(Texts.Get("final.bonus_whistle"));

            yield return new WaitForSecondsRealtime(rowDelay);
            callText.text = Texts.Get("final.call");
            yield return new WaitForSecondsRealtime(rowDelay);
            afadText.text = Texts.Get("final.afad");
            hintText.text = Texts.Get("prompt.main_menu");
            canLeave = true;
        }

        void AddDoneRow(string text)
        {
            var (label, check) = AddRow(text);
            label.color = doneColor;
            check.enabled = true;
        }

        (Text label, Image check) AddRow(string text)
        {
            var row = new GameObject($"Row{rows.Count}", typeof(RectTransform));
            var rect = (RectTransform)row.transform;
            rect.SetParent(list, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, rowHeight);
            rect.anchoredPosition = new Vector2(0f, -rows.Count * rowHeight);
            rows.Add(row);

            float box = rowHeight * 0.5f;
            var boxImage = UiBuilder.CreateImage(rect, "Box", boxColor);
            PlaceLeft(boxImage.rectTransform, box, 0f);
            var check = UiBuilder.CreateImage(boxImage.rectTransform, "Check", checkColor);
            UiBuilder.Stretch(check.rectTransform, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f));
            check.enabled = false;

            var label = UiBuilder.CreateText(rect, "Label", font, rowFontSize, wishColor, TextAnchor.MiddleLeft);
            UiBuilder.Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.rectTransform.offsetMin = new Vector2(box + 12f, 0f);
            label.text = text;
            return (label, check);
        }

        static void PlaceLeft(RectTransform rect, float size, float x)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        void BuildCanvas()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "FinalCanvas", sortingOrder);
            group = canvas.GetComponent<CanvasGroup>();

            var background = UiBuilder.CreateImage(canvas.transform, "Background", Color.black);
            UiBuilder.Stretch(background.rectTransform, Vector2.zero, Vector2.one);

            titleText = UiBuilder.CreateText(canvas.transform, "Title", font, titleFontSize, doneColor);
            UiBuilder.Stretch(titleText.rectTransform, new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.94f));

            list = new GameObject("List", typeof(RectTransform)).GetComponent<RectTransform>();
            list.SetParent(canvas.transform, false);
            UiBuilder.Stretch(list, new Vector2(0.25f, 0.3f), new Vector2(0.8f, 0.78f));

            callText = UiBuilder.CreateText(canvas.transform, "Call", font, rowFontSize + 2, doneColor);
            UiBuilder.Stretch(callText.rectTransform, new Vector2(0.1f, 0.18f), new Vector2(0.9f, 0.26f));

            afadText = UiBuilder.CreateText(canvas.transform, "Afad", font, rowFontSize - 2, wishColor);
            UiBuilder.Stretch(afadText.rectTransform, new Vector2(0.1f, 0.12f), new Vector2(0.9f, 0.18f));

            hintText = UiBuilder.CreateText(canvas.transform, "Hint", font, rowFontSize - 2, wishColor, TextAnchor.LowerRight);
            UiBuilder.Stretch(hintText.rectTransform, new Vector2(0.6f, 0.02f), new Vector2(0.98f, 0.08f));
        }
    }
}
