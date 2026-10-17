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
    // Arayüz turu: arkada evin bulanık, soğuk mavi görüntüsü ve düşen toz (UiBackdrop); konturlu başlık, altında
    // ince kırmızı çatlak, ara ara titrer; satırlar pixel panelde (MenuList); altta tuş kapaklı ipuçları (UiPromptRow).
    // Arayüz turu 2 (çok sade duruyordu): tepede sallanan lamba ve sıcak ışık konisi, kabartma başlık (UiTitle) ve
    // harfleri yaran kırmızı çatlak, süslü alt başlık, satır simgeleri, duvar köşelerinde çatlaklar, sol altta
    // jam adı; her titremede arka plan da sarsılır (moloz, toz), her üçüncüsü daha güçlü.
    public class MainMenu : MonoBehaviour
    {
        enum Screen { Hidden, Warning, Main, ConfirmNew, Controls, Credits }

        static bool warningShown;

        [SerializeField] Font font;
        [SerializeField] int titleFontSize = 64;
        [SerializeField] int subtitleFontSize = 16;
        [SerializeField] int itemFontSize = 20;
        [SerializeField] int bodyFontSize = 17;
        [SerializeField] float rowHeight = 32f;
        [SerializeField] Color normalColor = new Color(0.82f, 0.78f, 0.7f);
        [SerializeField] Color highlightColor = new Color(1f, 0.82f, 0.4f);
        [SerializeField] Color crackColor = new Color(0.9f, 0.22f, 0.16f);
        [Tooltip("Başlık bu aralıkla (sn) kısa bir an titrer (deprem)")]
        [SerializeField] float tremorInterval = 5f;
        [SerializeField] Color backgroundColor = new Color(0.02f, 0.03f, 0.06f, 1f);
        [SerializeField] int sortingOrder = 120;

        [Header("Görseller (Assets/Art/UI, prefab kurulurken bağlanır)")]
        [Tooltip("menu_arka_plan.png, 960 x 540: menü arka planı")]
        [SerializeField] Sprite backgroundSprite;
        [Tooltip("logo.png: oyunun adı; varsa yazı yerine gösterilir (kendi boyutunda)")]
        [SerializeField] Sprite logoSprite;

        CanvasGroup group;
        UiBackdrop backdrop;
        RectTransform titleGroup;
        Vector2 titleHome;
        Image bodyPanel;
        UiPromptRow footer;
        UiTitle titleText;
        Text subtitleText;
        Image logo;
        Text bodyText;
        Text footerText;
        RectTransform items;
        MenuList menu;
        Screen screen = Screen.Hidden;
        bool musicPlaying;

        // M38: ana menü ekranda mı (duraklatmanın zaman bekçisi için)
        public static bool Visible { get; private set; }
        int tremorCycle = -1;

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

            // Alt ekranlarda metin ortada, "Geri" altta
            bool subScreen = next == Screen.Controls || next == Screen.Credits || next == Screen.ConfirmNew;
            UiBuilder.Stretch(items, new Vector2(0.33f, subScreen ? 0.06f : 0.08f),
                new Vector2(0.67f, subScreen ? (next == Screen.ConfirmNew ? 0.26f : 0.2f) : 0.64f));

            menu.Clear();
            switch (next)
            {
                case Screen.Main:
                    // M42: "Bölüm seç" yerine kayıtlı döngüden "Devam et" (kayıt yoksa gri, seçilemez); yeni oyun kaydı
                    // silmeden önce sorar
                    menu.Add("menu.continue", () => StartLoop(SaveGame.Loop), SaveGame.Loop);
                    menu.SetIcon(UiIcons.Play);
                    menu.SetEnabled(SaveGame.Exists);
                    menu.Add("menu.new_game", () =>
                    {
                        if (SaveGame.Exists) Show(Screen.ConfirmNew);
                        else StartNewGame();
                    });
                    menu.SetIcon(UiIcons.House);
                    menu.Add("menu.settings", OpenSettings);
                    menu.SetIcon(UiIcons.Gear);
                    menu.Add("menu.controls", () => { if (SettingsMenu.Instance) SettingsMenu.Instance.OpenControls(Refresh); });
                    menu.SetIcon(UiIcons.Key);
                    menu.Add("menu.credits", () => Show(Screen.Credits));
                    menu.SetIcon(UiIcons.Heart);
                    menu.Add("menu.language", () => Texts.ToggleLanguage());
                    menu.SetIcon(UiIcons.Globe);
                    menu.Add("menu.quit", Quit);
                    menu.SetIcon(UiIcons.Door);
                    break;
                case Screen.ConfirmNew:
                    menu.Add("menu.new_confirm_yes", StartNewGame);
                    menu.SetIcon(UiIcons.House);
                    menu.Add("menu.cancel", () => Show(Screen.Main));
                    menu.SetIcon(UiIcons.Back);
                    break;
                case Screen.Controls:
                case Screen.Credits:
                    menu.Add("menu.back", () => Show(Screen.Main));
                    menu.SetIcon(UiIcons.Back);
                    break;
            }
            Refresh();
        }

        void Refresh()
        {
            bool showTitle = screen != Screen.Warning;
            titleText.Text = showTitle && !logoSprite ? Texts.Get("game.title") : string.Empty;
            subtitleText.text = Texts.Get("menu.subtitle");
            if (logo) logo.enabled = showTitle;
            if (titleGroup) titleGroup.gameObject.SetActive(showTitle);
            switch (screen)
            {
                case Screen.Warning:
                    bodyText.text = Texts.Get("content_warning");
                    footer.Set(Texts.Get("prompt.continue"));
                    break;
                case Screen.Controls:
                    bodyText.text = Texts.Get("controls.body");
                    footer.Set(Texts.Get("menu.hint.confirm"), Texts.Get("menu.hint.back"));
                    break;
                case Screen.Credits:
                    bodyText.text = Texts.Get("credits.body");
                    footer.Set(Texts.Get("menu.hint.confirm"), Texts.Get("menu.hint.back"));
                    break;
                case Screen.Main:
                    bodyText.text = string.Empty;
                    footer.Set(Texts.Get("menu.hint.select"), Texts.Get("menu.hint.confirm"));
                    break;
                case Screen.ConfirmNew:
                    bodyText.text = Texts.Get("menu.new_confirm");
                    footer.Set(Texts.Get("menu.hint.select"), Texts.Get("menu.hint.confirm"), Texts.Get("menu.hint.back"));
                    break;
                default:
                    bodyText.text = string.Empty;
                    footer.Set(Texts.Get("menu.hint.select"), Texts.Get("menu.hint.confirm"), Texts.Get("menu.hint.back"));
                    break;
            }
            // Uyarı, kontroller ve emeği geçenler metni panelin içinde
            bodyPanel.gameObject.SetActive(!string.IsNullOrEmpty(bodyText.text));
            footerText.text = Texts.Get("menu.jam");
            PlaceOrnaments();
            menu.Refresh();
        }

        void PlaceOrnaments()
        {
            float half = subtitleText.preferredWidth * 0.5f + 12f;
            var area = subtitleText.rectTransform.parent;
            ((RectTransform)area.Find("OrnamentLeft")).anchoredPosition = new Vector2(-half, 0f);
            ((RectTransform)area.Find("OrnamentRight")).anchoredPosition = new Vector2(half, 0f);
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

        // Alt başlığın iki yanındaki süs: ince çizgi ve ucunda küçük pixel elmas
        static void Ornament(RectTransform parent, Color color, bool left)
        {
            var root = new GameObject(left ? "OrnamentLeft" : "OrnamentRight", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;
            float side = left ? -1f : 1f;
            var line = UiBuilder.CreateImage(root, "Line", color);
            line.rectTransform.sizeDelta = new Vector2(46f, 2f);
            line.rectTransform.anchoredPosition = new Vector2(side * 23f, 0f);
            line.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1f, -1f);
            var diamond = UiBuilder.CreateImage(root, "Diamond", color);
            diamond.rectTransform.sizeDelta = new Vector2(6f, 6f);
            diamond.rectTransform.anchoredPosition = new Vector2(side * 49f, 0f);
            diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            diamond.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1f, -1f);
            // Alt başlık yazısı ölçülünce yanlara itilir (Refresh)
            root.name = left ? "OrnamentLeft" : "OrnamentRight";
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

        void BuildCanvas()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "MainMenuCanvas", sortingOrder);
            group = canvas.GetComponent<CanvasGroup>();

            // Arka plan: sanatçı görseli varsa o; yoksa evin bulanık soğuk mavi görüntüsü ve düşen toz
            if (backgroundSprite)
            {
                var background = UiBuilder.CreateImage(canvas.transform, "Background", Color.white);
                background.sprite = backgroundSprite;
                UiBuilder.Stretch(background.rectTransform, Vector2.zero, Vector2.one);
            }
            else backdrop = UiBackdrop.Create(canvas.transform, new Color(0.34f, 0.45f, 0.68f), 0.5f, true, 0.5f);

            // Duvar köşelerinde soğuk renkli ince çatlaklar (sol üst, sağ alt)
            var wallCrack = new Color(0.62f, 0.72f, 0.9f, 0.22f);
            foreach (var (anchor, direction, seed) in new[]
            {
                (new Vector2(0f, 1f), new Vector2(1f, -0.55f), 3),
                (new Vector2(0f, 1f), new Vector2(0.4f, -1f), 5),
                (new Vector2(1f, 0f), new Vector2(-1f, 0.45f), 11),
                (new Vector2(1f, 0f), new Vector2(-0.5f, 1f), 13),
            })
            {
                var corner = new GameObject("WallCrack", typeof(RectTransform)).GetComponent<RectTransform>();
                corner.SetParent(canvas.transform, false);
                corner.anchorMin = corner.anchorMax = anchor;
                corner.sizeDelta = Vector2.zero;
                UiBuilder.CreateCrack(corner, Vector2.zero, direction, seed % 2 == 1 && seed > 4 ? 90f : 150f, seed, wallCrack, 2f, 0f, 12f, 4f);
            }

            if (logoSprite)
            {
                logo = UiBuilder.CreateImage(canvas.transform, "Logo", Color.white);
                logo.sprite = logoSprite;
                logo.rectTransform.anchorMin = logo.rectTransform.anchorMax = new Vector2(0.5f, 0.83f);
                // Referans pikselinde kendi boyutu (ölçeklenmez)
                logo.rectTransform.sizeDelta = logoSprite.rect.size;
            }

            // Başlık ve altındaki çatlak birlikte titrer
            titleGroup = new GameObject("TitleGroup", typeof(RectTransform)).GetComponent<RectTransform>();
            titleGroup.SetParent(canvas.transform, false);
            UiBuilder.Stretch(titleGroup, new Vector2(0.1f, 0.68f), new Vector2(0.9f, 0.88f));
            titleHome = titleGroup.anchoredPosition;
            var titleArea = new GameObject("TitleArea", typeof(RectTransform)).GetComponent<RectTransform>();
            titleArea.SetParent(titleGroup, false);
            UiBuilder.Stretch(titleArea, new Vector2(0f, 0.35f), Vector2.one);
            titleText = UiTitle.Create(titleArea, "Title", font, titleFontSize, 3f);
            // Kırmızı çatlak (keşke ekranındaki çatlağın küçüğü)
            var crack = new GameObject("TitleCrack", typeof(RectTransform)).GetComponent<RectTransform>();
            crack.SetParent(titleArea, false);
            crack.anchorMin = crack.anchorMax = new Vector2(0.5f, 0.5f);
            crack.sizeDelta = Vector2.zero;
            // Harfleri yukarıdan aşağı yaran ana çatlak ve iki kısa kolu
            UiBuilder.CreateCrack(crack, new Vector2(-6f, 30f), new Vector2(0.28f, -1f), 70f, 7, crackColor, 2f, 0.25f, 9f, 4f);
            UiBuilder.CreateCrack(crack, new Vector2(2f, 12f), new Vector2(-1f, -0.5f), 34f, 9, crackColor, 2f, 0.25f, 8f, 3f);
            UiBuilder.CreateCrack(crack, new Vector2(10f, -14f), new Vector2(1f, -0.35f), 30f, 12, crackColor, 2f, 0.25f, 8f, 3f);

            var subtitleArea = new GameObject("SubtitleArea", typeof(RectTransform)).GetComponent<RectTransform>();
            subtitleArea.SetParent(titleGroup, false);
            UiBuilder.Stretch(subtitleArea, Vector2.zero, new Vector2(1f, 0.3f));
            subtitleText = UiBuilder.Outlined(UiBuilder.CreateText(subtitleArea, "Subtitle", font, subtitleFontSize, normalColor));
            UiBuilder.Stretch(subtitleText.rectTransform, Vector2.zero, Vector2.one);
            var ornamentColor = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.85f);
            Ornament(subtitleArea, ornamentColor, true);
            Ornament(subtitleArea, ornamentColor, false);

            bodyPanel = UiBuilder.CreatePanel(canvas.transform, "BodyPanel");
            UiBuilder.Stretch(bodyPanel.rectTransform, new Vector2(0.14f, 0.24f), new Vector2(0.86f, 0.7f));
            bodyPanel.gameObject.SetActive(false);
            bodyText = UiBuilder.Outlined(UiBuilder.CreateText(canvas.transform, "Body", font, bodyFontSize, normalColor));
            UiBuilder.Stretch(bodyText.rectTransform, new Vector2(0.16f, 0.26f), new Vector2(0.84f, 0.68f));

            items = new GameObject("Items", typeof(RectTransform)).GetComponent<RectTransform>();
            items.SetParent(canvas.transform, false);
            UiBuilder.Stretch(items, new Vector2(0.3f, 0.08f), new Vector2(0.7f, 0.66f));
            menu = new MenuList(items, font, itemFontSize, rowHeight, normalColor, highlightColor);

            // Sol altta soluk jam adı
            footerText = UiBuilder.Outlined(UiBuilder.CreateText(canvas.transform, "Footer", font, 14,
                new Color(normalColor.r, normalColor.g, normalColor.b, 0.55f), TextAnchor.LowerLeft));
            footerText.rectTransform.anchorMin = footerText.rectTransform.anchorMax = Vector2.zero;
            footerText.rectTransform.pivot = Vector2.zero;
            footerText.rectTransform.sizeDelta = new Vector2(260f, 24f);
            footerText.rectTransform.anchoredPosition = new Vector2(14f, 10f);
            footer = UiPromptRow.Create(canvas.transform, font, 18, 16f);

            group.alpha = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            warningShown = false;
            Visible = false;
        }
    }
}
