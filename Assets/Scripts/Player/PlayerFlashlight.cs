using Deprem.Core;
using UnityEngine;

namespace Deprem.Player
{
    /// <summary>
    /// El feneri (GDD: Spot Light, karakterin yönüne bakar). Çantada el feneri varsa kurtarma fazında yanar;
    /// karanlık üst katta kardeş ancak bununla görünür. Elektrik kesikken (kurtarma fazı) ev karanlıktır;
    /// gözün karanlığa alışması için oyuncunun çevresinde küçük, loş bir görüş ışığı kalır.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerFlashlight : MonoBehaviour
    {
        [SerializeField] private Light spot;
        [Tooltip("Elektrik kesikken oyuncunun çevresindeki loş ışık")]
        [SerializeField] private Light nightSight;
        [Tooltip("El fenerinin görünen huzmesi (spotun çocuğu)")]
        [SerializeField] private Renderer beam;
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
            bool dark = GameState.Phase == GamePhase.Rescue && !player.IsLying;
            if (nightSight != null) nightSight.enabled = dark;
            if (spot == null) return;
            bool on = dark && GameState.HasBagItem(BagItemId.Flashlight);
            spot.enabled = on;
            if (beam != null) beam.enabled = on;
            if (!on) return;
            spot.transform.localPosition = new Vector3(0.2f * player.Facing, height, -0.1f);
            spot.transform.rotation = Quaternion.LookRotation(new Vector3(player.Facing, -Mathf.Tan(downTilt * Mathf.Deg2Rad), 0f));
        }
    }
}
