using UnityEngine;
using UnityEngine.InputSystem;

namespace Deprem.Player
{
    /// <summary>F'ye basınca en yakın etkileşilebiliri çalıştırır. Current, UI'daki "F" ipucu için.</summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Key interactKey = Key.F;

        public IInteractable Current { get; private set; }

        private PlayerController player;
        private string shownPrompt = "";

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void Update()
        {
            Current = player.InputEnabled && !player.IsOnPath ? Interactables.FindNearest(player) : null;
            var keyboard = Keyboard.current;
            if (Current != null && keyboard != null && keyboard[interactKey].wasPressedThisFrame)
            {
                Current.Interact(player);
                Current = null; // ipucu bir sonraki karede yeniden hesaplanır (kapı açıldı, merdivene çıkıldı...)
            }
            UpdatePrompt();
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
            string key = Current == null ? "" : Current is IPromptSource source ? source.PromptKey : "prompt.interact";
            if (key == shownPrompt) return;
            shownPrompt = key;
            Deprem.Core.GameEvents.RaisePromptChanged(key);
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(shownPrompt) && string.IsNullOrEmpty(Deprem.Quake.QuakePrompts.Current))
                Deprem.Core.GameEvents.RaisePromptChanged("");
            shownPrompt = "";
        }
    }
}
