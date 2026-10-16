using System.Collections;
using Deprem.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace Deprem.Player
{
    /// <summary>
    /// Takip kamerasının hedefi: ruh fazında ruhu, diğer fazlarda oyuncuyu takip eder.
    /// Ruh objesini Berke'nin SpiritPhase'i faz başında "Spirit" adıyla kurar (ENTEGRASYON.md B2).
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public sealed class PlayerCameraTarget : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private string spiritObjectName = "Spirit";

        private CinemachineCamera vcam;

        private void Awake()
        {
            vcam = GetComponent<CinemachineCamera>();
            if (player == null) player = vcam.Follow;
        }

        private void OnEnable() => GameEvents.PhaseChanged += HandlePhaseChanged;
        private void OnDisable() => GameEvents.PhaseChanged -= HandlePhaseChanged;

        private void HandlePhaseChanged(GamePhase phase)
        {
            StopAllCoroutines();
            if (phase == GamePhase.Spirit) StartCoroutine(FollowSpirit());
            else vcam.Follow = player;
        }

        private IEnumerator FollowSpirit()
        {
            // Ruh objesi aynı event'te kuruluyor; bir kare bekle
            yield return null;
            var spirit = GameObject.Find(spiritObjectName);
            if (spirit != null) vcam.Follow = spirit.transform;
        }
    }
}
