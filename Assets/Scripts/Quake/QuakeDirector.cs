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
        [Tooltip("M18: döngü 2'de 'Az önce bunu yaşamadım mı?' balonunun süresi")]
        [SerializeField] private float dejaVuSeconds = 2.5f;
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
        [Tooltip("Sarsıntı bu şiddetin üstündeyken zıplanamaz")]
        [SerializeField] private float noJumpIntensity = 0.1f;
        [Tooltip("M15: merdiven/yol bittikten sonra artçı alanının sayılması için bekleme (sn)")]
        [SerializeField] private float aftershockAfterPathDelay = 0.75f;
        [Tooltip("Oyuncu bu alanlara girince artçı olur (her deneme bir kez). GDD: koridor, merdiven, üst kat")]
        [SerializeField] private AftershockZone[] aftershockZones =
        {
            new AftershockZone { name = "Koridora giriş", min = new Vector2(6.3f, -0.5f), max = new Vector2(7.2f, 1f) },
            // M15: merdiven çıkışının (x ~14,5) 1 m sağı; oyuncu sahanlıktaki konsolun (güvenli nokta 14,45-15,95) önünde
            new AftershockZone { name = "Merdiven başı", min = new Vector2(15.3f, 3.5f), max = new Vector2(16.8f, 5f) },
            new AftershockZone { name = "Üst kat, kardeşin odası", min = new Vector2(5.5f, 3.5f), max = new Vector2(6.8f, 5f) },
        };

        [Header("Oyuncu")]
        [SerializeField] private float playerPlaneZ = 1f;
        [Tooltip("Şilte üstü (HouseLayout yatak, Play testi 9'da büyüdü)")]
        [SerializeField] private float bedHeight = 0.66f;
        [Tooltip("Kalkınca yatağın ortasından bu kadar sağda durur. Kalkma çizimi yattığı yerde ayağa kalkıyor " +
                 "(PlayerVisual'daki yatan ve ayakta kaymaları aynı x); 0 dışı değer kalkarken yana kaydırır")]
        [SerializeField] private float standUpOffsetX = 0f;
        [Tooltip("Yatağın ortasından bu kadar önde (kameraya doğru, m) yatar: ön kenarda, sarkan bacağı şilte örtmez")]
        [SerializeField] private float bedEdgeOffsetZ = 0.75f;
        // M40: tuşlar ayarlardan değiştirilebilir (KeyBindings). Play testi 22: çök-kapan eğilme tuşuyla (S), C yok
        private static Key standUpKey => KeyBindings.Interact;
        private static Key walkLeftKey => KeyBindings.Left;
        private static Key walkRightKey => KeyBindings.Right;
        private static Key duckCoverKey => KeyBindings.Crouch;

        [Tooltip("M19: raf yatağa düşünce kısa kamera sarsıntısı")]
        [SerializeField, Range(0f, 1f)] private float shelfImpactShake = 0.85f;

        [Header("Tavandan düşen parça")]
        [Tooltip("Sıva parçası materyali (QuakeSetup atar)")]
        [SerializeField] private Material debrisMaterial;
        [Tooltip("Çök-kapan sırasında tavandan taş düşme aralığı (sn, rastgele)")]
        [SerializeField] private Vector2 rubbleInterval = new Vector2(0.35f, 0.7f);
        [Tooltip("Parça karakter düzleminin bu kadar önünde düşer (m)")]
        [SerializeField] private float debrisFrontOffset = 0.15f;
        [Tooltip("Tavandan kopan parçanın başa değmesi için en fazla bekleme (sn): değdiği an ölme animasyonu başlar")]
        [SerializeField] private float debrisImpactDelay = 1.5f;
        [Tooltip("Öldükten sonra kayıp ekranından önce sahnede kalma (sn): ölme kareleri biter, ruh yükselir, ekran kararır (Play testi 25)")]
        [SerializeField] private float deathLinger = 2.6f;
        [Header("Intro: giriş kapısı yakın çekimi")]
        [Tooltip("Kameranın kapıya kayma süresi (sn); dönüş bunun 0,6'sı")]
        [SerializeField] private float entranceCameraTravel = 1.6f;
        [Tooltip("Dolap devrildikten sonra kapıda kalma (sn)")]
        [SerializeField] private float entranceHold = 1.2f;
        [Tooltip("Yakın çekimde görüş açısı oranı (< 1 yaklaştırır)")]
        [SerializeField] private float entranceCloseUpFov = 0.6f;

        [Tooltip("Play testi 24: kardeş bulunamayınca gelen artçıda tavanın çökmesinden önceki sarsıntı (sn)")]
        [SerializeField] private float fatalAftershockLead = 1.2f;
        [Tooltip("Döngü 1: raf yatan oyuncunun üstüne düşer; kaplama kutusunun merkezi oyuncunun kökünden bu kadar uzakta")]
        [SerializeField] private Vector3 shelfLandOffset = new Vector3(-0.15f, 0.24f, -0.05f);
        [Tooltip("Rafın duvardan oyuncunun üstüne düşüşü (sn)")]
        [SerializeField] private float shelfDropTime = 0.45f;
        [Tooltip("Ana depremde düşen dekor oranı (tablo, saat, vazo...); kalanlar yamuk kalır")]
        [SerializeField, Range(0f, 1f)] private float mainQuakeDecorFall = 0.7f;
        [Tooltip("Artçıda kalan dekordan düşen oran")]
        [SerializeField, Range(0f, 1f)] private float aftershockDecorFall = 0.4f;

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
        private float offPathTime;

        private static QuakeDirector instance;
        private bool fatalAftershockRunning;

        /// <summary>
        /// Play testi 24: kardeş fenersiz bulunamayınca artçı gelir, tavan oyuncunun üstüne çöker, döngü reason ile
        /// biter. Kurtarma fazında değilse ya da deprem sistemi yoksa false (çağıran kendisi bitirir).
        /// </summary>
        public static bool RequestFatalAftershock(LoseReason reason, ItemId item = ItemId.None)
        {
            if (instance == null || instance.player == null || instance.fatalAftershockRunning || GameState.Phase != GamePhase.Rescue)
                return false;
            instance.StopAllCoroutines();
            instance.StartCoroutine(instance.FatalAftershock(reason, item));
            return true;
        }

        private IEnumerator FatalAftershock(LoseReason reason, ItemId item)
        {
            fatalAftershockRunning = true;
            aftershockRunning = true;
            player.InputEnabled = false;
            player.SetScriptedInput(0f);
            GameEvents.RaiseAftershockStarted();
            StartCoroutine(Shake(Mathf.Min(1f, aftershockIntensity * 1.4f), fatalAftershockLead + 1.5f));
            StartCoroutine(DropDecor(aftershockDecorFall, 0.1f, fatalAftershockLead));
            if (effects != null) effects.Burst(0.6f);
            yield return new WaitForSeconds(fatalAftershockLead);
            yield return HitByDebris();
            Fail(reason, item);
        }

        private void Awake()
        {
            instance = this;
            player = FindFirstObjectByType<PlayerController>();
            lights = FindFirstObjectByType<HouseLights>();
            var bedMarker = GameObject.Find("Marker_PlayerBed");
            bed = bedMarker != null ? bedMarker.transform : null;
            topplers.AddRange(FindObjectsByType<ItemToppler>(FindObjectsSortMode.None));
            zoneTriggered = new bool[aftershockZones.Length];
            if (!TryGetComponent(out duckCoverBar)) duckCoverBar = gameObject.AddComponent<DuckCoverBar>();
            if (player != null) duckCoverBar.Follow(player.transform);
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
                    // Döngü 1–2: çök-kapan-tutun henüz bilinmiyor, eğilme kapalı (Play testi 11); döngü 3+ açık
                    if (player != null) player.CrouchEnabled = GameState.StandUpUnlocked;
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
                    Say("bubble.rescue_start", 4f); // Play testi 5: kurtarma hedefi anlaşılsın
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
            if (player != null && player.IsOnPath) offPathTime = 0f;
            else offPathTime += Time.deltaTime;
            if (GameState.Phase != GamePhase.Rescue || aftershockRunning || player == null || player.IsOnPath) return;
            // M15: merdivenden yeni çıkan oyuncu önce yürüyüp güvenli noktayı görebilsin
            if (offPathTime < aftershockAfterPathDelay) return;
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
            // Intro sinematiği: 1. döngünün ilk denemesinde oyuncu okuldan gelip yatar; sonra gece açılır
            bool intro = IntroCinematic.ShouldPlay;
            if (intro) yield return Intro.Play();
            LieInBed();
            if (intro) StartCoroutine(Intro.FadeIn());
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
                // M18: önce yeniden doğduğunu fark eder
                Say("bubble.deja_vu", dejaVuSeconds);
                yield return new WaitForSeconds(dejaVuSeconds);
                Say("bubble.panic_run", 2.5f);
                yield return new WaitForSeconds(1f);
                yield return GetUp();
                player.InputEnabled = true;
                yield return new WaitForSeconds(scriptedWaitBeforeMainQuake * 0.5f);
            }
            else
            {
                // Döngü 3+ (serbest): "F: Kalk"; yürümeye başlamak da kaldırır
                // M18: daha önce yaşadığını bilir
                Say(GameState.FailedAttempts > 0 ? "bubble.deja_vu_again" : "bubble.wake", 2.5f);
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

            // Ruh fazından dönüş (döngü 4+): ruh bedene döndü (Berke, M31); beden yataktan kalkma animasyonuyla
            // kalkar (Play testi 20: odanın ortasında beliriyordu), sonra girdi açılır
            bool spiritReturn = player.IsLying && GameState.SpiritPhaseUnlocked;
            if (spiritReturn) StartCoroutine(RiseAfterSpirit());

            // Intro sinematiği: giriş kapısının önüne portmanto devrilir (her ana depremde). Intronun oynadığı
            // denemede önce kamera kapıya kayıp yaklaşır, dolap devrilir, sonra geri döner; deprem ondan sonra işler
            bool closeUp = Intro.PlayedThisAttempt && EntranceBlocker.Instance != null;
            if (closeUp)
            {
                StartCoroutine(Shake(mainQuakeIntensity, entranceCloseUpTime));
                yield return EntranceCloseUp();
            }
            else if (EntranceBlocker.Instance != null) StartCoroutine(ToppleEntranceBlocker(UnityEngine.Random.Range(0.6f, 1.4f)));

            StartCoroutine(Shake(mainQuakeIntensity, mainQuakeDuration));
            StartCoroutine(ToppleItems());
            StartCoroutine(DropDecor(mainQuakeDecorFall, toppleWindow.x * 0.5f, toppleWindow.y + 1.5f));
            if (effects != null) effects.Burst(1f);

            if (player.IsLying && !spiritReturn)
            {
                // Kalkmadı: yatağın üstündeki raf üstüne düşer.
                // M19: yatan oyuncunun dik kapsülü rafı havada tutmasın; raf yatağın şiltesine, yatan görselin önüne düşer
                var shelf = SecurableItem.Find(ItemId.BedShelf);
                bool shelfFalls = shelf == null || !shelf.IsSecured;
                if (shelfFalls) player.Body.detectCollisions = false;
                yield return new WaitForSeconds(toppleWindow.x + 0.45f);
                if (shelfFalls) QuakeCameraShake.Intensity = Mathf.Max(QuakeCameraShake.Intensity, shelfImpactShake);
                yield return new WaitForSeconds(1.05f);
                if (shelfFalls)
                {
                    // Play testi 5: sadece raf düşmesi ciddi durmuyordu; ağır raftan sonra tavan da yatağa çöker
                    yield return CollapseOnBed();
                    Fail(LoseReason.IgnoredForeshock, ItemId.BedShelf);
                    yield break;
                }
                player.Body.detectCollisions = true;
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

        private IEnumerator ToppleEntranceBlocker(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (EntranceBlocker.Instance != null) EntranceBlocker.Instance.Topple();
        }

        /// <summary>
        /// Intro sinematiği: ana deprem başlarken kamera giriş kapısına kayıp yaklaşır, portmanto devrilip kapının
        /// önünü kapatır, kamera geri döner. Oyuncu bu sırada yönlendiremez (Cinematic).
        /// </summary>
        private IEnumerator EntranceCloseUp()
        {
            var blocker = EntranceBlocker.Instance;
            var door = EntranceDoor.Instance;
            var focus = new GameObject("EntranceCloseUp").transform;
            var at = door != null ? door.Threshold : blocker.transform.position;
            focus.position = new Vector3(at.x - 0.6f, at.y + 1.1f, at.z);
            Cinematic.Begin(false);
            PlayerCameraTarget.Focus(focus, entranceCloseUpFov);
            yield return new WaitForSeconds(entranceCameraTravel);
            blocker.Topple();
            while (blocker.IsFalling) yield return null;
            yield return new WaitForSeconds(entranceHold);
            PlayerCameraTarget.Release();
            Destroy(focus.gameObject);
            yield return new WaitForSeconds(entranceCameraTravel * 0.6f);
            Cinematic.End();
        }

        private float entranceCloseUpTime => entranceCameraTravel * 1.6f + 1.6f + entranceHold;

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
            bool lying = player != null && player.IsLying;

            float elapsed = 0f;
            foreach (var entry in schedule)
            {
                if (entry.time > elapsed) yield return new WaitForSeconds(entry.time - elapsed);
                elapsed = Mathf.Max(elapsed, entry.time);
                var shelfItem = entry.toppler.GetComponent<SecurableItem>();
                if (lying && shelfItem != null && shelfItem.Id == ItemId.BedShelf)
                {
                    // Play testi 17: fizikle içinden geçip yatağın önüne düşüyor, kayboluyordu. Senaryolu: oyuncunun
                    // üstüne kapaklanır ve orada kalır; tavan parçaları da üstüne yığılır
                    var (center, rotation) = ShelfLanding(entry.toppler);
                    StartCoroutine(entry.toppler.DropOnto(center, rotation, shelfDropTime));
                }
                else entry.toppler.Topple();
                var item = entry.toppler.GetComponent<SecurableItem>();
                if (item != null) GameEvents.RaiseItemToppled(item.Id);
            }
        }

        /// <summary>Asılı ve masadaki dekorun bir kısmı rastgele anlarda düşer (kalanlar yamuk kalır)</summary>
        private IEnumerator DropDecor(float fraction, float from, float to)
        {
            var pending = new List<(float time, FallingDecor decor)>();
            foreach (var decor in FallingDecor.All)
                // Masadakiler ana depremde hep düşer (Play testi 19: vazo masada kalıyordu)
                if (!decor.HasFallen && (UnityEngine.Random.value < fraction || decor.OnSurface && fraction >= mainQuakeDecorFall))
                    pending.Add((UnityEngine.Random.Range(from, to), decor));
            pending.Sort((a, b) => a.time.CompareTo(b.time));
            float elapsed = 0f;
            foreach (var (time, decor) in pending)
            {
                if (time > elapsed) yield return new WaitForSeconds(time - elapsed);
                elapsed = Mathf.Max(elapsed, time);
                if (decor != null) decor.Drop();
            }
        }

        /// <summary>Yatan oyuncunun üstüne kapaklanmış rafın duruşu: öne yüzüstü, hafif yamuk</summary>
        public (Vector3 center, Quaternion rotation) ShelfLanding(ItemToppler shelf)
        {
            var rotation = Quaternion.AngleAxis(5f, Vector3.forward) * Quaternion.AngleAxis(-90f, Vector3.right) *
                           shelf.transform.rotation;
            return (player.transform.position + shelfLandOffset, rotation);
        }

        // ------------------------------------------------------------------ Artçılar

        private IEnumerator AftershockRoutine()
        {
            aftershockRunning = true;
            GameEvents.RaiseAftershockStarted();
            StartCoroutine(Shake(aftershockIntensity, aftershockDuration));
            StartCoroutine(DropDecor(aftershockDecorFall, 0.2f, aftershockDuration * 0.6f));
            if (effects != null) effects.Burst(0.4f);

            bool survived = true;
            yield return DuckCoverHold(aftershockDuration, aftershockGrace, result => survived = result);
            if (!survived)
            {
                player.SpeedMultiplier = injuredSpeedMultiplier;
                Say("bubble.injured", 2.5f); // yavaşlamanın sebebi anlaşılsın
                Feedback.Play(0.45f);
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
            QuakePrompts.Set(DuckCoverLearned ? "" : QuakePrompts.DuckCover); // Play testi 27: tuş ipucu sadece öğrenene kadar
            SafeSpot.ShowAll(true);
            var check = new DuckCoverHoldCheck(grace, duckCoverTolerance);
            bool warnedNotSafe = false;
            float nextRubble = rubbleInterval.x;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                bool pressing = Held(duckCoverKey);
                bool inSafeSpot = SafeSpot.AnyContains(player.transform.position);
                bool holding = pressing && inSafeSpot;
                // Play testi 11: çök-kapan sırasında yukarıdan taş, sıva yağsın (masanın üstüne ve iki yana)
                if (t >= nextRubble && t < duration - 0.8f)
                {
                    DropRubbleAround(holding);
                    nextRubble = t + UnityEngine.Random.Range(rubbleInterval.x, rubbleInterval.y);
                }
                // Güvenli masanın dışında C işe yaramaz: oyuncu bunu görsün (ipucu değişir, masa daha çok parlar, bir kez konuşur)
                bool wrongPlace = pressing && !inSafeSpot;
                QuakePrompts.Set(wrongPlace ? QuakePrompts.DuckCoverFind : DuckCoverLearned ? "" : QuakePrompts.DuckCover);
                SafeSpot.Emphasize(wrongPlace);
                if (wrongPlace && !warnedNotSafe)
                {
                    warnedNotSafe = true;
                    Say("bubble.not_safe_here", 2.5f);
                }
                check.Update(Time.deltaTime, holding);
                player.InputEnabled = !holding;
                player.SetScriptedInput(0f, crouch: holding);
                if (duckCoverBar != null) duckCoverBar.Show(1f - t / duration, holding);
                if (check.Failed) break;
                yield return null;
            }
            if (duckCoverBar != null) duckCoverBar.Hide();
            SafeSpot.Emphasize(false);
            SafeSpot.ShowAll(false);
            QuakePrompts.Clear();
            player.SetScriptedInput(0f);
            player.InputEnabled = true;
            if (!check.Failed && !DuckCoverLearned)
            {
                PlayerPrefs.SetInt(DuckCoverLearnedKey, 1);
                PlayerPrefs.Save();
            }
            result(!check.Failed);
        }

        // Play testi 27: "S: Çök-Kapan-Tutun" tuş ipucu bir kez başarıyla yapılana kadar gösterilir, sonra gösterilmez
        // (yanlış yerde basınca çıkan uyarı kalır)
        private const string DuckCoverLearnedKey = "tutorial.duck_cover";
        private static bool DuckCoverLearned => PlayerPrefs.GetInt(DuckCoverLearnedKey, 0) == 1;

        /// <summary>
        /// Çök-kapan sırasında oyuncunun çevresine tavandan taş ve sıva düşer; oyuncuya değmez (ölümdeki darbe değil).
        /// Masanın altındaysa bir kısmı masanın üstüne düşer (güvenli masaların tablasında ince çarpışma var).
        /// </summary>
        private void DropRubbleAround(bool underTable)
        {
            var p = player.transform.position;
            float ceilingY = p.y + 3f;
            foreach (var hit in Physics.RaycastAll(p + Vector3.up * player.StandingHeight, Vector3.up, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.rigidbody == player.Body || (hit.rigidbody != null && !hit.rigidbody.isKinematic)) continue;
                ceilingY = Mathf.Min(ceilingY, hit.point.y);
            }

            int count = UnityEngine.Random.Range(2, 4);
            for (int i = 0; i < count; i++)
            {
                bool onTable = underTable && UnityEngine.Random.value < 0.45f;
                float x = onTable
                    ? p.x + UnityEngine.Random.Range(-0.5f, 0.5f)
                    : p.x + Mathf.Sign(UnityEngine.Random.value - 0.5f) * UnityEngine.Random.Range(0.55f, 1.8f);
                // Masanın tablası Z 0,15–0,6'da (HouseBuilder): üstüne düşecekler oraya, diğerleri karakterin önüne
                float z = onTable ? UnityEngine.Random.Range(0.3f, 0.5f) : playerPlaneZ - debrisFrontOffset;
                float size = UnityEngine.Random.Range(0.08f, 0.22f);
                var position = new Vector3(x, ceilingY - 0.1f - UnityEngine.Random.Range(0f, 0.2f), z);
                SpawnDebris("Rubble", position, new Vector3(size, size * UnityEngine.Random.Range(0.6f, 1f), Mathf.Min(size, 0.18f)), 4f);
            }
            QuakeCameraShake.Intensity = Mathf.Max(QuakeCameraShake.Intensity, 0.75f);
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
            FallingDecor.ShakeAll(intensity); // Play testi 17: tablolar sallanır, yamulur
            // ENTEGRASYON B11: sallanırken zıplayınca karakter takılıp yavaşlıyordu; depremde zıplanmaz
            if (player != null) player.JumpEnabled = intensity < noJumpIntensity;
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
            var plaster = SpawnDebris("Plaster", center, new Vector3(0.9f, 0.1f, 0.25f), 25f);
            for (int i = 0; i < 6; i++)
            {
                var offset = new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), UnityEngine.Random.Range(-0.1f, 0.3f), 0f);
                float size = UnityEngine.Random.Range(0.1f, 0.24f);
                SpawnDebris("Chunk", center + offset, new Vector3(size, size, Mathf.Min(size, 0.2f)), 2f);
            }
            QuakeCameraShake.Intensity = Mathf.Max(QuakeCameraShake.Intensity, 0.6f);

            // Play testi 17: ölme animasyonu kayıp ekranını beklemesin; parça başa değdiği an başlar. Beden artık
            // çarpışmaz: parçalar başta sekmez, yere yığılan bedenin üstüne düşer ve orada kalır (DebrisSettle)
            // Play testi 26: sabit 0,3 sn beklerken parça daha havadaydı ("kafaya değmeden ölüyoruz"). Parçanın alt
            // kenarı başın tepesine inince ölüm başlar (en fazla debrisImpactDelay sn)
            float headTop = player.transform.position.y + player.StandingHeight;
            for (float t = 0f; t < debrisImpactDelay && plaster != null; t += Time.deltaTime)
            {
                if (plaster.transform.position.y - 0.05f <= headTop) break;
                yield return null;
            }
            player.Die();
            Feedback.Play(1f); // Play testi 25: sert darbe, dünya solar, bedenden ruh yükselir, ekran kararır
            yield return new WaitForSeconds(deathLinger);
        }

        /// <summary>
        /// Yatakta kalan oyuncunun üstüne tavan çöker: geniş beton parçası, sıva ve kırıntılar yatağın üstüne,
        /// sert sarsıntı ve toz patlaması. Sonra döngü biter.
        /// </summary>
        private IEnumerator CollapseOnBed()
        {
            QuakePrompts.Clear();
            var center = player.transform.position;
            float ceilingY = center.y + 2.8f;
            foreach (var hit in Physics.RaycastAll(center + Vector3.up * 0.3f, Vector3.up, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.rigidbody == player.Body || (hit.rigidbody != null && !hit.rigidbody.isKinematic)) continue;
                ceilingY = Mathf.Min(ceilingY, hit.point.y);
            }

            var top = new Vector3(center.x, ceilingY - 0.15f, center.z - debrisFrontOffset);
            SpawnDebris("CeilingSlab", top, new Vector3(1.6f, 0.18f, 0.6f), 120f);
            for (int i = 0; i < 9; i++)
            {
                var offset = new Vector3(UnityEngine.Random.Range(-0.9f, 0.9f), UnityEngine.Random.Range(-0.3f, 0.1f), 0f);
                float size = UnityEngine.Random.Range(0.12f, 0.3f);
                SpawnDebris("Chunk", top + offset, new Vector3(size, size, Mathf.Min(size, 0.25f)), 6f);
            }
            if (effects != null) effects.Burst(1.2f);
            QuakeCameraShake.Intensity = 1f;
            Feedback.Play(1f);
            // Yatan oyuncunun dik kapsülü parçaları havada tutmasın: parçalar yatağın şiltesine düşer
            player.Body.detectCollisions = false;
            yield return new WaitForSeconds(1.4f);
            player.Body.detectCollisions = true;
            yield return new WaitForSeconds(Mathf.Max(0f, deathLinger - 1.4f)); // ruh yükselip ekran kararsın
        }

        private GameObject SpawnDebris(string name, Vector3 position, Vector3 size, float mass)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.position = position;
            piece.transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-15f, 15f));
            piece.transform.localScale = size;
            if (debrisMaterial != null) piece.GetComponent<MeshRenderer>().sharedMaterial = debrisMaterial;
            DressAsPixelRubble(piece, name, size);
            var body = piece.AddComponent<Rigidbody>();
            body.mass = mass;
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            body.linearVelocity = Vector3.down * 1.5f;
            // Play testi 23: yere yığılan taşlar oyuncuyu sıkıştırıp buga sokuyordu; parçalar oyuncuyla çarpışmaz
            var playerCollider = player != null ? player.GetComponent<Collider>() : null;
            if (playerCollider != null) Physics.IgnoreCollision(piece.GetComponent<Collider>(), playerCollider);
            // Play testi 20: düşme sesi; büyük parça (tavan, sıva) daha gür
            piece.AddComponent<DebrisSettle>().SetImpactSound(mass >= 20f ? "item.crash" : "quake.debris",
                Mathf.Clamp(0.35f + mass / 20f, 0.35f, 1f));
            return piece;
        }

        private readonly Dictionary<Texture2D, Material> rubbleMaterials = new Dictionary<Texture2D, Material>();

        /// <summary>
        /// Berke'nin play testi: düz kutular pixel art'a uymuyordu. Kutu fizik için kalır ama görünmez;
        /// önüne (kameraya bakan yüze) düzensiz kenarlı, konturlu pixel moloz dokusu konur.
        /// </summary>
        private void DressAsPixelRubble(GameObject piece, string name, Vector3 size)
        {
            if (debrisMaterial == null) return;
            var texture = name == "CeilingSlab"
                ? PixelRubble.Slab(Mathf.RoundToInt(size.x * 64f), Mathf.RoundToInt(size.y * 64f) + 6)
                : PixelRubble.Chunk(UnityEngine.Random.Range(0, PixelRubble.AtlasTiles));
            if (!rubbleMaterials.TryGetValue(texture, out var material))
            {
                material = new Material(debrisMaterial) { name = "PixelRubble" };
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.EnableKeyword("_ALPHATEST_ON");
                rubbleMaterials[texture] = material;
            }

            piece.GetComponent<MeshRenderer>().enabled = false;
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name = "PixelFace";
            Destroy(face.GetComponent<Collider>());
            face.transform.SetParent(piece.transform, false);
            face.transform.localPosition = new Vector3(0f, 0f, -0.51f); // kutunun kameraya bakan yüzünün önü
            var renderer = face.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
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
            player.LieDown(position + Vector3.up * bedHeight + Vector3.back * bedEdgeOffsetZ);
        }

        private HitFeedback feedback;
        private IntroCinematic intro;

        private IntroCinematic Intro
        {
            get
            {
                if (intro == null) intro = GetComponent<IntroCinematic>();
                if (intro == null) intro = gameObject.AddComponent<IntroCinematic>();
                return intro;
            }
        }

        /// <summary>Darbe geri bildirimi (Play testi 23); prefab'da yoksa eklenir</summary>
        private HitFeedback Feedback
        {
            get
            {
                if (feedback == null) feedback = GetComponent<HitFeedback>();
                if (feedback == null) feedback = gameObject.AddComponent<HitFeedback>();
                return feedback;
            }
        }

        private IEnumerator RiseAfterSpirit()
        {
            yield return GetUp();
            player.InputEnabled = true;
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
