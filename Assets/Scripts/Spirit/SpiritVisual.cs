using UnityEngine;

namespace Deprem.Spirit
{
    // Ruhun görünüşü: ekip çizimi Assets/Art/Characters/ruh_N.png (128 x 128, sağa bakar, bacakları dağılan koşu).
    // Hareket ederken hızlı, dururken yavaş oynar; gittiği yöne döner; yarı saydam, soğuk renkli sprite
    // (ışıktan etkilenmez, ruh parlar). Pivot gövdenin ortası: ruhun etkileşim noktası ve sallanması aynı kalır.
    // SpiritPhase kurar (kare yoksa eski parlayan küre).
    // M30: dururken ekip çizimi ruh_bekleme_N (6 fps), hareket ederken ruh_N (10 fps); ikisi de aynı hizada.
    // Play testi: ruh duvarların içinden geçerken kalın duvar ve döşeme kutularının arkasında kayboluyordu. Sprite
    // her karede evin önüne (frontZ) kameraya doğru çekilir; aynı oranda küçültülür, ekrandaki yeri ve boyu değişmez.
    public class SpiritVisual : MonoBehaviour
    {
        [SerializeField] Texture2D[] frames;
        [Tooltip("Dururken (ruh_bekleme_N); yoksa hareket kareleri yavaş oynar")]
        [SerializeField] Texture2D[] idleFrames;
        [SerializeField] float moveFps = 10f;
        [SerializeField] float idleFps = 6f;
        [Tooltip("Başın tepesi, sprite pivotundan yukarı (m, ölçek 1): balon bunun üstüne konur")]
        [SerializeField] float headHeight = 0.92f;
        [Tooltip("Bu hızın altında duruyor sayılır (m/s)")]
        [SerializeField] float moveThreshold = 0.15f;
        [SerializeField] Color tint = new Color(0.82f, 0.93f, 1f, 0.88f);
        [Tooltip("Gövdenin ortası, karenin sol altından oran olarak (koşu çizimleriyle aynı hiza)")]
        [SerializeField] Vector2 pivot = new Vector2(55f / 128f, 60f / 128f);
        [SerializeField] float pixelsPerUnit = 64f;
        [Tooltip("Sprite'ın çizildiği derinlik: evin kesit yüzünün ve oda örtülerinin (-0,03) önü")]
        [SerializeField] float frontZ = -0.1f;

        SpriteRenderer spriteRenderer;
        Transform spriteTransform;
        Sprite[] sprites;
        Sprite[] idleSprites;
        float fade = 1f;
        SpiritController controller;
        float time;
        bool facingLeft;

        public static SpiritVisual Create(Transform parent, Texture2D[] frames, Texture2D[] idleFrames = null)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(parent, false);
            var visual = go.AddComponent<SpiritVisual>();
            visual.frames = frames;
            visual.idleFrames = idleFrames;
            visual.Build();
            return visual;
        }

        /// <summary>Başın tepesi (dünya; evin önüne yansıtılmış sprite'ın üstünde): balon buraya bağlanır</summary>
        public Vector3 HeadTop => spriteTransform
            ? spriteTransform.position + Vector3.up * headHeight * spriteTransform.lossyScale.y
            : transform.position + Vector3.up * headHeight;

        /// <summary>Ekrandaki ölçek (evin önüne yansıtma oranı); balon yüksekliği bununla çarpılır</summary>
        public float ProjectedScale => spriteTransform ? spriteTransform.lossyScale.y : 1f;

        /// <summary>0..1 saydamlık (M31: bedene dönerken solar)</summary>
        public void SetFade(float value)
        {
            fade = Mathf.Clamp01(value);
            if (spriteRenderer) spriteRenderer.color = new Color(tint.r, tint.g, tint.b, tint.a * fade);
        }

        void Build()
        {
            // Sprite ayrı çocukta: kök ruhla ve sallanmayla gider, çocuk evin önüne yansıtılır
            spriteTransform = new GameObject("Sprite").transform;
            spriteTransform.SetParent(transform, false);
            spriteRenderer = spriteTransform.gameObject.AddComponent<SpriteRenderer>();
            // Kendi materyal kopyası: çalışma anında üretilen başka bir sprite'la aynı çizimde birleşip
            // onun dokusunu almasın (önizlemede görüldü)
            spriteRenderer.sharedMaterial = new Material(spriteRenderer.sharedMaterial) { name = "SpiritSprite" };
            spriteRenderer.color = tint;
            spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            spriteRenderer.receiveShadows = false;
            sprites = ToSprites(frames);
            idleSprites = ToSprites(idleFrames);
            spriteRenderer.sprite = sprites.Length > 0 ? sprites[0] : null;
        }

        Sprite[] ToSprites(Texture2D[] textures)
        {
            if (textures == null) return new Sprite[0];
            var result = new Sprite[textures.Length];
            for (int i = 0; i < textures.Length; i++)
            {
                if (!textures[i]) continue;
                // FullRect: doku okunabilir değil, sıkı (alfa) mesh üretilemez
                result[i] = Sprite.Create(textures[i], new Rect(0, 0, textures[i].width, textures[i].height), pivot,
                    pixelsPerUnit, 0, SpriteMeshType.FullRect);
                result[i].name = textures[i].name;
            }
            return result;
        }

        void Update()
        {
            if (sprites == null || sprites.Length == 0) return;
            if (!controller) controller = GetComponentInParent<SpiritController>();

            Vector2 velocity = controller ? controller.Velocity : Vector2.zero;
            bool moving = velocity.magnitude > moveThreshold;
            if (Mathf.Abs(velocity.x) > moveThreshold) facingLeft = velocity.x < 0f;

            var set = !moving && idleSprites != null && idleSprites.Length > 0 ? idleSprites : sprites;
            time += Time.deltaTime * (moving ? moveFps : idleFps);
            var sprite = set[Mathf.FloorToInt(time) % set.Length];
            if (sprite) spriteRenderer.sprite = sprite;
            spriteRenderer.flipX = facingLeft;
        }

        void LateUpdate()
        {
            if (!spriteTransform) return;
            var cam = Camera.main;
            Vector3 target = transform.position;
            if (!cam)
            {
                spriteTransform.localPosition = Vector3.zero;
                spriteTransform.localScale = Vector3.one;
                return;
            }
            if (cam.orthographic)
            {
                spriteTransform.position = new Vector3(target.x, target.y, frontZ);
                spriteTransform.localScale = Vector3.one;
                return;
            }
            // Kameradan geçen aynı ışın üzerinde öne çek: ekrandaki izdüşüm aynı kalır
            Vector3 eye = cam.transform.position;
            float depth = target.z - eye.z;
            if (depth <= 0.01f) return;
            float t = Mathf.Clamp01((frontZ - eye.z) / depth);
            spriteTransform.position = eye + (target - eye) * t;
            spriteTransform.localScale = Vector3.one * t;
        }

        void OnDestroy()
        {
            if (spriteRenderer && spriteRenderer.sharedMaterial) Destroy(spriteRenderer.sharedMaterial);
            foreach (var set in new[] { sprites, idleSprites })
                if (set != null)
                    foreach (var sprite in set)
                        if (sprite) Destroy(sprite);
        }
    }
}
