using System.Collections.Generic;
using UnityEngine;

namespace Deprem.Visual
{
    /// <summary>
    /// 2.5D kenar ışığı: düz pixel art karakterin, sahnedeki yakın ışıklara bakan kenarında 2 piksellik parlak şerit
    /// (Deprem/RimLight). Karakterin quad'ının önünde aynı kareyi gösteren eklemeli bir kopya kurar; her kare
    /// gövdenin dokusunu (PlayerVisual'ın taktığı kareyi) kopyalar, ışık yönünü ve rengini hesaplar.
    /// Karakterin kendi ışıkları (el feneri, gece görüşü) sayılmaz. Doku yoksa (yer tutucu) çalışmaz.
    /// PlayerSetup kurar.
    /// </summary>
    [DefaultExecutionOrder(100)] // PlayerVisual kareyi taktıktan sonra
    public sealed class CharacterRimLight : MonoBehaviour
    {
        [SerializeField] private Renderer body;
        [SerializeField] private Material rimMaterial;
        [Tooltip("Kenar şeridinin kalınlığı (doku pikseli)")]
        [SerializeField] private float pixels = 2f;
        [Tooltip("Işık gücünü kenar parlaklığına çeviren çarpan")]
        [SerializeField] private float gain = 0.8f; // Berke'nin play testi: 0,35'te pek seçilmiyordu
        [SerializeField] private float maxStrength = 2.2f;
        [Tooltip("Sahnedeki ışık listesinin yenilenme aralığı (sn)")]
        [SerializeField] private float refreshInterval = 0.5f;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int RimDirId = Shader.PropertyToID("_RimDir");
        private static readonly int RimTexelId = Shader.PropertyToID("_RimTexel");
        private static readonly int RimPixelsId = Shader.PropertyToID("_RimPixels");

        private readonly List<Light> lights = new List<Light>();
        private MeshRenderer rim;
        private MaterialPropertyBlock bodyBlock;
        private MaterialPropertyBlock rimBlock;
        private float nextRefresh;

        public void Configure(Renderer bodyRenderer, Material material)
        {
            body = bodyRenderer;
            rimMaterial = material;
        }

        private void Start()
        {
            if (body == null || rimMaterial == null) return;
            var filter = body.GetComponent<MeshFilter>();
            if (filter == null) return;

            // Gövdenin hemen önünde (kameraya doğru) aynı mesh
            var go = new GameObject("RimLight");
            go.transform.SetParent(body.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.002f);
            go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            rim = go.AddComponent<MeshRenderer>();
            rim.sharedMaterial = rimMaterial;
            rim.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rim.receiveShadows = false;
            bodyBlock = new MaterialPropertyBlock();
            rimBlock = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (rim == null) return;

            body.GetPropertyBlock(bodyBlock);
            var texture = bodyBlock.GetTexture(BaseMapId);
            if (texture == null && body.sharedMaterial != null) texture = body.sharedMaterial.GetTexture(BaseMapId);
            if (texture == null || !body.enabled || !body.gameObject.activeInHierarchy)
            {
                rim.enabled = false;
                return;
            }

            if (Time.time >= nextRefresh) RefreshLights();
            ComputeLight(out var worldDir, out var color, out float strength);
            if (strength <= 0.01f)
            {
                rim.enabled = false;
                return;
            }

            // Dünya yönünü quad'ın yerel UV yönüne çevir: yön çevirme (negatif ölçek) ve yatma (dönüş) dahil
            var local = body.transform.InverseTransformDirection(new Vector3(worldDir.x, worldDir.y, 0f));
            var uvDir = new Vector2(local.x, local.y);
            if (uvDir.sqrMagnitude < 1e-6f)
            {
                rim.enabled = false;
                return;
            }
            uvDir.Normalize();

            rim.enabled = true;
            rimBlock.SetTexture(BaseMapId, texture);
            rimBlock.SetColor(RimColorId, color * Mathf.Min(strength, maxStrength));
            rimBlock.SetVector(RimDirId, uvDir);
            rimBlock.SetVector(RimTexelId, new Vector4(1f / texture.width, 1f / texture.height, 0f, 0f));
            rimBlock.SetFloat(RimPixelsId, pixels);
            rim.SetPropertyBlock(rimBlock);
        }

        private void RefreshLights()
        {
            nextRefresh = Time.time + refreshInterval;
            lights.Clear();
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (!light.transform.IsChildOf(transform)) lights.Add(light);
        }

        // Yakın ışıkların ağırlıklı X-Y yönü (karakterden ışığa) ve rengi
        private void ComputeLight(out Vector2 direction, out Color color, out float strength)
        {
            var center = body.bounds.center;
            var sum = Vector2.zero;
            var colorSum = Color.black;
            float weightSum = 0f;

            foreach (var light in lights)
            {
                if (light == null || !light.enabled || !light.gameObject.activeInHierarchy || light.intensity <= 0f) continue;

                Vector2 toLight;
                float weight;
                if (light.type == LightType.Directional)
                {
                    var d = -light.transform.forward;
                    toLight = new Vector2(d.x, d.y);
                    weight = light.intensity * toLight.magnitude * 0.5f;
                }
                else
                {
                    var delta = light.transform.position - center;
                    toLight = new Vector2(delta.x, delta.y);
                    float distance = delta.magnitude;
                    if (distance >= light.range) continue;
                    float falloff = 1f - distance / light.range;
                    weight = light.intensity * falloff * falloff;
                }
                if (toLight.sqrMagnitude < 1e-4f || weight <= 0f) continue;

                sum += toLight.normalized * weight;
                colorSum += light.color * weight;
                weightSum += weight;
            }

            direction = sum.sqrMagnitude > 1e-6f ? sum.normalized : Vector2.zero;
            color = weightSum > 0f ? colorSum / weightSum : Color.black;
            color.a = 0f;
            strength = sum.magnitude * gain;
        }
    }
}
