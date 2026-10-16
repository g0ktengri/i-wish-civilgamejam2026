using Deprem.Core;
using Deprem.Localization;
using Deprem.Loop;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Ana menü: ilk açılışta içerik uyarısı, sonra Yeni oyun / Bölüm seç / Kontroller /
    // Emeği geçenler / Dil / Çıkış. Menü açıkken oyun durur (timeScale 0).
    // Deneme başlatmak için sahne yeniden yüklendiğinde menü gizli kalır. Canvas'ı kendisi kurar.
    public class MainMenu : MonoBehaviour
    {
        enum Screen { Hidden, Warning, Main, Chapters, Controls, Credits }

        static bool warningShown;

        [SerializeField] Font font;
        [SerializeField] int titleFontSize = 40;
        [SerializeField] int itemFontSize = 18;
        [SerializeField] int bodyFontSize = 14;
        [SerializeField] float rowHeight = 30f;
        [SerializeField] Color normalColor = new Color(0.7f, 0.72f, 0.78f);
        [SerializeField] Color highlightColor = Color.white;
        [SerializeField] Color backgroundColor = new Color(0.02f, 0.03f, 0.06f, 1f);
        [SerializeField] int sortingOrder = 120;

        CanvasGroup group;
        Text titleText;
        Text bodyText;
        Text footerText;
        RectTransform items;
        MenuList menu;
        Screen screen = Screen.Hidden;

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
        }

        void Start()
        {
            if (LoopManager.HasPendingLoop || GameState.Phase != GamePhase.None) Show(Screen.Hidden);
            else Show(warningShown ? Screen.Main : Screen.Warning);
        }

        void HandleLoopStarted(int loop) => Show(Screen.Hidden);

        void Update()
        {
            switch (screen)
            {
                case Screen.Hidden:
                    return;
                case Screen.Warning:
                    if (UiInput.ConfirmOrClick())
                    {
                        warningShown = true;
                        Show(Screen.Main);
                    }
                    return;
                case Screen.Main:
                    menu.Tick();
                    return;
                default:
                    if (UiInput.Back()) Show(Screen.Main);
                    else menu.Tick();
                    return;
            }
        }

        void Show(Screen next)
        {
            screen = next;
            bool visible = next != Screen.Hidden;
            group.alpha = visible ? 1f : 0f;
            Time.timeScale = visible ? 0f : 1f;
            if (visible)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            // Alt ekranlarda metin ortada, "Geri" altta
            bool subScreen = next == Screen.Controls || next == Screen.Credits;
            UiBuilder.Stretch(items, new Vector2(0.3f, subScreen ? 0.06f : 0.08f),
                new Vector2(0.7f, subScreen ? 0.18f : 0.66f));

            menu.Clear();
            switch (next)
            {
                case Screen.Main:
                    menu.Add("menu.new_game", () => StartLoop(1));
                    menu.Add("menu.chapter_select", () => Show(Screen.Chapters));
                    menu.Add("menu.controls", () => Show(Screen.Controls));
                    menu.Add("menu.credits", () => Show(Screen.Credits));
                    menu.Add("menu.language", () => Texts.ToggleLanguage());
                    menu.Add("menu.quit", Quit);
                    break;
                case Screen.Chapters:
                    for (int i = 1; i <= GameConstants.LoopCount; i++)
                    {
                        int loop = i;
                        menu.Add("menu.chapter", () => StartLoop(loop), loop);
                    }
                    menu.Add("menu.back", () => Show(Screen.Main));
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
            titleText.text = screen == Screen.Warning ? string.Empty : Texts.Get("game.title");
            switch (screen)
            {
                case Screen.Warning:
                    bodyText.text = Texts.Get("content_warning");
                    footerText.text = Texts.Get("prompt.continue");
                    break;
                case Screen.Controls:
                    bodyText.text = Texts.Get("controls.body");
                    footerText.text = string.Empty;
                    break;
                case Screen.Credits:
                    bodyText.text = Texts.Get("credits.body");
                    footerText.text = string.Empty;
                    break;
                default:
                    bodyText.text = string.Empty;
                    footerText.text = screen == Screen.Main ? Texts.Get("controls.short") : string.Empty;
                    break;
            }
            menu.Refresh();
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

            var background = UiBuilder.CreateImage(canvas.transform, "Background", backgroundColor);
            UiBuilder.Stretch(background.rectTransform, Vector2.zero, Vector2.one);

            titleText = UiBuilder.CreateText(canvas.transform, "Title", font, titleFontSize, highlightColor);
            UiBuilder.Stretch(titleText.rectTransform, new Vector2(0.1f, 0.74f), new Vector2(0.9f, 0.92f));

            bodyText = UiBuilder.CreateText(canvas.transform, "Body", font, bodyFontSize, normalColor);
            UiBuilder.Stretch(bodyText.rectTransform, new Vector2(0.15f, 0.22f), new Vector2(0.85f, 0.74f));

            items = new GameObject("Items", typeof(RectTransform)).GetComponent<RectTransform>();
            items.SetParent(canvas.transform, false);
            UiBuilder.Stretch(items, new Vector2(0.3f, 0.08f), new Vector2(0.7f, 0.66f));
            menu = new MenuList(items, font, itemFontSize, rowHeight, normalColor, highlightColor);

            footerText = UiBuilder.CreateText(canvas.transform, "Footer", font, bodyFontSize - 2, normalColor);
            UiBuilder.Stretch(footerText.rectTransform, new Vector2(0.05f, 0.01f), new Vector2(0.95f, 0.07f));

            group.alpha = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => warningShown = false;
    }
}
