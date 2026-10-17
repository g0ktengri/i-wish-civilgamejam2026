using Deprem.Audio;
using UnityEngine;

namespace Deprem.Player
{
    /// <summary>Ayak sesleri ve yere iniş (ses sistemi yoksa sessiz)</summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerFootsteps : MonoBehaviour
    {
        [Tooltip("Koşarken iki adım arası (sn)")]
        [SerializeField] private float stepInterval = 0.32f;
        [SerializeField] private float crouchStepInterval = 0.5f;
        [SerializeField, Range(0f, 1f)] private float stepVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float landVolume = 0.7f;

        private PlayerController player;
        private float nextStep;
        private bool wasGrounded = true;

        private void Awake() => player = GetComponent<PlayerController>();

        private void Update()
        {
            bool grounded = player.IsGrounded && !player.IsLying;
            if (grounded && !wasGrounded) Sfx.PlayAt("player.land", transform.position, landVolume);
            wasGrounded = grounded;

            bool moving = grounded && Mathf.Abs(player.MoveInput) > 0.1f;
            if (!moving)
            {
                nextStep = Time.time + 0.08f; // yürümeye başlayınca ilk adım hemen
                return;
            }
            if (Time.time < nextStep) return;
            Sfx.PlayAt("player.step", transform.position, stepVolume);
            nextStep = Time.time + (player.IsCrouching ? crouchStepInterval : stepInterval);
        }
    }
}
