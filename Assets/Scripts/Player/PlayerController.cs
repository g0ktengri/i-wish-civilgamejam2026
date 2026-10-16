using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deprem.Player
{
    /// <summary>
    /// 2.5D karakter kontrolcüsü: A/D yürüme, Space zıplama, S eğilme (GDD "Kontroller").
    /// Karakter Z ekseninde kilitli, karakter düzleminde (Z = 1) yürür. Pivot ayak hizasındadır.
    ///
    /// Diğer sistemler için:
    ///   InputEnabled = false  -> klavye okunmaz (ruh fazı, çök-kapan-tutun, ara sahne)
    ///   SetScriptedInput(...) -> InputEnabled kapalıyken karakteri koddan yürütür (senaryolu döngü 1-2)
    ///   SpeedMultiplier       -> yaralanınca yavaşlama (artçıda yaralanma)
    ///   FollowPath(...)       -> karakteri noktalar boyunca taşır (merdiven)
    ///   Teleport(...)         -> döngü başında yerine koyma
    ///   LieDown / GetUp       -> yatakta uyuma ve kalkma (öncü sarsıntı)
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Yürüme")]
        [SerializeField] private float walkSpeed = 3.2f;
        [SerializeField] private float crouchSpeedMultiplier = 0.5f;
        [SerializeField] private float acceleration = 30f;

        [Header("Zıplama")]
        [SerializeField] private float jumpHeight = 1.1f;
        [SerializeField] private float coyoteTime = 0.1f;
        [SerializeField] private float jumpBufferTime = 0.12f;
        [SerializeField] private float fallGravityMultiplier = 1.8f;

        [Header("Gövde")]
        [SerializeField] private float standingHeight = 1.7f;
        [SerializeField] private float crouchHeight = 1f;
        [SerializeField] private float radius = 0.25f;
        [SerializeField] private float groundCheckDistance = 0.08f;

        [Header("Tuşlar")]
        [SerializeField] private Key leftKey = Key.A;
        [SerializeField] private Key rightKey = Key.D;
        [SerializeField] private Key jumpKey = Key.Space;
        [SerializeField] private Key crouchKey = Key.S;

        public bool InputEnabled { get; set; } = true;
        public float SpeedMultiplier { get; set; } = 1f;
        public bool IsGrounded { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsOnPath => path != null;
        public bool IsLying { get; private set; }
        /// <summary>1 sağ, -1 sol</summary>
        public int Facing { get; private set; } = 1;
        /// <summary>-1..1, animasyon için</summary>
        public float MoveInput { get; private set; }
        public float StandingHeight => standingHeight;
        public float CrouchHeight => crouchHeight;
        public Rigidbody Body => body;

        public event Action Jumped;
        public event Action Landed;

        private Rigidbody body;
        private CapsuleCollider capsule;
        private readonly RaycastHit[] hits = new RaycastHit[8];
        private readonly Collider[] overlaps = new Collider[8];

        private float clock;
        private Vector3 lastSafePosition;
        private float lastGroundedTime = float.NegativeInfinity;
        private float jumpRequestTime = float.NegativeInfinity;
        private float scriptedMove;
        private bool scriptedCrouch;

        private Vector3[] path;
        private int pathIndex;
        private float pathSpeed;
        private Action pathDone;

        private void Awake()
        {
            Initialize();
        }

        /// <summary>Awake'te çağrılır; edit mode testleri için ayrıca çağrılabilir.</summary>
        public void Initialize()
        {
            if (body != null) return;
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();

            body.mass = 70f;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.constraints = PlaneConstraints;

            capsule.radius = radius;
            capsule.direction = 1;
            capsule.sharedMaterial = new PhysicsMaterial("PlayerNoFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            SetCapsuleHeight(standingHeight);
            lastSafePosition = transform.position;
        }

        private static RigidbodyConstraints PlaneConstraints =>
            RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        private void Update()
        {
            if (!InputEnabled || IsOnPath || IsLying) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[jumpKey].wasPressedThisFrame) jumpRequestTime = clock;
        }

        private void FixedUpdate()
        {
            Simulate(Time.fixedDeltaTime);
        }

        /// <summary>Bir fizik adımı. FixedUpdate'ten çağrılır; testlerde elle çağrılabilir.</summary>
        public void Simulate(float dt)
        {
            clock += dt;
            if (IsOnPath)
            {
                StepPath(dt);
                return;
            }
            if (IsLying) return;

            float move;
            bool crouchHeld;
            if (InputEnabled)
            {
                var keyboard = Keyboard.current;
                move = 0f;
                crouchHeld = false;
                if (keyboard != null)
                {
                    if (keyboard[leftKey].isPressed) move -= 1f;
                    if (keyboard[rightKey].isPressed) move += 1f;
                    crouchHeld = keyboard[crouchKey].isPressed;
                }
            }
            else
            {
                move = scriptedMove;
                crouchHeld = scriptedCrouch;
            }

            var velocity = body.linearVelocity;
            UpdateGrounded(velocity.y);
            UpdateCrouch(crouchHeld);

            float speed = walkSpeed * SpeedMultiplier * (IsCrouching ? crouchSpeedMultiplier : 1f);
            velocity.x = Mathf.MoveTowards(velocity.x, move * speed, acceleration * dt);

            bool canJump = !IsCrouching && clock - lastGroundedTime <= coyoteTime;
            if (canJump && clock - jumpRequestTime <= jumpBufferTime)
            {
                velocity.y = Mathf.Sqrt(2f * -Physics.gravity.y * jumpHeight);
                jumpRequestTime = float.NegativeInfinity;
                lastGroundedTime = float.NegativeInfinity;
                IsGrounded = false;
                Jumped?.Invoke();
            }

            if (velocity.y < 0f) velocity.y += Physics.gravity.y * (fallGravityMultiplier - 1f) * dt;
            velocity.z = 0f;
            velocity.x = KeepInsideHouse(velocity.x);
            body.linearVelocity = velocity;

            MoveInput = move;
            if (move > 0.01f) Facing = 1;
            else if (move < -0.01f) Facing = -1;
        }

        /// <summary>
        /// Emniyet: duvarlar ne olursa olsun evin dışına çıkılmaz. Kat sınırına gelince dışarı doğru hız sıfırlanır;
        /// bir yerden düşerse son güvenli (yerde olduğu) noktaya geri konur.
        /// </summary>
        private float KeepInsideHouse(float velocityX)
        {
            var bounds = HouseBounds.Instance;
            if (bounds == null) return velocityX;

            var p = body.position;
            if (p.y < bounds.Floors[0].floorY - 3f)
            {
                Teleport(lastSafePosition);
                return 0f;
            }

            float clamped = bounds.ClampX(p, radius);
            if (!Mathf.Approximately(clamped, p.x))
            {
                bool pushedRight = clamped > p.x; // sol sınırdan içeri itildi
                p.x = clamped;
                body.position = p;
                if ((pushedRight && velocityX < 0f) || (!pushedRight && velocityX > 0f)) velocityX = 0f;
            }
            if (IsGrounded) lastSafePosition = body.position;
            return velocityX;
        }

        public void SetScriptedInput(float move, bool jump = false, bool crouch = false)
        {
            scriptedMove = Mathf.Clamp(move, -1f, 1f);
            scriptedCrouch = crouch;
            if (jump) jumpRequestTime = clock;
        }

        /// <summary>Karakteri fizikten çıkarıp noktalar boyunca sabit hızla taşır, sonunda fiziğe geri verir.</summary>
        public void FollowPath(Vector3[] points, float speed, Action onDone = null)
        {
            if (points == null || points.Length == 0) return;
            path = points;
            pathIndex = 0;
            pathSpeed = speed;
            pathDone = onDone;
            body.linearVelocity = Vector3.zero;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.isKinematic = true;
            SetCapsuleHeight(standingHeight);
            IsCrouching = false;
            MoveInput = 0f;
        }

        /// <summary>Yatağa yatırır: fizik dışı, girdi kapalı.</summary>
        public void LieDown(Vector3 position)
        {
            if (IsOnPath) EndPath(false);
            InputEnabled = false;
            IsLying = true;
            IsCrouching = false;
            SetCapsuleHeight(standingHeight);
            body.linearVelocity = Vector3.zero;
            body.isKinematic = true;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.position = position;
            transform.position = position;
            MoveInput = 0f;
        }

        /// <summary>Yataktan kalkıp karakter düzlemindeki noktaya yürür.</summary>
        public void GetUp(Vector3 standPosition, Action onDone = null)
        {
            if (!IsLying) return;
            IsLying = false;
            FollowPath(new[] { standPosition }, 1.5f, onDone);
        }

        public void Teleport(Vector3 position)
        {
            if (IsOnPath) EndPath(false);
            if (IsLying)
            {
                IsLying = false;
                body.isKinematic = false;
                body.constraints = PlaneConstraints;
            }
            body.position = position;
            transform.position = position;
            body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        private void StepPath(float dt)
        {
            var current = body.position;
            var target = path[pathIndex];
            var next = Vector3.MoveTowards(current, target, pathSpeed * dt);
            float dx = target.x - current.x;
            if (Mathf.Abs(dx) > 0.001f) Facing = dx > 0f ? 1 : -1;
            MoveInput = Facing;
            body.MovePosition(next);

            if ((next - target).sqrMagnitude < 0.0001f)
            {
                pathIndex++;
                if (pathIndex >= path.Length) EndPath(true);
            }
        }

        private void EndPath(bool completed)
        {
            var end = path[path.Length - 1];
            var done = pathDone;
            path = null;
            pathDone = null;
            MoveInput = 0f;
            body.isKinematic = false;
            body.position = end;
            transform.position = end;
            body.linearVelocity = Vector3.zero;
            body.constraints = PlaneConstraints;
            Physics.SyncTransforms();
            if (completed) done?.Invoke();
        }

        private void UpdateGrounded(float verticalVelocity)
        {
            bool wasGrounded = IsGrounded;
            IsGrounded = false;
            if (verticalVelocity <= 0.1f)
            {
                float castRadius = radius * 0.95f;
                var origin = body.position + Vector3.up * (radius + 0.05f);
                int count = Physics.SphereCastNonAlloc(origin, castRadius, Vector3.down, hits,
                    0.05f + (radius - castRadius) + groundCheckDistance, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    if (IsSelf(hits[i].collider) || hits[i].normal.y < 0.5f) continue;
                    IsGrounded = true;
                    break;
                }
            }

            if (IsGrounded) lastGroundedTime = clock;
            if (IsGrounded && !wasGrounded) Landed?.Invoke();
        }

        private void UpdateCrouch(bool held)
        {
            if (held && !IsCrouching)
            {
                IsCrouching = true;
                SetCapsuleHeight(crouchHeight);
            }
            else if (!held && IsCrouching && HasHeadroom())
            {
                IsCrouching = false;
                SetCapsuleHeight(standingHeight);
            }
        }

        /// <summary>Ayağa kalkacak yer var mı (devrilmiş eşyanın altındayken kalkamaz)</summary>
        private bool HasHeadroom()
        {
            var bottom = body.position + Vector3.up * (crouchHeight - radius);
            var top = body.position + Vector3.up * (standingHeight - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius * 0.9f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!IsSelf(overlaps[i])) return false;
            return true;
        }

        private bool IsSelf(Collider other)
        {
            return other == capsule || other.attachedRigidbody == body;
        }

        private void SetCapsuleHeight(float height)
        {
            capsule.height = height;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
        }
    }
}
