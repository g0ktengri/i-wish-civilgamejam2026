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
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] int titleFontSize = 26;
        [SerializeField] int itemFontSize = 18;
        [SerializeField] float rowHeight = 30f;
        [SerializeField] Color normalColor = new Color(0.7f, 0.72f, 0.78f);
        [SerializeField] Color highlightColor = Color.white;
        [SerializeField] Color overlayColor = new Color(0f, 0f, 0f, 0.7f);
        [SerializeField] int sortingOrder = 115;

        CanvasGroup group;
        Text titleText;
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
                menu.Add("menu.resume", () => SetPaused(false));
                menu.Add("menu.settings", () => { if (SettingsMenu.Instance) SettingsMenu.Instance.Open(Refresh); });
                menu.Add("menu.language", () => Texts.ToggleLanguage());
                menu.Add("menu.main_menu", ReturnToMenu);
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
            titleText.text = Texts.Get("menu.paused");
            menu.Refresh();
        }

        void BuildCanvas()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "PauseCanvas", sortingOrder);
            group = canvas.GetComponent<CanvasGroup>();

            var overlay = UiBuilder.CreateImage(canvas.transform, "Overlay", overlayColor);
            UiBuilder.Stretch(overlay.rectTransform, Vector2.zero, Vector2.one);

            titleText = UiBuilder.CreateText(canvas.transform, "Title", font, titleFontSize, highlightColor);
            UiBuilder.Stretch(titleText.rectTransform, new Vector2(0.2f, 0.62f), new Vector2(0.8f, 0.74f));

            var items = new GameObject("Items", typeof(RectTransform)).GetComponent<RectTransform>();
            items.SetParent(canvas.transform, false);
            UiBuilder.Stretch(items, new Vector2(0.3f, 0.2f), new Vector2(0.7f, 0.58f));
            menu = new MenuList(items, font, itemFontSize, rowHeight, normalColor, highlightColor);

            group.alpha = 0f;
        }
    }
}
