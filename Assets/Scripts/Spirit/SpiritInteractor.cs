using Deprem.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deprem.Spirit
{
    // Ruhun yanındaki en yakın etkileşimi bulur: sabitlenmemiş eşya ya da ISpiritTarget (çanta vb.).
    // Eşyada F minigame'i açar (MinigameHost MinigameOpened'ı dinler, bitince Secure() ve
    // RaiseMinigameClosed çağırır). Diğer hedeflerde hedefin Interact() metodunu çağırır.
    public class SpiritInteractor : MonoBehaviour
    {
        [SerializeField] float range = 1.2f;

        // Ruha en yakın, menzildeki sabitlenmemiş eşya (yoksa null). Vurgu okur.
        public static SecurableItem Nearest { get; private set; }
        // Eşyadan daha yakın bir ISpiritTarget varsa o (yoksa null)
        public static ISpiritTarget NearestTarget { get; private set; }
        // HUD'da gösterilecek ipucunun texts.csv anahtarı (yoksa null)
        public static string PromptKey { get; private set; }

        // Sahnede MinigameHost yoksa F ile eşya anında sabitlenir (SpiritPhase ayarlar, test için)
        public bool InstantSecure { get; set; }

        void OnDisable() => Clear();

        void Update()
        {
            Clear();
            if (Minigames.MinigameHost.AnyOpen || GameState.Phase != GamePhase.Spirit) return;

            var position = transform.position;
            float itemDistance = FindNearestItem(position, out var item);
            float targetDistance = FindNearestTarget(position, out var target);

            if (target != null && targetDistance < itemDistance)
            {
                NearestTarget = target;
                PromptKey = target.PromptKey;
            }
            else if (item)
            {
                Nearest = item;
                PromptKey = "prompt.secure";
            }

            var keyboard = Keyboard.current;
            if (keyboard == null || !Player.KeyBindings.Pressed(Player.KeyBindings.Interact)) return;

            if (NearestTarget != null) NearestTarget.Interact();
            else if (Nearest) Interact(Nearest);
        }

        void Interact(SecurableItem item)
        {
            if (InstantSecure && !Minigames.MinigameHost.Instance)
            {
                item.Secure();
                return;
            }
            GameEvents.RaiseMinigameOpened(item.Id, item.Minigame);
        }

        float FindNearestItem(Vector3 position, out SecurableItem best)
        {
            best = null;
            float bestDistance = float.MaxValue;
            foreach (var item in SecurableItem.All)
            {
                if (item.IsSecured) continue;
                float distance = PlanarDistance(position, item);
                if (distance > range || distance >= bestDistance) continue;
                best = item;
                bestDistance = distance;
            }
            return bestDistance;
        }

        static float FindNearestTarget(Vector3 position, out ISpiritTarget best)
        {
            best = null;
            float bestDistance = float.MaxValue;
            foreach (var target in SpiritTargets.All)
            {
                if (!target.CanInteract) continue;
                var point = target.Point;
                float distance = Vector2.Distance(new Vector2(position.x, position.y), new Vector2(point.x, point.y));
                if (distance > target.Range || distance >= bestDistance) continue;
                best = target;
                bestDistance = distance;
            }
            return bestDistance;
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

        static void Clear()
        {
            Nearest = null;
            NearestTarget = null;
            PromptKey = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Clear();
    }
}
