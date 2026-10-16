using Deprem.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deprem.Spirit
{
    // Ruh hareketi: W/A/S/D ile serbest uçar, duvarlardan ve katlardan geçer (collider yok).
    // Evin sınırları dışına çıkamaz (HouseBounds varsa kat sınırları). Minigame açıkken durur.
    public class SpiritController : MonoBehaviour
    {
        [SerializeField] float speed = 5f;
        [SerializeField] float acceleration = 25f;

        [Header("Sınırlar")]
        [Tooltip("Açıksa sınırlar aşağıdaki isimdeki ev objesinin renderer'larından hesaplanır")]
        [SerializeField] bool autoBounds = true;
        [SerializeField] string houseRootName = "House";
        [SerializeField] Vector2 boundsMin = new Vector2(-1f, -0.5f);
        [SerializeField] Vector2 boundsMax = new Vector2(40f, 7f);
        [SerializeField] float boundsPadding = 0.3f;

        [Header("Süzülme")]
        [SerializeField] float bobHeight = 0.08f;
        [SerializeField] float bobSpeed = 2.5f;

        Transform visual;
        Vector2 velocity;
        bool boundsReady;
        int minigamesOpen;

        public bool MovementEnabled => minigamesOpen == 0 && GameState.Phase == GamePhase.Spirit;

        public void SetVisual(Transform target) => visual = target;

        void OnEnable()
        {
            velocity = Vector2.zero;
            minigamesOpen = 0;
            GameEvents.MinigameOpened += HandleMinigameOpened;
            GameEvents.MinigameClosed += HandleMinigameClosed;
        }

        void OnDisable()
        {
            GameEvents.MinigameOpened -= HandleMinigameOpened;
            GameEvents.MinigameClosed -= HandleMinigameClosed;
        }

        void HandleMinigameOpened(ItemId id, MinigameType type) => minigamesOpen++;
        void HandleMinigameClosed(ItemId id, bool success) => minigamesOpen = Mathf.Max(0, minigamesOpen - 1);

        void Update()
        {
            if (!boundsReady) ResolveBounds();

            Vector2 input = MovementEnabled ? ReadInput() : Vector2.zero;
            velocity = Vector2.MoveTowards(velocity, input * speed, acceleration * Time.deltaTime);

            var position = transform.position + (Vector3)(velocity * Time.deltaTime);
            // Evin kat sınırları Mehmet'in HouseBounds'unda (ENTEGRASYON M7); yoksa renderer sınırları
            var houseBounds = Player.HouseBounds.Instance;
            if (houseBounds)
            {
                position = houseBounds.ClampToHouse(position, boundsPadding);
            }
            else
            {
                position.x = Mathf.Clamp(position.x, boundsMin.x, boundsMax.x);
                position.y = Mathf.Clamp(position.y, boundsMin.y, boundsMax.y);
            }
            transform.position = position;

            if (visual) visual.localPosition = new Vector3(0f, Mathf.Sin(Time.time * bobSpeed) * bobHeight, 0f);
        }

        static Vector2 ReadInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;
            var input = new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            return input.sqrMagnitude > 1f ? input.normalized : input;
        }

        void ResolveBounds()
        {
            boundsReady = true;
            if (!autoBounds) return;

            var house = GameObject.Find(houseRootName);
            if (!house) return;

            var renderers = house.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            boundsMin = new Vector2(bounds.min.x + boundsPadding, bounds.min.y + boundsPadding);
            boundsMax = new Vector2(bounds.max.x - boundsPadding, bounds.max.y - boundsPadding);
        }
    }
}
