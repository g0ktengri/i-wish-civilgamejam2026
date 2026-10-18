using System.Collections;
using Deprem.Localization;
using Deprem.Player;
using Deprem.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deprem.Quake
{
    /// <summary>
    /// Play testi 33: minigame öğreticisi (Berke, M61) gibi ilk çök-kapan-tutun anlatımı. Ana deprem ilk kez
    /// çök-kapanla karşılanacağı an oyun durur; vurgulanan alanın dışı kararır, çevresinde nabız gibi atan sarı
    /// çerçeve, altta adım metni ve "F: Devam". Adımlar: parlayan masa, masaya yürü, S basılı tut.
    /// QuakeDirector oynatır (yeni oyunda bir kez).
    /// </summary>
    public sealed class DuckCoverTutorial : MonoBehaviour
    {
        [Tooltip("Vurgulanan alanın dışındaki karartma (minigame öğreticisinin renginde; ev geniş olduğu için daha koyu)")]
        [SerializeField] private Color dim = new Color(0f, 0f, 0.02f, 0.8f);
        [SerializeField] private Color frameColor = new Color(1f, 0.82f, 0.4f);
        [SerializeField] private int fontSize = 20;
        [Tooltip("Alttaki adım panelinin boyu (referans pikseli)")]
        [SerializeField] private Vector2 panelSize = new Vector2(620f, 78f);
        [Tooltip("Panelin ekranın altından yüksekliği (referans pikseli)")]
        [SerializeField] private float panelBottom = 30f;
        [Tooltip("Vurgunun çevresindeki pay (referans pikseli)")]
        [SerializeField] private float padding = 10f;
        [Tooltip("Son adımda karakterin üstündeki süre çubuğu da vurgunun içinde kalsın (ayaktan, m)")]
        [SerializeField] private float barHeight = 1.9f;

        /// <summary>Öğretici ekranda mı (oyun durur; duraklatma menüsü bu sırada açılmaz)</summary>
        public static bool Active { get; private set; }

        private static readonly string[] StepKeys =
            { "tutorial.duck_cover.table", "tutorial.duck_cover.walk", "tutorial.duck_cover.hold" };

        private Canvas canvas;
        private RectTransform canvasRect;
        private readonly Image[] dims = new Image[4];
        private readonly Image[] frame = new Image[4];
        private Text text;
        private UiPrompt next;
        private int tickFrame;

        public IEnumerator Play(PlayerController player, SafeSpot spot)
        {
            if (player == null) yield break;
            Build();
            Active = true;
            tickFrame = Time.frameCount;
            float savedScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            bool savedInput = player.InputEnabled;
            player.InputEnabled = false;
            player.SetScriptedInput(0f);
            canvas.gameObject.SetActive(true);
            next.Set(Texts.Get("minigame.tutorial.continue"));

            for (int step = 0; step < StepKeys.Length; step++)
            {
                text.text = $"({step + 1}/{StepKeys.Length}) {Texts.Get(StepKeys[step])}";
                int shownFrame = Time.frameCount;
                while (true)
                {
                    tickFrame = Time.frameCount;
                    Time.timeScale = 0f; // sarsıntı, devrilen eşyalar ve sayaç okurken durur
                    Highlight(Area(step, player, spot));
                    Pulse();
                    yield return null; // timeScale 0'da da her kare döner
                    if (Time.frameCount > shownFrame + 1 && NextPressed()) break;
                }
                Audio.Sfx.Play("ui.move");
            }

            canvas.gameObject.SetActive(false);
            next.Set(null);
            Time.timeScale = savedScale;
            player.InputEnabled = savedInput;
            Active = false;
        }

        // Oynatan coroutine durdurulursa (QuakeDirector başka faza geçti) oyun donmuş kalmasın
        private void LateUpdate()
        {
            if (Active && Time.frameCount > tickFrame + 2) Abort();
        }

        private void OnDisable() => Abort();

        private void Abort()
        {
            if (!Active) return;
            Active = false;
            Time.timeScale = 1f;
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Active = false;

        private static bool NextPressed()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            return (keyboard != null && (keyboard.fKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame ||
                                         keyboard.enterKey.wasPressedThisFrame))
                   || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        }

        // ------------------------------------------------------------------ Vurgulanan alan

        /// <summary>Adımın vurgusu, tuvalin ortasına göre referans pikseli</summary>
        private Rect Area(int step, PlayerController player, SafeSpot spot)
        {
            var feet = player.transform.position;
            float height = step == 2 ? barHeight : player.StandingHeight + 0.1f;
            var body = new Bounds(feet + Vector3.up * (height * 0.5f), new Vector3(0.8f, height, 0.05f));
            if (spot == null) return Project(body);
            var table = spot.VisualBounds;
            switch (step)
            {
                case 0: return Project(table);
                case 1:
                    body.Encapsulate(table);
                    return Project(body);
                default: return Project(body);
            }
        }

        private Rect Project(Bounds bounds)
        {
            var cam = Camera.main;
            var size = canvasRect.rect.size;
            if (cam == null) return new Rect(-60f, -60f, 120f, 120f);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(i % 2 == 0 ? bounds.min.x : bounds.max.x, (i / 2) % 2 == 0 ? bounds.min.y : bounds.max.y,
                    i / 4 == 0 ? bounds.min.z : bounds.max.z);
                var viewport = cam.WorldToViewportPoint(corner);
                var local = new Vector2((viewport.x - 0.5f) * size.x, (viewport.y - 0.5f) * size.y);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            min -= Vector2.one * padding;
            max += Vector2.one * padding;
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // Vurgunun çevresinde dört karartma parçası (alanın kendisi açık kalır) ve 3 px çerçeve
        private void Highlight(Rect area)
        {
            var half = canvasRect.rect.size * 0.5f;
            float x0 = Mathf.Clamp(Mathf.Round(area.xMin), -half.x, half.x), x1 = Mathf.Clamp(Mathf.Round(area.xMax), -half.x, half.x);
            float y0 = Mathf.Clamp(Mathf.Round(area.yMin), -half.y, half.y), y1 = Mathf.Clamp(Mathf.Round(area.yMax), -half.y, half.y);
            Place(dims[0], -half.x, y1, half.x, half.y);
            Place(dims[1], -half.x, -half.y, half.x, y0);
            Place(dims[2], -half.x, y0, x0, y1);
            Place(dims[3], x1, y0, half.x, y1);
            const float t = 3f;
            Place(frame[0], x0 - t, y1, x1 + t, y1 + t);
            Place(frame[1], x0 - t, y0 - t, x1 + t, y0);
            Place(frame[2], x0 - t, y0, x0, y1);
            Place(frame[3], x1, y0, x1 + t, y1);
        }

        private void Pulse()
        {
            float alpha = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
            foreach (var image in frame) image.color = new Color(frameColor.r, frameColor.g, frameColor.b, alpha);
        }

        private static void Place(Graphic image, float x0, float y0, float x1, float y1)
        {
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Mathf.Max(0f, x1 - x0), Mathf.Max(0f, y1 - y0));
            rect.anchoredPosition = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
        }

        // ------------------------------------------------------------------ Arayüz

        private void Build()
        {
            if (canvas != null) return;
            canvas = UiBuilder.CreateCanvas(transform, "DuckCoverTutorial", 70); // ipucunun (60) ve çubuğun üstünde
            canvasRect = (RectTransform)canvas.transform;
            for (int i = 0; i < 4; i++)
            {
                dims[i] = UiBuilder.CreateImage(canvas.transform, "Dim", dim);
                frame[i] = UiBuilder.CreateImage(canvas.transform, "Frame", frameColor);
                frame[i].gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1f, -1f);
            }

            var panel = UiBuilder.CreatePanel(canvas.transform, "Panel");
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.sizeDelta = panelSize;
            panelRect.anchoredPosition = new Vector2(0f, panelBottom);

            var font = UiBuilder.HudFont;
            text = UiBuilder.Outlined(UiBuilder.CreateText(panel.transform, "Text", font, fontSize, UiBuilder.HudText));
            UiBuilder.Stretch(text.rectTransform, Vector2.zero, Vector2.one);
            text.rectTransform.offsetMin = new Vector2(16f, 8f);
            text.rectTransform.offsetMax = new Vector2(-16f, -8f);
            text.lineSpacing = 0.9f;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = fontSize;

            // "F: Devam" minigame öğreticisindeki gibi tuş kapaklı, panelin sağ üst köşesinin hemen üstünde
            next = UiPrompt.Create(panel.transform, "Next", font, 14);
            next.RemapKeys = false; // F sabit okunur
            next.Rect.anchorMin = next.Rect.anchorMax = Vector2.one;
            next.Rect.pivot = new Vector2(1f, 0f);
            next.Rect.anchoredPosition = new Vector2(0f, 6f);
            canvas.gameObject.SetActive(false);
        }
    }
}
