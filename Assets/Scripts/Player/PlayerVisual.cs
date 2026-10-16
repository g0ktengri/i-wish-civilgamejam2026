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
        [Tooltip("Yatarken görselin kalınlığı (yer tutucunun eni, m)")]
        [SerializeField] private float lyingThickness = 0.6f;

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
            // Yer tutucu: yatarken yan yatır, baş solda (yatağın başlığı), gövde pivotun ortasında ve
            // şiltenin üstünde (pivot = şilte üstü). Sprite gelince "yatakta uyuma" animasyonu.
            visual.localRotation = player.IsLying ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            visual.localPosition = player.IsLying
                ? new Vector3(player.StandingHeight * 0.5f, lyingThickness * 0.5f, 0f)
                : Vector3.zero;
        }
    }
}
