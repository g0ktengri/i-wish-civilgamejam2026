using Deprem.Core;
using Deprem.Localization;
using UnityEngine;

namespace Deprem.Family
{
    /// <summary>
    /// Deprem kabininin ne olduğu anlaşılsın diye: kapının üstünde "DEPREM KABİNİ" tabelası (texts.csv),
    /// kurtarma fazında yeşil ışık ve kabinin ön yüzünde nabız gibi parlama. HouseBuilder kabine ekler.
    /// </summary>
    public sealed class CabinBeacon : MonoBehaviour
    {
        [SerializeField] private TextMesh sign;
        [SerializeField] private string signKey = "cabin.sign";
        [SerializeField] private Light beacon;
        [Tooltip("Ön yüz materyali _EMISSION açık olmalı (HouseBuilder açar)")]
        [SerializeField] private Renderer body;
        [SerializeField] private int frontMaterialIndex;
        [SerializeField] private Color glowColor = new Color(0.35f, 1f, 0.5f);
        [SerializeField] private float glowStrength = 0.6f;
        [SerializeField] private float beaconIntensity = 2.5f;
        [SerializeField] private float pulseSpeed = 3f;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock block;

        public void Configure(TextMesh signText, Light beaconLight, Renderer bodyRenderer, int frontIndex)
        {
            sign = signText;
            beacon = beaconLight;
            body = bodyRenderer;
            frontMaterialIndex = frontIndex;
        }

        private void Start()
        {
            if (sign != null) sign.text = Texts.Get(signKey);
            Apply(0f, false);
        }

        private void Update()
        {
            bool active = GameState.Phase == GamePhase.Rescue;
            float pulse = active ? 0.5f * (1f + Mathf.Sin(Time.time * pulseSpeed)) : 0f;
            Apply(pulse, active);
        }

        private void Apply(float pulse, bool active)
        {
            if (beacon != null)
            {
                beacon.enabled = active;
                beacon.intensity = beaconIntensity * (0.4f + 0.6f * pulse);
            }
            if (body == null) return;
            block ??= new MaterialPropertyBlock();
            body.GetPropertyBlock(block, frontMaterialIndex);
            block.SetColor(EmissionColor, glowColor * (glowStrength * pulse));
            body.SetPropertyBlock(block, frontMaterialIndex);
        }
    }
}
