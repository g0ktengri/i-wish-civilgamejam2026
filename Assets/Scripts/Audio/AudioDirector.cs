using System.Collections.Generic;
using Deprem.Core;
using UnityEngine;

namespace Deprem.Audio
{
    /// <summary>
    /// Oyunun ses yöneticisi (GDD ses tablosu). GameEvents'i dinler: öncü uğultusu, ana deprem uğultusu ve gıcırtılar,
    /// devrilen eşya ve cam, elektrik kesilme tıkı, kurtarmada uzak uğultu ve son saniyelerde kalp atışı, ruh fazında
    /// boğuk ambiyans ve saat tik-takı. Diğer sistemler Sfx.Play ile çağırır (menü, minigame, keşke, final).
    /// Kaynak havuzu: 2D tek seferlik sesler (duraklatmadan etkilenmez), 3D sesler, döngüler ve müzik.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour, ISoundBackend
    {
        [SerializeField] private SoundBank bank;
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.7f;
        [SerializeField] private int voices2D = 12;
        [SerializeField] private int voices3D = 8;

        [Header("Ana deprem")]
        [Tooltip("İki gıcırtı arası (sn)")]
        [SerializeField] private Vector2 creakInterval = new Vector2(2.8f, 5.5f);

        [Header("Kurtarma (RescueTimer gerilimi 0..1)")]
        [Tooltip("Uzak uğultunun sesi: sakin → son saniyeler")]
        [SerializeField] private Vector2 rescueRumbleVolume = new Vector2(0.18f, 0.75f);
        [Tooltip("Kalp atışı aralığı (sn): gerilim başlarken → en sonda")]
        [SerializeField] private Vector2 heartbeatInterval = new Vector2(1.05f, 0.42f);

        [Header("Ruh fazı")]
        [Tooltip("Son kaç saniyede tik-tak yükselir")]
        [SerializeField] private float spiritWarningSeconds = 10f;

        public static AudioDirector Instance { get; private set; }

        private sealed class Loop
        {
            public AudioSource source;
            public float target;
            public float speed;
            public float baseVolume;
        }

        private readonly List<AudioSource> pool2D = new List<AudioSource>();
        private readonly List<AudioSource> pool3D = new List<AudioSource>();
        private readonly Dictionary<string, Loop> loops = new Dictionary<string, Loop>();
        private AudioSource musicA, musicB;
        private string musicKey;
        private float musicFadeSpeed = 1f;

        private bool mainShaking;
        private float nextCreak;
        private float tension;
        private float nextHeartbeat;
        private int lastSpiritSecond = -1;
        private bool tickTock;

        // ------------------------------------------------------------------ Yaşam döngüsü

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Sfx.Backend = this;
            for (int i = 0; i < voices2D; i++) pool2D.Add(CreateSource("2D_" + i, false, true));
            for (int i = 0; i < voices3D; i++) pool3D.Add(CreateSource("3D_" + i, true, false));
            musicA = CreateSource("MusicA", false, true);
            musicB = CreateSource("MusicB", false, true);
            musicA.loop = musicB.loop = true;
        }

        private void Start()
        {
            // Sahnede dinleyici yoksa kameraya eklenir (ses hiç duyulmaz yoksa)
            if (FindAnyObjectByType<AudioListener>() == null)
            {
                var cam = Camera.main;
                (cam != null ? cam.gameObject : gameObject).AddComponent<AudioListener>();
            }
        }

        private void OnEnable()
        {
            GameEvents.LoopStarted += OnLoopStarted;
            GameEvents.PhaseChanged += OnPhaseChanged;
            GameEvents.LoopFailed += OnLoopFailed;
            GameEvents.GameWon += OnGameWon;
            GameEvents.ForeshockStarted += OnForeshock;
            GameEvents.MainQuakeStarted += OnMainQuakeStarted;
            GameEvents.MainQuakeEnded += OnMainQuakeEnded;
            GameEvents.AftershockStarted += OnAftershock;
            GameEvents.ItemToppled += OnItemToppled;
            GameEvents.PlayerInjured += OnPlayerInjured;
            GameEvents.SpiritTimeChanged += OnSpiritTime;
            GameEvents.ItemSecured += OnItemSecured;
            GameEvents.BagItemCollected += OnBagItemCollected;
            GameEvents.FamilyMemberRescued += OnFamilyRescued;
            GameEvents.FamilyMemberEnteredCabin += OnFamilyEnteredCabin;
            GameEvents.PlayerEnteredCabin += OnPlayerEnteredCabin;
            GameEvents.PauseChanged += OnPause;
        }

        private void OnDisable()
        {
            GameEvents.LoopStarted -= OnLoopStarted;
            GameEvents.PhaseChanged -= OnPhaseChanged;
            GameEvents.LoopFailed -= OnLoopFailed;
            GameEvents.GameWon -= OnGameWon;
            GameEvents.ForeshockStarted -= OnForeshock;
            GameEvents.MainQuakeStarted -= OnMainQuakeStarted;
            GameEvents.MainQuakeEnded -= OnMainQuakeEnded;
            GameEvents.AftershockStarted -= OnAftershock;
            GameEvents.ItemToppled -= OnItemToppled;
            GameEvents.PlayerInjured -= OnPlayerInjured;
            GameEvents.SpiritTimeChanged -= OnSpiritTime;
            GameEvents.ItemSecured -= OnItemSecured;
            GameEvents.BagItemCollected -= OnBagItemCollected;
            GameEvents.FamilyMemberRescued -= OnFamilyRescued;
            GameEvents.FamilyMemberEnteredCabin -= OnFamilyEnteredCabin;
            GameEvents.PlayerEnteredCabin -= OnPlayerEnteredCabin;
            GameEvents.PauseChanged -= OnPause;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            if (Sfx.Backend == (ISoundBackend)this) Sfx.Backend = null;
            AudioListener.pause = false;
        }

        private void Update()
        {
            // Sesler gerçek zamanla akar: keşke ekranında timeScale 0 iken de geçişler biter
            float dt = Time.unscaledDeltaTime;
            UpdateLoops(dt);
            UpdateMusic(dt);
            if (AudioListener.pause) return;

            if (mainShaking)
            {
                // Play testi 20: rastgele çıtırtı tekrarı çok fazlaydı; düşen parçalar artık kendi sesini çıkarıyor
                // (DebrisSettle). Burada sadece seyrek, kısık ahşap gıcırtısı kalır
                if (Time.time >= nextCreak)
                {
                    Play2D("quake.creak", 0.45f, Random.Range(-0.7f, 0.7f));
                    nextCreak = Time.time + Random.Range(creakInterval.x, creakInterval.y);
                }
            }

            if (GameState.Phase == GamePhase.Rescue && tension > 0.001f && Time.time >= nextHeartbeat)
            {
                Play2D("rescue.heartbeat", Mathf.Lerp(0.45f, 1f, tension), 0f);
                nextHeartbeat = Time.time + Mathf.Lerp(heartbeatInterval.x, heartbeatInterval.y, tension);
            }
        }

        // ------------------------------------------------------------------ ISoundBackend (Sfx)

        void ISoundBackend.Play(string key, float volume, Vector3? position, float pitch)
        {
            var source = position.HasValue ? Play3D(key, position.Value, volume) : Play2D(key, volume, 0f);
            if (source != null) source.pitch *= pitch;
        }

        void ISoundBackend.PlayMusic(string key, float fadeSeconds) => PlayMusic(key, fadeSeconds);
        void ISoundBackend.StopMusic(float fadeSeconds) => PlayMusic(null, fadeSeconds);

        void ISoundBackend.SetTension(float value)
        {
            float previous = tension;
            tension = Mathf.Clamp01(value);
            if (previous <= 0.001f && tension > 0.001f) nextHeartbeat = Time.time;
            if (GameState.Phase == GamePhase.Rescue)
                SetLoop("quake.rumble", Mathf.Lerp(rescueRumbleVolume.x, rescueRumbleVolume.y, tension), 0.5f,
                    Mathf.Lerp(0.85f, 1.1f, tension));
        }

        // ------------------------------------------------------------------ Olaylar

        private void OnLoopStarted(int loop)
        {
            mainShaking = false;
            tension = 0f;
            lastSpiritSecond = -1;
            StopAllLoops(0.3f);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Foreshock:
                    SetLoop("ambience.night", 0.5f, 1.5f);
                    break;
                case GamePhase.Spirit:
                    StopLoop("ambience.night", 0.8f);
                    StopLoop("quake.rumble", 0.8f);
                    SetLoop("spirit.ambience", 0.8f, 1.5f);
                    StopTension(1f);
                    lastSpiritSecond = -1;
                    break;
                case GamePhase.MainQuake:
                    StopLoop("spirit.ambience", 0.4f);
                    StopLoop("ambience.night", 1f);
                    break;
                case GamePhase.Rescue:
                    SetLoop("ambience.dark", 0.6f, 2f);
                    SetLoop("quake.rumble", rescueRumbleVolume.x, 2f, 0.85f);
                    PlayMusic("music.tension", 4f); // Play testi 20: arkada hafif gerilim
                    break;
                case GamePhase.Failed:
                    mainShaking = false;
                    tension = 0f;
                    StopAllLoops(0.08f);
                    StopTension(0.1f);
                    break;
                case GamePhase.Won:
                    mainShaking = false;
                    tension = 0f;
                    StopAllLoops(2f);
                    StopTension(1.5f);
                    break;
            }
        }

        private void OnLoopFailed(LoseReason reason, ItemId item)
        {
            // Keşke ekranı sessizlikle başlar (GDD); cam kırılmasını keşke ekranı çalar
            mainShaking = false;
            tension = 0f;
            StopAllLoops(0.08f);
        }

        /// <summary>Gerilim müziği çalıyorsa susturur (menü ve final müziğine dokunmaz)</summary>
        private void StopTension(float fadeSeconds)
        {
            if (musicKey == "music.tension") PlayMusic(null, fadeSeconds);
        }

        private void OnGameWon()
        {
            mainShaking = false;
            StopAllLoops(2f);
        }

        private void OnForeshock()
        {
            Play2D("quake.foreshock", 1f, 0f);
        }

        private void OnMainQuakeStarted()
        {
            Play2D("quake.impact", 1f, 0f);
            SetLoop("quake.rumble", 1f, 0.5f, 1f);
            mainShaking = true;
            nextCreak = Time.time + 0.6f;
        }

        private void OnMainQuakeEnded()
        {
            mainShaking = false;
            StopLoop("quake.rumble", 0.9f);
            // Elektrik gider: tık, ardından sessizlik (GDD)
            Play2D("power.off", 1f, 0f);
        }

        private void OnAftershock()
        {
            Play2D("quake.aftershock", 1f, 0f);
            Play2D("quake.creak", 0.7f, Random.Range(-0.5f, 0.5f), 0.5f);
        }

        private void OnItemToppled(ItemId id)
        {
            var item = SecurableItem.Find(id);
            Vector3? at = item != null ? item.transform.position : (Vector3?)null;
            Play("item.crash", 1f, at, 0.35f);
            bool glass = id == ItemId.StairMirror || id == ItemId.KitchenCabinets || id == ItemId.BedFrame || id == ItemId.TvUnit;
            if (glass) Play("glass.break", 0.9f, at, 0.45f);
        }

        private void OnPlayerInjured(int injuries) => Play2D("player.hit", 1f, 0f);
        private void OnItemSecured(ItemId id) => Play2D("item.secured", 0.9f, 0f);
        private void OnBagItemCollected(BagItemId id) => Play2D("bag.collect", 0.9f, 0f);
        private void OnFamilyRescued(FamilyMemberId id) => Play2D("family.rescued", 0.8f, 0f);
        private void OnFamilyEnteredCabin(FamilyMemberId id) => Play2D("cabin.door", 0.8f, 0f);
        private void OnPlayerEnteredCabin() => Play2D("cabin.door", 1f, 0f);

        private void OnSpiritTime(float secondsLeft)
        {
            int second = Mathf.CeilToInt(secondsLeft);
            if (second == lastSpiritSecond || secondsLeft <= 0f) return;
            bool first = lastSpiritSecond < 0;
            lastSpiritSecond = second;
            if (first) return; // faz başında tek tık olmasın
            bool warning = secondsLeft <= spiritWarningSeconds;
            tickTock = !tickTock;
            var source = Play2D(tickTock ? "spirit.tick" : "spirit.tock", warning ? 1f : 0.55f, 0f);
            if (source != null && warning) source.pitch *= 1.12f;
        }

        private void OnPause(bool paused)
        {
            // Döngüler ve 3D sesler durur; 2D sesler (menü tıkları) duraklatma menüsünde de çalar
            AudioListener.pause = paused;
        }

        // ------------------------------------------------------------------ Çalma

        private AudioSource Play(string key, float volume, Vector3? position, float delay = 0f) =>
            position.HasValue ? Play3D(key, position.Value, volume, delay) : Play2D(key, volume, 0f, delay);

        private AudioSource Play2D(string key, float volume, float pan, float delay = 0f)
        {
            var source = Prepare(pool2D, key, volume);
            if (source == null) return null;
            source.panStereo = pan;
            Begin(source, delay);
            return source;
        }

        private AudioSource Play3D(string key, Vector3 position, float volume, float delay = 0f)
        {
            var source = Prepare(pool3D, key, volume);
            if (source == null) return null;
            source.transform.position = position;
            Begin(source, delay);
            return source;
        }

        private static void Begin(AudioSource source, float delay)
        {
            if (delay > 0f) source.PlayDelayed(delay);
            else source.Play();
        }

        private AudioSource Prepare(List<AudioSource> pool, string key, float volume)
        {
            var entry = bank != null ? bank.Find(key) : null;
            if (entry == null) return null;
            var source = FreeSource(pool);
            source.clip = entry.clips[Random.Range(0, entry.clips.Length)];
            source.volume = Mathf.Clamp01(entry.volume * volume * masterVolume * Sfx.MasterVolume * Sfx.EffectsVolume);
            source.pitch = Random.Range(entry.pitch.x, entry.pitch.y);
            return source;
        }

        /// <summary>Boştaki kaynak; hepsi doluysa en çok ilerlemiş olan kesilir</summary>
        private static AudioSource FreeSource(List<AudioSource> pool)
        {
            AudioSource oldest = pool[0];
            float best = -1f;
            foreach (var s in pool)
            {
                if (!s.isPlaying) return s;
                float progress = s.clip != null ? s.time / Mathf.Max(0.01f, s.clip.length) : 1f;
                if (progress > best)
                {
                    best = progress;
                    oldest = s;
                }
            }
            oldest.Stop();
            return oldest;
        }

        // ------------------------------------------------------------------ Döngüler

        private void SetLoop(string key, float volume, float fadeSeconds, float pitch = 1f)
        {
            if (!loops.TryGetValue(key, out var loop))
            {
                var entry = bank != null ? bank.Find(key) : null;
                if (entry == null) return;
                var source = CreateSource("Loop_" + key, false, false);
                source.clip = entry.clips[0];
                source.loop = true;
                source.volume = 0f;
                loop = new Loop { source = source, baseVolume = entry.volume };
                loops[key] = loop;
            }
            loop.target = Mathf.Clamp01(volume * loop.baseVolume * masterVolume);
            loop.speed = fadeSeconds > 0f ? 1f / fadeSeconds : 1000f;
            loop.source.pitch = pitch;
            if (!loop.source.isPlaying)
            {
                // Her döngü farklı yerden başlar: aynı uğultu baştan duyulmasın
                loop.source.time = Random.Range(0f, loop.source.clip.length * 0.9f);
                loop.source.Play();
            }
        }

        private void StopLoop(string key, float fadeSeconds)
        {
            if (!loops.TryGetValue(key, out var loop)) return;
            loop.target = 0f;
            loop.speed = fadeSeconds > 0f ? 1f / fadeSeconds : 1000f;
        }

        private void StopAllLoops(float fadeSeconds)
        {
            foreach (var key in loops.Keys) StopLoop(key, fadeSeconds);
        }

        private void UpdateLoops(float dt)
        {
            foreach (var loop in loops.Values)
            {
                var s = loop.source;
                s.volume = Mathf.MoveTowards(s.volume, loop.target * Sfx.MasterVolume * Sfx.EffectsVolume, loop.speed * dt);
                if (s.volume <= 0f && loop.target <= 0f && s.isPlaying) s.Stop();
            }
        }

        // ------------------------------------------------------------------ Müzik

        private void PlayMusic(string key, float fadeSeconds)
        {
            if (key == musicKey) return;
            musicKey = key;
            musicFadeSpeed = fadeSeconds > 0f ? 1f / fadeSeconds : 1000f;
            // A susarken B yeni müziği başlatır
            (musicA, musicB) = (musicB, musicA);
            var entry = key != null && bank != null ? bank.Find(key) : null;
            if (entry == null) return;
            musicA.clip = entry.clips[0];
            musicA.volume = 0f;
            musicA.Play();
        }

        private void UpdateMusic(float dt)
        {
            var entry = musicKey != null && bank != null ? bank.Find(musicKey) : null;
            float target = entry != null ? entry.volume * musicVolume * masterVolume * Sfx.MasterVolume * Sfx.MusicVolume : 0f;
            musicA.volume = Mathf.MoveTowards(musicA.volume, target, musicFadeSpeed * dt);
            musicB.volume = Mathf.MoveTowards(musicB.volume, 0f, musicFadeSpeed * dt);
            if (musicB.isPlaying && musicB.volume <= 0f) musicB.Stop();
        }

        private AudioSource CreateSource(string name, bool spatial, bool ignorePause)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.ignoreListenerPause = ignorePause;
            if (spatial)
            {
                // Kamera ~11 m uzakta: ses çoğunlukla duyulur, yönü sağ-sol kulaktan gelir
                source.spatialBlend = 0.65f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 8f;
                source.maxDistance = 40f;
                source.dopplerLevel = 0f;
            }
            return source;
        }
    }
}
