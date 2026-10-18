using System.Collections;
using System.Collections.Generic;
using Deprem.Core;
using Deprem.Localization;
using Deprem.Loop;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Final ekranı: herkes kabine girince ev kararır, oyun boyunca söylenen keşke cümleleri tek tek gelir.
    // M47 (Mehmet'in play testi 24: hazırlık listesi yerine çarpıcı bir son): sonra her cümlede "Keşke" çatlayıp
    // kırılır (wish.shatter) ve cümle bugüne döner ("Keşke kitaplığı sabitleseydim" → "Kitaplığı sabitle.").
    // En sonda tek ve büyük cümle ("Deprem geldiğinde keşkelerin olmasın. Bugün hazırlan."), altında afad.gov.tr.
    // Hiç keşke söylenmediyse (kayıttan devam) GDD'nin beş dersi gösterilir. F ile ana menüye dönülür.
    public class FinalScreen : MonoBehaviour
    {
        // GDD "Eğitim içeriği" sırası (geçmiş boşsa)
        static readonly (LoseReason reason, ItemId item)[] Lessons =
        {
            (LoseReason.IgnoredForeshock, ItemId.None), (LoseReason.NoDuckCoverHold, ItemId.None),
            (LoseReason.FamilyHitByItem, ItemId.None), (LoseReason.MissingBagItem, ItemId.None),
            (LoseReason.AftershockInjury, ItemId.None),
        };

        [Header("Zamanlama (sn)")]
        [Tooltip("Kabin kapanma anı oynansın diye kararmadan önceki bekleme")]
        [SerializeField] float startDelay = 2f;
        [SerializeField] float fadeTime = 1.2f;
        [Tooltip("Keşke cümleleri arasındaki bekleme")]
        [SerializeField] float lineDelay = 0.6f;
        [Tooltip("Her cümlenin çatlayıp bugüne dönmesi")]
        [SerializeField] float shatterTime = 0.35f;
        [SerializeField] float turnDelay = 0.45f;
        [SerializeField] float closingDelay = 0.9f;
        [Tooltip("En fazla bu kadar keşke cümlesi gösterilir")]
        [SerializeField] int maxLines = 7;

        [Header("Görünüm")]
        [SerializeField] Font font;
        [SerializeField] int lineFontSize = 20;
        [SerializeField] int closingFontSize = 30;
        [SerializeField] int subFontSize = 22;
        [SerializeField] int smallFontSize = 14;
        [SerializeField] float lineHeight = 34f;
        [Tooltip("Arkadaki ev biraz görünsün diye tam siyah değil")]
        [SerializeField] float backgroundAlpha = 0.97f;
        [SerializeField] Color wishColor = new Color(0.62f, 0.6f, 0.58f);
        [SerializeField] Color adviceColor = new Color(1f, 0.82f, 0.4f);
        [SerializeField] Color creamColor = new Color(0.96f, 0.92f, 0.84f);
        [SerializeField] int sortingOrder = 110;

        CanvasGroup group;
        RectTransform list;
        RectTransform closingArea;
        UiTitle closingText;
        Text subText;
        Text siteText;
        Text hintText;
        readonly List<Text> lines = new List<Text>();
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
            // Mehmet (outro geçişi, Berke düzenleyecek): mezarlık outrosu varsa final, outro bitip ekran kararınca başlar
            if (Quake.Outro.Available)
            {
                Quake.Outro.Finished -= HandleOutroFinished;
                Quake.Outro.Finished += HandleOutroFinished;
                return;
            }
            StartCoroutine(Play());
        }

        void HandleOutroFinished()
        {
            Quake.Outro.Finished -= HandleOutroFinished;
            StopAllCoroutines();
            StartCoroutine(Play(afterOutro: true));
        }

        void Update()
        {
            if (!canLeave || !UiInput.ConfirmOrClick()) return;
            canLeave = false;
            if (LoopManager.Instance) LoopManager.Instance.ReturnToMenu();
        }

        // afterOutro: ekran zaten siyah, müzik outroyla başladı; bekleme yok
        IEnumerator Play(bool afterOutro = false)
        {
            canLeave = false;
            Clear();

            if (!afterOutro) Audio.Sfx.PlayMusic("music.final"); // M25
            if (!afterOutro) yield return new WaitForSecondsRealtime(startDelay);
            for (float t = 0f; t < (afterOutro ? 0.3f : fadeTime); t += Time.unscaledDeltaTime)
            {
                group.alpha = t / fadeTime;
                yield return null;
            }
            group.alpha = 1f;

            // 1) Söylenen keşkeler tek tek
            var said = WishLines.History.Count > 0 ? new List<(LoseReason, ItemId)>(WishLines.History)
                : new List<(LoseReason, ItemId)>(Lessons);
            if (said.Count > maxLines) said.RemoveRange(maxLines, said.Count - maxLines);
            Audio.Sfx.Play("wish.note");
            foreach (var (reason, item) in said)
            {
                var line = AddLine(WishLines.Wish(reason, item));
                yield return FadeText(line, wishColor, 0.35f);
                yield return new WaitForSecondsRealtime(lineDelay);
            }
            yield return new WaitForSecondsRealtime(turnDelay * 2f);

            // 2) Her birinde "Keşke" çatlar, cümle bugüne döner
            for (int i = 0; i < said.Count; i++)
            {
                yield return Shatter(lines[i], WishLines.Advice(said[i].Item1, said[i].Item2));
                yield return new WaitForSecondsRealtime(turnDelay);
            }

            // 3) Tek ve büyük cümle, altında AFAD
            yield return new WaitForSecondsRealtime(closingDelay);
            closingText.Text = Texts.Get("final.closing");
            yield return FadeGroup(closingArea, 0.8f);
            yield return new WaitForSecondsRealtime(0.5f);
            subText.text = Texts.Get("final.closing_sub");
            yield return FadeText(subText, adviceColor, 0.6f);
            yield return new WaitForSecondsRealtime(0.6f);
            siteText.text = Texts.Get("final.site");
            yield return FadeText(siteText, new Color(creamColor.r, creamColor.g, creamColor.b, 0.6f), 0.5f);
            hintText.text = Texts.Get("prompt.main_menu");
            canLeave = true;
        }

        // Satır titrer, üstünden çatlak geçer (krem), yazı kırılıp söner; sonra öğüt vurgu renginde büyüyerek gelir
        IEnumerator Shatter(Text line, string advice)
        {
            Audio.Sfx.Play("wish.shatter");
            var crack = new GameObject("Crack", typeof(RectTransform)).GetComponent<RectTransform>();
            crack.SetParent(line.rectTransform, false);
            crack.anchorMin = crack.anchorMax = new Vector2(0.5f, 0.5f);
            crack.sizeDelta = Vector2.zero;
            float width = Mathf.Max(80f, line.preferredWidth + 16f);
            UiBuilder.CreateCrack(crack, new Vector2(-width * 0.5f, 2f), new Vector2(1f, -0.04f), width, line.GetHashCode(),
                creamColor, 2f, 0.25f, 11f, 3f);

            var home = line.rectTransform.anchoredPosition;
            var from = line.color;
            for (float t = 0f; t < shatterTime; t += Time.unscaledDeltaTime)
            {
                float k = t / shatterTime;
                line.rectTransform.anchoredPosition = home + new Vector2(Mathf.Round(Random.Range(-3f, 3f) * (1f - k)),
                    Mathf.Round(Random.Range(-1.5f, 1.5f) * (1f - k)));
                line.color = new Color(from.r, from.g, from.b, from.a * (1f - k * k));
                yield return null;
            }
            line.rectTransform.anchoredPosition = home;
            Destroy(crack.gameObject);

            line.text = advice;
            line.fontStyle = FontStyle.Normal;
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.3f;
                float ease = 1f - (1f - k) * (1f - k);
                line.color = new Color(adviceColor.r, adviceColor.g, adviceColor.b, ease);
                line.rectTransform.localScale = Vector3.one * (1f + 0.15f * (1f - ease));
                yield return null;
            }
            line.color = adviceColor;
            line.rectTransform.localScale = Vector3.one;
        }

        IEnumerator FadeText(Text text, Color to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                text.color = new Color(to.r, to.g, to.b, to.a * (t / duration));
                yield return null;
            }
            text.color = to;
        }

        static IEnumerator FadeGroup(RectTransform rect, float duration)
        {
            var fade = rect.GetComponent<CanvasGroup>();
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                fade.alpha = t / duration;
                yield return null;
            }
            fade.alpha = 1f;
        }

        Text AddLine(string text)
        {
            var line = UiBuilder.Outlined(UiBuilder.CreateText(list, $"Line{lines.Count}", font, lineFontSize, Color.clear));
            var rect = line.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, lineHeight);
            rect.anchoredPosition = new Vector2(0f, -lines.Count * lineHeight);
            line.horizontalOverflow = HorizontalWrapMode.Overflow;
            line.text = text;
            lines.Add(line);
            return line;
        }

        void Clear()
        {
            foreach (var line in lines)
                if (line)
                {
                    if (Application.isPlaying) Destroy(line.gameObject);
                    else DestroyImmediate(line.gameObject);
                }
            lines.Clear();
            closingText.Text = string.Empty;
            closingArea.GetComponent<CanvasGroup>().alpha = 0f;
            subText.text = string.Empty;
            siteText.text = string.Empty;
            hintText.text = string.Empty;
        }

        void BuildCanvas()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "FinalCanvas", sortingOrder);
            group = canvas.GetComponent<CanvasGroup>();

            var background = UiBuilder.CreateImage(canvas.transform, "Background", new Color(0f, 0f, 0f, backgroundAlpha));
            UiBuilder.Stretch(background.rectTransform, Vector2.zero, Vector2.one);

            list = new GameObject("Lines", typeof(RectTransform)).GetComponent<RectTransform>();
            list.SetParent(canvas.transform, false);
            UiBuilder.Stretch(list, new Vector2(0.08f, 0.44f), new Vector2(0.92f, 0.9f));

            closingArea = new GameObject("Closing", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            closingArea.SetParent(canvas.transform, false);
            UiBuilder.Stretch(closingArea, new Vector2(0.04f, 0.28f), new Vector2(0.96f, 0.4f));
            closingText = UiTitle.Create(closingArea, "Text", font, closingFontSize, 2f);
            closingArea.GetComponent<CanvasGroup>().alpha = 0f;

            subText = UiBuilder.Outlined(UiBuilder.CreateText(canvas.transform, "Sub", font, subFontSize, adviceColor));
            UiBuilder.Stretch(subText.rectTransform, new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.28f));

            siteText = UiBuilder.Outlined(UiBuilder.CreateText(canvas.transform, "Site", font, smallFontSize, creamColor));
            UiBuilder.Stretch(siteText.rectTransform, new Vector2(0.1f, 0.12f), new Vector2(0.9f, 0.18f));

            hintText = UiBuilder.Outlined(UiBuilder.CreateText(canvas.transform, "Hint", font, smallFontSize,
                new Color(creamColor.r, creamColor.g, creamColor.b, 0.7f), TextAnchor.LowerRight));
            UiBuilder.Stretch(hintText.rectTransform, new Vector2(0.6f, 0.02f), new Vector2(0.98f, 0.08f));
        }
    }
}
