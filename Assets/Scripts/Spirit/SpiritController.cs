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
        [SerializeField] float bobHeight = 0.12f;
        [SerializeField] float bobSpeed = 2.5f;
        [Tooltip("M45 (hayalet çok aşağıdaydı, ayağı yere giriyordu): gövde merkezi en alt zeminin en az bu kadar üstünde, " +
                 "en üst tavanın en az bu kadar altında (m). Görselin merkezi altından 0,94 m, tepesinden 1,06 m uzakta.")]
        [SerializeField] float floorClearance = 1.15f;
        [SerializeField] float ceilingClearance = 1.1f;

        Transform visual;
        Vector2 velocity;
        bool boundsReady;

        // Sadece minigame paneli ekrandayken durur (ENTEGRASYON M9). Event saymak yerine panele bakılır:
        // açılamayan minigame'de ya da event sırası karışınca ruh takılı kalmaz.
        public bool MovementEnabled => GameState.Phase == GamePhase.Spirit && !Minigames.MinigameHost.AnyOpen && !Locked;

        /// <summary>M31: ruh bedene dönerken girdi kapalı (SpiritPhase yönetir)</summary>
        public bool Locked { get; set; }

        public void SetVisual(Transform target) => visual = target;

        /// <summary>Anlık hız (SpiritVisual yönü ve animasyon hızı için)</summary>
        public Vector2 Velocity => velocity;

        void OnEnable() => velocity = Vector2.zero;

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
                position.y = ClampHover(position.y, houseBounds);
            }
            else
            {
                position.x = Mathf.Clamp(position.x, boundsMin.x, boundsMax.x);
                position.y = Mathf.Clamp(position.y, boundsMin.y, boundsMax.y);
            }
            transform.position = position;

            if (visual) visual.localPosition = new Vector3(0f, Mathf.Sin(Time.time * bobSpeed) * bobHeight, 0f);
        }

        // M45: ruh yerde yürümez, süzülür: en alt zeminden ve en üst tavandan görselin boyu kadar uzak durur
        float ClampHover(float y, Player.HouseBounds houseBounds)
        {
            var floors = houseBounds.Floors;
            if (floors == null || floors.Length == 0) return y;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var floor in floors)
            {
                minY = Mathf.Min(minY, floor.floorY);
                maxY = Mathf.Max(maxY, floor.ceilingY);
            }
            float low = minY + floorClearance, high = maxY - ceilingClearance;
            return high > low ? Mathf.Clamp(y, low, high) : y;
        }

        static Vector2 ReadInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;
            var input = new Vector2(
                (Player.KeyBindings.Held(Player.KeyBindings.SpiritRight) ? 1f : 0f) - (Player.KeyBindings.Held(Player.KeyBindings.SpiritLeft) ? 1f : 0f),
                (Player.KeyBindings.Held(Player.KeyBindings.SpiritUp) ? 1f : 0f) - (Player.KeyBindings.Held(Player.KeyBindings.SpiritDown) ? 1f : 0f));
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
