using UnityEngine;
using UnityEngine.InputSystem;

namespace Deprem.Player
{
    /// <summary>F'ye basınca en yakın etkileşilebiliri çalıştırır. Current, UI'daki "F" ipucu için.</summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        private static Key interactKey => KeyBindings.Interact; // M40: ayarlardan değiştirilebilir

        public IInteractable Current { get; private set; }
        /// <summary>Merdivende: F geri döndürür, ipucu "F: Geri dön"</summary>
        public bool OnReversiblePath { get; private set; }

        /// <summary>
        /// ENTEGRASYON M46 (Play testi 24): ekrandaki ipucunun dünya konumu. Etkileşilen objenin tepesinin biraz
        /// üstü; deprem ipuçlarında ("F: Kalk", "S: Çök-Kapan-Tutun") oyuncunun başının üstü. İpucu yoksa null.
        /// Berke'nin ipucu arayüzü bunu ekrana çevirip ipucunu orada gösterir.
        /// </summary>
        public static Vector3? PromptAnchor { get; private set; }

        [Tooltip("İpucu objenin tepesinden bu kadar yukarıda (m)")]
        [SerializeField] private float anchorLift = 0.18f;
        [Tooltip("Yüksek objelerde (merdiven, kapı) ipucu oyuncunun ayağından en fazla bu kadar yukarıda (m)")]
        [SerializeField] private float anchorMaxHeight = 2.3f;

        private PlayerController player;
        private string shownPrompt = "";
        private IInteractable anchoredTarget;
        private Vector3 anchorTop;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void Update()
        {
            // Intro sinematiği: oyuncu yönlendiremez, hiçbir tuş ipucu görünmez (merdivendeki "Yukarı çık" dahil)
            bool cinematic = Deprem.Quake.Cinematic.IsPlaying;
            var keyboard = Keyboard.current;
            bool pressed = keyboard != null && keyboard[interactKey].wasPressedThisFrame;
            // Play testi 31: merdivende inerken / çıkarken F'ye tekrar basınca geri döner
            OnReversiblePath = player.InputEnabled && player.IsOnPath && player.PathReversible && !cinematic;
            if (OnReversiblePath && pressed)
            {
                player.ReversePath();
                pressed = false;
            }
            Current = player.InputEnabled && !player.IsOnPath && !cinematic ? Interactables.FindNearest(player) : null;
            if (Current != null && pressed)
            {
                Current.Interact(player);
                Current = null; // ipucu bir sonraki karede yeniden hesaplanır (kapı açıldı, merdivene çıkıldı...)
            }
            UpdatePrompt();
            UpdateAnchor();
        }

        private void UpdateAnchor()
        {
            var feet = player.transform.position;
            if (!string.IsNullOrEmpty(Deprem.Quake.QuakePrompts.Current))
            {
                PromptAnchor = feet + Vector3.up * (player.StandingHeight + 0.35f);
                return;
            }
            if (OnReversiblePath)
            {
                PromptAnchor = feet + Vector3.up * (player.StandingHeight + 0.35f);
                anchoredTarget = null;
                return;
            }
            if (Current == null)
            {
                PromptAnchor = null;
                anchoredTarget = null;
                return;
            }
            if (!ReferenceEquals(Current, anchoredTarget))
            {
                anchoredTarget = Current;
                anchorTop = TopOf(Current);
            }
            var point = Current.InteractionPoint; // takip eden / hareket eden hedefte x güncel kalsın
            PromptAnchor = new Vector3(point.x, Mathf.Min(anchorTop.y + anchorLift, feet.y + anchorMaxHeight), anchorTop.z);
        }

        // Hedefin görünen parçalarının tepesi (görsel yoksa etkileşim noktasının 1,2 m üstü)
        private static Vector3 TopOf(IInteractable target)
        {
            var point = target.InteractionPoint;
            if (target is Component component)
            {
                bool any = false;
                var bounds = new Bounds(point, Vector3.zero);
                foreach (var r in component.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || r is ParticleSystemRenderer) continue;
                    if (any) bounds.Encapsulate(r.bounds);
                    else bounds = r.bounds;
                    any = true;
                }
                if (any) return new Vector3(point.x, bounds.max.y, Mathf.Min(point.z, bounds.min.z));
            }
            return point + Vector3.up * 1.2f;
        }

        /// <summary>
        /// "F: Kapıyı aç" gibi ipucu, Berke'nin PromptHud'u gösterir. Deprem ipuçları (F: Kalk, C: Çök-Kapan-Tutun)
        /// önceliklidir: onlar ekrandayken buradan ipucu gönderilmez.
        /// </summary>
        private void UpdatePrompt()
        {
            if (!string.IsNullOrEmpty(Deprem.Quake.QuakePrompts.Current))
            {
                shownPrompt = null; // deprem ipucu bitince bizimki yeniden gönderilsin
                return;
            }
            string key = OnReversiblePath ? "prompt.stairs_back"
                : Current == null ? "" : Current is IPromptSource source ? source.PromptKey : "prompt.interact";
            if (key == shownPrompt) return;
            shownPrompt = key;
            Deprem.Core.GameEvents.RaisePromptChanged(key);
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(shownPrompt) && string.IsNullOrEmpty(Deprem.Quake.QuakePrompts.Current))
                Deprem.Core.GameEvents.RaisePromptChanged("");
            shownPrompt = "";
            PromptAnchor = null;
            anchoredTarget = null;
        }
    }
}
