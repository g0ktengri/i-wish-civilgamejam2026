using UnityEngine;

namespace Deprem.Player
{
    /// <summary>
    /// Karakterin 2D görünüşünü yönüne göre çevirir, eğilince alçaltır.
    /// Şimdilik yer tutucu quad; sanatçının sprite şeritleri gelince animasyon buraya eklenecek.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerVisual : MonoBehaviour
    {
        [SerializeField] private Transform visual;

        private PlayerController player;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void LateUpdate()
        {
            if (visual == null) return;
            float height = player.IsCrouching ? player.CrouchHeight / player.StandingHeight : 1f;
            visual.localScale = new Vector3(player.Facing, height, 1f);
        }
    }
}
