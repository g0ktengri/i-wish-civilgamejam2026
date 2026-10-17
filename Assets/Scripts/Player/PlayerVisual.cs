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
        [Tooltip("El feneri yanarken koşu (oyuncu_fenerle_kosma_N); yoksa normal koşu")]
        [SerializeField] private Texture2D[] flashlightRunFrames;
        [Tooltip("Fener yanarken dururken ve havadayken gösterilen fenerle koşu karesi (en dar adımlı)")]
        [SerializeField] private int flashlightIdleFrame = 2;
        [Tooltip("Fenerle koşu karelerinde fenerin ucu (px, sol üstten); PlayerSetup çizimden bulur")]
        [SerializeField] private Vector2[] flashlightTips;
        [SerializeField] private Texture2D[] idleFrames;
        [SerializeField] private float idleFps = 2f;
        [Tooltip("Ayaktan çömelmeye; bir kez oynar, son karede kalır")]
        [SerializeField] private Texture2D[] crouchFrames;
        [SerializeField] private float crouchFps = 14f;
        [Tooltip("Çömelme çiziminin son karedeki ölçeği (en, boy). Çizim ~1,3 m; masalar 0,78 m. Play testi 7: masanın " +
                 "çok üstünde kalıyordu; play testi 8: 0,82 × 0,58 cüce gibiydi. 0,72 ile ~0,92 m, masanın arkasından baş görünür")]
        [SerializeField] private Vector2 crouchedScale = new Vector2(0.95f, 0.72f);
        [Tooltip("Bu yatay hızın altında duruyor sayılır (m/s)")]
        [SerializeField] private float moveThreshold = 0.2f;

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private PlayerController player;
        private PlayerFlashlight flashlight;
        private MaterialPropertyBlock block;
        private float frameTime;
        private float idleTime;
        private float crouchTime;
        private Texture2D shownTexture;

        /// <summary>Gösterilen fenerli karede fenerin ucu var mı (PlayerFlashlight huzmeyi oradan çıkarır)</summary>
        public bool HasFlashlightTip { get; private set; }
        /// <summary>Fenerin ucu, oyuncu kökünün yerel uzayında (yön ve ölçek dahil)</summary>
        public Vector3 FlashlightTipLocal { get; private set; }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            flashlight = GetComponent<PlayerFlashlight>();
        }

        private void LateUpdate()
        {
            if (visual == null) return;
            // Çömelme çizimi varsa görsel alçaltılmaz, kareler çömelir
            bool hasCrouchArt = crouchFrames != null && crouchFrames.Length > 0;
            float height = player.IsCrouching && !hasCrouchArt ? player.CrouchHeight / player.StandingHeight : 1f;
            float width = 1f;
            if (player.IsCrouching && hasCrouchArt)
            {
                // Çömelme kareleriyle birlikte yavaşça küçülür (ayaklar yerde kalır: pivot tabanda)
                float t = Mathf.Clamp01(crouchTime * crouchFps / Mathf.Max(1, crouchFrames.Length - 1));
                width = Mathf.Lerp(1f, crouchedScale.x, t);
                height = Mathf.Lerp(1f, crouchedScale.y, t);
            }
            visual.localScale = new Vector3(player.Facing * width, height, 1f);
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

            // Fener yanarken ayaktaki her duruşta fenerli kareler (dururken / havadayken de): normal koşuya geçip
            // fener elden kaybolmasın
            bool flashlightPose = flashlight != null && flashlight.IsOn && flashlightRunFrames != null &&
                                  flashlightRunFrames.Length > 0 && !player.IsLying && !player.IsCrouching;
            int flashlightIndex = -1;

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
            else if (flashlightPose)
            {
                if (moving && (player.IsGrounded || player.IsOnPath))
                {
                    frameTime += dt * runFps;
                    flashlightIndex = Mathf.FloorToInt(frameTime) % flashlightRunFrames.Length;
                }
                else
                {
                    flashlightIndex = Mathf.Clamp(flashlightIdleFrame, 0, flashlightRunFrames.Length - 1);
                }
                texture = flashlightRunFrames[flashlightIndex];
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
            UpdateFlashlightTip(flashlightIndex, texture);

            if (texture == null || texture == shownTexture) return;
            shownTexture = texture;
            block ??= new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            block.SetTexture(BaseMap, texture);
            body.SetPropertyBlock(block);
        }

        /// <summary>Gösterilen fenerli karedeki fener ucunu oyuncu kökünün yerel uzayına çevirir</summary>
        private void UpdateFlashlightTip(int index, Texture2D texture)
        {
            HasFlashlightTip = false;
            if (index < 0 || flashlightTips == null || index >= flashlightTips.Length || texture == null) return;
            var px = flashlightTips[index];
            if (px.x < 0f) return;
            var quad = body.transform;
            // Quad: 1 x 1 mesh, localScale metre; piksel (sol üstten) -> quad'ın yerel ofseti
            var offset = new Vector3(
                ((px.x + 0.5f) / texture.width - 0.5f) * quad.localScale.x,
                (0.5f - (px.y + 0.5f) / texture.height) * quad.localScale.y,
                0f);
            FlashlightTipLocal = visual.localPosition + Vector3.Scale(visual.localScale, quad.localPosition + offset);
            HasFlashlightTip = true;
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
