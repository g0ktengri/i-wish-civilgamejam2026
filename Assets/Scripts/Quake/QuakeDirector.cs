using System;
using System.Collections;
using System.Collections.Generic;
using Deprem.Core;
using Deprem.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deprem.Quake
{
    /// <summary>
    /// Deprem akışı (GDD "Oynanış"):
    ///   Foreshock : oyuncu yatakta, kısa sallantı. Döngü 1 uyumaya devam eder, döngü 2 senaryolu kalkar,
    ///               döngü 3+ "F: Kalk". Sonra ana depreme geçer.
    ///   MainQuake : uzun sarsıntı, sabitlenmemiş eşyalar devrilir, "C" basılı tutulmalı.
    ///               Bitince elektrik kesilir ve kurtarma fazına geçilir.
    ///   Rescue    : belirli noktalarda artçılar; C yapılmazsa yaralanma (ikincisinde döngü biter).
    /// Ruh fazından (döngü 4+) ana depreme geçişi ruh sistemi (Berke) yapar; burası MainQuake fazını dinler.
    /// </summary>
    public sealed class QuakeDirector : MonoBehaviour
    {
        [Serializable]
        public struct AftershockZone
        {
            public string name;
            [Tooltip("X-Y düzleminde alan (min)")] public Vector2 min;
            [Tooltip("X-Y düzleminde alan (max)")] public Vector2 max;
        }

        [Header("Öncü sarsıntı")]
        [SerializeField] private float foreshockDelay = 2f;
        [SerializeField] private float foreshockDuration = 3f;
        [SerializeField, Range(0f, 1f)] private float foreshockIntensity = 0.3f;
        [Tooltip("Döngü 1-2: öncüden ana depreme kadar bekleme (sn)")]
        [SerializeField] private float scriptedWaitBeforeMainQuake = 5f;
        [Tooltip("Döngü 3+: 'F: Kalk' için süre; kalkmazsa ana deprem başlar")]
        [SerializeField] private float standUpWindow = 8f;
        [Tooltip("Kalktıktan sonra ana depreme kadar bekleme (sn)")]
        [SerializeField] private float waitAfterStandUp = 3f;
        [Tooltip("Döngü 2: ana deprem başladıktan kaç sn sonra tavan oyuncunun üstüne çöker")]
        [SerializeField] private float scriptedHitDelay = 2.5f;

        [Header("Ana deprem")]
        [Tooltip("Play testi 3: 12 sn çok uzundu")]
        [SerializeField] private float mainQuakeDuration = 7f;
        [SerializeField, Range(0f, 1f)] private float mainQuakeIntensity = 1f;
        [Tooltip("Eşyalar bu aralıkta, rastgele sırayla devrilir (deprem başından itibaren sn)")]
        [SerializeField] private Vector2 toppleWindow = new Vector2(1.5f, 5f);
        [Tooltip("C uyarısından sonra güvenli noktaya yürüyüp basmak için süre")]
        [SerializeField] private float duckCoverGrace = 3f;
        [Tooltip("C kısa süre bırakılırsa affedilir")]
        [SerializeField] private float duckCoverTolerance = 0.35f;

        [Header("Artçılar")]
        [SerializeField] private float aftershockDuration = 3.5f;
        [SerializeField, Range(0f, 1f)] private float aftershockIntensity = 0.5f;
        [SerializeField] private float aftershockGrace = 2.5f;
        [SerializeField] private float injuredSpeedMultiplier = 0.6f;
        [Tooltip("Oyuncu bu alanlara girince artçı olur (her deneme bir kez). GDD: koridor, merdiven, üst kat")]
        [SerializeField] private AftershockZone[] aftershockZones =
        {
            new AftershockZone { name = "Koridora giriş", min = new Vector2(6.3f, -0.5f), max = new Vector2(7.2f, 1f) },
            new AftershockZone { name = "Merdiven başı", min = new Vector2(14f, 3.5f), max = new Vector2(15.5f, 5f) },
            new AftershockZone { name = "Üst kat, kardeşin odası", min = new Vector2(5.5f, 3.5f), max = new Vector2(6.8f, 5f) },
        };

        [Header("Oyuncu")]
        [SerializeField] private float playerPlaneZ = 1f;
        [SerializeField] private float bedHeight = 0.5f;
        [Tooltip("Kalkınca yatağın ortasından bu kadar sağda durur (odanın güvenli masasının önü)")]
        [SerializeField] private float standUpOffsetX = 1.2f;
        [SerializeField] private Key standUpKey = Key.F;
        [SerializeField] private Key walkLeftKey = Key.A;
        [SerializeField] private Key walkRightKey = Key.D;
        [SerializeField] private Key duckCoverKey = Key.C;

        [Header("Tavandan düşen parça")]
        [Tooltip("Sıva parçası materyali (QuakeSetup atar)")]
        [SerializeField] private Material debrisMaterial;
        [Tooltip("Parça karakter düzleminin bu kadar önünde düşer (m)")]
        [SerializeField] private float debrisFrontOffset = 0.15f;

        [Header("Geliştirme")]
        [Tooltip("Sahnede döngü yöneticisi yoksa bu döngüyle başlat (0 = kapalı)")]
        [SerializeField, Range(0, GameConstants.LoopCount)] private int devStartLoop;
        [Tooltip("HUD yokken tuş ipucunu ekranın ortasında göster (Berke'nin PromptHud'u varken kapalı)")]
        [SerializeField] private bool showDebugPrompt;

        private PlayerController player;
        private HouseLights lights;
        private Transform bed;
        private readonly List<ItemToppler> topplers = new List<ItemToppler>();
        private bool[] zoneTriggered;
        private bool aftershockRunning;
        private DuckCoverBar duckCoverBar;
        private QuakeEffects effects;

        private void Awake()
        {
            player = FindFirstObjectByType<PlayerController>();
            lights = FindFirstObjectByType<HouseLights>();
            var bedMarker = GameObject.Find("Marker_PlayerBed");
            bed = bedMarker != null ? bedMarker.transform : null;
            topplers.AddRange(FindObjectsByType<ItemToppler>(FindObjectsSortMode.None));
            zoneTriggered = new bool[aftershockZones.Length];
            if (!TryGetComponent(out duckCoverBar)) duckCoverBar = gameObject.AddComponent<DuckCoverBar>();
            TryGetComponent(out effects);
        }

        private void OnEnable() => GameEvents.PhaseChanged += HandlePhaseChanged;
        private void OnDisable() => GameEvents.PhaseChanged -= HandlePhaseChanged;

        private IEnumerator Start()
        {
            if (devStartLoop <= 0) yield break;
            // Döngü yöneticisi bir kare bekleyip başlatır; o yoksa geliştirme için biz başlatalım
            yield return null;
            yield return null;
            if (GameState.Phase != GamePhase.None) yield break;
            GameState.ResetAll();
            GameState.BeginAttempt(devStartLoop);
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Foreshock:
                    Restart(ForeshockRoutine());
                    break;
                case GamePhase.Spirit:
                    StopQuake();
                    LieInBed(); // ruh bedenden ayrılır, beden yatakta kalır
                    break;
                case GamePhase.MainQuake:
                    Restart(MainQuakeRoutine());
                    break;
                case GamePhase.Rescue:
                    if (player != null) player.InputEnabled = true;
                    break;
                case GamePhase.Failed:
                case GamePhase.Won:
                    StopQuake();
                    if (player != null)
                    {
                        player.InputEnabled = false;
                        player.SetScriptedInput(0f);
                    }
                    break;
            }
        }

        private void Update()
        {
            if (GameState.Phase != GamePhase.Rescue || aftershockRunning || player == null || player.IsOnPath) return;
            var p = player.transform.position;
            for (int i = 0; i < aftershockZones.Length; i++)
            {
                var zone = aftershockZones[i];
                if (zoneTriggered[i] || p.x < zone.min.x || p.x > zone.max.x || p.y < zone.min.y || p.y > zone.max.y) continue;
                zoneTriggered[i] = true;
                StartCoroutine(AftershockRoutine());
                break;
            }
        }

        // ------------------------------------------------------------------ Öncü sarsıntı

        private IEnumerator ForeshockRoutine()
        {
            LieInBed();
            yield return new WaitForSeconds(foreshockDelay);

            GameEvents.RaiseForeshockStarted();
            yield return Shake(foreshockIntensity, foreshockDuration);

            if (GameState.CurrentLoop == 1)
            {
                // Döngü 1 (senaryolu): sarsıntıyla uyanır, önemsemez, uyumaya devam eder
                Say("bubble.ignore_foreshock", 3.5f);
                yield return new WaitForSeconds(scriptedWaitBeforeMainQuake);
            }
            else if (!GameState.StandUpUnlocked)
            {
                // Döngü 2 (senaryolu ama kontrol oyuncuda): ciddiye alıp kalkar, kaçmak ister;
                // çök-kapan-tutun henüz bilinmiyor, ana depremde nereye giderse gitsin tavan çöker
                Say("bubble.panic_run", 2.5f);
                yield return new WaitForSeconds(1f);
                yield return GetUp();
                player.InputEnabled = true;
                yield return new WaitForSeconds(scriptedWaitBeforeMainQuake * 0.5f);
            }
            else
            {
                // Döngü 3+ (serbest): "F: Kalk"; yürümeye başlamak da kaldırır
                Say("bubble.wake", 2f);
                QuakePrompts.Set(QuakePrompts.StandUp);
                bool stoodUp = false;
                for (float t = 0f; t < standUpWindow; t += Time.deltaTime)
                {
                    if (Pressed(standUpKey) || Held(walkLeftKey) || Held(walkRightKey))
                    {
                        stoodUp = true;
                        break;
                    }
                    yield return null;
                }
                QuakePrompts.Clear();
                if (stoodUp)
                {
                    yield return GetUp();
                    player.InputEnabled = true;
                    yield return new WaitForSeconds(waitAfterStandUp);
                }
            }

            GameState.SetPhase(GamePhase.MainQuake);
        }

        // ------------------------------------------------------------------ Ana deprem

        private IEnumerator MainQuakeRoutine()
        {
            GameEvents.RaiseMainQuakeStarted();

            // Ruh fazından dönüş (döngü 4+): ruh bedene döner, oyuncu yatağın yanında ayakta
            if (player.IsLying && GameState.SpiritPhaseUnlocked)
            {
                player.Teleport(StandPosition());
                player.InputEnabled = true;
            }

            StartCoroutine(Shake(mainQuakeIntensity, mainQuakeDuration));
            StartCoroutine(ToppleItems());
            if (effects != null) effects.Burst(1f);

            if (player.IsLying)
            {
                // Kalkmadı: yatağın üstündeki raf üstüne düşer
                yield return new WaitForSeconds(toppleWindow.x + 1.5f);
                var shelf = SecurableItem.Find(ItemId.BedShelf);
                if (shelf == null || !shelf.IsSecured)
                {
                    Fail(LoseReason.IgnoredForeshock, ItemId.BedShelf);
                    yield break;
                }
                yield return new WaitForSeconds(mainQuakeDuration - toppleWindow.x - 1.5f);
            }
            else if (!GameState.StandUpUnlocked)
            {
                // Döngü 2 (senaryolu): oyuncu serbest ama ç-k-t ipucu ve güvenli noktalar yok; tavan çöker
                yield return new WaitForSeconds(scriptedHitDelay);
                player.InputEnabled = false;
                player.SetScriptedInput(0f);
                yield return HitByDebris();
                Fail(LoseReason.NoDuckCoverHold);
                yield break;
            }
            else
            {
                bool survived = false;
                yield return DuckCoverHold(mainQuakeDuration, duckCoverGrace, result => survived = result);
                if (!survived)
                {
                    yield return HitByDebris();
                    Fail(LoseReason.NoDuckCoverHold);
                    yield break;
                }
            }

            player.SetScriptedInput(0f);
            GameEvents.RaiseMainQuakeEnded();
            if (lights != null) lights.SetPower(false);
            yield return new WaitForSeconds(1f);
            GameState.SetPhase(GamePhase.Rescue);
        }

        private IEnumerator ToppleItems()
        {
            var schedule = new List<(float time, ItemToppler toppler)>();
            foreach (var toppler in topplers)
            {
                var item = toppler.GetComponent<SecurableItem>();
                if (item != null && item.IsSecured) continue;
                bool first = item != null && item.Id == ItemId.BedShelf;
                float time = first ? toppleWindow.x : UnityEngine.Random.Range(toppleWindow.x, toppleWindow.y);
                schedule.Add((time, toppler));
            }
            schedule.Sort((a, b) => a.time.CompareTo(b.time));

            float elapsed = 0f;
            foreach (var entry in schedule)
            {
                if (entry.time > elapsed) yield return new WaitForSeconds(entry.time - elapsed);
                elapsed = Mathf.Max(elapsed, entry.time);
                entry.toppler.Topple();
                var item = entry.toppler.GetComponent<SecurableItem>();
                if (item != null) GameEvents.RaiseItemToppled(item.Id);
            }
        }

        // ------------------------------------------------------------------ Artçılar

        private IEnumerator AftershockRoutine()
        {
            aftershockRunning = true;
            GameEvents.RaiseAftershockStarted();
            StartCoroutine(Shake(aftershockIntensity, aftershockDuration));
            if (effects != null) effects.Burst(0.4f);

            bool survived = true;
            yield return DuckCoverHold(aftershockDuration, aftershockGrace, result => survived = result);
            if (!survived)
            {
                player.SpeedMultiplier = injuredSpeedMultiplier;
                GameState.AddAftershockInjury(); // ikincisinde döngüyü GameState bitirir
            }

            if (GameState.Phase == GamePhase.Rescue) player.InputEnabled = true;
            aftershockRunning = false;
        }

        // ------------------------------------------------------------------ Ortak parçalar

        /// <summary>
        /// Süre boyunca güvenli noktada C basılı tutulmalı. Güvenli noktalar yerde parlar;
        /// nokta dışında C işe yaramaz, oyuncu yürüyebilir. Tutarken çömelir ve hareket edemez.
        /// </summary>
        private IEnumerator DuckCoverHold(float duration, float grace, Action<bool> result)
        {
            QuakePrompts.Set(QuakePrompts.DuckCover);
            SafeSpot.ShowAll(true);
            var check = new DuckCoverHoldCheck(grace, duckCoverTolerance);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                bool holding = Held(duckCoverKey) && SafeSpot.AnyContains(player.transform.position);
                check.Update(Time.deltaTime, holding);
                player.InputEnabled = !holding;
                player.SetScriptedInput(0f, crouch: holding);
                if (duckCoverBar != null) duckCoverBar.Show(1f - t / duration, holding);
                if (check.Failed) break;
                yield return null;
            }
            if (duckCoverBar != null) duckCoverBar.Hide();
            SafeSpot.ShowAll(false);
            QuakePrompts.Clear();
            player.SetScriptedInput(0f);
            player.InputEnabled = true;
            result(!check.Failed);
        }

        /// <summary>Sarsıntı: kamera, lambalar ve eşyaların tıkırdaması. Başta yükselir, sonda söner.</summary>
        private IEnumerator Shake(float intensity, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float rampIn = Mathf.Clamp01(t / (duration * 0.15f));
                float rampOut = Mathf.Clamp01((duration - t) / (duration * 0.2f));
                ApplyShake(intensity * Mathf.Min(rampIn, rampOut));
                yield return null;
            }
            ApplyShake(0f);
        }

        private void ApplyShake(float intensity)
        {
            QuakeCameraShake.Intensity = intensity;
            if (lights != null) lights.Flicker(intensity);
            if (effects != null) effects.SetIntensity(intensity);
            foreach (var toppler in topplers) toppler.Rattle(intensity);
        }

        /// <summary>Oyuncunun tam üstündeki tavandan bir sıva parçası ve kırıntılar düşer.</summary>
        private IEnumerator HitByDebris()
        {
            QuakePrompts.Clear();
            SafeSpot.ShowAll(false);
            var head = player.transform.position + Vector3.up * player.StandingHeight;
            float ceilingY = head.y + 1.5f;
            foreach (var hit in Physics.RaycastAll(head, Vector3.up, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.rigidbody == player.Body) continue;
                ceilingY = Mathf.Min(ceilingY, hit.point.y);
            }

            // Karakterin düz görselinin (z = playerPlaneZ) hemen önünde, kapsülün ön yarısında: başa çarpar
            // ve önde kalır. Z ekseni kilitli, arkaya kayıp görselin arkasında kaybolmaz.
            var center = new Vector3(player.transform.position.x, ceilingY - 0.1f, playerPlaneZ - debrisFrontOffset);
            SpawnDebris("Plaster", center, new Vector3(0.9f, 0.1f, 0.25f), 25f);
            for (int i = 0; i < 4; i++)
            {
                var offset = new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(-0.1f, 0f), 0f);
                float size = UnityEngine.Random.Range(0.1f, 0.22f);
                SpawnDebris("Chunk", center + offset, new Vector3(size, size, Mathf.Min(size, 0.2f)), 2f);
            }
            QuakeCameraShake.Intensity = Mathf.Max(QuakeCameraShake.Intensity, 0.6f);
            yield return new WaitForSeconds(0.9f);
        }

        private void SpawnDebris(string name, Vector3 position, Vector3 size, float mass)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.position = position;
            piece.transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-15f, 15f));
            piece.transform.localScale = size;
            if (debrisMaterial != null) piece.GetComponent<MeshRenderer>().sharedMaterial = debrisMaterial;
            var body = piece.AddComponent<Rigidbody>();
            body.mass = mass;
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            body.linearVelocity = Vector3.down * 1.5f;
        }

        private void Fail(LoseReason reason, ItemId item = ItemId.None)
        {
            StopQuake();
            GameState.FailLoop(reason, item);
        }

        private void StopQuake()
        {
            StopAllCoroutines();
            ApplyShake(0f);
            SafeSpot.ShowAll(false);
            QuakePrompts.Clear();
            if (duckCoverBar != null) duckCoverBar.Hide();
            aftershockRunning = false;
        }

        private void Restart(IEnumerator routine)
        {
            StopQuake();
            StartCoroutine(routine);
        }

        private void Say(string key, float seconds)
        {
            var bubble = player != null ? player.GetComponent<SpeechBubble>() : null;
            if (bubble != null) bubble.Say(key, seconds);
        }

        private void LieInBed()
        {
            if (player == null) return;
            var position = bed != null ? bed.position : player.transform.position;
            player.LieDown(position + Vector3.up * bedHeight);
        }

        private IEnumerator GetUp()
        {
            if (!player.IsLying) yield break;
            bool done = false;
            player.GetUp(StandPosition(), () => done = true);
            while (!done) yield return null;
        }

        private Vector3 StandPosition()
        {
            var basePosition = bed != null ? bed.position : player.transform.position;
            return new Vector3(basePosition.x + standUpOffsetX, 0f, playerPlaneZ);
        }

        private static bool Pressed(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasPressedThisFrame;
        }

        private static bool Held(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].isPressed;
        }

        private void OnGUI()
        {
            if (!showDebugPrompt || string.IsNullOrEmpty(QuakePrompts.Current)) return;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Screen.height * 0.04f),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(0, Screen.height * 0.75f, Screen.width, Screen.height * 0.1f),
                Deprem.Localization.Texts.Get(QuakePrompts.Current), style);
        }
    }
}
