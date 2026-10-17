using UnityEngine;

namespace Deprem.Spirit
{
    // Ruhun görünüşü: ekip çizimi Assets/Art/Characters/ruh_N.png (128 x 128, sağa bakar, bacakları dağılan koşu).
    // Hareket ederken hızlı, dururken yavaş oynar; gittiği yöne döner; yarı saydam, soğuk renkli sprite
    // (ışıktan etkilenmez, ruh parlar). Pivot gövdenin ortası: ruhun etkileşim noktası ve sallanması aynı kalır.
    // SpiritPhase kurar (kare yoksa eski parlayan küre).
    // Play testi: ruh duvarların içinden geçerken kalın duvar ve döşeme kutularının arkasında kayboluyordu. Sprite
    // her karede evin önüne (frontZ) kameraya doğru çekilir; aynı oranda küçültülür, ekrandaki yeri ve boyu değişmez.
    public class SpiritVisual : MonoBehaviour
    {
        [SerializeField] Texture2D[] frames;
        [SerializeField] float moveFps = 10f;
        [SerializeField] float idleFps = 4f;
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
        SpiritController controller;
        float time;
        bool facingLeft;

        public static SpiritVisual Create(Transform parent, Texture2D[] frames)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(parent, false);
            var visual = go.AddComponent<SpiritVisual>();
            visual.frames = frames;
            visual.Build();
            return visual;
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
            sprites = new Sprite[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                if (!frames[i]) continue;
                // FullRect: doku okunabilir değil, sıkı (alfa) mesh üretilemez
                sprites[i] = Sprite.Create(frames[i], new Rect(0, 0, frames[i].width, frames[i].height), pivot, pixelsPerUnit,
                    0, SpriteMeshType.FullRect);
                sprites[i].name = frames[i].name;
            }
            spriteRenderer.sprite = sprites.Length > 0 ? sprites[0] : null;
        }

        void Update()
        {
            if (sprites == null || sprites.Length == 0) return;
            if (!controller) controller = GetComponentInParent<SpiritController>();

            Vector2 velocity = controller ? controller.Velocity : Vector2.zero;
            bool moving = velocity.magnitude > moveThreshold;
            if (Mathf.Abs(velocity.x) > moveThreshold) facingLeft = velocity.x < 0f;

            time += Time.deltaTime * (moving ? moveFps : idleFps);
            var sprite = sprites[Mathf.FloorToInt(time) % sprites.Length];
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
            if (sprites == null) return;
            foreach (var sprite in sprites)
                if (sprite) Destroy(sprite);
        }
    }
}
