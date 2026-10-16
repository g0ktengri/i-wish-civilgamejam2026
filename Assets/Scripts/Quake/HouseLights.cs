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
        [Tooltip("Tavandaki lamba gövdeleri: elektrik varken parlak, kesilince sönük materyal")]
        [SerializeField] private Renderer[] lampFixtures;
        [SerializeField] private Material fixtureOn;
        [SerializeField] private Material fixtureOff;
        [Tooltip("Sarsıntı en güçlüyken lambanın kısılabileceği oran")]
        [SerializeField] private float flickerAmount = 0.6f;

        public bool PowerOn { get; private set; } = true;

        private float[] baseIntensities;
        private float[] emergencyBaseIntensities;

        public void Configure(Light[] lampLights, Light[] emergency, Renderer[] fixtures, Material on, Material off)
        {
            lamps = lampLights;
            emergencyLights = emergency;
            lampFixtures = fixtures;
            fixtureOn = on;
            fixtureOff = off;
        }

        private void Awake()
        {
            baseIntensities = new float[lamps.Length];
            for (int i = 0; i < lamps.Length; i++) baseIntensities[i] = lamps[i].intensity;
            emergencyBaseIntensities = new float[emergencyLights.Length];
            for (int i = 0; i < emergencyLights.Length; i++) emergencyBaseIntensities[i] = emergencyLights[i].intensity;
            SetPower(true);
        }

        /// <summary>Acil durum ışıklarını nabız gibi attırır (0 = sabit, 1 = en güçlü). Kurtarma süresi azalırken.</summary>
        public void PulseEmergency(float amount)
        {
            if (PowerOn || emergencyBaseIntensities == null) return;
            float pulse = 1f - amount * 0.5f * (1f + Mathf.Sin(Time.time * Mathf.Lerp(3f, 10f, amount)));
            for (int i = 0; i < emergencyLights.Length; i++)
                emergencyLights[i].intensity = emergencyBaseIntensities[i] * pulse;
        }

        public void SetPower(bool on)
        {
            PowerOn = on;
            foreach (var lamp in lamps) lamp.enabled = on;
            foreach (var light in emergencyLights) light.enabled = !on;
            if (lampFixtures != null)
                foreach (var fixture in lampFixtures)
                    if (fixture != null) fixture.sharedMaterial = on ? fixtureOn : fixtureOff;
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
