using System.Collections.Generic;
using Deprem.Core;
using Deprem.Player;
using UnityEngine;

namespace Deprem.Family
{
    /// <summary>
    /// Kurtarılan aile üyeleri oyuncunun izinden (merdiven ve zıplamalar dahil) sırayla yürür.
    /// Kabin girişinde F: takip edenler kabine girer; herkes kabindeyse oyuncu da girer ve oyun kazanılır.
    /// </summary>
    public sealed class FamilyDirector : MonoBehaviour
    {
        [Tooltip("Takip edenler arası mesafe (iz boyunca, m)")]
        [SerializeField] private float spacing = 0.9f;
        [Tooltip("Takip edenlerin hedefe yetişme hızı (m/s)")]
        [SerializeField] private float catchUpSpeed = 6f;
        [Tooltip("Her takipçi bir öncekinin biraz arkasında durur (Z)")]
        [SerializeField] private float depthOffset = 0.12f;
        [SerializeField] private float trailStep = 0.05f;
        [SerializeField] private int maxTrailPoints = 600;

        [Header("Kabin")]
        [SerializeField] private string cabinEntranceMarker = "Marker_CabinEntrance";
        [SerializeField] private float cabinRange = 1.2f;
        [Tooltip("Kabine girenlerin konulduğu nokta (kabinin içi, girişe göre)")]
        [SerializeField] private Vector3 cabinInsideOffset = new Vector3(0f, 0.2f, 1.75f);

        private readonly List<Vector3> trail = new List<Vector3>();
        private readonly List<FamilyMember> followers = new List<FamilyMember>();
        private PlayerController player;
        private CabinEntrance cabin;

        public IReadOnlyList<FamilyMember> Followers => followers;
        public bool PlayerInCabin { get; private set; }

        private void Awake()
        {
            Initialize();
        }

        /// <summary>Awake'te çağrılır; edit mode testleri için ayrıca çağrılabilir.</summary>
        public void Initialize()
        {
            if (cabin != null) return;
            player = FindFirstObjectByType<PlayerController>();
            var marker = GameObject.Find(cabinEntranceMarker);
            cabin = new CabinEntrance(this, marker != null ? marker.transform.position : transform.position);
        }

        private void OnEnable()
        {
            EnsureRegistered();
            GameEvents.FamilyMemberRescued += OnMemberRescued;
        }

        private void OnDisable()
        {
            Interactables.Unregister(cabin);
            GameEvents.FamilyMemberRescued -= OnMemberRescued;
        }

        public void EnsureRegistered()
        {
            Initialize();
            Interactables.Register(cabin);
        }

        public void OnMemberRescued(FamilyMemberId id)
        {
            var member = FamilyMember.Find(id);
            if (member != null && !followers.Contains(member)) followers.Add(member);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>İzi kaydeder ve takipçileri yerleştirir. Update'ten çağrılır; testlerde elle çağrılabilir.</summary>
        public void Tick(float dt)
        {
            if (player == null || PlayerInCabin) return;

            var p = player.transform.position;
            if (trail.Count == 0 || (trail[trail.Count - 1] - p).sqrMagnitude > trailStep * trailStep)
            {
                trail.Add(p);
                if (trail.Count > maxTrailPoints) trail.RemoveAt(0);
            }

            for (int i = 0; i < followers.Count; i++)
            {
                var target = PointBehind((i + 1) * spacing) + Vector3.forward * depthOffset * (i + 1);
                var member = followers[i];
                member.SetFollowPosition(Vector3.MoveTowards(member.transform.position, target, catchUpSpeed * dt));
            }
        }

        /// <summary>İz boyunca oyuncunun bu kadar gerisindeki nokta</summary>
        private Vector3 PointBehind(float distance)
        {
            float remaining = distance;
            for (int i = trail.Count - 1; i > 0; i--)
            {
                float segment = Vector3.Distance(trail[i], trail[i - 1]);
                if (segment >= remaining) return Vector3.Lerp(trail[i], trail[i - 1], remaining / segment);
                remaining -= segment;
            }
            return trail.Count > 0 ? trail[0] : player.transform.position;
        }

        private bool EveryoneInCabin()
        {
            foreach (var member in FamilyMember.All)
                if (member.isActiveAndEnabled && member.State != FamilyState.InCabin) return false;
            return true;
        }

        private void EnterCabin()
        {
            var inside = cabin.InteractionPoint + cabinInsideOffset;
            foreach (var member in followers) member.EnterCabin(inside);
            followers.Clear();

            if (!EveryoneInCabin()) return;
            PlayerInCabin = true;
            player.LieDown(inside); // kabinin içinde, görünmez ve hareketsiz
            GameEvents.RaisePlayerEnteredCabin();
            GameState.WinLoop();
        }

        private sealed class CabinEntrance : IInteractable, IPromptSource
        {
            private readonly FamilyDirector director;

            public string PromptKey => director.followers.Count > 0 ? "prompt.cabin_family" : "prompt.cabin_enter";

            public CabinEntrance(FamilyDirector director, Vector3 point)
            {
                this.director = director;
                InteractionPoint = point;
            }

            public Vector3 InteractionPoint { get; }
            public float InteractionRange => director.cabinRange;

            public bool CanInteract(PlayerController player)
            {
                return GameState.Phase == GamePhase.Rescue && (director.followers.Count > 0 || director.EveryoneInCabin());
            }

            public void Interact(PlayerController player)
            {
                director.EnterCabin();
            }
        }
    }
}
