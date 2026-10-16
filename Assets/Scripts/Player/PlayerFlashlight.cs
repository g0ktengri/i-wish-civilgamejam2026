using Deprem.Core;
using UnityEngine;

namespace Deprem.Player
{
    /// <summary>
    /// El feneri (GDD: Spot Light, karakterin yönüne bakar). Çantada el feneri varsa kurtarma fazında yanar;
    /// karanlık üst katta kardeş ancak bununla görünür.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerFlashlight : MonoBehaviour
    {
        [SerializeField] private Light spot;
        [SerializeField] private float height = 1.3f;
        [Tooltip("Aşağı eğim (derece)")]
        [SerializeField] private float downTilt = 12f;

        private PlayerController player;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void LateUpdate()
        {
            if (spot == null) return;
            bool on = GameState.Phase == GamePhase.Rescue && GameState.HasBagItem(BagItemId.Flashlight) && !player.IsLying;
            spot.enabled = on;
            if (!on) return;
            spot.transform.localPosition = new Vector3(0.2f * player.Facing, height, -0.1f);
            spot.transform.rotation = Quaternion.LookRotation(new Vector3(player.Facing, -Mathf.Tan(downTilt * Mathf.Deg2Rad), 0f));
        }
    }
}
