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

        [Header("Ana deprem")]
        [SerializeField] private float mainQuakeDuration = 12f;
        [SerializeField, Range(0f, 1f)] private float mainQuakeIntensity = 1f;
        [Tooltip("Eşyalar bu aralıkta, rastgele sırayla devrilir (deprem başından itibaren sn)")]
        [SerializeField] private Vector2 toppleWindow = new Vector2(1.5f, 5f);
        [Tooltip("C uyarısından sonra basmaya başlamak için süre")]
        [SerializeField] private float duckCoverGrace = 1.5f;
        [Tooltip("C kısa süre bırakılırsa affedilir")]
        [SerializeField] private float duckCoverTolerance = 0.35f;

        [Header("Artçılar")]
        [SerializeField] private float aftershockDuration = 3.5f;
        [SerializeField, Range(0f, 1f)] private float aftershockIntensity = 0.5f;
        [SerializeField] private float aftershockGrace = 1.2f;
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
        [SerializeField] private Key standUpKey = Key.F;
        [SerializeField] private Key duckCoverKey = Key.C;

        [Header("Geliştirme")]
        [Tooltip("Sahnede döngü yöneticisi yoksa bu döngüyle başlat (0 = kapalı)")]
        [SerializeField, Range(0, GameConstants.LoopCount)] private int devStartLoop;
        [Tooltip("HUD yokken tuş ipucunu ekranın ortasında göster")]
        [SerializeField] private bool showDebugPrompt = true;

        private PlayerController player;
        private HouseLights lights;
        private Transform bed;
        private readonly List<ItemToppler> topplers = new List<ItemToppler>();
        private bool[] zoneTriggered;
        private bool aftershockRunning;

        private void Awake()
        {
            player = FindFirstObjectByType<PlayerController>();
            lights = FindFirstObjectByType<HouseLights>();
            var bedMarker = GameObject.Find("Marker_PlayerBed");
            bed = bedMarker != null ? bedMarker.transform : null;
            topplers.AddRange(FindObjectsByType<ItemToppler>(FindObjectsSortMode.None));
            zoneTriggered = new bool[aftershockZones.Length];
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
                // Döngü 1 (senaryolu): uyumaya devam eder
                yield return new WaitForSeconds(scriptedWaitBeforeMainQuake);
            }
            else if (!GameState.StandUpUnlocked)
            {
                // Döngü 2 (senaryolu): kalkar
                yield return GetUp();
                yield return new WaitForSeconds(scriptedWaitBeforeMainQuake * 0.5f);
            }
            else
            {
                // Döngü 3+: "F: Kalk"
                QuakePrompts.Set(QuakePrompts.StandUp);
                bool stoodUp = false;
                for (float t = 0f; t < standUpWindow; t += Time.deltaTime)
                {
                    if (Pressed(standUpKey))
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
                // Döngü 2 (senaryolu): kapıya koşar, üstüne eşya düşer
                player.InputEnabled = false;
                player.SetScriptedInput(1f);
                yield return new WaitForSeconds(1.2f);
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

        /// <summary>Süre boyunca C basılı tutulmalı. Tutarken oyuncu çömelir ve hareket edemez.</summary>
        private IEnumerator DuckCoverHold(float duration, float grace, Action<bool> result)
        {
            QuakePrompts.Set(QuakePrompts.DuckCover);
            var check = new DuckCoverHoldCheck(grace, duckCoverTolerance);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                bool holding = Held(duckCoverKey);
                check.Update(Time.deltaTime, holding);
                player.InputEnabled = !holding;
                player.SetScriptedInput(0f, crouch: holding);
                if (check.Failed) break;
                yield return null;
            }
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
            foreach (var toppler in topplers) toppler.Rattle(intensity);
        }

        private IEnumerator HitByDebris()
        {
            QuakePrompts.Clear();
            var debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.name = "Debris";
            debris.transform.localScale = new Vector3(0.6f, 0.35f, 0.5f);
            debris.transform.position = player.transform.position + Vector3.up * 2.6f;
            debris.AddComponent<Rigidbody>().mass = 15f;
            yield return new WaitForSeconds(0.8f);
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
            QuakePrompts.Clear();
            aftershockRunning = false;
        }

        private void Restart(IEnumerator routine)
        {
            StopQuake();
            StartCoroutine(routine);
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
            return new Vector3(basePosition.x + 0.8f, 0f, playerPlaneZ);
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
