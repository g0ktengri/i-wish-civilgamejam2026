using UnityEngine;

namespace Deprem.Spirit
{
    // Ruhun görünüşü: ekip çizimi Assets/Art/Characters/ruh_N.png (128 x 128, sağa bakar, bacakları dağılan koşu).
    // Hareket ederken hızlı, dururken yavaş oynar; gittiği yöne döner; yarı saydam, soğuk renkli sprite
    // (ışıktan etkilenmez, ruh parlar). Pivot gövdenin ortası: ruhun etkileşim noktası ve sallanması aynı kalır.
    // SpiritPhase kurar (kare yoksa eski parlayan küre).
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

        SpriteRenderer spriteRenderer;
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
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
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

        void OnDestroy()
        {
            if (spriteRenderer && spriteRenderer.sharedMaterial) Destroy(spriteRenderer.sharedMaterial);
            if (sprites == null) return;
            foreach (var sprite in sprites)
                if (sprite) Destroy(sprite);
        }
    }
}
