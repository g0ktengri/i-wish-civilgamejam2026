using System.Collections.Generic;
using Deprem.Audio;
using Deprem.Core;
using Deprem.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Quake
{
    /// <summary>
    /// Kurtarma süresi (GDD "Kurtarma süresi"): büyük bir artçı yaklaşıyor, süre bitince vurur.
    ///   - Oyuncu çök-kapan-tutundan kalkınca (Rescue fazı) başlar. Sayı yok: ekranın üstünde titreşen sarsıntı çubuğu.
    ///   - Son saniyelerde kamera hafifçe titrer, acil durum ışıkları nabız gibi atar.
    ///   - Gizli tolerans: çubuk bitince büyük artçı hemen vurmaz; bu sürede kabine giren kurtulur.
    ///   - Görünmez kolaylaştırma: her kayıpta +10 sn, en fazla +30 sn (GameState.FailedAttempts).
    ///   - Süre biterse keşke cümlesi oyuncunun yanında en çok vakit geçirdiği devrilmiş eşyayı söyler.
    /// </summary>
    public sealed class RescueTimer : MonoBehaviour
    {
        private enum TimerState
        {
            Idle,
            Counting,
            Grace,
            FinalShock,
            Done,
        }

        [Header("Süre")]
        [Tooltip("GDD: en iyi rotanın süresi x 1,3. İlk testten sonra ayarlanacak.")]
        [SerializeField] private float baseDuration = 75f;
        [SerializeField] private float bonusPerFailure = 15f; // jüri temposu (Berke): 10 → 15, kayıptan sonra daha rahat
        [SerializeField] private float maxBonus = 30f;
        [Tooltip("Çubuk bitince büyük artçıdan önceki gizli süre (GDD: 5-8 sn)")]
        [SerializeField] private float hiddenTolerance = 6f;
        [Tooltip("Son bu kadar saniyede titreme ve nabız başlar")]
        [SerializeField] private float tensionWindow = 15f;

        [Header("Büyük artçı")]
        [SerializeField] private float finalShockDuration = 2f;
        [SerializeField, Range(0f, 1f)] private float graceShake = 0.25f;

        [Header("En çok yavaşlatan eşya")]
        [Tooltip("Oyuncu devrilmiş eşyaya bu kadar yakınken (X, m) eşya onu yavaşlatıyor sayılır")]
        [SerializeField] private float slowRange = 1.2f;
        [Tooltip("Karakter düzlemi (Z); sadece bu düzlemi kesen, yani yolu tıkayan eşyalar sayılır")]
        [SerializeField] private float playerPlaneZ = 1f;
        [SerializeField] private float playerRadius = 0.25f;

        [Header("Süre çubuğu (Play testi 12: düz sarı şerit yerine parçalı pixel çubuk)")]
        [SerializeField] private int barSegments = 24;
        [SerializeField] private Vector2 segmentSize = new Vector2(10f, 15f); // Play testi 21: küçüktü
        [SerializeField] private float segmentGap = 3f;
        [SerializeField] private int labelFontSize = 20;
        [SerializeField] private float barTopMargin = 14f;
        [SerializeField] private float barRightMargin = 16f;
        [SerializeField] private Color calmColor = new Color(1f, 0.85f, 0.45f);
        [SerializeField] private Color urgentColor = new Color(1f, 0.15f, 0.1f);
        [Tooltip("Çubuğun en fazla titreme miktarı (referans piksel)")]
        [SerializeField] private float barMaxJitter = 4f;

        public float Duration { get; private set; }
        public float Remaining { get; private set; }
        public bool IsRunning => state == TimerState.Counting || state == TimerState.Grace || state == TimerState.FinalShock;

        private TimerState state = TimerState.Idle;
        private float stateTime;
        private PlayerController player;
        private HouseLights lights;
        private readonly Dictionary<ItemToppler, float> slowTime = new Dictionary<ItemToppler, float>();
        private readonly List<ItemToppler> topplers = new List<ItemToppler>();

        private GameObject barRoot;
        private SegmentMeter meter;

        private void Awake()
        {
            Initialize();
        }

        /// <summary>Awake'te çağrılır; edit mode testleri için ayrıca çağrılabilir.</summary>
        public void Initialize()
        {
            if (player != null) return;
            player = FindFirstObjectByType<PlayerController>();
            lights = FindFirstObjectByType<HouseLights>();
            topplers.Clear();
            topplers.AddRange(FindObjectsByType<ItemToppler>(FindObjectsSortMode.None));
        }

        private void OnEnable() => GameEvents.PhaseChanged += OnPhaseChanged;

        private void OnDisable()
        {
            GameEvents.PhaseChanged -= OnPhaseChanged;
            QuakeCameraShake.AmbientIntensity = 0f;
        }

        public void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Rescue) Begin();
            else Stop();
        }

        private void Begin()
        {
            Initialize();
            float bonus = Mathf.Min(GameState.FailedAttempts * bonusPerFailure, maxBonus);
            Duration = baseDuration + bonus;
            Remaining = Duration;
            slowTime.Clear();
            SetState(TimerState.Counting);
            EnsureBar();
            barRoot.SetActive(true);
        }

        private void Stop()
        {
            if (state == TimerState.Idle) return;
            SetState(TimerState.Idle);
            QuakeCameraShake.AmbientIntensity = 0f;
            if (lights != null) lights.PulseEmergency(0f);
            Sfx.SetTension(0f);
            if (barRoot != null) barRoot.SetActive(false);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Update'ten çağrılır; testlerde elle çağrılabilir.</summary>
        public void Tick(float dt)
        {
            if (!IsRunning || GameState.Phase != GamePhase.Rescue) return;
            stateTime += dt;

            switch (state)
            {
                case TimerState.Counting:
                    Remaining = Mathf.Max(0f, Remaining - dt);
                    AccumulateSlowing(dt);
                    float tension = Mathf.Clamp01(1f - Remaining / tensionWindow);
                    QuakeCameraShake.AmbientIntensity = tension * 0.15f;
                    if (lights != null) lights.PulseEmergency(tension);
                    Sfx.SetTension(tension); // uzak uğultu yükselir, son 15 sn kalp atışı
                    if (Remaining <= 0f) SetState(TimerState.Grace);
                    break;

                case TimerState.Grace:
                    // Sadece uğultu ve hafif sallantı; bu sürede kabine giren kurtulur
                    QuakeCameraShake.AmbientIntensity = graceShake;
                    if (lights != null) lights.PulseEmergency(1f);
                    Sfx.SetTension(1f); // GDD: gizli toleransta sadece uğultu
                    if (stateTime >= hiddenTolerance)
                    {
                        SetState(TimerState.FinalShock);
                        Sfx.Play("quake.big");
                    }
                    break;

                case TimerState.FinalShock:
                    QuakeCameraShake.AmbientIntensity = 1f;
                    if (stateTime >= finalShockDuration)
                    {
                        SetState(TimerState.Done);
                        QuakeCameraShake.AmbientIntensity = 0f;
                        GameState.FailLoop(LoseReason.RescueTimeout, MostSlowingItem());
                    }
                    break;
            }
            UpdateBar();
        }

        private void SetState(TimerState next)
        {
            state = next;
            stateTime = 0f;
        }

        // ------------------------------------------------------------------ En çok yavaşlatan eşya

        private void AccumulateSlowing(float dt)
        {
            if (player == null) return;
            var p = player.transform.position;
            foreach (var toppler in topplers)
            {
                if (toppler == null || !toppler.HasToppled) continue;
                var b = toppler.CombinedBounds();
                // Duvardaki raf gibi arkaya düşen eşyalar yolu tıkamaz, sayılmaz
                bool blocksPath = b.min.z < playerPlaneZ + playerRadius && b.max.z > playerPlaneZ - playerRadius;
                if (!blocksPath) continue;
                bool sameFloor = p.y > b.min.y - 1f && p.y < b.max.y + 1f;
                bool near = p.x > b.min.x - slowRange && p.x < b.max.x + slowRange;
                if (!sameFloor || !near) continue;
                slowTime.TryGetValue(toppler, out float t);
                slowTime[toppler] = t + dt;
            }
        }

        /// <summary>Oyuncunun yanında en çok vakit geçirdiği, yolu tıkayan devrilmiş eşya; yoksa ilk devrilen, o da yoksa None.</summary>
        public ItemId MostSlowingItem()
        {
            // M17: oyuncunun açamadığı kapıyı kapatan eşya en çok yavaşlatandır (babaya hiç ulaşamadı)
            if (Deprem.Player.HouseDoor.BlockedByItem != ItemId.None) return Deprem.Player.HouseDoor.BlockedByItem;

            ItemToppler best = null;
            float bestTime = 0f;
            foreach (var pair in slowTime)
            {
                if (pair.Key == null || pair.Value <= bestTime) continue;
                best = pair.Key;
                bestTime = pair.Value;
            }
            if (best == null)
                foreach (var toppler in topplers)
                    if (toppler != null && toppler.HasToppled) { best = toppler; break; }

            var item = best != null ? best.GetComponent<SecurableItem>() : null;
            return item != null ? item.Id : ItemId.None;
        }

        // ------------------------------------------------------------------ Sarsıntı çubuğu

        private void EnsureBar()
        {
            if (barRoot != null) return;

            var canvas = Deprem.UI.UiBuilder.CreateCanvas(transform, "RescueBar", 40);
            barRoot = canvas.gameObject;
            meter = SegmentMeter.Create(canvas.transform, "TimeMeter", barSegments, segmentSize, segmentGap,
                Deprem.Localization.Texts.Get("hud.rescue_time"), labelFontSize);
            // Play testi 22: üst ortada hedef paneline ve aile oklarına biniyordu; sağ üst köşe
            meter.Root.anchorMin = meter.Root.anchorMax = new Vector2(1f, 1f);
            meter.Root.pivot = new Vector2(1f, 1f);
            meter.Root.anchoredPosition = new Vector2(-barRightMargin, -barTopMargin);
        }

        private void UpdateBar()
        {
            if (meter == null) return;
            float fraction = Duration > 0f ? Remaining / Duration : 0f;
            float urgency = state == TimerState.Counting ? Mathf.Clamp01(1f - Remaining / tensionWindow) : 1f;
            meter.SetValue(fraction, Color.Lerp(calmColor, urgentColor, urgency), urgency > 0f);

            // Sakinken de hafif titrer (GDD: titreşen sarsıntı çubuğu); süre azaldıkça sertleşir
            float jitter = barMaxJitter * Mathf.Lerp(0.15f, 1f, urgency);
            meter.Root.anchoredPosition = new Vector2(-barRightMargin + Random.Range(-jitter, jitter), -barTopMargin + Random.Range(-jitter, jitter) * 0.5f);
        }
    }
}
