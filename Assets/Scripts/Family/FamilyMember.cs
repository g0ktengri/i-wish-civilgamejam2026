using System.Collections.Generic;
using Deprem.Core;
using Deprem.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deprem.Family
{
    public enum FamilyState
    {
        Waiting,   // kendi odasında, sağ
        Ducking,   // ana depremde çök-kapan-tutun
        Injured,   // yaralı, tedavi bekliyor (baba: ayağının üstünde kaya, kan kaybı)
        Hit,       // üstüne eşya devrildi
        Following, // oyuncuyu takip ediyor
        InCabin,
        WalkingToCabin, // Berke'nin play testi 16: kayası kaldırılan baba kabine kendisi, yavaşça yürür
        Bleeding,       // kabine varınca kanaması başladı, çantada ilk yardım yok: kabin girişinde yatıyor
    }

    /// <summary>
    /// Bir aile üyesi (GDD "Artçılar ve kurtarma"):
    ///   - dangerItem sabitlenmeden devrilirse üstüne düşer; oyuncu yanına gelince FamilyHitByItem.
    ///   - requiredBagItem çantada yoksa oyuncu yanına gelince MissingBagItem (baba: ilk yardım, kardeş: el feneri).
    ///   - Yaralıysa F basılı tutularak tedavi edilir, değilse F ile toplanır; sonra takip eder.
    ///   - Berke'nin play testi 16: babanın ayağına kaya düşmüş. F 3 sn basılı tutulunca kaya kalkar (çanta şart değil),
    ///     baba teşekkür eder ve kabine kendisi aksayarak yürür (FamilyDirector).
    ///   - Kan kaybı kabinde ortaya çıkar (cabinBagItem = ilk yardım): çantada varsa yarasını sarıp girer; yoksa girişte
    ///     yığılır, seslenir, oyuncu görünce MissingBagItem (oyuncu ilk yardım çantasının gerektiğini orada fark eder).
    /// Takip ve kabin FamilyDirector'da.
    /// </summary>
    public sealed class FamilyMember : MonoBehaviour, IInteractable, IPromptSource
    {
        private static readonly List<FamilyMember> AllMembers = new List<FamilyMember>();
        public static IReadOnlyList<FamilyMember> All => AllMembers;

        [SerializeField] private FamilyMemberId id;
        [SerializeField] private string homeMarker = "Marker_Father";
        [Tooltip("Sabitlenmeden devrilirse bu üyenin üstüne düşen eşya")]
        [SerializeField] private ItemId dangerItem;
        [Tooltip("Kurtarmak için çantada olması gereken malzeme (yoksa None)")]
        [SerializeField] private BagItemId requiredBagItem;
        [Tooltip("Kabine varınca gereken malzeme (baba: ilk yardım; yoksa girişte kanayıp yığılır)")]
        [SerializeField] private BagItemId cabinBagItem;
        [Tooltip("Ana depremden sonra yaralı mı (baba: pencere camı, GDD)")]
        [SerializeField] private bool injuredAfterQuake;

        [Header("Saklanma (kardeş, Play testi 24)")]
        [Tooltip("Kurtarma fazında karanlıkta saklanır, görünmez; requiredBagItem (el feneri) varsa yaklaşınca bulunur")]
        [SerializeField] private bool hidesInDark;
        [SerializeField] private float findRange = 2.2f;
        [Tooltip("Fenersiz oyuncu odada bu kadar arayınca artçı gelir, tavan üstüne çöker")]
        [SerializeField] private float searchTime = 6f; // Play testi 26: odaya girişten itibaren sayılıyor

        [SerializeField] private float treatHoldTime = 3f;
        [Tooltip("Yaralıyken ayağının üstündeki kayanın materyali (siva.mat kopyası, doku PixelRubble); boşsa kaya yok")]
        [SerializeField] private Material rockMaterial;
        [SerializeField] private Vector3 rockOffset = new Vector3(-0.22f, 0.2f, -0.25f);
        [SerializeField] private Vector2 rockSize = new Vector2(0.85f, 0.65f);
        [Tooltip("F basılı tutulurken taşın kalktığı yükseklik (ilerlemeyle)")]
        [SerializeField] private float rockLiftHeight = 0.2f;
        [SerializeField] private float rockRollDistance = 0.9f;
        [SerializeField] private float rockRollTime = 0.55f;
        [SerializeField] private int pebbleCount = 6;
        [Tooltip("Oyuncu bu kadar yaklaşınca durumu fark eder (aynı kat, X ekseninde)")]
        [SerializeField] private float discoverRange = 3.5f;
        [Tooltip("Durumu görülünce çalan müzik vurgusunun mesafesi (m)")]
        [SerializeField] private float stingerRange = 5f;
        [SerializeField] private float interactRange = 1.2f;
        [Tooltip("Fark ettikten sonra keşke ekranına kadar süre: oyuncu durur, döner, balonla tepki verir")]
        [SerializeField] private float failDelay = 4f; // 3 sn okunmuyordu, 5,5 sn fazla uzundu
        [Tooltip("Kayıp fark edilince sahne aydınlanır: karanlıkta devrilen eşya ve üye görünsün")]
        [SerializeField] private float revealIntensity = 3f;
        [SerializeField] private float revealRange = 4.5f;
        [SerializeField] private Color revealColor = new Color(0.85f, 0.9f, 1f);
        private static Key interactKey => Deprem.Player.KeyBindings.Interact; // M40: ayarlardan değiştirilebilir

        [Header("Görünüş")]
        [SerializeField] private Transform visual;
        [Tooltip("Yatarken gövdenin yarı uzunluğu (m): ezilince gövde eşyanın ortasına gelsin")]
        [SerializeField] private float lyingHalfLength = 0.8f;
        [Tooltip("Kurtarma fazında (karanlık) üyenin çevresindeki hafif ışık: ezilen / yaralı üye görünsün")]
        [SerializeField] private Light glow;

        [Header("Seslenme (kurtarma fazı)")]
        [Tooltip("Ayakta / yaralı üye oyuncu uzaktayken bu aralıklarla seslenir: bubble.family_call.{üye}")]
        [SerializeField] private Vector2 callInterval = new Vector2(5f, 8f);
        [SerializeField] private float callDuration = 2.5f;
        [Tooltip("Oyuncu bu kadar yakındaysa seslenmez (m)")]
        [SerializeField] private float callQuietRange = 2.5f;

        public FamilyMemberId Id => id;
        /// <summary>Kabine vardığında gereken malzeme çantada yok mu (FamilyDirector kabin girişinde sorar)</summary>
        public bool NeedsCabinBagItem => cabinBagItem != BagItemId.None && !GameState.HasBagItem(cabinBagItem);
        public FamilyState State { get; private set; } = FamilyState.Waiting;
        public ItemId HitBy { get; private set; }
        public bool IsTreating { get; private set; }
        public float TreatProgress => treatHoldTime > 0f ? Mathf.Clamp01(treatTimer / treatHoldTime) : 1f;
        public int Facing { get; private set; } = -1;
        /// <summary>Oyuncu durumu fark edip tepki verdi mi (keşke ekranı failDelay sonra)</summary>
        public bool PlayerReacted { get; private set; }
        /// <summary>Karanlıkta saklanıyor, henüz bulunmadı (görünmez, etkileşilemez)</summary>
        public bool Hidden { get; private set; }
        /// <summary>M53: oyuncu fenersiz bu üyenin odasında onu arıyor (ok gösterilmez)</summary>
        public bool BeingSearched { get; private set; }
        /// <summary>Kabinde gereken malzeme (baba: ilk yardım)</summary>
        public BagItemId CabinBagItem => cabinBagItem;

        private float searchTimer;
        private bool searchWarned;
        private bool aftershockRequested;

        private float discoverTimer;
        private bool stingerPlayed;
        private float treatTimer;
        private float callTimer = 2f;
        private SpeechBubble bubble;
        private PlayerController player;
        private bool registered;
        private GameObject rock;

        public static FamilyMember Find(FamilyMemberId memberId)
        {
            foreach (var member in AllMembers)
                if (member.id == memberId) return member;
            return null;
        }

        /// <summary>Listeyi boşaltır (Play başında; editör testlerinde sahne yeniden açılınca)</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ClearRegistry()
        {
            AllMembers.Clear();
        }

        private void OnEnable()
        {
            EnsureRegistered();
            GameEvents.PhaseChanged += OnPhaseChanged;
            GameEvents.ItemToppled += OnItemToppled;
        }

        private void OnDisable()
        {
            AllMembers.Remove(this);
            Interactables.Unregister(this);
            registered = false;
            GameEvents.PhaseChanged -= OnPhaseChanged;
            GameEvents.ItemToppled -= OnItemToppled;
        }

        private void Start()
        {
            PlaceAtHome();
        }

        /// <summary>OnEnable'da çağrılır; edit mode testleri için ayrıca çağrılabilir.</summary>
        public void EnsureRegistered()
        {
            if (registered) return;
            registered = true;
            if (!AllMembers.Contains(this)) AllMembers.Add(this);
            Interactables.Register(this);
        }

        public void PlaceAtHome()
        {
            var marker = GameObject.Find(homeMarker);
            if (marker != null) transform.position = marker.transform.position;
        }

        public void OnPhaseChanged(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.MainQuake:
                    if (State == FamilyState.Waiting) State = FamilyState.Ducking;
                    break;
                case GamePhase.Rescue:
                    if (HitBy != ItemId.None)
                    {
                        State = FamilyState.Hit;
                        PinUnder(HitBy);
                    }
                    else if (State == FamilyState.Ducking || State == FamilyState.Waiting)
                    {
                        State = injuredAfterQuake ? FamilyState.Injured : FamilyState.Waiting;
                        if (State == FamilyState.Injured) SpawnRock();
                        Hidden = hidesInDark && State == FamilyState.Waiting;
                    }
                    break;
            }
        }

        /// <summary>
        /// Play testi 4: devrilen eşya üyenin üstüne düşmüş gibi görünmüyordu. Elektrik kesilince (kurtarma fazı)
        /// ezilen üye devrilmiş eşyanın altında yatar: yatan görsel pivotun solunda uzandığı için gövde ortası eşyanın ortasına.
        /// </summary>
        private void PinUnder(ItemId item)
        {
            var securable = SecurableItem.Find(item);
            if (securable == null) return;
            var collider = securable.GetComponent<Collider>();
            float centerX = collider != null ? collider.bounds.center.x : securable.transform.position.x;
            var p = transform.position;
            // Eşyanın ön kenarının hemen önünde yatar: karanlıkta ve kameradan eşyanın altında kaybolmasın
            float frontZ = collider != null ? collider.bounds.min.z - 0.08f : p.z;
            transform.position = new Vector3(centerX + lyingHalfLength, p.y, Mathf.Min(p.z, frontZ));
        }

        public void OnItemToppled(ItemId item)
        {
            if (item != ItemId.None && item == dangerItem && State != FamilyState.Following && State != FamilyState.InCabin)
                HitBy = item;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            Tick(Time.deltaTime, keyboard != null && keyboard[interactKey].isPressed);
        }

        /// <summary>Kurtarma fazı kuralları. Update'ten çağrılır; testlerde elle çağrılabilir.</summary>
        public void Tick(float dt, bool interactHeld)
        {
            if (GameState.Phase != GamePhase.Rescue) return;
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (player == null) return;

            if (Hidden)
            {
                SearchInDark(dt);
                if (Hidden)
                {
                    CallForHelp(dt);
                    return;
                }
            }

            bool missingBagItem = requiredBagItem != BagItemId.None && !GameState.HasBagItem(requiredBagItem);
            bool lost = State == FamilyState.Hit || State == FamilyState.Bleeding ||
                        (missingBagItem && (State == FamilyState.Waiting || State == FamilyState.Injured));
            PlayDiscoverStinger(lost);
            if (lost && IsNear(player.transform.position, discoverRange) && InSameRoom())
            {
                if (!PlayerReacted) ReactToLoss(State == FamilyState.Hit);
                discoverTimer += dt;
                if (discoverTimer >= failDelay)
                {
                    if (State == FamilyState.Hit) GameState.FailLoop(LoseReason.FamilyHitByItem, HitBy);
                    else GameState.FailLoop(LoseReason.MissingBagItem);
                }
                return;
            }

            CallForHelp(dt);

            if (IsTreating)
            {
                if (!interactHeld || !IsNear(player.transform.position, interactRange * 1.5f))
                {
                    IsTreating = false;
                    treatTimer = 0f;
                }
                else
                {
                    treatTimer += dt;
                    if (treatTimer >= treatHoldTime) Rescue();
                }
            }
        }

        /// <summary>
        /// Play testi 24: kardeş karanlıkta saklanır, sadece sesi gelir. El feneri varsa yaklaşınca ışık onu bulur.
        /// Yoksa oyuncu odada el yordamıyla arar ("Hiçbir şey göremiyorum..."); searchTime sonra artçı gelir ve
        /// tavan oyuncunun üstüne çöker (QuakeDirector). Keşke: deprem çantası (el feneri).
        /// </summary>
        private void SearchInDark(float dt)
        {
            bool hasLight = requiredBagItem == BagItemId.None || GameState.HasBagItem(requiredBagItem);
            BeingSearched = false;
            if (hasLight)
            {
                if (!IsNear(player.transform.position, findRange)) return;
                Hidden = false;
                callTimer = callDuration + 1f;
                if (bubble == null) bubble = GetComponent<SpeechBubble>();
                if (bubble != null) bubble.Say("bubble.family_call." + id.ToString().ToLowerInvariant(), callDuration);
                return;
            }
            // Play testi 26: odaya girer girmez ("Kardeşim, neredesin?... Bir el fenerim olsaydı!"), yanına gelmeden
            var visibility = Deprem.Visual.RoomVisibility.Instance;
            bool inRoom = visibility != null
                ? visibility.SameRoom(player.transform.position, transform.position)
                : IsNear(player.transform.position, discoverRange);
            if (!inRoom) return;
            BeingSearched = true;
            if (!searchWarned)
            {
                searchWarned = true;
                var playerBubble = player.GetComponent<SpeechBubble>();
                if (playerBubble != null)
                    playerBubble.Say("bubble.family_missing." + requiredBagItem.ToString().ToLowerInvariant(), searchTime);
            }
            searchTimer += dt;
            if (searchTimer < searchTime || aftershockRequested) return;
            aftershockRequested = true;
            BagLessons.Learn(requiredBagItem); // M54: bir dahaki döngüde el feneri evde bulunur
            if (!Deprem.Quake.QuakeDirector.RequestFatalAftershock(LoseReason.MissingBagItem))
                GameState.FailLoop(LoseReason.MissingBagItem); // sahnede deprem sistemi yoksa (editör testi)
        }

        /// <summary>
        /// Play testi: kardeşin üstüne oyuncak dolabı düşünce oyuncu odaya girmeden (duvarın arkasından) görüp
        /// konuşuyordu. Kayıp tepkisi oyuncu üyenin odasındayken; oda sistemi yoksa (editör testi) her zaman.
        /// </summary>
        private bool InSameRoom()
        {
            var visibility = Deprem.Visual.RoomVisibility.Instance;
            return visibility == null || visibility.SameRoom(player.transform.position, transform.position);
        }

        /// <summary>
        /// Play testi 20: üyenin durumu ilk görüldüğü an bir müzik vurgusu (yaralı / bekliyor: gerilim,
        /// kaybedilmiş: ağır kayıp vurgusu). Odası açık ve oyuncu yakınsa, döngüde bir kez.
        /// </summary>
        private void PlayDiscoverStinger(bool lost)
        {
            if (stingerPlayed || State == FamilyState.Following || State == FamilyState.InCabin) return;
            if (!IsNear(player.transform.position, stingerRange)) return;
            var visibility = Deprem.Visual.RoomVisibility.Instance;
            if (visibility != null && !visibility.IsVisibleAt(transform.position)) return;
            if (lost && !InSameRoom()) return; // kayıp vurgusu da odaya girince (yukarıdaki tepkiyle birlikte)
            stingerPlayed = true;
            Deprem.Audio.Sfx.Play(lost ? "family.loss" : "family.discover");
        }

        /// <summary>
        /// Play testi 5: "ailemi kurtarmalıyım" anlaşılmıyordu. Bekleyen ya da yaralı üye, oyuncu uzaktayken
        /// ara ara seslenir (balon; ekranda görünür, oda karanlık olsa da).
        /// </summary>
        private void CallForHelp(float dt)
        {
            if (State != FamilyState.Waiting && State != FamilyState.Injured && State != FamilyState.Bleeding) return;
            if (IsNear(player.transform.position, callQuietRange)) return;
            // Play testi 11: kardeş odaya girmeden konuşuyordu; odası örtülüyken seslenmez
            var visibility = Deprem.Visual.RoomVisibility.Instance;
            if (visibility != null && !visibility.IsVisibleAt(transform.position)) return;
            callTimer -= dt;
            if (callTimer > 0f) return;
            callTimer = Random.Range(callInterval.x, callInterval.y);
            if (bubble == null) bubble = GetComponent<SpeechBubble>();
            string call = State == FamilyState.Bleeding ? "bubble.family_bleeding." : "bubble.family_call.";
            if (bubble != null) bubble.Say(call + id.ToString().ToLowerInvariant(), callDuration);
        }

        /// <summary>
        /// Oyuncu ezilmiş / kurtarılamayan aile üyesini görünce durur, ona döner ve konuşur
        /// (bubble.family_hit.father, bubble.family_missing.flashlight ...). Keşke ekranı failDelay sonra.
        /// </summary>
        private void ReactToLoss(bool hit)
        {
            PlayerReacted = true;
            player.InputEnabled = false;
            player.SetScriptedInput(0f);
            player.Face(transform.position.x >= player.transform.position.x ? 1 : -1);
            var missing = State == FamilyState.Bleeding ? cabinBagItem : requiredBagItem;
            if (!hit) BagLessons.Learn(missing); // M54
            string key = hit
                ? "bubble.family_hit." + id.ToString().ToLowerInvariant()
                : "bubble.family_missing." + missing.ToString().ToLowerInvariant();
            var bubble = player.GetComponent<SpeechBubble>();
            if (bubble != null) bubble.Say(key, failDelay);
            StartCoroutine(Reveal());
        }

        /// <summary>Kayıp anında üyenin önünde yumuşakça yükselen ışık: karanlıkta devrilen gardırop / raf görünür</summary>
        private System.Collections.IEnumerator Reveal()
        {
            var light = new GameObject("LossReveal").AddComponent<Light>();
            light.transform.SetParent(transform, false);
            // Gölgesiz ve alçakta: devrilen eşya ışığı kesmesin, altındaki üye görünsün
            light.transform.localPosition = new Vector3(0f, 0.7f, -1.4f);
            light.type = LightType.Point;
            light.color = revealColor;
            light.range = revealRange;
            light.shadows = LightShadows.None;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                light.intensity = Mathf.Lerp(0f, revealIntensity, t / 0.8f);
                yield return null;
            }
            light.intensity = revealIntensity;
        }

        // ------------------------------------------------------------------ F etkileşimi

        public Vector3 InteractionPoint => transform.position;
        public float InteractionRange => interactRange;
        public string PromptKey => State == FamilyState.Injured ? (rock != null ? "prompt.lift_rock" : "prompt.treat") : "prompt.take_along";

        public bool CanInteract(PlayerController who)
        {
            if (GameState.Phase != GamePhase.Rescue || IsTreating || Hidden) return false;
            if (State != FamilyState.Waiting && State != FamilyState.Injured) return false;
            return requiredBagItem == BagItemId.None || GameState.HasBagItem(requiredBagItem);
        }

        public void Interact(PlayerController who)
        {
            if (State == FamilyState.Injured)
            {
                // İlk yardım: F basılı tutulur (Tick sayar)
                IsTreating = true;
                treatTimer = 0f;
            }
            else
            {
                Rescue();
            }
        }

        private void Rescue()
        {
            IsTreating = false;
            if (State == FamilyState.Injured && rock != null)
            {
                // Kaya kalkar; baba kabine kendisi yürür (FamilyDirector takibe almaz, yürütür)
                StartCoroutine(LiftRock());
                State = FamilyState.WalkingToCabin;
                if (bubble == null) bubble = GetComponent<SpeechBubble>();
                if (bubble != null) bubble.Say("bubble.father_freed", 3f);
            }
            else
            {
                State = FamilyState.Following;
                // Play testi 30: konuşmalar yavandı; kurtarılınca rahatlayıp konuşur ("Oğlum! İyi ki geldin...")
                if (bubble == null) bubble = GetComponent<SpeechBubble>();
                if (bubble != null) bubble.Say("bubble.family_rescued." + id.ToString().ToLowerInvariant(), 3f);
            }
            GameState.MarkRescued(id); // FamilyDirector FamilyMemberRescued'i dinleyip takibe alır (yürüyen hariç)
        }

        // ------------------------------------------------------------------ Kaya (baba)

        private void SpawnRock()
        {
            if (rock != null || rockMaterial == null) return;
            rock = GameObject.CreatePrimitive(PrimitiveType.Quad);
            rock.name = "Rock";
            var collider = rock.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            rock.transform.SetParent(transform, false);
            rock.transform.localPosition = rockOffset;
            rock.transform.localScale = new Vector3(rockSize.x, rockSize.y, 1f);

            // QuakeDirector.DressAsPixelRubble ile aynı: pixel moloz dokusu, alfa kesme
            var material = new Material(rockMaterial) { name = "Rock" };
            material.SetTexture("_BaseMap", Deprem.Quake.PixelRubble.Chunk(Random.Range(0, Deprem.Quake.PixelRubble.AtlasTiles)));
            material.SetColor("_BaseColor", new Color(0.82f, 0.78f, 0.74f));
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            var renderer = rock.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        /// <summary>
        /// Play testi 24: taş kalkarken hiçbir şey görünmüyordu. F basılı tutulurken taş titreyerek ilerlemeyle yükselir
        /// (LateUpdate); bitince yana yuvarlanıp yere düşer ve orada kalır, çakıllar saçılır, ses çıkar.
        /// </summary>
        private void AnimateRockStrain()
        {
            if (rock == null) return;
            float p = IsTreating ? TreatProgress : 0f;
            float jitter = IsTreating ? 0.012f + 0.02f * p : 0f;
            rock.transform.localPosition = rockOffset + new Vector3(Random.Range(-jitter, jitter), rockLiftHeight * p, 0f);
            rock.transform.localRotation = Quaternion.Euler(0f, 0f, IsTreating ? Random.Range(-3f, 3f) * (0.4f + p) - 8f * p : 0f);
        }

        private System.Collections.IEnumerator LiftRock()
        {
            var r = rock;
            rock = null;
            if (r == null) yield break;
            var start = r.transform.localPosition;
            var startRotation = r.transform.localRotation.eulerAngles.z;
            // Kayanın kendi gölgesi yerde kalır; taş düşünce yeni yerine taşınır
            float side = Facing >= 0 ? -1f : 1f; // babanın ayak ucunun tersine, odanın içine doğru
            var end = new Vector3(start.x + side * rockRollDistance, rockSize.y * 0.5f - 0.03f, start.z);
            Deprem.Audio.Sfx.PlayAt("player.land", transform.position, 0.7f);
            for (float t = 0f; t < rockRollTime && r != null; t += Time.deltaTime)
            {
                float k = t / rockRollTime;
                var p = Vector3.Lerp(start, end, k);
                p.y += 0.35f * Mathf.Sin(k * Mathf.PI); // kaldırılıp yana atılır
                r.transform.localPosition = p;
                r.transform.localRotation = Quaternion.Euler(0f, 0f, startRotation + side * 200f * k);
                yield return null;
            }
            if (r == null) yield break;
            r.transform.localPosition = end;
            r.transform.localRotation = Quaternion.Euler(0f, 0f, side * 20f);
            // Yere bırakılınca: tok vuruş, çakıllar, hafif sarsıntı
            Deprem.Audio.Sfx.PlayAt("quake.debris", r.transform.position, 1f);
            Deprem.Quake.QuakeCameraShake.Intensity = Mathf.Max(Deprem.Quake.QuakeCameraShake.Intensity, 0.35f);
            for (int i = 0; i < pebbleCount; i++) StartCoroutine(Pebble(r.transform.position));
            // Taş kurtarma boyunca orada kalır; üye kabine yürürken ona bağlı kalmasın
            r.transform.SetParent(transform.parent, true);
        }

        private System.Collections.IEnumerator Pebble(Vector3 from)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Pebble";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform.parent, true);
            float size = Random.Range(0.06f, 0.13f);
            go.transform.localScale = new Vector3(size, size, 1f);
            var renderer = go.GetComponent<MeshRenderer>();
            if (rockMaterial != null)
            {
                var material = new Material(rockMaterial) { name = "Pebble" };
                material.SetTexture("_BaseMap", Deprem.Quake.PixelRubble.Chunk(Random.Range(0, Deprem.Quake.PixelRubble.AtlasTiles)));
                material.SetColor("_BaseColor", new Color(0.78f, 0.74f, 0.7f));
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.EnableKeyword("_ALPHATEST_ON");
                renderer.sharedMaterial = material;
            }
            float floorY = transform.position.y + size * 0.5f;
            var velocity = new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(1.2f, 2.6f), 0f);
            var p = from + new Vector3(0f, 0f, Random.Range(-0.15f, 0.05f));
            float spin = Random.Range(-600f, 600f), angle = 0f;
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                velocity.y -= 9.8f * Time.deltaTime;
                p += velocity * Time.deltaTime;
                angle += spin * Time.deltaTime;
                if (p.y <= floorY)
                {
                    p.y = floorY;
                    velocity = new Vector3(velocity.x * 0.4f, -velocity.y * 0.25f, 0f);
                    spin *= 0.4f;
                }
                go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, 0f, angle));
                yield return null;
            }
        }

        /// <summary>Sinematik: bir yöne döner (1 sağ, -1 sol)</summary>
        public void Face(int direction) => Facing = direction >= 0 ? 1 : -1;

        /// <summary>Kabine yürürken FamilyDirector çağırır (aksayarak; Facing yöne göre)</summary>
        public void WalkTo(Vector3 position) => SetFollowPosition(position);

        // ------------------------------------------------------------------ Takip ve kabin (FamilyDirector çağırır)

        public void SetFollowPosition(Vector3 position)
        {
            float dx = position.x - transform.position.x;
            // Play testi 29: izdeki küçük geri adımlarda bir anlık ters dönme olmasın
            if (Mathf.Abs(dx) > 0.01f) Facing = dx > 0f ? 1 : -1;
            transform.position = position;
        }

        /// <summary>Kabine vardı ama kanaması başladı ve ilk yardım yok: girişin yanında yığılır, seslenir</summary>
        public void CollapseAtCabin(Vector3 position)
        {
            State = FamilyState.Bleeding;
            transform.position = position;
            callTimer = callDuration + 1f;
            if (bubble == null) bubble = GetComponent<SpeechBubble>();
            if (bubble != null) bubble.Say("bubble.family_bleeding." + id.ToString().ToLowerInvariant(), callDuration + 0.5f);
        }

        /// <summary>Kabine vardı, çantadaki malzemeyle (ilk yardım) yarasını sardı</summary>
        public void TreatAtCabin()
        {
            if (cabinBagItem == BagItemId.None) return;
            if (bubble == null) bubble = GetComponent<SpeechBubble>();
            if (bubble != null) bubble.Say("bubble.family_treated." + id.ToString().ToLowerInvariant(), callDuration);
        }

        /// <summary>
        /// Play testi 25: kabinde, herkes içerideyken kanaması durmayan üyenin vedası ("Elveda oğlum...").
        /// Kayıp vurgusu çalar; keşke ekranını FamilyDirector açar.
        /// </summary>
        public void SayFarewell(float duration)
        {
            if (bubble == null) bubble = GetComponent<SpeechBubble>();
            if (bubble != null) bubble.Say("bubble.family_farewell." + id.ToString().ToLowerInvariant(), duration);
            Deprem.Audio.Sfx.Play("family.loss");
        }

        public void EnterCabin(Vector3 inside)
        {
            State = FamilyState.InCabin;
            transform.position = inside;
            if (visual != null) visual.gameObject.SetActive(false);
            GameEvents.RaiseFamilyMemberEnteredCabin(id);
        }

        private bool IsNear(Vector3 position, float range)
        {
            var p = transform.position;
            return Mathf.Abs(position.x - p.x) <= range && Mathf.Abs(position.y - p.y) < 1.5f;
        }

        // ------------------------------------------------------------------ Görünüş (yer tutucu)

        private void LateUpdate()
        {
            if (glow != null)
                glow.enabled = GameState.Phase == GamePhase.Rescue && State != FamilyState.InCabin && !Hidden;
            if (visual == null) return;
            // Saklanan kardeş görünmez; bulununca ortaya çıkar (kabindeyken EnterCabin gizler)
            if (State != FamilyState.InCabin && visual.gameObject.activeSelf == Hidden) visual.gameObject.SetActive(!Hidden);
            bool lying = State == FamilyState.Injured || State == FamilyState.Hit || State == FamilyState.Bleeding;
            bool ducking = State == FamilyState.Ducking;
            visual.localRotation = lying ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            visual.localScale = new Vector3(Facing, ducking ? 0.6f : 1f, 1f);
            // Kabine yürüyen yaralı baba aksar: adımlarda hafif iniş ve yana yatma
            if (State == FamilyState.WalkingToCabin)
            {
                float step = Mathf.Abs(Mathf.Sin(Time.time * 5f));
                visual.localPosition = new Vector3(0f, -0.04f * step, 0f);
                visual.localRotation = Quaternion.Euler(0f, 0f, 4f * step * Facing);
            }
            else visual.localPosition = Vector3.zero;
            AnimateRockStrain();
            Animate(lying || ducking);
        }

        // ------------------------------------------------------------------ Kareler (sanatçı çizimi gelen üyeler)

        [Header("Kareler (ekip çizimi; yoksa düz renkli yer tutucu)")]
        [SerializeField] private Renderer body;
        [Tooltip("Yürüme döngüsü (ör. baba_yurume_1..8), sağa bakar")]
        [SerializeField] private Texture2D[] walkFrames;
        [Tooltip("Dururken gösterilen kare (en dar adımlı)")]
        [SerializeField] private int idleFrame = 2;
        [SerializeField] private float walkFps = 10f;
        [Tooltip("Kabine aksayarak yürürken")]
        [SerializeField] private float limpFps = 6f;
        [Tooltip("Bekleme döngüsü (ör. baba_bekleme_1..5); boşsa idleFrame karesi")]
        [SerializeField] private Texture2D[] idleFrames;
        [SerializeField] private float idleFps = 5f;
        [Tooltip("Hareket durduktan sonra yürüme karelerinin sürdüğü süre (sn); takipte kare kare duraklama titremesin")]
        [SerializeField] private float walkHoldTime = 0.25f;
        private float moveHold;

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private MaterialPropertyBlock block;
        private Texture2D shownFrame;
        private Vector3 lastPosition;
        private float frameTime;
        private float idleTime;

        private void Animate(bool still)
        {
            if (body == null || walkFrames == null || walkFrames.Length == 0) return;
            float dt = Time.deltaTime;
            float speed = dt > 0f ? (transform.position - lastPosition).magnitude / dt : 0f;
            lastPosition = transform.position;
            // Play testi 29: takip eden anne kare kare yürüyüp duruyordu (oyuncunun izi 5 cm'lik adımlarla güncellenir),
            // yürüme ve bekleme kareleri arasında gidip geliyordu. Hareket bitince kısa bir süre daha yürüyor sayılır
            if (speed > 0.2f) moveHold = walkHoldTime;
            else moveHold -= dt;
            Texture2D frame;
            if (!still && moveHold > 0f)
            {
                frameTime += dt * (State == FamilyState.WalkingToCabin ? limpFps : walkFps);
                frame = walkFrames[Mathf.FloorToInt(frameTime) % walkFrames.Length];
            }
            else if (idleFrames != null && idleFrames.Length > 0)
            {
                idleTime += dt * idleFps;
                frame = idleFrames[Mathf.FloorToInt(idleTime) % idleFrames.Length];
            }
            else frame = walkFrames[Mathf.Clamp(idleFrame, 0, walkFrames.Length - 1)];
            if (frame == shownFrame) return;
            shownFrame = frame;
            block ??= new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            block.SetTexture(BaseMap, frame);
            body.SetPropertyBlock(block);
        }
    }
}
