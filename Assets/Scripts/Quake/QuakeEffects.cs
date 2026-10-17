using System;
using System.Collections;
using Deprem.Core;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Sarsıntının görsel efektleri (Play testi 3: "depremde efekt az"):
    ///   - her odanın tavanından sarsıntının şiddetine göre toz, güçlüyken sıva parçaları dökülür,
    ///   - devrilen eşya yere vurunca tabanında toz bulutu kalkar.
    /// Şiddeti QuakeDirector verir (SetIntensity); oda tavanlarını ve materyalleri QuakeSetup yazar.
    /// Parçacıklar kare (pixel art); sanatçı dokusu gelince materyallere takılır (Particles/toz_*.png, siva.png).
    /// </summary>
    public sealed class QuakeEffects : MonoBehaviour
    {
        [Serializable]
        public struct Ceiling
        {
            [Tooltip("Tavanın hemen altı, odanın ortası (dünya)")] public Vector3 center;
            [Tooltip("X: genişlik, Y: derinlik (Z)")] public Vector2 size;
        }

        [SerializeField] private Ceiling[] ceilings;
        [SerializeField] private Material dustMaterial;
        [SerializeField] private Material chunkMaterial;

        [Header("Tavandan toz")]
        [Tooltip("Tam şiddette oda başına saniyede toz tanesi")]
        // Berke'nin play testi: toz yetersizdi; oranlar ve boyutlar arttı, tavandan ince toz akıntıları eklendi
        [SerializeField] private float dustRate = 320f;
        [SerializeField] private Color dustColor = new Color(0.8f, 0.77f, 0.7f, 0.6f);
        [Tooltip("Tam şiddette oda başına saniyede iri, yavaş düşen toz bulutu")]
        [SerializeField] private float clumpRate = 24f;
        [SerializeField] private Color clumpColor = new Color(0.78f, 0.75f, 0.68f, 0.28f);
        [Tooltip("Tam şiddette oda başına saniyede sıva parçası (şiddet 0,3'ün üstünde başlar); yere çarpıp seker")]
        [SerializeField] private float chunkRate = 18f;
        [Tooltip("Tam şiddette tavandaki her çatlaktan saniyede akan ince toz")]
        [SerializeField] private float streamRate = 70f;
        [Tooltip("Oda başına tavan çatlağı (ince toz akıntısı) sayısı")]
        [SerializeField] private int streamsPerRoom = 2;
        [Tooltip("Ana depremin ilk vuruşunda oda başına toz patlaması (tane)")]
        [SerializeField] private int burstDust = 260;

        [Header("Devrilince toz bulutu")]
        [SerializeField] private int puffCount = 90;
        [Tooltip("Devrilme başladıktan yere vurana kadar (sn)")]
        [SerializeField] private float puffDelay = 0.55f;
        [SerializeField] private Color puffColor = new Color(0.72f, 0.68f, 0.6f, 0.55f);

        private ParticleSystem[] dust;
        private ParticleSystem[] clumps;
        private ParticleSystem[] chunks;
        private ParticleSystem[] streams;
        private ParticleSystem puff;

        public void Configure(Ceiling[] roomCeilings, Material dustMat, Material chunkMat)
        {
            ceilings = roomCeilings;
            dustMaterial = dustMat;
            chunkMaterial = chunkMat;
        }

        private void Awake()
        {
            if (ceilings == null) ceilings = new Ceiling[0];
            dust = new ParticleSystem[ceilings.Length];
            clumps = new ParticleSystem[ceilings.Length];
            chunks = new ParticleSystem[ceilings.Length];
            streams = new ParticleSystem[ceilings.Length * Mathf.Max(0, streamsPerRoom)];
            var rubble = PixelRubbleMaterial();
            for (int i = 0; i < ceilings.Length; i++)
            {
                var c = ceilings[i];
                var box = new Vector3(c.size.x, 0.05f, c.size.y);
                dust[i] = CreateSystem("Dust_" + i, dustMaterial, c.center, box, 0.04f, 0.1f, 2f, 0.25f, dustColor, 900);
                Drift(dust[i], 0.25f);
                clumps[i] = CreateSystem("Clumps_" + i, dustMaterial, c.center, box, 0.6f, 1.4f, 3.5f, 0.05f, clumpColor, 200);
                var grow = clumps[i].sizeOverLifetime;
                grow.enabled = true;
                grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 1.6f));
                Drift(clumps[i], 0.15f);
                // Sıva parçaları pixel moloz (düzensiz kenarlı, konturlu); 4 değişkenden biri rastgele
                chunks[i] = CreateSystem("Chunks_" + i, rubble != null ? rubble : (chunkMaterial != null ? chunkMaterial : dustMaterial),
                    c.center, box, 0.1f, 0.22f, 2.2f, 1f, Color.white, 160);
                if (rubble != null)
                {
                    var sheet = chunks[i].textureSheetAnimation;
                    sheet.enabled = true;
                    sheet.numTilesX = PixelRubble.AtlasTiles;
                    sheet.numTilesY = 1;
                    sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
                    sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, PixelRubble.AtlasTiles - 0.01f);
                    var spin = chunks[i].rotationOverLifetime;
                    spin.enabled = true;
                    spin.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
                }
                // Sıva parçaları zemine (ve eşyalara) çarpıp seker, sonra yerde kalıp solar
                var collision = chunks[i].collision;
                collision.enabled = true;
                collision.type = ParticleSystemCollisionType.World;
                collision.mode = ParticleSystemCollisionMode.Collision3D;
                collision.bounce = 0.35f;
                collision.dampen = 0.4f;
                collision.lifetimeLoss = 0f;
                collision.quality = ParticleSystemCollisionQuality.Medium;
            }
            // Tavan çatlaklarından ince toz akıntısı: oda başına rastgele yerlerde dar kutular
            for (int i = 0; i < ceilings.Length; i++)
            for (int k = 0; k < streamsPerRoom; k++)
            {
                var c = ceilings[i];
                var position = c.center + new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f) * c.size.x, 0f,
                    UnityEngine.Random.Range(-0.3f, 0.1f) * c.size.y);
                var stream = CreateSystem($"Stream_{i}_{k}", dustMaterial, position, new Vector3(0.12f, 0.02f, 0.12f),
                    0.03f, 0.07f, 1.6f, 0.6f, dustColor, 300);
                Drift(stream, 0.05f);
                streams[i * streamsPerRoom + k] = stream;
            }
            puff = CreateSystem("ToppleDust", dustMaterial, transform.position, Vector3.one * 0.1f, 0.25f, 0.6f, 1.6f, -0.03f, puffColor, 700);
            var size = puff.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
        }

        private void OnEnable() => GameEvents.ItemToppled += OnItemToppled;
        private void OnDisable() => GameEvents.ItemToppled -= OnItemToppled;

        /// <summary>0 = sakin, 1 = ana depremin en güçlü anı</summary>
        public void SetIntensity(float intensity)
        {
            if (dust == null) return;
            intensity = Mathf.Clamp01(intensity);
            for (int i = 0; i < dust.Length; i++)
            {
                var dustEmission = dust[i].emission;
                dustEmission.rateOverTime = intensity * dustRate;
                var clumpEmission = clumps[i].emission;
                clumpEmission.rateOverTime = intensity * intensity * clumpRate;
                var chunkEmission = chunks[i].emission;
                chunkEmission.rateOverTime = Mathf.Max(0f, intensity - 0.3f) / 0.7f * chunkRate;
            }
            if (streams == null) return;
            foreach (var stream in streams)
            {
                if (stream == null) continue;
                var emission = stream.emission;
                emission.rateOverTime = intensity * streamRate;
            }
        }

        /// <summary>Sarsıntının ilk vuruşu: her odanın tavanından bir anda toz boşalır (strength 1 = ana deprem)</summary>
        public void Burst(float strength)
        {
            if (dust == null) return;
            for (int i = 0; i < dust.Length; i++)
            {
                dust[i].Emit(Mathf.RoundToInt(burstDust * strength));
                clumps[i].Emit(Mathf.RoundToInt(burstDust * 0.12f * strength));
                chunks[i].Emit(Mathf.RoundToInt(burstDust * 0.08f * strength));
            }
        }

        private void OnItemToppled(ItemId id)
        {
            var item = SecurableItem.Find(id);
            if (item != null) StartCoroutine(PuffAfterImpact(item));
        }

        private IEnumerator PuffAfterImpact(SecurableItem item)
        {
            yield return new WaitForSeconds(puffDelay);
            if (item == null) yield break;
            var collider = item.GetComponent<Collider>();
            var bounds = collider != null ? collider.bounds : new Bounds(item.transform.position, Vector3.one);
            var basePosition = new Vector3(bounds.center.x, bounds.min.y + 0.1f, Mathf.Max(0.3f, bounds.center.z));
            for (int i = 0; i < puffCount; i++)
            {
                var emit = new ParticleSystem.EmitParams
                {
                    position = basePosition + new Vector3(UnityEngine.Random.Range(-bounds.extents.x, bounds.extents.x), 0f,
                        UnityEngine.Random.Range(-0.2f, 0.2f)),
                    velocity = new Vector3(UnityEngine.Random.Range(-1.6f, 1.6f), UnityEngine.Random.Range(0.1f, 0.9f),
                        UnityEngine.Random.Range(-0.3f, 0.3f)),
                    applyShapeToPosition = false,
                };
                puff.Emit(emit, 1);
            }
        }

        // Moloz parçacıkları için pixel moloz atlası ve alfa kesmeli materyal (chunkMaterial'in kopyası)
        private Material PixelRubbleMaterial()
        {
            if (chunkMaterial == null) return null;
            var material = new Material(chunkMaterial) { name = "PixelRubbleParticles" };
            material.SetTexture("_BaseMap", PixelRubble.Atlas());
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            return material;
        }

        /// <summary>Toz düz düşmesin: havada hafifçe salınsın</summary>
        private static void Drift(ParticleSystem ps, float strength)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.3f;
            noise.quality = ParticleSystemNoiseQuality.Low;
        }

        private ParticleSystem CreateSystem(string name, Material material, Vector3 position, Vector3 box,
            float minSize, float maxSize, float lifetime, float gravity, Color color, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
            main.gravityModifier = gravity;
            main.startColor = color;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;

            // Ömrünün sonunda solar
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ps.Play();
            return ps;
        }
    }
}
