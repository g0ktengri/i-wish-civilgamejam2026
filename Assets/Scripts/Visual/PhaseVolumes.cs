using Deprem.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Deprem.Visual
{
    /// <summary>
    /// GDD "Post-processing": 3 Volume profili ve fazlara göre yumuşak geçiş.
    ///   Gece          : öncü sarsıntı ve ana deprem (elektrik varken)
    ///   Ruh fazı      : soğuk mavi, soluk (Spirit)
    ///   Elektrik kesik: ana deprem bitince (MainQuakeEnded) ve kurtarma fazında
    /// Volume'ları VolumeSetup kurar.
    /// </summary>
    public sealed class PhaseVolumes : MonoBehaviour
    {
        [SerializeField] private Volume night;
        [SerializeField] private Volume spirit;
        [SerializeField] private Volume powerOut;
        [Tooltip("Geçiş süresi (sn)")]
        [SerializeField] private float blendTime = 0.8f;

        public Volume Night => night;
        public Volume Spirit => spirit;
        public Volume PowerOut => powerOut;

        private Volume target;

        public void Configure(Volume nightVolume, Volume spiritVolume, Volume powerOutVolume)
        {
            night = nightVolume;
            spirit = spiritVolume;
            powerOut = powerOutVolume;
        }

        private void Awake()
        {
            SetImmediate(night);
        }

        private void OnEnable()
        {
            GameEvents.PhaseChanged += OnPhaseChanged;
            GameEvents.MainQuakeEnded += OnMainQuakeEnded;
        }

        private void OnDisable()
        {
            GameEvents.PhaseChanged -= OnPhaseChanged;
            GameEvents.MainQuakeEnded -= OnMainQuakeEnded;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Foreshock:
                case GamePhase.MainQuake:
                    target = night;
                    break;
                case GamePhase.Spirit:
                    target = spirit;
                    break;
                case GamePhase.Rescue:
                    target = powerOut;
                    break;
            }
        }

        private void OnMainQuakeEnded()
        {
            target = powerOut;
        }

        /// <summary>Geçişsiz, anında (sahne açılışı ve önizleme)</summary>
        public void SetImmediate(Volume volume)
        {
            target = volume;
            foreach (var v in new[] { night, spirit, powerOut })
                if (v != null) v.weight = v == volume ? 1f : 0f;
        }

        private void Update()
        {
            float step = blendTime > 0f ? Time.deltaTime / blendTime : 1f;
            foreach (var v in new[] { night, spirit, powerOut })
                if (v != null) v.weight = Mathf.MoveTowards(v.weight, v == target ? 1f : 0f, step);
        }
    }
}
