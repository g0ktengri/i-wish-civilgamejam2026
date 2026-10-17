using System;
using System.Collections.Generic;
using System.Linq;
using Deprem.Audio;
using Deprem.Localization;
using UnityEngine;
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
    public class SettingsMenu : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] int titleFontSize = 34;
        [SerializeField] int itemFontSize = 19;
        [SerializeField] int bodyFontSize = 17;
        [SerializeField] float rowHeight = 32f;
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
        Action onClose;
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

        void OnEnable() => Texts.LanguageChanged += Refresh;
        void OnDisable() => Texts.LanguageChanged -= Refresh;

        public void Open(Action closed)
        {
            onClose = closed;
            open = true;
            showingControls = false;
            group.alpha = 1f;
            group.blocksRaycasts = true;
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
            if (UiInput.Back())
            {
                Audio.Sfx.Play("ui.back");
                if (showingControls)
                {
                    showingControls = false;
                    Rebuild();
                }
                else Close();
                return;
            }
            menu.Tick();
        }

        void Rebuild()
        {
            menu.Clear();
            if (showingControls)
            {
                menu.Add("menu.back", () => { showingControls = false; Rebuild(); });
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

        // %10 adımlarla; sağ / onay artırır (100'den sonra 0'a döner), sol azaltır
        void AddVolume(string key, Func<float> get, Action<float> set, Sprite icon)
        {
            menu.AddSlider(key, get, direction =>
            {
                float value = get() + direction * 0.1f;
                if (value > 1.001f) value = 0f;
                set(Mathf.Clamp01(value));
            });
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
            titleText.Text = Texts.Get("menu.settings");
            bodyText.text = showingControls ? Texts.Get("controls.body") : string.Empty;
            bodyPanel.gameObject.SetActive(showingControls);
            if (showingControls) footer.Set(Texts.Get("menu.hint.confirm"), Texts.Get("menu.hint.back"));
            else footer.Set(Texts.Get("menu.hint.select"), Texts.Get("menu.hint.change"), Texts.Get("menu.hint.back"));
            UiBuilder.Stretch(items, new Vector2(showingControls ? 0.33f : 0.22f, showingControls ? 0.06f : 0.1f),
                new Vector2(showingControls ? 0.67f : 0.78f, showingControls ? 0.2f : 0.78f));
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
