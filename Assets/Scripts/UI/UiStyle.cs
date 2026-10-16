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
