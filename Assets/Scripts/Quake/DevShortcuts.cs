using Deprem.Core;
using Deprem.Loop;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Deprem.Quake
{
    /// <summary>
    /// Geliştirici (admin) kısayolları: trailer çekimi ve hızlı deneme için (Mehmet'in isteği). Sadece editörde,
    /// development build'de ya da build "-dev" ile açılınca (Build/Keske/Kayit_icin_ac.bat); oyuncuda yok.
    ///   Ctrl+Shift+1..5  o döngüden başla (1: intro ve raf, 3: çök-kapan, 4: ruh fazı)
    ///   Ctrl+Shift+O     oyunu kazanılmış say, outro başlasın
    ///   Ctrl+Shift+L     stüdyo logosunu oynat (outrodan menüye dönüşteki)
    ///   Ctrl+Shift+H     HUD'u gizle / göster (ipucu, hedef, çanta, sayaçlar; balonlar ve ekranlar kalır)
    ///   Ctrl+Shift+G     hızlı sar (4x) aç / kapat
    /// </summary>
    public sealed class DevShortcuts : MonoBehaviour
    {
        private const float FastForwardScale = 4f;

        // Gizlenen HUD tuvalleri (UiBuilder.CreateCanvas adları)
        private static readonly string[] HudCanvases =
            { "PromptCanvas", "ObjectiveCanvas", "BagHudCanvas", "SpiritHudCanvas", "RescueBar", "DuckCoverBar" };

        private static bool hudHidden;
        private static bool fastForward;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            hudHidden = false;
            fastForward = false;
            // Normal build'de "-dev" ile açılırsa da çalışır (trailer çekimi: development build köşeye yazı basıyor)
            bool devArg = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-dev") >= 0;
            if (!Application.isEditor && !Debug.isDebugBuild && !devArg) return;
            var go = new GameObject("DevShortcuts");
            DontDestroyOnLoad(go);
            go.AddComponent<DevShortcuts>();
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (hudHidden) ApplyHud();
        }

        private void Update()
        {
            if (fastForward && Time.timeScale > 0f && !DuckCoverTutorial.Active) Time.timeScale = FastForwardScale;
            // HUD sonradan kurulan tuvalleri de gizlensin (çubuklar ilk gösterildiklerinde kurulur)
            if (hudHidden && Time.frameCount % 30 == 0) ApplyHud();

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.ctrlKey.isPressed || !keyboard.shiftKey.isPressed) return;

            Key[] loops = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
            for (int i = 0; i < loops.Length; i++)
            {
                if (!keyboard[loops[i]].wasPressedThisFrame || LoopManager.Instance == null) continue;
                Debug.Log($"[Dev] Döngü {i + 1}'den başlanıyor");
                LoopManager.Instance.StartFromLoop(i + 1);
                return;
            }

            if (keyboard.oKey.wasPressedThisFrame) StartOutro();
            if (keyboard.lKey.wasPressedThisFrame) Visual.StudioLogo.Play(null);
            if (keyboard.hKey.wasPressedThisFrame)
            {
                hudHidden = !hudHidden;
                ApplyHud();
                Debug.Log("[Dev] HUD " + (hudHidden ? "gizli" : "açık"));
            }
            if (keyboard.gKey.wasPressedThisFrame)
            {
                fastForward = !fastForward;
                if (!fastForward && Time.timeScale > 1f) Time.timeScale = 1f;
                Debug.Log("[Dev] Hızlı sarma " + (fastForward ? "açık (4x)" : "kapalı"));
            }
        }

        // Her yerden çalışır (Mehmet: menüde basınca bir şey olmuyordu); ayrıntı Outro.Request'te
        private static void StartOutro()
        {
            Debug.Log("[Dev] Outro isteniyor");
            Outro.Request();
        }

        private static void ApplyHud()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (System.Array.IndexOf(HudCanvases, canvas.gameObject.name) >= 0) canvas.enabled = !hudHidden;
        }
    }
}
