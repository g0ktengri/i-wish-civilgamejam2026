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
        [Tooltip("Fenerin ucu: ayaktan yükseklik ve gövdeden öne (m). Fenerle koşu çizimindeki elin ortalaması")]
        [SerializeField] private float height = 1.15f;
        [SerializeField] private float forward = 0.5f;
        [Tooltip("Aşağı eğim (derece)")]
        [SerializeField] private float downTilt = 12f;

        private PlayerController player;
        private PlayerVisual visual;

        /// <summary>Fener yanıyor mu (PlayerVisual fenerle koşu karelerine geçer)</summary>
        public bool IsOn { get; private set; }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void LateUpdate()
        {
            bool dark = GameState.Phase == GamePhase.Rescue && !player.IsLying;
            if (nightSight != null) nightSight.enabled = dark;
            bool on = dark && GameState.HasBagItem(BagItemId.Flashlight);
            IsOn = on;
            if (spot == null) return;
            spot.enabled = on;
            if (beam != null) beam.enabled = on;
            if (!on) return;
            // Huzme fenerin ucundan çıkar (karede el oynadıkça); uç yoksa ortalama el konumu
            if (visual == null) visual = GetComponent<PlayerVisual>();
            spot.transform.localPosition = visual != null && visual.HasFlashlightTip
                ? visual.FlashlightTipLocal + new Vector3(0f, 0f, -0.1f)
                : new Vector3(forward * player.Facing, height, -0.1f);
            spot.transform.rotation = Quaternion.LookRotation(new Vector3(player.Facing, -Mathf.Tan(downTilt * Mathf.Deg2Rad), 0f));
        }
    }
}
