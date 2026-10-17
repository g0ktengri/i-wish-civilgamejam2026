using Deprem.Audio;
using UnityEngine;

namespace Deprem.Player
{
    /// <summary>Ayak sesleri ve yere iniş (ses sistemi yoksa sessiz). Merdivende de (StairPath) adım atar.</summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerFootsteps : MonoBehaviour
    {
        [Tooltip("Koşarken iki adım arası (sn)")]
        [SerializeField] private float stepInterval = 0.32f;
        [SerializeField] private float crouchStepInterval = 0.5f;
        [Tooltip("Merdivende iki adım arası (sn)")]
        [SerializeField] private float stairStepInterval = 0.3f;
        [Tooltip("Play testi 20: adımlar çok zayıftı")]
        [SerializeField, Range(0f, 1f)] private float stepVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float landVolume = 0.9f;

        private PlayerController player;
        private float nextStep;
        private bool wasGrounded = true;
        private Vector3 lastPosition;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            lastPosition = transform.position;
        }

        private void Update()
        {
            var position = transform.position;
            float speed = Time.deltaTime > 0f ? (position - lastPosition).magnitude / Time.deltaTime : 0f;
            lastPosition = position;

            bool grounded = (player.IsGrounded || player.IsOnPath) && !player.IsLying && !player.IsDead;
            if (grounded && !wasGrounded && !player.IsOnPath) Sfx.Play("player.land", landVolume);
            wasGrounded = grounded;

            // Merdivende girdi yok (yol yürütüyor): hızdan anlaşılır
            bool moving = grounded && (player.IsOnPath ? speed > 0.3f : Mathf.Abs(player.MoveInput) > 0.1f);
            if (!moving)
            {
                nextStep = Time.time + 0.08f; // yürümeye başlayınca ilk adım hemen
                return;
            }
            if (Time.time < nextStep) return;
            Sfx.Play("player.step", stepVolume); // ekrana bağlı: kameradan uzaklık kısmasın
            nextStep = Time.time + (player.IsOnPath ? stairStepInterval : player.IsCrouching ? crouchStepInterval : stepInterval);
        }
    }
}
