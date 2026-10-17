using UnityEngine;

namespace Deprem.Player
{
    /// <summary>
    /// Karakterin 2D görünüşü: yönüne göre çevirir, yürürken koşma, dururken bekleme, eğilince çömelme
    /// karelerini oynatır. Kareler (Assets/Art/Characters/oyuncu_kosma_N / oyuncu_bekleme_N / oyuncu_egilme_N.png,
    /// 128 x 128, sağa bakar) quad'ın materyaline MaterialPropertyBlock ile takılır. Çömelme bir kez oynar ve son
    /// karede kalır. Bekleme ya da çömelme çizimi yoksa eski davranış (koşma karesi, eğilince alçaltma).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerVisual : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [Tooltip("Yatarken görselin kalınlığı (yer tutucunun eni, m)")]
        [SerializeField] private float lyingThickness = 0.6f;

        [Header("Kare animasyonu")]
        [SerializeField] private Renderer body;
        [SerializeField] private Texture2D[] runFrames;
        [Tooltip("Dururken gösterilen kare (bekleme animasyonu gelene kadar en dar adımlı koşma karesi)")]
        [SerializeField] private int idleFrame = 2;
        [Tooltip("Havadayken gösterilen kare (zıplama animasyonu gelene kadar)")]
        [SerializeField] private int airFrame;
        [SerializeField] private float runFps = 12f;
        [SerializeField] private Texture2D[] idleFrames;
        [SerializeField] private float idleFps = 2f;
        [Tooltip("Ayaktan çömelmeye; bir kez oynar, son karede kalır")]
        [SerializeField] private Texture2D[] crouchFrames;
        [SerializeField] private float crouchFps = 14f;
        [Tooltip("Bu yatay hızın altında duruyor sayılır (m/s)")]
        [SerializeField] private float moveThreshold = 0.2f;

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");        private PlayerController player;
        private MaterialPropertyBlock block;
        private float frameTime;
        private float idleTime;
        private float crouchTime;
        private Texture2D shownTexture;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void LateUpdate()
        {
            if (visual == null) return;
            // Çömelme çizimi varsa görsel alçaltılmaz, kareler çömelir
            bool hasCrouchArt = crouchFrames != null && crouchFrames.Length > 0;
            float height = player.IsCrouching && !hasCrouchArt ? player.CrouchHeight / player.StandingHeight : 1f;
            visual.localScale = new Vector3(player.Facing, height, 1f);
            // Yer tutucu: yatarken yan yatır, baş solda (yatağın başlığı), gövde pivotun ortasında ve
            // şiltenin üstünde (pivot = şilte üstü). Sprite gelince "yatakta uyuma" animasyonu.
            visual.localRotation = player.IsLying ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            visual.localPosition = player.IsLying
                ? new Vector3(player.StandingHeight * 0.5f, lyingThickness * 0.5f, 0f)
                : Vector3.zero;

            Animate(Time.deltaTime);
        }

        private void Animate(float dt)
        {
            if (body == null || runFrames == null || runFrames.Length == 0) return;

            float speed = player.Body != null ? Mathf.Abs(player.Body.linearVelocity.x) : 0f;
            bool moving = (speed > moveThreshold || player.IsOnPath) && !player.IsLying && !player.IsCrouching;
            crouchTime = player.IsCrouching ? crouchTime + dt : 0f;

            Texture2D texture;
            if (player.IsCrouching && crouchFrames != null && crouchFrames.Length > 0)
            {
                int frame = Mathf.Min(Mathf.FloorToInt(crouchTime * crouchFps), crouchFrames.Length - 1);
                texture = crouchFrames[frame];
            }
            else if (player.IsLying || player.IsCrouching)
            {
                texture = IdleTexture(dt);
            }
            else if (!player.IsGrounded && !player.IsOnPath)
            {
                texture = runFrames[Mathf.Clamp(airFrame, 0, runFrames.Length - 1)];
            }
            else if (moving)
            {
                frameTime += dt * runFps;
                texture = runFrames[Mathf.FloorToInt(frameTime) % runFrames.Length];
            }
            else
            {
                frameTime = 0f;
                texture = IdleTexture(dt);
            }

            if (texture == null || texture == shownTexture) return;
            shownTexture = texture;
            block ??= new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            block.SetTexture(BaseMap, texture);
            body.SetPropertyBlock(block);
        }

        // Bekleme çizimi varsa yavaş döngü, yoksa koşma karelerinden en dar adımlısı
        private Texture2D IdleTexture(float dt)
        {
            if (idleFrames == null || idleFrames.Length == 0)
                return runFrames[Mathf.Clamp(idleFrame, 0, runFrames.Length - 1)];
            idleTime += dt * idleFps;
            return idleFrames[Mathf.FloorToInt(idleTime) % idleFrames.Length];
        }
    }
}
