using System.Collections;
using Deprem.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace Deprem.Player
{
    /// <summary>
    /// Takip kamerasının hedefi: ruh fazında ruhu, diğer fazlarda oyuncuyu takip eder.
    /// Ruh objesini Berke'nin SpiritPhase'i faz başında "Spirit" adıyla kurar (ENTEGRASYON.md B2).
    /// Sinematikler (intro, giriş kapısı yakın çekimi) Focus ile kamerayı başka bir noktaya götürüp yaklaştırır,
    /// Release ile geri verir.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public sealed class PlayerCameraTarget : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private string spiritObjectName = "Spirit";
        [Tooltip("Yakınlaşma / uzaklaşma hızı (görüş açısı)")]
        [SerializeField] private float zoomSpeed = 2.5f;

        private static PlayerCameraTarget instance;
        private CinemachineCamera vcam;
        private float baseFov;
        private float targetFov;
        private bool focused;

        private void Awake()
        {
            instance = this;
            vcam = GetComponent<CinemachineCamera>();
            if (player == null) player = vcam.Follow;
            baseFov = targetFov = vcam.Lens.FieldOfView;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnEnable() => GameEvents.PhaseChanged += HandlePhaseChanged;
        private void OnDisable() => GameEvents.PhaseChanged -= HandlePhaseChanged;

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (focused) return; // sinematik bitince Release doğru hedefe döner
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

        private void Update()
        {
            var lens = vcam.Lens;
            if (Mathf.Abs(lens.FieldOfView - targetFov) < 0.01f) return;
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFov, 1f - Mathf.Exp(-zoomSpeed * Time.unscaledDeltaTime));
            vcam.Lens = lens;
        }

        /// <summary>Sinematik: kamera hedefe kayar; fovScale &lt; 1 yaklaştırır, &gt; 1 uzaklaştırır</summary>
        public static void Focus(Transform target, float fovScale)
        {
            if (instance == null || target == null) return;
            instance.StopAllCoroutines();
            instance.focused = true;
            instance.vcam.Follow = target;
            instance.targetFov = instance.baseFov * fovScale;
        }

        /// <summary>Sinematik bitti: fazın hedefine (oyuncu ya da ruh) ve normal görüş açısına döner</summary>
        public static void Release()
        {
            if (instance == null) return;
            instance.focused = false;
            instance.targetFov = instance.baseFov;
            instance.HandlePhaseChanged(GameState.Phase);
        }
    }
}
