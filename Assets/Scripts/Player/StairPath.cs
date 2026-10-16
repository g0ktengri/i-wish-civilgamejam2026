using UnityEngine;

namespace Deprem.Player
{
    /// <summary>
    /// Merdiven: alt veya üst uçta F'ye basınca karakter basamaklar boyunca taşınır.
    /// Basamaklar karakter düzleminin arkasındaki şeritte olduğu için normal yürüyüşle çıkılmaz.
    /// Noktalar HouseBuilder tarafından doldurulur: [0] alt uç (karakter düzlemi) ... [son] üst uç (karakter düzlemi).
    /// Aile üyeleri de aynı noktaları (Points) kullanabilir.
    /// </summary>
    public sealed class StairPath : MonoBehaviour
    {
        [SerializeField] private Vector3[] points;
        [SerializeField] private float climbSpeed = 2.2f;
        [SerializeField] private float interactionRange = 0.8f;

        public Vector3[] Points => points;
        public float ClimbSpeed => climbSpeed;

        private Endpoint lower;
        private Endpoint upper;

        public void Configure(Vector3[] worldPoints)
        {
            points = worldPoints;
        }

        private void OnEnable()
        {
            EnsureRegistered();
        }

        private void OnDisable()
        {
            if (lower != null) Interactables.Unregister(lower);
            if (upper != null) Interactables.Unregister(upper);
        }

        /// <summary>OnEnable'da çağrılır; edit mode testleri için ayrıca çağrılabilir.</summary>
        public void EnsureRegistered()
        {
            if (points == null || points.Length < 2) return;
            lower ??= new Endpoint(this, true);
            upper ??= new Endpoint(this, false);
            Interactables.Register(lower);
            Interactables.Register(upper);
        }

        public Vector3[] GetRoute(bool up)
        {
            var route = (Vector3[])points.Clone();
            if (!up) System.Array.Reverse(route);
            return route;
        }

        private sealed class Endpoint : IInteractable, IPromptSource
        {
            private readonly StairPath stairs;
            private readonly bool isBottom;

            public Endpoint(StairPath stairs, bool isBottom)
            {
                this.stairs = stairs;
                this.isBottom = isBottom;
            }

            public string PromptKey => isBottom ? "prompt.stairs_up" : "prompt.stairs_down";
            public Vector3 InteractionPoint => isBottom ? stairs.points[0] : stairs.points[stairs.points.Length - 1];
            public float InteractionRange => stairs.interactionRange;

            public bool CanInteract(PlayerController player)
            {
                return stairs.isActiveAndEnabled;
            }

            public void Interact(PlayerController player)
            {
                player.FollowPath(stairs.GetRoute(isBottom), stairs.climbSpeed);
            }
        }
    }
}
