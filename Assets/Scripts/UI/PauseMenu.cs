using Deprem.Audio;
using Deprem.Core;
using Deprem.Localization;
using Deprem.Loop;
using Deprem.Minigames;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Esc ile duraklatma: Devam et / Dil / Ana menü. Oyun durur (timeScale 0), PauseChanged gönderilir.
    // Minigame açıkken Esc minigame'i kapatır; keşke ve final ekranlarında duraklatma yok.
    // Arayüz turu 2: kabartma başlık, altında vurgu renginde bölüm adı, tepede sallanan lamba, satır simgeleri.
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] int titleFontSize = 40;
        [SerializeField] int subtitleFontSize = 17;
        [SerializeField] int itemFontSize = 20;
        [SerializeField] float rowHeight = 34f;
        [SerializeField] Color normalColor = new Color(0.82f, 0.78f, 0.7f);
        [SerializeField] Color highlightColor = new Color(1f, 0.82f, 0.4f);
        [SerializeField] int sortingOrder = 115;

        CanvasGroup group;
        UiBackdrop backdrop;
        UiPromptRow footer;
        UiTitle titleText;
        Text subtitleText;
        MenuList menu;
        bool paused;
        int minigameClosedFrame = -1;

        void Awake() => BuildCanvas();

        void OnEnable()
        {
            GameEvents.MinigameClosed += HandleMinigameClosed;
            Texts.LanguageChanged += Refresh;
        }

        void OnDisable()
        {
            GameEvents.MinigameClosed -= HandleMinigameClosed;
            Texts.LanguageChanged -= Refresh;
            if (paused) SetPaused(false);
        }

        // Esc ile kapanan minigame aynı karede duraklatmayı açmasın
        void HandleMinigameClosed(ItemId id, bool success) => minigameClosedFrame = Time.frameCount;

        void Update()
        {
            if (paused)
            {
                // Ayarlar açıkken (ya da Esc ile bu karede kapandıysa) duraklatma tuşları işlemez
                if (SettingsMenu.IsOpen || SettingsMenu.ClosedFrame == Time.frameCount) return;
                if (UiInput.Back())
                {
                    Sfx.Play("ui.back");
                    SetPaused(false);
                }
                else menu.Tick();
                return;
            }

            // M38 (donma: oyuncu ve süre birlikte durmuştu): oyun sürerken hiçbir menü açık değilse zaman durmuş olamaz.
            // timeScale 0'da kalmışsa (bir menü geçişinde açık kalmış) düzelt ve Console'a yaz.
            if (Time.timeScale == 0f && !MainMenu.Visible && !SettingsMenu.IsOpen && CanPause())
            {
                Debug.LogWarning($"[PauseMenu] Menü kapalıyken timeScale 0'da kalmış (faz {GameState.Phase}); 1'e alındı.");
                Time.timeScale = 1f;
            }

            if (UiInput.Back() && CanPause())
            {
                Sfx.Play("ui.click");
                SetPaused(true);
            }
        }

        bool CanPause()
        {
            if (minigameClosedFrame == Time.frameCount) return false;
            if (MinigameHost.Instance && MinigameHost.Instance.IsOpen) return false;
            var phase = GameState.Phase;
            return phase != GamePhase.None && phase != GamePhase.Failed && phase != GamePhase.Won;
        }

        void SetPaused(bool value)
        {
            paused = value;
            group.alpha = value ? 1f : 0f;
            Time.timeScale = value ? 0f : 1f;

            menu.Clear();
            if (value)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                backdrop.Capture();
                menu.Add("menu.resume", () => SetPaused(false));
                menu.SetIcon(UiIcons.Play);
                menu.Add("menu.settings", () => { if (SettingsMenu.Instance) SettingsMenu.Instance.Open(Refresh); });
                menu.SetIcon(UiIcons.Gear);
                menu.Add("menu.language", () => Texts.ToggleLanguage());
                menu.SetIcon(UiIcons.Globe);
                menu.Add("menu.main_menu", ReturnToMenu);
                menu.SetIcon(UiIcons.House);
                Refresh();
            }
            GameEvents.RaisePauseChanged(value);
        }

        void ReturnToMenu()
        {
            paused = false;
            if (LoopManager.Instance) LoopManager.Instance.ReturnToMenu();
        }

        void Refresh()
        {
            titleText.Text = Texts.Get("menu.paused");
            subtitleText.text = Texts.Format("menu.chapter", GameState.CurrentLoop);
            footer.Set(Texts.Get("menu.hint.select"), Texts.Get("menu.hint.confirm"), Texts.Get("menu.hint.back"));
            menu.Refresh();
        }

        void BuildCanvas()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "PauseCanvas", sortingOrder);
            group = canvas.GetComponent<CanvasGroup>();

            // Duraklatma: arkada oyunun bulanık soğuk mavi görüntüsü (UiBackdrop, toz yok)
            backdrop = UiBackdrop.Create(canvas.transform, new Color(0.34f, 0.45f, 0.68f), 0.55f, false, 0.5f);

            var titleArea = new GameObject("TitleArea", typeof(RectTransform)).GetComponent<RectTransform>();
            titleArea.SetParent(canvas.transform, false);
            UiBuilder.Stretch(titleArea, new Vector2(0.2f, 0.7f), new Vector2(0.8f, 0.82f));
            titleText = UiTitle.Create(titleArea, "Title", font, titleFontSize, 2f);
            subtitleText = UiBuilder.Outlined(UiBuilder.CreateText(canvas.transform, "Subtitle", font, subtitleFontSize, highlightColor));
            UiBuilder.Stretch(subtitleText.rectTransform, new Vector2(0.2f, 0.64f), new Vector2(0.8f, 0.7f));

            var items = new GameObject("Items", typeof(RectTransform)).GetComponent<RectTransform>();
            items.SetParent(canvas.transform, false);
            UiBuilder.Stretch(items, new Vector2(0.35f, 0.2f), new Vector2(0.65f, 0.6f));
            footer = UiPromptRow.Create(canvas.transform, font, 18, 16f);
            menu = new MenuList(items, font, itemFontSize, rowHeight, normalColor, highlightColor);

            group.alpha = 0f;
        }
    }
}
