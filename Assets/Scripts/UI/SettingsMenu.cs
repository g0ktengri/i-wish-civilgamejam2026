using System;
using System.Collections.Generic;
using System.Linq;
using Deprem.Audio;
using Deprem.Localization;
using Deprem.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Ayarlar kaydı (M32): ses düzeyleri ve ekran sarsıntısı PlayerPrefs'te; oyun açılınca yüklenip Mehmet'in
    // ses sistemine (Deprem.Audio.Sfx) ve kamera sarsıntısına (Deprem.Quake.QuakeCameraShake) verilir.
    // Tam ekran ve çözünürlüğü Unity kendisi hatırlar; dili Texts saklar.
    public static class SettingsStore
    {
        const string MasterKey = "settings.master";
        const string MusicKey = "settings.music";
        const string EffectsKey = "settings.effects";
        const string ShakeKey = "settings.shake";

        public static float Master { get => Sfx.MasterVolume; set { Sfx.MasterVolume = Clamp(value); Save(); } }
        public static float Music { get => Sfx.MusicVolume; set { Sfx.MusicVolume = Clamp(value); Save(); } }
        public static float Effects { get => Sfx.EffectsVolume; set { Sfx.EffectsVolume = Clamp(value); Save(); } }
        public static float Shake { get => Quake.QuakeCameraShake.Strength; set { Quake.QuakeCameraShake.Strength = Clamp(value); Save(); } }

        static float Clamp(float value) => Mathf.Round(Mathf.Clamp01(value) * 10f) / 10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Load()
        {
            Sfx.MasterVolume = PlayerPrefs.GetFloat(MasterKey, 1f);
            Sfx.MusicVolume = PlayerPrefs.GetFloat(MusicKey, 1f);
            Sfx.EffectsVolume = PlayerPrefs.GetFloat(EffectsKey, 1f);
            Quake.QuakeCameraShake.Strength = PlayerPrefs.GetFloat(ShakeKey, 1f);
        }

        static void Save()
        {
            PlayerPrefs.SetFloat(MasterKey, Sfx.MasterVolume);
            PlayerPrefs.SetFloat(MusicKey, Sfx.MusicVolume);
            PlayerPrefs.SetFloat(EffectsKey, Sfx.EffectsVolume);
            PlayerPrefs.SetFloat(ShakeKey, Quake.QuakeCameraShake.Strength);
            PlayerPrefs.Save();
        }
    }

    // Ayarlar menüsü (M32, Mehmet'in play testi 20): ana menüden ve duraklatmadan açılır.
    // W/S seç, A/D (ya da oklar) değiştir, F/Enter/tık değiştirir ya da açar, Esc geri.
    // Ses (genel, müzik, efekt), ekran sarsıntısı, tam ekran, çözünürlük, dil, kontroller. Canvas'ı kendisi kurar.
    // Arayüz turu 2: kabartma başlık, satır simgeleri, ses ve sarsıntı için 10 dilimli pixel çubuk, değerler sağda.
    // M40: Kontroller = tuş değiştirme (Deprem.Player.KeyBindings): satıra F / tık, sonra yeni tuşa bas; Esc vazgeçer;
    // "Varsayılana dön". Ana menüdeki "Kontroller" doğrudan bu ekranı açar (OpenControls).
    public class SettingsMenu : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] int titleFontSize = 34;
        [SerializeField] int itemFontSize = 19;
        [SerializeField] int bodyFontSize = 17;
        [SerializeField] float rowHeight = 30f;
        [SerializeField] Color normalColor = new Color(0.82f, 0.78f, 0.7f);
        [SerializeField] Color highlightColor = new Color(1f, 0.82f, 0.4f);
        [Tooltip("Arkadaki ana menü / duraklatma görünsün diye yarı saydam")]
        [SerializeField] Color backgroundColor = new Color(0.01f, 0.02f, 0.05f, 0.8f);
        [Tooltip("Ana menünün ve duraklatmanın üstünde")]
        [SerializeField] int sortingOrder = 125;

        public static SettingsMenu Instance { get; private set; }
        public static bool IsOpen => Instance && Instance.open;
        // Esc ile kapandığı karede duraklatma / ana menü aynı tuşu işlemesin
        public static int ClosedFrame { get; private set; } = -1;

        CanvasGroup group;
        UiTitle titleText;
        Text bodyText;
        UiPromptRow footer;
        Image bodyPanel;
        RectTransform items;
        MenuList menu;
        bool open;
        bool showingControls;
        bool controlsOnly;
        string listening;
        int listenFrame;
        float resetFeedbackUntil;
        const float ResetFeedbackSeconds = 1.6f;
        Action onClose;

        // M40: değiştirilebilir eylemler (KeyBindings adları) ve satır metinleri
        static readonly (string action, string key)[] Bindings =
        {
            ("Left", "controls.bind.left"), ("Right", "controls.bind.right"), ("Jump", "controls.bind.jump"),
            ("Crouch", "controls.bind.crouch"), ("Interact", "controls.bind.interact"),
            ("SpiritUp", "controls.bind.spirit_up"), ("SpiritDown", "controls.bind.spirit_down"),
            ("SpiritLeft", "controls.bind.spirit_left"), ("SpiritRight", "controls.bind.spirit_right"),
        };
        List<Vector2Int> resolutions;

        void Awake()
        {
            Instance = this;
            BuildCanvas();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable()
        {
            Texts.LanguageChanged += Refresh;
            KeyBindings.Changed += HandleBindingsChanged;
        }

        void OnDisable()
        {
            Texts.LanguageChanged -= Refresh;
            KeyBindings.Changed -= HandleBindingsChanged;
        }

        public void Open(Action closed)
        {
            onClose = closed;
            open = true;
            showingControls = false;
            controlsOnly = false;
            listening = null;
            group.alpha = 1f;
            group.blocksRaycasts = true;
            Rebuild();
        }

        /// <summary>M40: doğrudan tuş değiştirme ekranı (ana menüdeki "Kontroller"); Geri ayarları değil menüyü açar</summary>
        public void OpenControls(Action closed)
        {
            Open(closed);
            showingControls = true;
            controlsOnly = true;
            Rebuild();
        }

        void Close()
        {
            open = false;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            ClosedFrame = Time.frameCount;
            menu.Clear();
            var callback = onClose;
            onClose = null;
            callback?.Invoke();
        }

        void Update()
        {
            if (!open) return;
            if (listening != null)
            {
                ListenForKey();
                return;
            }
            if (UiInput.Back())
            {
                Audio.Sfx.Play("ui.back");
                LeaveControls();
                return;
            }
            // "Geri geldi" yazısının süresi dolunca satırı temizle
            if (resetFeedbackUntil > 0f && Time.unscaledTime >= resetFeedbackUntil)
            {
                resetFeedbackUntil = 0f;
                menu.Refresh();
            }
            menu.Tick();
        }

        void LeaveControls()
        {
            if (showingControls && !controlsOnly)
            {
                showingControls = false;
                Rebuild();
            }
            else Close();
        }

        // M40: satır seçilince "Bir tuşa bas…"; basılan ilk tuş (Esc hariç) o eyleme atanır, Esc vazgeçer.
        // Satırı açan tuş (F / Enter / tık) aynı karede sayılmaz.
        void StartListening(string action)
        {
            listening = action;
            listenFrame = Time.frameCount;
            Refresh();
        }

        void ListenForKey()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || Time.frameCount == listenFrame) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Audio.Sfx.Play("ui.back");
                listening = null;
                Refresh();
                return;
            }
            foreach (var control in keyboard.allKeys)
            {
                if (control == null || !control.wasPressedThisFrame || control.keyCode == Key.None) continue;
                KeyBindings.Set(listening, control.keyCode);
                Audio.Sfx.Play("ui.click");
                listening = null;
                Refresh();
                return;
            }
        }

        void ResetBindings()
        {
            resetFeedbackUntil = Time.unscaledTime + ResetFeedbackSeconds;
            try
            {
                KeyBindings.ResetDefaults();
            }
            finally
            {
                // Başka bir Changed abonesi hata verse de liste yenilensin
                Refresh();
            }
        }

        void HandleBindingsChanged()
        {
            if (open && showingControls) menu.Refresh();
        }

        void Rebuild()
        {
            menu.Clear();
            if (showingControls)
            {
                foreach (var (action, key) in Bindings)
                {
                    // M44: ruh tuşları ayrı başlık altında, sade adlarla (Yukarı / Aşağı / Sola / Sağa)
                    if (action == "SpiritUp") menu.AddHeader("controls.header.spirit");
                    menu.AddValue(key, () => new object[]
                    {
                        listening == action ? Texts.Get("controls.press") : KeyBindings.Label(KeyBindings.Get(action)),
                    }, () => StartListening(action));
                    menu.SetIcon(action.StartsWith("Spirit") ? UiIcons.Heart : UiIcons.Key);
                }
                // M43: tuşlar zaten varsayılansa ekranda bir şey değişmiyordu, çalışmıyor sanılıyordu: satırın sağında
                // kısa süre "Geri geldi" yazar. Liste KeyBindings.Changed ile de yenilenir.
                menu.AddValue("controls.reset", () => new object[]
                {
                    Time.unscaledTime < resetFeedbackUntil ? Texts.Get("controls.reset_done") : string.Empty,
                }, ResetBindings);
                menu.SetIcon(UiIcons.Gear);
                menu.Add("menu.back", LeaveControls);
                menu.SetIcon(UiIcons.Back);
                Refresh();
                return;
            }
            AddVolume("settings.master", () => SettingsStore.Master, v => SettingsStore.Master = v, UiIcons.Speaker);
            AddVolume("settings.music", () => SettingsStore.Music, v => SettingsStore.Music = v, UiIcons.Music);
            AddVolume("settings.effects", () => SettingsStore.Effects, v => SettingsStore.Effects = v, UiIcons.Speaker);
            AddVolume("settings.shake", () => SettingsStore.Shake, v => SettingsStore.Shake = v, UiIcons.Shake);
            menu.AddAdjustable("settings.fullscreen", () => new object[] { Texts.Get(Screen.fullScreen ? "settings.on" : "settings.off") },
                _ => Screen.fullScreen = !Screen.fullScreen);
            menu.SetIcon(UiIcons.Screen);
            menu.AddAdjustable("settings.resolution", () => new object[] { ResolutionLabel() }, StepResolution);
            menu.SetIcon(UiIcons.Screen);
            menu.AddAdjustable("menu.language", () => null, _ => Texts.ToggleLanguage());
            menu.SetIcon(UiIcons.Globe);
            menu.Add("menu.controls", () => { showingControls = true; Rebuild(); });
            menu.SetIcon(UiIcons.Key);
            menu.Add("menu.back", Close);
            menu.SetIcon(UiIcons.Back);
            Refresh();
        }

        // %10 adımlarla (M39: %0 ve %100'de durur; çubuğa tıklamak o kareye ayarlar)
        void AddVolume(string key, Func<float> get, Action<float> set, Sprite icon)
        {
            menu.AddSlider(key, get, set);
            menu.SetIcon(icon);
        }

        // Ekranın desteklediği çözünürlükler (tekrarsız en x boy), mevcut olanın sırası
        List<Vector2Int> Resolutions()
        {
            if (resolutions != null) return resolutions;
            resolutions = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct()
                .OrderBy(r => r.x * r.y).ToList();
            if (resolutions.Count == 0) resolutions.Add(new Vector2Int(Screen.width, Screen.height));
            return resolutions;
        }

        string ResolutionLabel() => $"{Screen.width} x {Screen.height}";

        void StepResolution(int direction)
        {
            var list = Resolutions();
            int current = list.FindIndex(r => r.x == Screen.width && r.y == Screen.height);
            if (current < 0) current = list.Count - 1;
            var next = list[(current + (direction < 0 ? -1 : 1) + list.Count) % list.Count];
            Screen.SetResolution(next.x, next.y, Screen.fullScreenMode);
        }

        void Refresh()
        {
            if (!titleText) return;
            titleText.Text = Texts.Get(showingControls ? "menu.controls" : "menu.settings");
            bodyText.text = string.Empty;
            bodyPanel.gameObject.SetActive(false);
            if (listening != null) footer.Set(Texts.Get("menu.hint.cancel"));
            else if (showingControls) footer.Set(Texts.Get("menu.hint.select"), Texts.Get("controls.hint.change"), Texts.Get("menu.hint.back"));
            else footer.Set(Texts.Get("menu.hint.select"), Texts.Get("menu.hint.change"), Texts.Get("menu.hint.back"));
            // Tuş listesi 11 satır: daha yukarıdan başlar
            UiBuilder.Stretch(items, new Vector2(0.22f, 0.1f), new Vector2(0.78f, showingControls ? 0.82f : 0.78f));
            menu.Refresh();
        }

        void BuildCanvas()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "SettingsCanvas", sortingOrder);
            group = canvas.GetComponent<CanvasGroup>();

            var background = UiBuilder.CreateImage(canvas.transform, "Background", backgroundColor);
            UiBuilder.Stretch(background.rectTransform, Vector2.zero, Vector2.one);

            var titleArea = new GameObject("TitleArea", typeof(RectTransform)).GetComponent<RectTransform>();
            titleArea.SetParent(canvas.transform, false);
            UiBuilder.Stretch(titleArea, new Vector2(0.2f, 0.8f), new Vector2(0.8f, 0.94f));
            titleText = UiTitle.Create(titleArea, "Title", font, titleFontSize, 2f);

            bodyPanel = UiBuilder.CreatePanel(canvas.transform, "BodyPanel");
            UiBuilder.Stretch(bodyPanel.rectTransform, new Vector2(0.14f, 0.24f), new Vector2(0.86f, 0.78f));
            bodyPanel.gameObject.SetActive(false);
            bodyText = UiBuilder.Outlined(UiBuilder.CreateText(canvas.transform, "Body", font, bodyFontSize, normalColor));
            UiBuilder.Stretch(bodyText.rectTransform, new Vector2(0.16f, 0.26f), new Vector2(0.84f, 0.76f));

            items = new GameObject("Items", typeof(RectTransform)).GetComponent<RectTransform>();
            items.SetParent(canvas.transform, false);
            menu = new MenuList(items, font, itemFontSize, rowHeight, normalColor, highlightColor);

            footer = UiPromptRow.Create(canvas.transform, font, 18, 16f);

            group.alpha = 0f;
            group.blocksRaycasts = false;
            Refresh();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Instance = null;
            ClosedFrame = -1;
        }
    }
}
