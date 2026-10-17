using UnityEngine;

namespace Deprem.Player
{
    /// <summary>
    /// Karakterin 2D görünüşü: yönüne göre çevirir, yürürken koşma karelerini oynatır, eğilince alçaltır.
    /// Kareler (Assets/Art/Characters/oyuncu_kosma_N.png, 128 x 128, sağa bakar) quad'ın materyaline
    /// MaterialPropertyBlock ile takılır. Bekleme ve eğilme çizimleri gelene kadar duruşta idleFrame gösterilir,
    /// eğilince görsel alçaltılır. Kare yoksa düz renkli yer tutucu kalır.
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
        [Tooltip("Bu yatay hızın altında duruyor sayılır (m/s)")]
        [SerializeField] private float moveThreshold = 0.2f;

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");        private PlayerController player;
        private MaterialPropertyBlock block;
        private float frameTime;
        private int shownFrame = -1;

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

            Animate(Time.deltaTime);
        }

        private void Animate(float dt)
        {
            if (body == null || runFrames == null || runFrames.Length == 0) return;

            int frame;
            float speed = player.Body != null ? Mathf.Abs(player.Body.linearVelocity.x) : 0f;
            bool moving = (speed > moveThreshold || player.IsOnPath) && !player.IsLying && !player.IsCrouching;
            if (player.IsLying || player.IsCrouching)
            {
                frame = idleFrame;
            }
            else if (!player.IsGrounded && !player.IsOnPath)
            {
                frame = airFrame;
            }
            else if (moving)
            {
                frameTime += dt * runFps;
                frame = Mathf.FloorToInt(frameTime) % runFrames.Length;
            }
            else
            {
                frameTime = 0f;
                frame = idleFrame;
            }

            frame = Mathf.Clamp(frame, 0, runFrames.Length - 1);
            if (frame == shownFrame || runFrames[frame] == null) return;
            shownFrame = frame;
            block ??= new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            block.SetTexture(BaseMap, runFrames[frame]);
            body.SetPropertyBlock(block);
        }
    }
}
