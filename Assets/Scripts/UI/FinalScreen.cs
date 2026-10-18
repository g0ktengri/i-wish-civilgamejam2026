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
    // Outro (mezarlık, Mehmet; Berke müsait olmadığı için bu kısmı da Mehmet yazdı): sahnede outro varsa keşkeler
    // karakter mezarlığa yürürken gökyüzünde tek tek çatlayıp bugüne döner; outro bitip ekran kararınca geçmiş
    // depremleri anma listesi hızla kayar, sonra aynı kapanış (büyük cümle, AFAD, ana menü).
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

        [Header("Outro (mezarlık): yürürken farkındalık")]
        [SerializeField] int awarenessMaxLines = 3;
        [Tooltip("Bir cümle (keşke, çatlama, öğüt, sönme) en az bu kadar sürsün; süre yetmezse daha az cümle")]
        [SerializeField] float awarenessMinLineTime = 3.4f;
        [SerializeField] int awarenessFontSize = 22;
        [Tooltip("Outronun kararma katmanının (98) üstünde, final ekranının altında")]
        [SerializeField] int awarenessSortingOrder = 99;

        [Header("Outro bitince: geçmiş depremleri anma")]
        [Tooltip("Listenin alttan girip üstten çıkması (sn)")]
        [SerializeField] float memorialScrollTime = 12f;
        [SerializeField] int memorialFontSize = 18;
        [SerializeField] int memorialTitleFontSize = 22;
        [SerializeField] float memorialLineHeight = 30f;
        [Tooltip("Sarı yazılan satır: oyun 6 Şubat 2023 depremiyle ilgili (Mehmet'in isteği)")]
        [SerializeField] string memorialHighlightKey = "memorial.16";
        [Tooltip("Outrodan sonra ortadaki kapanış cümlesinin en büyük boyu (sığmazsa küçülür)")]
        [SerializeField] int centeredClosingFontSize = 40;
        [SerializeField] int centeredSubFontSize = 30;

        CanvasGroup awarenessGroup;
        Text awarenessLine;
        Coroutine awarenessRoutine;
        Text memorialTitle;
        RectTransform memorialList;

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
            // Outro (mezarlık, Mehmet): karakter yürürken farkındalık mesajları, outro bitip ekran kararınca anma ve
            // kapanış. Outro yoksa (test sahnesi) eski akış.
            if (Quake.Outro.Available)
            {
                Clear();
                Quake.Outro.AwarenessStarted -= HandleAwarenessStarted;
                Quake.Outro.AwarenessStarted += HandleAwarenessStarted;
                Quake.Outro.AwarenessEnded -= HandleAwarenessEnded;
                Quake.Outro.AwarenessEnded += HandleAwarenessEnded;
                Quake.Outro.Finished -= HandleOutroFinished;
                Quake.Outro.Finished += HandleOutroFinished;
                return;
            }
            StartCoroutine(Play());
        }

        void HandleAwarenessStarted(float seconds)
        {
            Quake.Outro.AwarenessStarted -= HandleAwarenessStarted;
            if (awarenessRoutine != null) StopCoroutine(awarenessRoutine);
            awarenessRoutine = StartCoroutine(Awareness(seconds));
        }

        void HandleAwarenessEnded()
        {
            Quake.Outro.AwarenessEnded -= HandleAwarenessEnded;
            if (awarenessRoutine != null) StopCoroutine(awarenessRoutine);
            awarenessRoutine = StartCoroutine(FadeCanvasGroup(awarenessGroup, awarenessGroup.alpha, 0f, 0.6f));
        }

        void HandleOutroFinished()
        {
            Quake.Outro.Finished -= HandleOutroFinished;
            StopAllCoroutines();
            awarenessGroup.alpha = 0f;
            StartCoroutine(MemorialAndClosing());
        }

        // ------------------------------------------------------------------ Outro: yürürken farkındalık

        // Mezarlığın gökyüzünde (sinema bandının altında) tek tek: söylenen keşke gelir, çatlayıp bugüne döner, söner.
        // Verilen sürenin içinde biter (karakter ilk mezara varınca).
        IEnumerator Awareness(float seconds)
        {
            var said = SaidWishes();
            int count = Mathf.Clamp(Mathf.FloorToInt(seconds / awarenessMinLineTime), 1, Mathf.Min(awarenessMaxLines, said.Count));
            float per = seconds / count;
            awarenessGroup.alpha = 1f;
            Audio.Sfx.Play("wish.note");
            for (int i = 0; i < count; i++)
            {
                var (reason, item) = said[i];
                awarenessLine.text = WishLines.Wish(reason, item);
                awarenessLine.fontStyle = FontStyle.Normal;
                awarenessLine.rectTransform.localScale = Vector3.one;
                yield return FadeText(awarenessLine, wishColor, 0.4f);
                yield return new WaitForSecondsRealtime(per * 0.25f);
                yield return Shatter(awarenessLine, WishLines.Advice(reason, item));
                yield return new WaitForSecondsRealtime(Mathf.Max(0.3f, per - 0.4f - per * 0.25f - shatterTime - 0.3f - 0.5f));
                var shown = awarenessLine.color;
                for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
                {
                    awarenessLine.color = new Color(shown.r, shown.g, shown.b, shown.a * (1f - t / 0.5f));
                    yield return null;
                }
                awarenessLine.color = Color.clear;
            }
            awarenessRoutine = null;
        }

        // ------------------------------------------------------------------ Outro bitince: anma ve kapanış

        // Siyah ekranda başlık, altında geçmiş depremler hızla yukarı kayar; sonra büyük kapanış cümlesi ve AFAD
        IEnumerator MemorialAndClosing()
        {
            canLeave = false;
            group.alpha = 1f; // outro ekranı zaten kararttı
            memorialTitle.text = Texts.Get("memorial.title");
            yield return FadeText(memorialTitle, creamColor, 0.8f);
            yield return new WaitForSecondsRealtime(0.4f);

            BuildMemorialList();
            var viewport = (RectTransform)memorialList.parent;
            float viewHeight = viewport.rect.height;
            float listHeight = memorialList.sizeDelta.y;
            float from = -viewHeight, to = listHeight; // alttan girer, üstten çıkar
            for (float t = 0f; t < memorialScrollTime; t += Time.unscaledDeltaTime)
            {
                memorialList.anchoredPosition = new Vector2(0f, Mathf.Round(Mathf.Lerp(from, to, t / memorialScrollTime)));
                FadeMemorialEdges(viewHeight);
                yield return null;
            }
            memorialList.anchoredPosition = new Vector2(0f, to);
            yield return FadeText(memorialTitle, Color.clear, 0.6f);
            CenterClosing();
            yield return Closing();
        }

        // Mehmet'in isteği: outrodan sonra kapanış bütün ekranı kullansın ve sığsın. Liste yokken blok ekranın
        // ortasında, büyük; cümle ekrana sığmazsa küçülür ya da iki satıra iner.
        void CenterClosing()
        {
            UiBuilder.Stretch(closingArea, new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.74f));
            foreach (var layer in closingArea.GetComponentsInChildren<Text>(true))
            {
                layer.fontSize = centeredClosingFontSize;
                layer.horizontalOverflow = HorizontalWrapMode.Wrap;
                layer.resizeTextForBestFit = true;
                layer.resizeTextMinSize = 18;
                layer.resizeTextMaxSize = centeredClosingFontSize;
            }
            UiBuilder.Stretch(subText.rectTransform, new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.48f));
            subText.fontSize = centeredSubFontSize;
            subText.resizeTextForBestFit = true;
            subText.resizeTextMinSize = 16;
            subText.resizeTextMaxSize = centeredSubFontSize;
            UiBuilder.Stretch(siteText.rectTransform, new Vector2(0.1f, 0.26f), new Vector2(0.9f, 0.34f));
            siteText.fontSize = subFontSize;
        }

        // Satır alttan girerken ve üstten çıkarken yarım satır boyunca solar (kesilip başlığa yapışık durmasın)
        void FadeMemorialEdges(float viewHeight)
        {
            float fade = memorialLineHeight * 1.5f;
            for (int i = 0; i < memorialList.childCount; i++)
            {
                var text = memorialList.GetChild(i).GetComponent<Text>();
                if (!text) continue;
                // Satırın ortasının görünür alanın üstünden uzaklığı (0 üst kenar, viewHeight alt kenar)
                float y = -(memorialList.anchoredPosition.y + text.rectTransform.anchoredPosition.y) + memorialLineHeight * 0.5f;
                float a = Mathf.Clamp01(Mathf.Min(y, viewHeight - y) / fade);
                var c = text.color;
                text.color = new Color(c.r, c.g, c.b, a);
            }
        }

        void BuildMemorialList()
        {
            ClearChildren(memorialList);
            var entries = new List<(string text, bool highlight)>();
            for (int i = 1; ; i++)
            {
                string key = $"memorial.{i:00}";
                if (!Texts.Has(key)) break;
                entries.Add((Texts.Get(key), key == memorialHighlightKey));
            }
            entries.Add((string.Empty, false));
            entries.Add((Texts.Get("memorial.everyone"), true));
            for (int i = 0; i < entries.Count; i++)
            {
                var text = UiBuilder.Outlined(UiBuilder.CreateText(memorialList, $"Entry{i}", font, memorialFontSize,
                    entries[i].highlight ? adviceColor : creamColor));
                var rect = text.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, memorialLineHeight);
                rect.anchoredPosition = new Vector2(0f, -i * memorialLineHeight);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.text = entries[i].text;
            }
            memorialList.sizeDelta = new Vector2(0f, entries.Count * memorialLineHeight);
        }

        static IEnumerator FadeCanvasGroup(CanvasGroup target, float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                target.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            target.alpha = to;
        }

        List<(LoseReason, ItemId)> SaidWishes()
        {
            var said = WishLines.History.Count > 0 ? new List<(LoseReason, ItemId)>(WishLines.History)
                : new List<(LoseReason, ItemId)>(Lessons);
            if (said.Count > maxLines) said.RemoveRange(maxLines, said.Count - maxLines);
            return said;
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
            Clear();

            Audio.Sfx.PlayMusic("music.final"); // M25
            yield return new WaitForSecondsRealtime(startDelay);
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                group.alpha = t / fadeTime;
                yield return null;
            }
            group.alpha = 1f;

            // 1) Söylenen keşkeler tek tek
            var said = SaidWishes();
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
            yield return Closing();
        }

        IEnumerator Closing()
        {
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
            memorialTitle.color = Color.clear;
            ClearChildren(memorialList);
            awarenessLine.color = Color.clear;
            awarenessGroup.alpha = 0f;
        }

        static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
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

            // Anma: başlık ve maskeli, kayan liste (siyah zeminin üstünde)
            memorialTitle = UiBuilder.Outlined(UiBuilder.CreateText(canvas.transform, "MemorialTitle", font,
                memorialTitleFontSize, Color.clear));
            UiBuilder.Stretch(memorialTitle.rectTransform, new Vector2(0.06f, 0.8f), new Vector2(0.94f, 0.9f));
            var viewport = new GameObject("MemorialView", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(canvas.transform, false);
            // Mehmet'in play testi: üstten çıkan satır başlığa yapışık duruyordu; arada boşluk, kenarlarda solma
            UiBuilder.Stretch(viewport, new Vector2(0.1f, 0.12f), new Vector2(0.9f, 0.7f));
            memorialList = new GameObject("MemorialList", typeof(RectTransform)).GetComponent<RectTransform>();
            memorialList.SetParent(viewport, false);
            memorialList.anchorMin = new Vector2(0f, 1f);
            memorialList.anchorMax = new Vector2(1f, 1f);
            memorialList.pivot = new Vector2(0.5f, 1f);
            memorialList.sizeDelta = Vector2.zero;

            // Outro: mezarlığın gökyüzünde farkındalık cümlesi (ayrı tuval; siyah zemin yok, mezarlık görünür)
            var awarenessCanvas = UiBuilder.CreateCanvas(transform, "AwarenessCanvas", awarenessSortingOrder);
            awarenessGroup = awarenessCanvas.GetComponent<CanvasGroup>();
            var band = UiBuilder.CreateImage(awarenessCanvas.transform, "Band", new Color(0f, 0f, 0.02f, 0.35f));
            UiBuilder.Stretch(band.rectTransform, new Vector2(0f, 0.68f), new Vector2(1f, 0.84f));
            awarenessLine = UiBuilder.Outlined(UiBuilder.CreateText(awarenessCanvas.transform, "Line", font,
                awarenessFontSize, Color.clear));
            UiBuilder.Stretch(awarenessLine.rectTransform, new Vector2(0.04f, 0.68f), new Vector2(0.96f, 0.84f));
            awarenessLine.horizontalOverflow = HorizontalWrapMode.Overflow;
            awarenessGroup.alpha = 0f;
        }
    }
}
