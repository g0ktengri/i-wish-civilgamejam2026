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
    // Çanta gibi Berke'nin diğer sistemleri Open(type, onDone) ile doğrudan açar.
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

        [Header("Vida sıkma")]
        [SerializeField] float screwTurns = 2.5f;
        [SerializeField] bool screwClockwiseOnly = true;
        [SerializeField] float screwMinRadius = 20f;

        [Header("Taşı ve yerleştir")]
        [SerializeField] float carrySnapRadius = 30f;

        [Header("Kilitle")]
        [SerializeField] float lockCyclesPerSecond = 0.8f;
        [SerializeField] float lockZoneWidth = 0.2f;

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
        public bool Open(MinigameType type, Action<bool> done)
        {
            if (!OpenPanel(type, success =>
                {
                    GameEvents.RaiseMinigameClosed(ItemId.None, success);
                    done?.Invoke(success);
                }))
                return false;
            GameEvents.RaiseMinigameOpened(ItemId.None, type);
            return true;
        }

        bool OpenPanel(MinigameType type, Action<bool> done)
        {
            if (IsOpen) return false;
            current = Create(type);
            onDone = done;
            finishing = false;

            foreach (Transform child in content)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            panel.color = panelColor;
            current.Build(content);

            hintText.text = Texts.Get(HintKey(type));
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
            if (!item || item.IsSecured || !OpenPanel(type, success => FinishItem(id, success)))
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

        Minigame Create(MinigameType type)
        {
            switch (type)
            {
                case MinigameType.CarryAndPlace: return new CarryMinigame(carrySnapRadius);
                case MinigameType.Lock: return new LockMinigame(lockCyclesPerSecond, lockZoneWidth);
                default: return new ScrewMinigame(screwTurns, screwClockwiseOnly, screwMinRadius);
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
