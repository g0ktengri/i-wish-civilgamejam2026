using Deprem.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deprem.Spirit
{
    // Ruhun yanındaki sabitlenmemiş eşyayı bulur; F'ye basınca o eşyanın minigame'ini açar.
    // Minigame sistemi MinigameOpened'ı dinler, bitince Secure() ve RaiseMinigameClosed çağırır.
    public class SpiritInteractor : MonoBehaviour
    {
        [SerializeField] float range = 1.2f;

        bool minigameOpen;

        // Ruha en yakın, menzildeki sabitlenmemiş eşya (yoksa null). HUD ve vurgu okur.
        public static SecurableItem Nearest { get; private set; }

        // Minigame'ler hazır olana kadar F ile eşya anında sabitlenir (SpiritPhase ayarlar)
        public bool InstantSecure { get; set; }

        void OnEnable()
        {
            minigameOpen = false;
            GameEvents.MinigameOpened += HandleMinigameOpened;
            GameEvents.MinigameClosed += HandleMinigameClosed;
        }

        void OnDisable()
        {
            GameEvents.MinigameOpened -= HandleMinigameOpened;
            GameEvents.MinigameClosed -= HandleMinigameClosed;
            Nearest = null;
        }

        void HandleMinigameOpened(ItemId id, MinigameType type) => minigameOpen = true;
        void HandleMinigameClosed(ItemId id, bool success) => minigameOpen = false;

        void Update()
        {
            Nearest = minigameOpen || GameState.Phase != GamePhase.Spirit ? null : FindNearest();
            if (!Nearest) return;

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.fKey.wasPressedThisFrame) return;

            var item = Nearest;
            if (InstantSecure)
            {
                item.Secure();
                return;
            }
            GameEvents.RaiseMinigameOpened(item.Id, item.Minigame);
        }

        SecurableItem FindNearest()
        {
            var position = transform.position;
            SecurableItem best = null;
            float bestDistance = range;

            foreach (var item in SecurableItem.All)
            {
                if (item.IsSecured) continue;
                float distance = PlanarDistance(position, item);
                if (distance > bestDistance) continue;
                best = item;
                bestDistance = distance;
            }
            return best;
        }

        // X-Y düzleminde eşyanın kutusuna olan uzaklık (Z, 2.5D'de önemsiz)
        static float PlanarDistance(Vector3 position, SecurableItem item)
        {
            var collider = item.GetComponent<Collider>();
            Vector3 closest;
            if (collider)
            {
                var bounds = collider.bounds;
                closest = bounds.ClosestPoint(new Vector3(position.x, position.y, bounds.center.z));
            }
            else
            {
                closest = item.transform.position;
            }
            return Vector2.Distance(new Vector2(position.x, position.y), new Vector2(closest.x, closest.y));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Nearest = null;
    }
}
