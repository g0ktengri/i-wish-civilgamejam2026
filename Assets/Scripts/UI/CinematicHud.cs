using System;
using System.Reflection;
using Deprem.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Intro sinematiği arayüzü (M56, M57; Mehmet'in Deprem.Quake.Cinematic sözleşmesi):
    // - Cinematic.IsPlaying iken üstte ve altta siyah sinema bantları yumuşakça girer, oyun içi HUD'lar (ipucu, hedef ve
    //   oklar, çanta, ruh sayacı) gizlenir; bitince bantlar çıkar, HUD'lar döner. Duraklatma (Esc) çalışır.
    // - Cinematic.CanSkip iken sağ altta "Boşluk (basılı tut): Geç" ve dolan halka; basılı tutulunca Cinematic.Skip().
    // Cinematic sınıfı yansımayla okunur: gelmeden de derlenir; yokken hiçbir şey görünmez.
    public class CinematicHud : MonoBehaviour
    {
        [SerializeField] Font font;
        [Tooltip("0: UiStyle.hudFontSize'tan küçük (yazı boyu)")]
        [SerializeField] int fontSize = 16;
        [Tooltip("Bantların yüksekliği (referans pikseli, 540'lık ekranda)")]
        [SerializeField] float barHeight = 58f;
        [Tooltip("Bantların girip çıkma süresi (sn)")]
        [SerializeField] float barSeconds = 0.6f;
        [Tooltip("Geçmek için basılı tutma süresi (sn)")]
        [SerializeField] float holdSeconds = 1f;
        [SerializeField] int sortingOrder = 100;

        RectTransform top;
        RectTransform bottom;
        RectTransform skipRoot;
        UiPrompt skipPrompt;
        Image ring;
        float show;
        float hold;
        bool hudHidden;
        bool skipped;
        static Sprite ringSprite;

        // Yansıma: Deprem.Quake.Cinematic (IsPlaying, CanSkip, Skip)
        static bool lookedUp;
        static PropertyInfo isPlaying;
        static PropertyInfo canSkip;
        static FieldInfo isPlayingField;
        static FieldInfo canSkipField;
        static MethodInfo skip;

        // Gizlenen oyun içi HUD'lar (kendi Canvas'ları kapatılır; kendi kodları alpha'larıyla oynamaya devam eder)
        static readonly Type[] HudTypes =
        {
            typeof(PromptHud), typeof(ObjectiveHud), typeof(BagHud), typeof(SpiritHud),
        };

        public static bool Playing => Read(isPlaying, isPlayingField);
        static bool Skippable => Read(canSkip, canSkipField);

        void Awake() => BuildCanvas();

        void OnDisable() => SetHudHidden(false);

        void Update()
        {
            bool playing = Playing;
            float target = playing ? 1f : 0f;
            show = Mathf.MoveTowards(show, target, Time.unscaledDeltaTime / Mathf.Max(0.05f, barSeconds));
            float eased = show * show * (3f - 2f * show);
            top.sizeDelta = new Vector2(0f, barHeight * eased);
            bottom.sizeDelta = new Vector2(0f, barHeight * eased);
            top.gameObject.SetActive(show > 0f);
            bottom.gameObject.SetActive(show > 0f);

            if (playing != hudHidden) SetHudHidden(playing);

            // Geç: Boşluk basılı tutulunca halka dolar, dolunca intro geçilir
            bool skippable = playing && Skippable && Time.timeScale > 0f;
            skipRoot.gameObject.SetActive(skippable);
            if (!skippable)
            {
                hold = 0f;
                skipped = false;
                return;
            }
            skipPrompt.Set(Texts.Get("cinematic.skip"));
            var keyboard = Keyboard.current;
            bool held = keyboard != null && keyboard.spaceKey.isPressed;
            hold = held && !skipped ? hold + Time.unscaledDeltaTime : Mathf.MoveTowards(hold, 0f, Time.unscaledDeltaTime * 2f);
            ring.fillAmount = Mathf.Clamp01(hold / Mathf.Max(0.1f, holdSeconds));
            if (!skipped && hold >= holdSeconds)
            {
                skipped = true;
                hold = 0f;
                Audio.Sfx.Play("ui.click");
                skip?.Invoke(null, null);
            }
        }

        void SetHudHidden(bool hidden)
        {
            hudHidden = hidden;
            foreach (var type in HudTypes)
                foreach (var hud in FindObjectsByType(type, FindObjectsSortMode.None))
                    foreach (var canvas in ((Component)hud).GetComponentsInChildren<Canvas>(true))
                        canvas.enabled = !hidden;
        }

        static bool Read(PropertyInfo property, FieldInfo field)
        {
            LookUp();
            try
            {
                object value = property != null ? property.GetValue(null) : field?.GetValue(null);
                return value is bool b && b;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static void LookUp()
        {
            if (lookedUp) return;
            lookedUp = true;
            var type = Type.GetType("Deprem.Quake.Cinematic, Assembly-CSharp");
            if (type == null) return;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            isPlaying = type.GetProperty("IsPlaying", flags);
            canSkip = type.GetProperty("CanSkip", flags);
            isPlayingField = isPlaying == null ? type.GetField("IsPlaying", flags) : null;
            canSkipField = canSkip == null ? type.GetField("CanSkip", flags) : null;
            skip = type.GetMethod("Skip", flags, null, Type.EmptyTypes, null);
        }

        void BuildCanvas()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "CinematicCanvas", sortingOrder);
            top = Bar(canvas.transform, "TopBar", 1f);
            bottom = Bar(canvas.transform, "BottomBar", 0f);

            // Sağ altta: dolan halka ve tuş ipucu (bandın içinde)
            skipRoot = new GameObject("Skip", typeof(RectTransform)).GetComponent<RectTransform>();
            skipRoot.SetParent(canvas.transform, false);
            skipRoot.anchorMin = skipRoot.anchorMax = new Vector2(1f, 0f);
            skipRoot.pivot = new Vector2(1f, 0f);
            skipRoot.anchoredPosition = new Vector2(-18f, 12f);
            skipRoot.sizeDelta = Vector2.zero;

            var ringBack = UiBuilder.CreateImage(skipRoot, "RingBack", new Color(1f, 1f, 1f, 0.2f));
            ringBack.sprite = RingSprite;
            Place(ringBack.rectTransform);
            ring = UiBuilder.CreateImage(skipRoot, "Ring", new Color(1f, 0.82f, 0.4f));
            ring.sprite = RingSprite;
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 0f;
            Place(ring.rectTransform);

            skipPrompt = UiPrompt.Create(skipRoot, "Prompt", font ? font : UiBuilder.HudFont, fontSize);
            skipPrompt.Animate = false;
            skipPrompt.RemapKeys = false;
            var rect = skipPrompt.Rect;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-36f, 0f);
            skipRoot.gameObject.SetActive(false);
        }

        static void Place(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(28f, 28f);
            rect.anchoredPosition = new Vector2(0f, 3f);
        }

        static RectTransform Bar(Transform parent, string name, float y)
        {
            var bar = UiBuilder.CreateImage(parent, name, Color.black);
            var rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0f, y);
            rect.anchorMax = new Vector2(1f, y);
            rect.pivot = new Vector2(0.5f, y);
            rect.sizeDelta = Vector2.zero;
            bar.gameObject.SetActive(false);
            return rect;
        }

        // 32 x 32 pixel halka (dış yarıçap 15, kalınlık 4), beyaz (rengi Image.color'dan)
        static Sprite RingSprite
        {
            get
            {
                if (ringSprite) return ringSprite;
                const int n = 32;
                var texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "UiSkipRing",
                };
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                    texture.SetPixel(x, y, d <= 15f && d >= 11f ? Color.white : Color.clear);
                }
                texture.Apply();
                ringSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return ringSprite;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            lookedUp = false;
            isPlaying = canSkip = null;
            isPlayingField = canSkipField = null;
            skip = null;
        }
    }
}
