using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Evin tavan lambaları ve acil durum ışıkları. Sarsıntıda titrer; ana deprem bitince
    /// elektrik kesilir, ev karanlık ve kırmızı acil durum ışığına geçer (GDD).
    /// Diziler HouseBuilder tarafından doldurulur.
    /// </summary>
    public sealed class HouseLights : MonoBehaviour
    {
        [SerializeField] private Light[] lamps;
        [SerializeField] private Light[] emergencyLights;
        [Tooltip("Sarsıntı en güçlüyken lambanın kısılabileceği oran")]
        [SerializeField] private float flickerAmount = 0.6f;

        public bool PowerOn { get; private set; } = true;

        private float[] baseIntensities;

        public void Configure(Light[] lampLights, Light[] emergency)
        {
            lamps = lampLights;
            emergencyLights = emergency;
        }

        private void Awake()
        {
            baseIntensities = new float[lamps.Length];
            for (int i = 0; i < lamps.Length; i++) baseIntensities[i] = lamps[i].intensity;
            SetPower(true);
        }

        public void SetPower(bool on)
        {
            PowerOn = on;
            foreach (var lamp in lamps) lamp.enabled = on;
            foreach (var light in emergencyLights) light.enabled = !on;
            if (on) Flicker(0f);
        }

        /// <summary>0 = sabit, 1 = en güçlü titreme</summary>
        public void Flicker(float intensity)
        {
            if (!PowerOn || baseIntensities == null) return;
            for (int i = 0; i < lamps.Length; i++)
            {
                float dim = intensity > 0f ? Random.Range(0f, flickerAmount * intensity) : 0f;
                lamps[i].intensity = baseIntensities[i] * (1f - dim);
            }
        }
    }
}
