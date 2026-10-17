using Deprem.Audio;
using Deprem.Core;
using Deprem.Localization;
using Deprem.Loop;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Ana menü: ilk açılışta içerik uyarısı, sonra Devam et (M42, kayıttan) / Yeni oyun / Ayarlar / Kontroller /
    // Emeği geçenler / Dil / Çıkış. Menü açıkken oyun durur (timeScale 0).
    // Deneme başlatmak için sahne yeniden yüklendiğinde menü gizli kalır. Canvas'ı kendisi kurar.
    // Arayüz turu: arkada evin bulanık, soğuk mavi görüntüsü ve düşen toz (UiBackdrop); tepede sallanan lamba ve sıcak
    // ışık konisi; kabartma başlık (UiTitle); ara ara sarsıntı (moloz, toz, lamba).
    // Arayüz turu 3 (Berke: "iç arayüz gibi olmuş, bağımsız olsun", sonra "kırmızı kalksın, ortada olsun"): oyun içi
    // HUD paneli yok. Ortalı başlık ekranı: lamba başlığın üstünde, altında iki yanı süslü alt başlık, çıplak ve büyük
    // satırlar (MenuList plain: seçili satırda iki yanda elmas, altında ortadan açılan ince çatlak); ortadaki sütun
    // yumuşak koyu bantla okunur, iki yanda ev görünür. Metinler (uyarı, emeği geçenler, onay) ortada, üstte ve altta
    // kısa çizgiyle; tuş ipuçları sağ altta düz yazı, jam adı sol altta.
    public class MainMenu : MonoBehaviour
    {
        enum Screen { Hidden, Warning, Main, ConfirmNew, Controls, Credits }

        static bool warningShown;
        static Sprite shadeSprite;

        [SerializeField] Font font;
        [SerializeField] int titleFontSize = 72;
        [SerializeField] int subtitleFontSize = 16;
        [SerializeField] int itemFontSize = 24;
        [SerializeField] int bodyFontSize = 17;
        [SerializeField] int hintFontSize = 15;
        [SerializeField] float rowHeight = 36f;
        [SerializeField] Color normalColor = new Color(0.82f, 0.78f, 0.7f);
        [SerializeField] Color highlightColor = new Color(1f, 0.82f, 0.4f);
        [Tooltip("Başlık bu aralıkla (sn) kısa bir an titrer (deprem)")]
        [SerializeField] float tremorInterval = 5f;
        [Tooltip("Ortadaki sütunun arkasındaki koyu bandın en koyu yeri (0-1)")]
        [SerializeField] float centerShade = 0.6f;
        [SerializeField] int sortingOrder = 120;

        [Header("Görseller (Assets/Art/UI, prefab kurulurken bağlanır)")]
        [Tooltip("menu_arka_plan.png, 960 x 540: menü arka planı")]
        [SerializeField] Sprite backgroundSprite;
        [Tooltip("logo.png: oyunun adı; varsa yazı yerine gösterilir (kendi boyutunda)")]
        [SerializeField] Sprite logoSprite;

        CanvasGroup group;
        UiBackdrop backdrop;
        Image shade;
        RectTransform titleGroup;
        RectTransform ornamentLeft;
        RectTransform ornamentRight;
        Vector2 titleHome;
        UiTitle titleText;
        Text subtitleText;
        Image logo;
        RectTransform bodyRoot;
        Text bodyText;
        Text hintText;
        Text footerText;
        RectTransform items;
        MenuList menu;
        Screen screen = Screen.Hidden;
        bool musicPlaying;
        int tremorCycle = -1;

        // M38: ana menü ekranda mı (duraklatmanın zaman bekçisi için)
        public static bool Visible { get; private set; }

        void Awake() => BuildCanvas();

        void OnEnable()
        {
            LoopManager.MenuPresent = true;
            GameEvents.LoopStarted += HandleLoopStarted;
            Texts.LanguageChanged += Refresh;
        }

        void OnDisable()
        {
            LoopManager.MenuPresent = false;
            GameEvents.LoopStarted -= HandleLoopStarted;
            Texts.LanguageChanged -= Refresh;
            if (screen != Screen.Hidden) Time.timeScale = 1f;
            Visible = false;
        }

        void Start()
        {
            if (LoopManager.HasPendingLoop || GameState.Phase != GamePhase.None) Show(Screen.Hidden);
            else Show(warningShown ? Screen.Main : Screen.Warning);
        }

        void HandleLoopStarted(int loop) => Show(Screen.Hidden);

        void Update()
        {
            Tremor();
            switch (screen)
            {
                case Screen.Hidden:
                    return;
                case Screen.Warning:
                    if (UiInput.ConfirmOrClick())
                    {
                        Sfx.Play("ui.click");
                        warningShown = true;
                        Show(Screen.Main);
                    }
                    return;
                case Screen.Main:
                    if (SettingsMenu.IsOpen || SettingsMenu.ClosedFrame == Time.frameCount) return;
                    menu.Tick();
                    return;
                default:
                    if (UiInput.Back())
                    {
                        Sfx.Play("ui.back");
                        Show(Screen.Main);
                    }
                    else menu.Tick();
                    return;
            }
        }

        void Show(Screen next)
        {
            screen = next;
            Visible = next != Screen.Hidden;
            bool visible = next != Screen.Hidden;
            group.alpha = visible ? 1f : 0f;
            Time.timeScale = visible ? 0f : 1f;
            if (visible)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                if (backdrop) backdrop.Capture();
            }
            // M25: ana menü müziği alt ekranlarda kesilmez; oyun başlayınca susar
            if (visible != musicPlaying)
            {
                musicPlaying = visible;
                if (visible) Sfx.PlayMusic("music.menu");
                else Sfx.StopMusic();
            }

            menu.Clear();
            switch (next)
            {
                case Screen.Main:
                    // M42: "Bölüm seç" yerine kayıtlı döngüden "Devam et" (kayıt yoksa soluk, seçilemez); yeni oyun kaydı
                    // silmeden önce sorar
                    menu.Add("menu.continue", () => StartLoop(SaveGame.Loop));
                    menu.SetEnabled(SaveGame.Exists);
                    menu.Add("menu.new_game", () =>
                    {
                        if (SaveGame.Exists) Show(Screen.ConfirmNew);
                        else StartNewGame();
                    });
                    menu.Add("menu.settings", OpenSettings);
                    menu.Add("menu.controls", () => { if (SettingsMenu.Instance) SettingsMenu.Instance.OpenControls(Refresh); });
                    menu.Add("menu.credits", () => Show(Screen.Credits));
                    menu.Add("menu.language", () => Texts.ToggleLanguage());
                    menu.Add("menu.quit", Quit);
                    break;
                case Screen.ConfirmNew:
                    menu.Add("menu.new_confirm_yes", StartNewGame);
                    menu.Add("menu.cancel", () => Show(Screen.Main));
                    break;
                case Screen.Controls:
                case Screen.Credits:
                    menu.Add("menu.back", () => Show(Screen.Main));
                    break;
            }
            Refresh();
        }

        void Refresh()
        {
            bool warning = screen == Screen.Warning;
            titleText.Text = !warning && !logoSprite ? Texts.Get("game.title") : string.Empty;
            subtitleText.text = Texts.Get("menu.subtitle");
            if (logo) logo.enabled = !warning;
            titleGroup.gameObject.SetActive(!warning);
            items.gameObject.SetActive(!warning);
            switch (screen)
            {
                case Screen.Warning:
                    bodyText.text = Texts.Get("content_warning");
                    SetHints(Texts.Get("prompt.continue"));
                    break;
                case Screen.Controls:
                    bodyText.text = Texts.Get("controls.body");
                    SetHints(Texts.Get("menu.hint.confirm"), Texts.Get("menu.hint.back"));
                    break;
                case Screen.Credits:
                    bodyText.text = Texts.Get("credits.body");
                    SetHints(Texts.Get("menu.hint.confirm"), Texts.Get("menu.hint.back"));
                    break;
                case Screen.ConfirmNew:
                    bodyText.text = Texts.Get("menu.new_confirm");
                    SetHints(Texts.Get("menu.hint.select"), Texts.Get("menu.hint.confirm"), Texts.Get("menu.hint.back"));
                    break;
                default:
                    bodyText.text = string.Empty;
                    SetHints(Texts.Get("menu.hint.select"), Texts.Get("menu.hint.confirm"));
                    break;
            }

            // Metinler ortada, üstte ve altta kısa çizgi; uyarıda başlık yok, diğerlerinde başlığın altında ve satırlar
            // metnin altına iner
            bool hasBody = !string.IsNullOrEmpty(bodyText.text);
            bodyRoot.gameObject.SetActive(hasBody);
            if (warning) UiBuilder.Stretch(bodyRoot, new Vector2(0.14f, 0.38f), new Vector2(0.86f, 0.62f));
            else UiBuilder.Stretch(bodyRoot, new Vector2(0.18f, screen == Screen.ConfirmNew ? 0.4f : 0.3f), new Vector2(0.82f, 0.56f));
            UiBuilder.Stretch(items, new Vector2(0.3f, 0.04f), new Vector2(0.7f, hasBody ? (screen == Screen.ConfirmNew ? 0.36f : 0.26f) : 0.56f));

            footerText.text = Texts.Get("menu.jam");
            PlaceTitleDetails();
            menu.Refresh();
        }

        // Sağ altta düz yazı ipuçları: tuş kısmı vurgu renginde ("W/S: Seç" → W/S Seç)
        void SetHints(params string[] hints)
        {
            var builder = new System.Text.StringBuilder();
            string accent = UiBuilder.Hex(highlightColor);
            foreach (var hint in hints)
            {
                if (builder.Length > 0) builder.Append("     ");
                int colon = hint.IndexOf(':');
                if (colon > 0) builder.Append($"<color={accent}>{hint.Substring(0, colon)}</color> {hint.Substring(colon + 1).Trim()}");
                else builder.Append(hint);
            }
            hintText.text = builder.ToString();
        }

        // Alt başlığın süsleri yazının iki yanına
        void PlaceTitleDetails()
        {
            float half = Mathf.Round(subtitleText.preferredWidth * 0.5f + 14f);
            ornamentLeft.anchoredPosition = new Vector2(-half, 0f);
            ornamentRight.anchoredPosition = new Vector2(half, 0f);
        }

        // M32: ayarlar ana menünün üstünde açılır; kapanınca ana menü yenilenir (dil değişmiş olabilir)
        void OpenSettings()
        {
            if (SettingsMenu.Instance) SettingsMenu.Instance.Open(Refresh);
        }

        // Başlık ara ara kısa bir an titrer (deprem); oyun dururken de (gerçek zaman). Her titremenin başında arka plan
        // sarsılır (moloz, toz, lamba); her üçüncüsü daha güçlü
        void Tremor()
        {
            if (!titleGroup || screen == Screen.Hidden) return;
            float interval = Mathf.Max(1f, tremorInterval);
            int cycle = Mathf.FloorToInt(Time.unscaledTime / interval);
            bool strong = cycle % 3 == 2;
            if (cycle != tremorCycle)
            {
                tremorCycle = cycle;
                if (backdrop) backdrop.Quake(strong ? 1f : 0.45f);
            }
            float t = Time.unscaledTime % interval;
            float amount = strong ? 3f : 2f;
            titleGroup.anchoredPosition = t < (strong ? 0.6f : 0.35f)
                ? titleHome + new Vector2(Mathf.Round(Random.Range(-amount, amount)), Mathf.Round(Random.Range(-amount, amount) * 0.75f))
                : titleHome;
        }

        static void StartNewGame()
        {
            SaveGame.Clear();
            StartLoop(1);
        }

        static void StartLoop(int loop)
        {
            warningShown = true;
            if (LoopManager.Instance) LoopManager.Instance.StartFromLoop(loop);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            Debug.Log("[MainMenu] Çıkış (Editor'de çalışmaz)");
#endif
            Application.Quit();
        }

        static RectTransform CreateRect(Transform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        // Alt başlığın bir yanındaki süs: kısa çizgi ve dış ucunda elmas (side: -1 sol, +1 sağ)
        static RectTransform Ornament(RectTransform parent, Color color, float side)
        {
            var root = CreateRect(parent, side < 0f ? "OrnamentLeft" : "OrnamentRight");
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;
            Line(root, "Line", color, new Vector2(56f, 2f), new Vector2(side * 28f, 0f));
            Line(root, "Diamond", color, new Vector2(6f, 6f), new Vector2(side * 59f, 0f)).rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, 45f);
            return root;
        }

        static Image Line(Transform parent, string name, Color color, Vector2 size, Vector2 position)
        {
            var line = UiBuilder.CreateImage(parent, name, color);
            line.rectTransform.sizeDelta = size;
            line.rectTransform.anchoredPosition = position;
            line.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1f, -1f);
            return line;
        }

        void BuildCanvas()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "MainMenuCanvas", sortingOrder);
            group = canvas.GetComponent<CanvasGroup>();
            var accent = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.85f);

            // Arka plan: sanatçı görseli varsa o; yoksa evin bulanık soğuk mavi görüntüsü, toz ve başlığın üstünde lamba
            if (backgroundSprite)
            {
                var background = UiBuilder.CreateImage(canvas.transform, "Background", Color.white);
                background.sprite = backgroundSprite;
                UiBuilder.Stretch(background.rectTransform, Vector2.zero, Vector2.one);
            }
            else backdrop = UiBackdrop.Create(canvas.transform, new Color(0.34f, 0.45f, 0.68f), 0.42f, true, 0.5f);

            // Ortadaki sütun okunsun diye ortası koyu, iki yana açılan bant (lambanın altında: ışığı bandın üstünde)
            shade = UiBuilder.CreateImage(canvas.transform, "CenterShade", new Color(0.01f, 0.02f, 0.04f, centerShade));
            shade.sprite = ShadeSprite;
            if (backdrop) backdrop.InsertBelowLamp(shade.transform);
            UiBuilder.Stretch(shade.rectTransform, new Vector2(0.18f, 0f), new Vector2(0.82f, 1f));

            if (logoSprite)
            {
                logo = UiBuilder.CreateImage(canvas.transform, "Logo", Color.white);
                logo.sprite = logoSprite;
                logo.rectTransform.anchorMin = logo.rectTransform.anchorMax = new Vector2(0.5f, 0.76f);
                // Referans pikselinde kendi boyutu (ölçeklenmez)
                logo.rectTransform.sizeDelta = logoSprite.rect.size;
            }

            // Orta sütun: başlık (titrer) ve iki yanı süslü alt başlık
            titleGroup = CreateRect(canvas.transform, "TitleGroup");
            UiBuilder.Stretch(titleGroup, new Vector2(0.1f, 0.6f), new Vector2(0.9f, 0.86f));
            titleHome = titleGroup.anchoredPosition;
            var titleArea = CreateRect(titleGroup, "TitleArea");
            UiBuilder.Stretch(titleArea, new Vector2(0f, 0.32f), Vector2.one);
            titleText = UiTitle.Create(titleArea, "Title", font, titleFontSize, 3f);

            var subtitleArea = CreateRect(titleGroup, "SubtitleArea");
            UiBuilder.Stretch(subtitleArea, Vector2.zero, new Vector2(1f, 0.28f));
            subtitleText = UiBuilder.Outlined(UiBuilder.CreateText(subtitleArea, "Subtitle", font, subtitleFontSize, normalColor));
            UiBuilder.Stretch(subtitleText.rectTransform, Vector2.zero, Vector2.one);
            subtitleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            ornamentLeft = Ornament(subtitleArea, accent, -1f);
            ornamentRight = Ornament(subtitleArea, accent, 1f);

            // Satırlar: ortada, başlığın altında, panelsiz
            items = CreateRect(canvas.transform, "Items");
            UiBuilder.Stretch(items, new Vector2(0.3f, 0.04f), new Vector2(0.7f, 0.56f));
            menu = new MenuList(items, font, itemFontSize, rowHeight, normalColor, highlightColor, true)
            {
                UnderlineColor = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.9f),
            };

            // Metin alanı (uyarı, emeği geçenler, onay): panelsiz, ortada, üstte ve altta kısa çizgi
            bodyRoot = CreateRect(canvas.transform, "Body");
            foreach (var y in new[] { 0f, 1f })
            {
                var line = Line(bodyRoot, "Line", accent, new Vector2(140f, 2f), Vector2.zero);
                line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0.5f, y);
            }
            bodyText = UiBuilder.Outlined(UiBuilder.CreateText(bodyRoot, "Text", font, bodyFontSize, normalColor));
            UiBuilder.Stretch(bodyText.rectTransform, Vector2.zero, Vector2.one);
            bodyRoot.gameObject.SetActive(false);

            // Sol altta soluk jam adı, sağ altta tuş ipuçları
            footerText = UiBuilder.Outlined(UiBuilder.CreateText(canvas.transform, "Footer", font, 14,
                new Color(normalColor.r, normalColor.g, normalColor.b, 0.55f), TextAnchor.LowerLeft));
            footerText.rectTransform.anchorMin = footerText.rectTransform.anchorMax = Vector2.zero;
            footerText.rectTransform.pivot = Vector2.zero;
            footerText.rectTransform.sizeDelta = new Vector2(260f, 24f);
            footerText.rectTransform.anchoredPosition = new Vector2(14f, 10f);

            hintText = UiBuilder.Outlined(UiBuilder.CreateText(canvas.transform, "Hints", font, hintFontSize,
                new Color(normalColor.r, normalColor.g, normalColor.b, 0.85f), TextAnchor.LowerRight));
            hintText.rectTransform.anchorMin = hintText.rectTransform.anchorMax = new Vector2(1f, 0f);
            hintText.rectTransform.pivot = new Vector2(1f, 0f);
            hintText.rectTransform.sizeDelta = new Vector2(600f, 24f);
            hintText.rectTransform.anchoredPosition = new Vector2(-16f, 10f);
            hintText.supportRichText = true;
            hintText.horizontalOverflow = HorizontalWrapMode.Overflow;

            group.alpha = 0f;
        }

        // 64 x 1: ortası dolu, iki yana doğru saydamlaşan beyaz (rengi Image.color'dan; yumuşak)
        static Sprite ShadeSprite
        {
            get
            {
                if (shadeSprite) return shadeSprite;
                const int n = 64;
                var texture = new Texture2D(n, 1, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "MenuLeftShade",
                };
                for (int x = 0; x < n; x++)
                {
                    float k = Mathf.Abs((x + 0.5f) / n - 0.5f) * 2f; // 0 ortada, 1 kenarda
                    texture.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.3f, 1f, k))));

                }
                texture.Apply();
                shadeSprite = Sprite.Create(texture, new Rect(0, 0, n, 1), new Vector2(0.5f, 0.5f), 100f);
                return shadeSprite;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            warningShown = false;
            Visible = false;
        }
    }
}
