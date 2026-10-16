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

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void Update()
        {
            Current = player.InputEnabled && !player.IsOnPath ? Interactables.FindNearest(player) : null;
            var keyboard = Keyboard.current;
            if (Current != null && keyboard != null && keyboard[interactKey].wasPressedThisFrame)
                Current.Interact(player);
        }
    }
}
