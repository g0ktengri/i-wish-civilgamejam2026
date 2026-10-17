using Deprem.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deprem.Spirit
{
    // Ruhun yanındaki en yakın etkileşimi bulur: sabitlenmemiş eşya ya da ISpiritTarget (çanta vb.).
    // M48: uzaklık tek bir noktadan değil, ruhun boyunca uzanan dikey "erişim çizgisinden" ölçülür; ruh yerdekilere
    // de uzanır. M50'den beri ruhun kökü ayak hizasında: çizgi ayakların biraz altından başa kadar.
    // Eşyada F minigame'i açar (MinigameHost MinigameOpened'ı dinler, bitince Secure() ve
    // RaiseMinigameClosed çağırır). Diğer hedeflerde hedefin Interact() metodunu çağırır.
    public class SpiritInteractor : MonoBehaviour
    {
        [SerializeField] float range = 1.2f;
        [Tooltip("M48/M50: erişim çizgisi ruhun kökünden (ayaklarından) bu kadar aşağı uzanır (m)")]
        [SerializeField] float reachDown = 0.35f;
        [Tooltip("Erişim çizgisinin kökten yukarı uzanması (m; yaklaşık baş hizası)")]
        [SerializeField] float reachUp = 1.15f;

        // Ruha en yakın, menzildeki sabitlenmemiş eşya (yoksa null). Vurgu okur.
        public static SecurableItem Nearest { get; private set; }
        // Eşyadan daha yakın bir ISpiritTarget varsa o (yoksa null)
        public static ISpiritTarget NearestTarget { get; private set; }
        // HUD'da gösterilecek ipucunun texts.csv anahtarı (yoksa null)
        public static string PromptKey { get; private set; }
        // Berke'nin play testi: "F" ipucu hedefin üstünde çıksın. İpucunun dünya konumu (eşyanın tepesi ya da hedef
        // noktasının biraz üstü); ipucu yoksa null. SpiritHud ekrana çevirir.
        public static Vector3? PromptAnchor { get; private set; }

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
                PromptAnchor = target.Point + Vector3.up * 0.6f;
            }
            else if (item)
            {
                Nearest = item;
                PromptKey = "prompt.secure";
                PromptAnchor = TopOf(item);
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
                float distance = ReachDistance(position, item);
                if (distance > range || distance >= bestDistance) continue;
                best = item;
                bestDistance = distance;
            }
            return bestDistance;
        }

        float FindNearestTarget(Vector3 position, out ISpiritTarget best)
        {
            best = null;
            float bestDistance = float.MaxValue;
            foreach (var target in SpiritTargets.All)
            {
                if (!target.CanInteract) continue;
                float distance = ReachDistance(position, target.Point);
                if (distance > target.Range || distance >= bestDistance) continue;
                best = target;
                bestDistance = distance;
            }
            return bestDistance;
        }

        // X-Y düzleminde noktanın erişim çizgisine (ruhun x'inde, kökün reachDown altından reachUp üstüne) uzaklığı

        float ReachDistance(Vector3 position, Vector3 point)
        {
            float y = Mathf.Clamp(point.y, position.y - reachDown, position.y + reachUp);
            return Vector2.Distance(new Vector2(position.x, y), new Vector2(point.x, point.y));
        }

        // Eşyanın kutusunun erişim çizgisine en yakın noktası üzerinden (Z, 2.5D'de önemsiz)
        float ReachDistance(Vector3 position, SecurableItem item)
        {
            var collider = item.GetComponent<Collider>();
            if (!collider) return ReachDistance(position, item.transform.position);
            var bounds = collider.bounds;
            // Çizginin kutuya en yakın yüksekliği, sonra o noktaya kutunun en yakın noktası
            float y = Mathf.Clamp(bounds.center.y, position.y - reachDown, position.y + reachUp);
            var closest = bounds.ClosestPoint(new Vector3(position.x, y, bounds.center.z));
            return ReachDistance(position, closest);
        }


        // Eşyanın görünen kutusunun tepesinin ortası (biraz yukarıda); kutu yoksa eşyanın 1,2 m üstü
        static Vector3 TopOf(SecurableItem item)
        {
            var collider = item.GetComponent<Collider>();
            if (!collider) return item.transform.position + Vector3.up * 1.2f;
            var bounds = collider.bounds;
            return new Vector3(bounds.center.x, bounds.max.y + 0.15f, bounds.min.z);
        }

        static void Clear()
        {
            Nearest = null;
            NearestTarget = null;
            PromptKey = null;
            PromptAnchor = null;
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Clear();
    }
}
