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
        [Tooltip("Çömelirken fenerin yüksekliği ve öne uzaklığı (m): çömelme çiziminde eller baş hizasında, ~0,7 m")]
        [SerializeField] private float crouchHeight = 0.7f;
        [SerializeField] private float crouchForward = 0.4f;
        [Tooltip("Aşağı eğim (derece)")]
        [SerializeField] private float downTilt = 12f;

        [Tooltip("Görünen huzmenin en uzun hali (mesh boyu, m)")]
        [SerializeField] private float beamLength = 3f;

        private PlayerController player;
        private PlayerVisual visual;
        private Collider[] ownColliders;
        private readonly RaycastHit[] hits = new RaycastHit[8];

        /// <summary>
        /// Play testi 12: huzme duvarın arkasına geçiyordu. Lensten ileri ışın atılır; ilk duvarda / kapalı kapıda /
        /// eşyada huzme orada biter (huzme bütün olarak küçülür, koni biçimi korunur).
        /// </summary>
        private void ClipBeamAtWall()
        {
            if (beam == null) return;
            if (ownColliders == null) ownColliders = GetComponentsInChildren<Collider>(true);
            var origin = spot.transform.position;
            var direction = spot.transform.forward;
            float distance = beamLength;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, beamLength, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (System.Array.IndexOf(ownColliders, hits[i].collider) >= 0) continue;
                distance = Mathf.Min(distance, hits[i].distance);
            }
            beam.transform.localScale = Vector3.one * Mathf.Clamp(distance / beamLength, 0.12f, 1f);
        }

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
            // Eğilince fener de iner (Play testi 10: çömelince fener ayakta kalıyordu); çömelme çiziminde ayrı
            // fener yok, eller baş hizasında
            if (visual != null && visual.HasFlashlightTip)
                spot.transform.localPosition = visual.FlashlightTipLocal + new Vector3(0f, 0f, -0.1f);
            else if (player.IsCrouching)
                spot.transform.localPosition = new Vector3(crouchForward * player.Facing, crouchHeight, -0.1f);
            else
                spot.transform.localPosition = new Vector3(forward * player.Facing, height, -0.1f);
            spot.transform.rotation = Quaternion.LookRotation(new Vector3(player.Facing, -Mathf.Tan(downTilt * Mathf.Deg2Rad), 0f));
            ClipBeamAtWall();
        }
    }
}
