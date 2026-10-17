using System;
using System.Collections;
using Deprem.Core;
using Deprem.Localization;
using Deprem.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deprem.Minigames
{
    // Minigame'leri açar ve kapatır. Eşya için: GameEvents.MinigameOpened gelince açılır,
    // başarılıysa SecurableItem.Secure() çağırır, sonunda RaiseMinigameClosed.
    // Çanta gibi Berke'nin diğer sistemleri Open(type, onDone, görsel) ile doğrudan açar.
    // Esc ile vazgeçilir; ruh fazı biterse minigame başarısız kapanır. Canvas'ı kendisi kurar.
    public class MinigameHost : MonoBehaviour
    {
        [Header("Görünüm")]
        [SerializeField] Font font;
        [SerializeField] int sortingOrder = 80;
        [SerializeField] Vector2 panelSize = new Vector2(256f, 256f); // GDD yakın çekim boyutu
        [SerializeField] Color overlayColor = new Color(0.02f, 0.04f, 0.1f, 0.6f);
        [SerializeField] Color panelColor = new Color(0.1f, 0.12f, 0.16f, 0.95f);
        [SerializeField] Color successColor = new Color(0.2f, 0.4f, 0.25f, 0.95f);
        [SerializeField] float successHold = 0.4f;

        // GDD: 60 sn'de 5-6 eşya (yol dahil); her minigame yaklaşık 5-7 sn sürmeli

        [Header("Vida sıkma")]
        [Tooltip("1 = levhanın ortasında tek vida, 4 = köşelerde dört vida (Play testi: 4 vidaya süre yetmiyordu)")]
        [SerializeField] int screwCount = 1;
        [Tooltip("Her vida için tornavidayla gereken tur")]
        [SerializeField] float screwTurnsPerScrew = 2f;
        [Tooltip("Ters yönde dönmenin geri alma katsayısı (1 = aynı hızda gevşer)")]
        [SerializeField] float screwReversePenalty = 1f;
        [Tooltip("Fare vidaya bundan yakınken dönüş sayılmaz (referans pikseli)")]
        [SerializeField] float screwMinRadius = 18f;

        [Header("Taşı ve yerleştir")]
        [SerializeField] float carrySnapRadius = 14f;
        [Tooltip("Duvardaki boşluğun yüksekliği (eşya 44)")]
        [SerializeField] float carryGapHeight = 70f;

        [Header("Görseller (Assets/Art/UI, prefab kurulurken bağlanır)")]
        [SerializeField] MinigameArt art = new MinigameArt();

        [Header("Kilitle")]
        // Play testi 8: mutfak dolabı geçilemiyordu; son isabette alan ~%15, hız ~1 tur/sn
        [SerializeField] float lockCyclesPerSecond = 0.75f;
        [SerializeField] float lockZoneWidth = 0.24f;
        [SerializeField] int lockRequiredHits = 3;
        [SerializeField] float lockShrinkPerHit = 0.8f;
        [SerializeField] float lockSpeedUpPerHit = 1.15f;

        CanvasGroup group;
        Image panel;
        RectTransform content;
        Text hintText;
        Text cancelText;

        Minigame current;
        Action<bool> onDone;
        bool finishing;

        public static MinigameHost Instance { get; private set; }
        public bool IsOpen => current != null;
        // Sahnede açık bir minigame paneli var mı (başarı beklemesi dahil, panel kapanınca false)
        public static bool AnyOpen => Instance && Instance.IsOpen;

        void Awake()
        {
            Instance = this;
            BuildCanvas();
            group.alpha = 0f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable()
        {
            GameEvents.MinigameOpened += HandleMinigameOpened;
            GameEvents.PhaseChanged += HandlePhaseChanged;
        }

        void OnDisable()
        {
            GameEvents.MinigameOpened -= HandleMinigameOpened;
            GameEvents.PhaseChanged -= HandlePhaseChanged;
        }

        // Çanta malzemesi gibi eşya olmayan şeyler için. Ruhun durması için
        // MinigameOpened/Closed event'leri ItemId.None ile tetiklenir.
        public bool Open(MinigameType type, Action<bool> done, Sprite itemSprite = null)
        {
            if (!OpenPanel(type, itemSprite, success =>
                {
                    GameEvents.RaiseMinigameClosed(ItemId.None, success);
                    done?.Invoke(success);
                }))
                return false;
            GameEvents.RaiseMinigameOpened(ItemId.None, type);
            return true;
        }

        bool OpenPanel(MinigameType type, Sprite itemSprite, Action<bool> done)
        {
            if (IsOpen) return false;
            current = Create(type, itemSprite);
            onDone = done;
            finishing = false;

            foreach (Transform child in content)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            panel.sprite = art.panel;
            panel.color = art.panel ? Color.white : panelColor;
            current.Build(content, art);

            hintText.text = type == MinigameType.Lock
                ? Texts.Format(HintKey(type), lockRequiredHits)
                : Texts.Get(HintKey(type));
            cancelText.text = Texts.Get("prompt.cancel");
            group.alpha = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            return true;
        }

        void HandleMinigameOpened(ItemId id, MinigameType type)
        {
            if (id == ItemId.None) return; // Open() ile açılan, eşya olmayan minigame

            // Eşya yoksa, zaten sabitse ya da başka minigame açıksa hemen kapat ki ruh takılı kalmasın
            var item = SecurableItem.Find(id);
            if (!item || item.IsSecured || !OpenPanel(type, CarrySpriteFor(id), success => FinishItem(id, success)))
                GameEvents.RaiseMinigameClosed(id, false);
        }

        static void FinishItem(ItemId id, bool success)
        {
            if (success)
            {
                var item = SecurableItem.Find(id);
                if (item) item.Secure();
            }
            GameEvents.RaiseMinigameClosed(id, success);
        }

        void HandlePhaseChanged(GamePhase phase)
        {
            if (IsOpen && phase != GamePhase.Spirit) Close(false);
        }

        void Update()
        {
            if (!IsOpen || finishing) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close(false);
                return;
            }

            if (current.Tick(ReadInput())) StartCoroutine(Succeed());
        }

        IEnumerator Succeed()
        {
            finishing = true;
            panel.color = successColor;
            yield return new WaitForSeconds(successHold);
            Close(true);
        }

        void Close(bool success)
        {
            StopAllCoroutines();
            var callback = onDone;
            current = null;
            onDone = null;
            finishing = false;
            group.alpha = 0f;
            callback?.Invoke(success);
        }

        MinigameInput ReadInput()
        {
            var input = new MinigameInput { DeltaTime = Time.deltaTime };
            var mouse = Mouse.current;
            if (mouse == null) return input;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(content, mouse.position.ReadValue(), null,
                out input.Pointer);
            input.Held = mouse.leftButton.isPressed;
            input.Pressed = mouse.leftButton.wasPressedThisFrame;
            input.Released = mouse.leftButton.wasReleasedThisFrame;
            return input;
        }

        // Taşı-yerleştir eşyalarının kendi görseli (GDD: raf, ayna, çerçeve)
        Sprite CarrySpriteFor(ItemId id)
        {
            switch (id)
            {
                case ItemId.BedShelf: return art.carryBedShelf;
                case ItemId.StairMirror: return art.carryMirror;
                case ItemId.BedFrame: return art.carryFrame;
                default: return null;
            }
        }

        Minigame Create(MinigameType type, Sprite itemSprite)
        {
            switch (type)
            {
                case MinigameType.CarryAndPlace: return new CarryMinigame(carrySnapRadius, carryGapHeight, itemSprite);
                case MinigameType.Lock:
                    return new LockMinigame(lockCyclesPerSecond, lockZoneWidth, lockRequiredHits, lockShrinkPerHit,
                        lockSpeedUpPerHit);
                default:
                    return new ScrewMinigame(screwTurnsPerScrew, screwReversePenalty, screwMinRadius, screwCount);
            }
        }

        static string HintKey(MinigameType type)
        {
            switch (type)
            {
                case MinigameType.CarryAndPlace: return "minigame.carry.hint";
                case MinigameType.Lock: return "minigame.lock.hint";
                default: return "minigame.screw.hint";
            }
        }

        void BuildCanvas()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "MinigameCanvas", sortingOrder);
            group = canvas.GetComponent<CanvasGroup>();

            var overlay = UiBuilder.CreateImage(canvas.transform, "Overlay", overlayColor);
            UiBuilder.Stretch(overlay.rectTransform, Vector2.zero, Vector2.one);

            panel = UiBuilder.CreateImage(canvas.transform, "Panel", panelColor);
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = panelSize;
            panelRect.anchoredPosition = new Vector2(0f, 20f);

            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(panel.transform, false);
            UiBuilder.Stretch(content, Vector2.zero, Vector2.one);

            hintText = UiBuilder.CreateText(canvas.transform, "Hint", font, 16, Color.white);
            UiBuilder.Stretch(hintText.rectTransform, new Vector2(0.2f, 0.1f), new Vector2(0.8f, 0.18f));

            cancelText = UiBuilder.CreateText(canvas.transform, "Cancel", font, 12, new Color(0.75f, 0.75f, 0.75f),
                TextAnchor.LowerRight);
            UiBuilder.Stretch(cancelText.rectTransform, new Vector2(0.6f, 0.02f), new Vector2(0.98f, 0.08f));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;
    }
}
