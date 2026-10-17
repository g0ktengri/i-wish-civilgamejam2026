using UnityEngine;

namespace Deprem.UI
{
    // Bütün oyunun ortak UI stili: font, konuşma balonu, HUD paneli.
    // Assets/Resources/UiStyle.asset; BerkeSystems prefab'ı kurulurken Assets/Art/UI ve
    // Assets/Art/Fonts'tan doldurulur. Boş alan için UiBuilder düz renkli kenarlıklı kutu çizer.
    public class UiStyle : ScriptableObject
    {
        public const string ResourcePath = "UiStyle";

        [Tooltip("Assets/Art/Fonts altındaki ilk font; boşsa Unity'nin yerleşik fontu")]
        public Font font;

        [Tooltip("konusma_balonu.png, 48 x 32, 9-slice (kenar 8 px): konuşma balonunun gövdesi")]
        public Sprite bubble;
        [Tooltip("konusma_balonu_kuyruk.png, 16 x 12: balonun altındaki kuyruk (ucu aşağıda)")]
        public Sprite bubbleTail;
        [Tooltip("hud_ipucu.png, 192 x 32, 9-slice (kenar 8 px): tuş ipucu, sayaç ve hedef panelleri")]
        public Sprite panel;
        [Tooltip("hud_ok.png, 32 x 32: ekran kenarındaki yön oku, sağa bakar (kod döndürür ve renklendirir; beyaz çizin)")]
        public Sprite arrow;
        [Tooltip("hud_tus.png, 12 x 14, 9-slice (kenar 4): tuş kapağı ([F], [C]); altta 2-3 px koyu kalınlık")]
        public Sprite keyCap;

        // M24: bütün HUD'ların ortak renkleri ve boyu (oyunun sıcak ahşap / gece paleti)
        [Header("HUD")]
        public Color hudText = new Color(0.96f, 0.92f, 0.84f);
        [Tooltip("Sayaç, vurgulanan sayı, çantaya eklenen malzeme")]
        public Color hudAccent = new Color(1f, 0.82f, 0.4f);
        [Tooltip("Başlık, ikincil yazı")]
        public Color hudMuted = new Color(0.74f, 0.69f, 0.6f);
        public Color hudWarning = new Color(1f, 0.42f, 0.36f);
        public Color keyText = new Color(0.13f, 0.11f, 0.1f);
        public int hudFontSize = 15;
        [Tooltip("Kenarlardan pay (referans pikseli, 960 x 540)")]
        public float hudMargin = 16f;

        static UiStyle current;
        static bool loaded;

        public static UiStyle Current
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    current = Resources.Load<UiStyle>(ResourcePath);
                }
                return current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            current = null;
            loaded = false;
        }
    }
}
