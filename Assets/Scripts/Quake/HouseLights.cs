using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Evin tavan lambaları ve acil durum ışıkları. Sarsıntıda titrer; ana deprem bitince
    /// elektrik kesilir, ev karanlık ve loş, soğuk bir ışıkla kalır ("emergency" ışıklar; kırmızı değil).
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
        [Tooltip("Önden gelen dolgu ışığı: elektrik varken lambayla aynı sıcak ton, kesilince soğuk ay ışığı")]
        [SerializeField] private Light fill;
        [SerializeField] private Color fillPowerOn = new Color(1f, 0.88f, 0.74f);
        [SerializeField] private Color fillPowerOff = new Color(0.8f, 0.79f, 0.78f); // Play testi 27: mavi değil, nötr gece
        [Tooltip("Elektrik kesikken dolgu ışığının oranı (karanlık gerçekten karanlık olsun)")]
        [SerializeField] private float fillPowerOffScale = 0.25f;
        [Tooltip("Elektrik kesikken ortam ışığının oranı")]
        [SerializeField] private float ambientPowerOffScale = 0.2f;
        [Tooltip("Sarsıntı en güçlüyken lambanın kısılabileceği oran")]
        [SerializeField] private float flickerAmount = 0.6f;
        [Tooltip("Sarsıntı en güçlüyken lambanın sallanma genliği (m)")]
        [SerializeField] private float swayAmount = 0.25f;

        [Header("Avizeler")]
        [Tooltip("Tavandan sarkan avizeler (pivot tavanda). Sarsıntıda sarkaç gibi sallanır, ışığı da onunla gider; " +
                 "abajur, ampul, hale ve huzme parlaklığı (_Glow) elektrik ve titremeyle değişir")]
        [SerializeField] private Transform[] pendants;
        [Tooltip("Sarsıntı en güçlüyken sallanma açısı (derece)")]
        [SerializeField] private float swingDegrees = 16f;
        [Tooltip("Sarkacın açısal hızı (rad/sn); 0,6 m'lik avize için ~4")]
        [SerializeField] private float swingSpeed = 4f;
        [Tooltip("Sarsıntı bitince sallanmanın sönme hızı (1/sn)")]
        [SerializeField] private float swingDamping = 0.45f;

        public bool PowerOn { get; private set; } = true;

        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private Light[] pendantLights;
        private float[] pendantBaseIntensities;
        private Renderer[][] pendantRenderers;
        private bool[] lampOnPendant;
        private MaterialPropertyBlock glowBlock;
        private float swing;

        private float[] baseIntensities;
        private float[] emergencyBaseIntensities;
        private Vector3[] lampBasePositions;
        private float fillBaseIntensity;
        private Color ambientBase;

        public void Configure(Light[] lampLights, Light[] emergency, Renderer[] fixtures, Material on, Material off, Light fillLight)
        {
            lamps = lampLights;
            emergencyLights = emergency;
            lampFixtures = fixtures;
            fixtureOn = on;
            fixtureOff = off;
            fill = fillLight;
        }

        public void ConfigurePendants(Transform[] pendantRoots) => pendants = pendantRoots;

        // Avizelerin ışığı ve _Glow kullanan parçaları (edit mode önizlemesinde Awake'ten önce de çağrılır)
        private void EnsurePendants()
        {
            if (pendantLights != null || pendants == null) return;
            pendantLights = new Light[pendants.Length];
            pendantBaseIntensities = new float[pendants.Length];
            pendantRenderers = new Renderer[pendants.Length][];
            for (int i = 0; i < pendants.Length; i++)
            {
                if (pendants[i] == null) continue;
                pendantLights[i] = pendants[i].GetComponentInChildren<Light>(true);
                pendantBaseIntensities[i] = pendantLights[i] != null ? pendantLights[i].intensity : 0f;
                var glowing = new System.Collections.Generic.List<Renderer>();
                foreach (var r in pendants[i].GetComponentsInChildren<Renderer>(true))
                    if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(GlowId)) glowing.Add(r);
                pendantRenderers[i] = glowing.ToArray();
            }
        }

        private void SetPendantGlow(int i, float glow)
        {
            if (pendantRenderers[i] == null) return;
            glowBlock ??= new MaterialPropertyBlock();
            foreach (var r in pendantRenderers[i])
            {
                r.GetPropertyBlock(glowBlock);
                glowBlock.SetFloat(GlowId, glow);
                r.SetPropertyBlock(glowBlock);
            }
        }

        // Avize sarkaç gibi sallanır: sarsıntı büyürken genlik hızla artar, bitince yavaşça söner (elektrik
        // kesikken de). Parlaklık ışığın o anki gücünü (titreme) izler.
        private void Update()
        {
            EnsurePendants();
            if (pendants == null || pendants.Length == 0) return;
            float shake = QuakeCameraShake.Intensity;
            swing = shake > swing ? Mathf.MoveTowards(swing, shake, Time.deltaTime * 2.5f) : swing * Mathf.Exp(-swingDamping * Time.deltaTime);
            for (int i = 0; i < pendants.Length; i++)
            {
                if (pendants[i] == null) continue;
                float phase = Time.time * swingSpeed + i * 1.3f;
                float angle = Mathf.Sin(phase) * swingDegrees * swing;
                pendants[i].localRotation = Quaternion.Euler(Mathf.Sin(phase * 0.7f + 0.5f) * angle * 0.3f, 0f, angle);
                float glow = PowerOn && pendantLights[i] != null && pendantBaseIntensities[i] > 0f
                    ? pendantLights[i].intensity / pendantBaseIntensities[i] : PowerOn ? 1f : 0f;
                SetPendantGlow(i, glow);
            }
        }

        private void Awake()
        {
            baseIntensities = new float[lamps.Length];
            lampBasePositions = new Vector3[lamps.Length];
            for (int i = 0; i < lamps.Length; i++)
            {
                baseIntensities[i] = lamps[i].intensity;
                lampBasePositions[i] = lamps[i].transform.localPosition;
            }
            emergencyBaseIntensities = new float[emergencyLights.Length];
            for (int i = 0; i < emergencyLights.Length; i++) emergencyBaseIntensities[i] = emergencyLights[i].intensity;
            fillBaseIntensity = fill != null ? fill.intensity : 0f;
            ambientBase = RenderSettings.ambientLight;
            EnsurePendants();
            lampOnPendant = new bool[lamps.Length];
            if (pendants != null)
                for (int i = 0; i < lamps.Length; i++)
                    foreach (var pendant in pendants)
                        if (pendant != null && lamps[i].transform.IsChildOf(pendant)) lampOnPendant[i] = true;
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
            if (fill != null)
            {
                fill.color = on ? fillPowerOn : fillPowerOff;
                // Awake'ten önce çağrılırsa (edit mode önizleme) taban değeri o anki değerdir
                if (fillBaseIntensity <= 0f) fillBaseIntensity = fill.intensity;
                fill.intensity = on ? fillBaseIntensity : fillBaseIntensity * fillPowerOffScale;
            }
            if (ambientBase == default) ambientBase = RenderSettings.ambientLight;
            RenderSettings.ambientLight = on ? ambientBase : ambientBase * ambientPowerOffScale;
            EnsurePendants();
            if (pendantRenderers != null)
                for (int i = 0; i < pendantRenderers.Length; i++) SetPendantGlow(i, on ? 1f : 0f);
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
                // Lamba sallanır: ışık kayar, gölgeler odada gidip gelir (avizedeki ışık avizeyle sallanır, Update)
                if (lampOnPendant != null && lampOnPendant[i]) continue;
                var sway = new Vector3(Mathf.Sin(Time.time * 6f + i * 1.7f), 0f, Mathf.Cos(Time.time * 4.5f + i) * 0.5f);
                lamps[i].transform.localPosition = lampBasePositions[i] + sway * (swayAmount * intensity);
            }
        }
    }
}
