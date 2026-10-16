using Unity.Cinemachine;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Takip kamerasına eklenen sarsıntı. Şiddeti QuakeDirector ayarlar (0 = yok, 1 = ana deprem).
    /// </summary>
    public sealed class QuakeCameraShake : CinemachineExtension
    {
        public static float Intensity { get; set; }

        [SerializeField] private float amplitude = 0.2f;
        [SerializeField] private float frequency = 9f;
        [SerializeField] private float rollDegrees = 1.2f;

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Noise || Intensity <= 0f) return;

            float t = Time.time * frequency;
            var offset = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f);
            state.PositionCorrection += offset * (2f * amplitude * Intensity);
            float roll = (Mathf.PerlinNoise(t, t) - 0.5f) * 2f * rollDegrees * Intensity;
            state.OrientationCorrection *= Quaternion.Euler(0f, 0f, roll);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Intensity = 0f;
        }
    }
}
