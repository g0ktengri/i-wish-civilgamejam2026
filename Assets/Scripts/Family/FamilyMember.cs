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
    }

    /// <summary>
    /// Bir aile üyesi (GDD "Artçılar ve kurtarma"):
    ///   - dangerItem sabitlenmeden devrilirse üstüne düşer; oyuncu yanına gelince FamilyHitByItem.
    ///   - requiredBagItem çantada yoksa oyuncu yanına gelince MissingBagItem (baba: ilk yardım, kardeş: el feneri).
    ///   - Yaralıysa F basılı tutularak tedavi edilir, değilse F ile toplanır; sonra takip eder.
    ///   - Berke'nin play testi 16: babanın ayağına kaya düşmüş (kan kaybı da var: ilk yardım çantası yine şart).
    ///     F 3 sn basılı tutulunca kaya kalkar, baba teşekkür eder ve kabine kendisi aksayarak yürür (FamilyDirector).
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
        [Tooltip("Ana depremden sonra yaralı mı (baba: pencere camı, GDD)")]
        [SerializeField] private bool injuredAfterQuake;

        [SerializeField] private float treatHoldTime = 3f;
        [Tooltip("Yaralıyken ayağının üstündeki kayanın materyali (siva.mat kopyası, doku PixelRubble); boşsa kaya yok")]
        [SerializeField] private Material rockMaterial;
        [SerializeField] private Vector3 rockOffset = new Vector3(-0.22f, 0.2f, -0.25f);
        [SerializeField] private Vector2 rockSize = new Vector2(0.7f, 0.55f);
        [Tooltip("Oyuncu bu kadar yaklaşınca durumu fark eder (aynı kat, X ekseninde)")]
        [SerializeField] private float discoverRange = 3.5f;
        [SerializeField] private float interactRange = 1.2f;
        [Tooltip("Fark ettikten sonra keşke ekranına kadar süre: oyuncu durur, döner, balonla tepki verir")]
        [SerializeField] private float failDelay = 4f; // 3 sn okunmuyordu, 5,5 sn fazla uzundu
        [Tooltip("Kayıp fark edilince sahne aydınlanır: karanlıkta devrilen eşya ve üye görünsün")]
        [SerializeField] private float revealIntensity = 3f;
        [SerializeField] private float revealRange = 4.5f;
        [SerializeField] private Color revealColor = new Color(0.85f, 0.9f, 1f);
        [SerializeField] private Key interactKey = Key.F;

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
        public FamilyState State { get; private set; } = FamilyState.Waiting;
        public ItemId HitBy { get; private set; }
        public bool IsTreating { get; private set; }
        public float TreatProgress => treatHoldTime > 0f ? Mathf.Clamp01(treatTimer / treatHoldTime) : 1f;
        public int Facing { get; private set; } = -1;
        /// <summary>Oyuncu durumu fark edip tepki verdi mi (keşke ekranı failDelay sonra)</summary>
        public bool PlayerReacted { get; private set; }

        private float discoverTimer;
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

            bool missingBagItem = requiredBagItem != BagItemId.None && !GameState.HasBagItem(requiredBagItem);
            bool lost = State == FamilyState.Hit || (missingBagItem && (State == FamilyState.Waiting || State == FamilyState.Injured));
            if (lost && IsNear(player.transform.position, discoverRange))
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
        /// Play testi 5: "ailemi kurtarmalıyım" anlaşılmıyordu. Bekleyen ya da yaralı üye, oyuncu uzaktayken
        /// ara ara seslenir (balon; ekranda görünür, oda karanlık olsa da).
        /// </summary>
        private void CallForHelp(float dt)
        {
            if (State != FamilyState.Waiting && State != FamilyState.Injured) return;
            if (IsNear(player.transform.position, callQuietRange)) return;
            // Play testi 11: kardeş odaya girmeden konuşuyordu; odası örtülüyken seslenmez
            var visibility = Deprem.Visual.RoomVisibility.Instance;
            if (visibility != null && !visibility.IsVisibleAt(transform.position)) return;
            callTimer -= dt;
            if (callTimer > 0f) return;
            callTimer = Random.Range(callInterval.x, callInterval.y);
            if (bubble == null) bubble = GetComponent<SpeechBubble>();
            if (bubble != null) bubble.Say("bubble.family_call." + id.ToString().ToLowerInvariant(), callDuration);
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
            string key = hit
                ? "bubble.family_hit." + id.ToString().ToLowerInvariant()
                : "bubble.family_missing." + requiredBagItem.ToString().ToLowerInvariant();
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
            if (GameState.Phase != GamePhase.Rescue || IsTreating) return false;
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

        // Kaya yana devrilip düşer ve kaybolur
        private System.Collections.IEnumerator LiftRock()
        {
            var r = rock;
            rock = null;
            if (r == null) yield break;
            var start = r.transform.localPosition;
            for (float t = 0f; t < 0.6f && r != null; t += Time.deltaTime)
            {
                float k = t / 0.6f;
                r.transform.localPosition = start + new Vector3(0.55f * k, 0.45f * Mathf.Sin(k * Mathf.PI), 0f);
                r.transform.localRotation = Quaternion.Euler(0f, 0f, -120f * k);
                yield return null;
            }
            if (r != null) Destroy(r);
        }

        /// <summary>Kabine yürürken FamilyDirector çağırır (aksayarak; Facing yöne göre)</summary>
        public void WalkTo(Vector3 position) => SetFollowPosition(position);

        // ------------------------------------------------------------------ Takip ve kabin (FamilyDirector çağırır)

        public void SetFollowPosition(Vector3 position)
        {
            float dx = position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.001f) Facing = dx > 0f ? 1 : -1;
            transform.position = position;
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
                glow.enabled = GameState.Phase == GamePhase.Rescue && State != FamilyState.InCabin;
            if (visual == null) return;
            bool lying = State == FamilyState.Injured || State == FamilyState.Hit;
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
        }
    }
}
