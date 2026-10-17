using System.Collections.Generic;
using Deprem.Audio;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Odalardaki küçük dekor (Play testi 17: odalar aşırı sadeydi): duvarda tablo, fotoğraf, saat, ayna; masada
    /// vazo, kupa. Sarsıntıda sallanır ve yamulur (öncüden sonra yamuk kalır), ana depremde düşer; camlı olan
    /// çatlar, seramik kırılıp dağılır. Oyunu etkilemez, sabitlenmez. QuakeDirector sallar ve düşürür.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FallingDecor : MonoBehaviour
    {
        public enum MountKind
        {
            Wall,    // çivide asılı: üst ortasından sallanır
            Surface, // masada duruyor: zıplar, kayar
        }

        public enum BreakKind
        {
            None,     // tablo, poster: düşer, kalır
            Crack,    // camlı çerçeve, ayna, saat: cam çatlar (brokenTexture)
            Shatter,  // vazo, kupa, şişe: kırılır, parçaları saçılır
        }

        [SerializeField] private MountKind mount;
        [SerializeField] private BreakKind breakKind;
        [SerializeField] private Renderer view;
        [SerializeField] private Texture2D brokenTexture;
        [Tooltip("Kırılınca saçılan parçaların rengi")]
        [SerializeField] private Color shardColor = new Color(0.75f, 0.85f, 0.9f);
        [SerializeField] private Material shardMaterial;
        [Tooltip("Görselin yarı yüksekliği (m): duvardakiler üst ortadan sallanır")]
        [SerializeField] private float halfHeight = 0.3f;
        [SerializeField] private float maxSwing = 7f;
        [Tooltip("Sarsıntıda biriken yamukluk sınırı (derece); öncüden sonra öyle kalır")]
        [SerializeField] private float maxTilt = 12f;
        [SerializeField] private float tiltDrift = 30f;
        [Header("Cam sesi (Play testi 29: çok sık geliyordu)")]
        [Tooltip("Çatlayan çerçeve / saat / aynanın cam sesi çalma olasılığı; gerisi sessiz çatlar")]
        [SerializeField, Range(0f, 1f)] private float crackSoundChance = 0.3f;
        [Tooltip("İki cam sesi arasında en az süre (sn), bütün dekorlar için ortak")]
        [SerializeField] private float glassSoundInterval = 1.2f;
        private static float lastGlassSound = -100f;

        private static readonly List<FallingDecor> all = new List<FallingDecor>();
        public static IReadOnlyList<FallingDecor> All => all;

        public bool HasFallen { get; private set; }
        public bool IsBroken { get; private set; }

        private Rigidbody body;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private float tilt;
        private float phase;
        private float slide;
        private float settled;
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            restPosition = transform.position;
            restRotation = transform.rotation;
            phase = Random.value * 10f;
            // Dekor oyuncunun yolunu kesmez: masadan düşen kupa ayağa takılmasın
            var player = FindFirstObjectByType<Deprem.Player.PlayerController>();
            var playerCollider = player != null ? player.GetComponent<Collider>() : null;
            if (playerCollider != null)
                foreach (var c in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(c, playerCollider);
        }

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        public static void ShakeAll(float intensity)
        {
            for (int i = 0; i < all.Count; i++) all[i].Shake(intensity);
        }

        /// <summary>Sarsıntıda her kare: sallanır, yavaş yavaş yamulur. 0'da yamuk duruşunda kalır.</summary>
        public void Shake(float intensity)
        {
            if (HasFallen) return;
            float dt = Time.deltaTime;
            if (intensity > 0f) tilt = Mathf.Clamp(tilt + Random.Range(-1f, 1f) * tiltDrift * intensity * dt, -maxTilt, maxTilt);
            if (mount == MountKind.Wall)
            {
                float angle = tilt + Mathf.Sin(Time.time * 9f + phase) * maxSwing * intensity;
                var rotation = restRotation * Quaternion.Euler(0f, 0f, angle);
                var pivot = restPosition + restRotation * Vector3.up * halfHeight; // çivi
                transform.SetPositionAndRotation(pivot + rotation * Vector3.down * halfHeight, rotation);
            }
            else
            {
                // Masada titreyerek kayar, hafifçe yamulur
                slide += Random.Range(-1f, 1f) * intensity * dt * 0.4f;
                float hop = Mathf.Abs(Mathf.Sin(Time.time * 23f + phase)) * 0.012f * intensity;
                transform.SetPositionAndRotation(restPosition + new Vector3(slide, hop, 0f),
                    restRotation * Quaternion.Euler(0f, 0f, tilt * 0.4f + Random.Range(-3f, 3f) * intensity));
            }
        }

        /// <summary>Yerinden kopar ve düşer</summary>
        public void Drop()
        {
            if (HasFallen) return;
            HasFallen = true;
            body.isKinematic = false;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            if (mount == MountKind.Wall)
                body.linearVelocity = new Vector3(Random.Range(-0.3f, 0.3f), 0f, -0.4f);
            else
            {
                // Play testi 19: masanın tablasında sürtünmeyle kalıyordu. Kaygan, öne ve yana fırlar, masadan düşer
                var slippery = new PhysicsMaterial("DecorSlip") { dynamicFriction = 0f, staticFriction = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum };
                foreach (var c in GetComponents<Collider>()) c.sharedMaterial = slippery;
                body.linearVelocity = new Vector3(Random.Range(-0.8f, 0.8f), 1.1f, -1.9f);
                body.angularVelocity = new Vector3(0f, 0f, Random.Range(-8f, 8f));
            }
        }

        /// <summary>Masada duruyor mu (ana depremde hepsi düşer)</summary>
        public bool OnSurface => mount == MountKind.Surface;

        private void OnCollisionEnter(Collision collision)
        {
            if (!HasFallen || IsBroken || collision.relativeVelocity.magnitude < 1.2f) return;
            if (breakKind == BreakKind.None)
            {
                Sfx.PlayAt("quake.debris", transform.position, 0.5f);
                return;
            }
            Break();
        }

        private void FixedUpdate()
        {
            if (!HasFallen || body.isKinematic) return;
            // Masadan indikten sonra kaygan değil: yerde kayıp gitmesin
            if (mount == MountKind.Surface && restPosition.y - transform.position.y > 0.3f)
                foreach (var c in GetComponents<Collider>()) c.sharedMaterial = null;
            // Yere oturunca donar: oyuncunun yolunda itilip kaymasın
            bool still = body.linearVelocity.sqrMagnitude < 0.02f;
            settled = still ? settled + Time.fixedDeltaTime : 0f;
            if (settled <= 0.4f) return;
            body.isKinematic = true;
            // Çarpma hızı kaçırılsa da (yavaş kayıp düştüyse) yüksekten düşen cam ve seramik kırılır
            if (!IsBroken && breakKind != BreakKind.None && restPosition.y - transform.position.y > 0.4f) Break();
        }

        private void Break()
        {
            IsBroken = true;
            bool audible = breakKind == BreakKind.Shatter || Random.value < crackSoundChance;
            if (audible && (Time.time - lastGlassSound >= glassSoundInterval || Time.time < lastGlassSound))
            {
                lastGlassSound = Time.time;
                Sfx.PlayAt("glass.break", transform.position, breakKind == BreakKind.Shatter ? 0.6f : 0.5f);
            }
            if (breakKind == BreakKind.Crack && brokenTexture != null && view != null)
            {
                var block = new MaterialPropertyBlock();
                view.GetPropertyBlock(block);
                block.SetTexture(BaseMap, brokenTexture);
                view.SetPropertyBlock(block);
            }
            else if (breakKind == BreakKind.Shatter)
            {
                if (view != null) view.enabled = false;
                foreach (var c in GetComponents<Collider>()) c.enabled = false;
                body.isKinematic = true;
            }
            SpawnShards(breakKind == BreakKind.Shatter ? 7 : 4);
        }

        private void SpawnShards(int count)
        {
            if (shardMaterial == null) return;
            var player = FindFirstObjectByType<Deprem.Player.PlayerController>();
            var playerCollider = player != null ? player.GetComponent<Collider>() : null;
            for (int i = 0; i < count; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "Shard";
                float size = Random.Range(0.03f, 0.07f);
                shard.transform.localScale = new Vector3(size, size * Random.Range(0.4f, 1f), 0.01f);
                shard.transform.SetPositionAndRotation(transform.position + new Vector3(Random.Range(-0.1f, 0.1f), 0.05f, -0.02f),
                    Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
                var r = shard.GetComponent<MeshRenderer>();
                r.sharedMaterial = shardMaterial;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", shardColor * Random.Range(0.8f, 1.1f));
                r.SetPropertyBlock(block);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var c = shard.GetComponent<Collider>();
                if (playerCollider != null) Physics.IgnoreCollision(c, playerCollider);
                var rb = shard.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
                rb.linearVelocity = new Vector3(Random.Range(-1.4f, 1.4f), Random.Range(0.8f, 2f), Random.Range(-0.5f, 0f));
                shard.AddComponent<DebrisSettle>();
            }
        }
    }
}
