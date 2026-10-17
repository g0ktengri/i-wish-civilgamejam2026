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
    // Berke'nin play testi (son): minigame'ler çok kolay, hep aynı ve oyunun tarzına uymuyordu. Her eşya kendi kurulumunu
    // alır (VariantFor); çerçeve, başlık ve ipuçları HUD'un pixel stilinde, içerik kodla çizilen pixel art (MinigamePixels).
    // M26: arkada düz karartma yerine açılış anındaki oda görüntüsü, bulanık ve ruh fazının soğuk mavisinde.
    public class MinigameHost : MonoBehaviour
    {
        [Header("Görünüm")]
        [SerializeField] Font font;
        [SerializeField] int sortingOrder = 80;
        [SerializeField] Vector2 panelSize = new Vector2(256f, 256f); // GDD yakın çekim boyutu
        [Tooltip("Çerçevenin içerikten payı (referans pikseli)")]
        [SerializeField] float framePadding = 8f;
        [Tooltip("Başlık şeridinin yüksekliği: başlık çerçevenin içinde, içeriğin üstünde (M28: tek, sade çerçeve)")]
        [SerializeField] float headerHeight = 26f;
        [Tooltip("Bulanık oda görüntüsünün üstündeki karartma (M28: biraz daha koyu)")]
        [SerializeField] Color overlayColor = new Color(0.02f, 0.03f, 0.07f, 0.66f);
        [Tooltip("Arkadaki bulanık oda görüntüsünün rengi (ruh fazının soğuk mavisi)")]
        [SerializeField] Color snapshotTint = new Color(0.34f, 0.45f, 0.68f);
        [Tooltip("Bulanıklık: oda görüntüsü bu çözünürlüğe küçültülür (küçük = daha bulanık)")]
        [SerializeField] Vector2Int snapshotSize = new Vector2Int(96, 54);
        [Tooltip("Minigame'in kendi arka planına hafif soğuk ton")]
        [SerializeField] Color contentTint = new Color(0.9f, 0.94f, 1f);
        [SerializeField] Color successColor = new Color(0.5f, 0.86f, 0.52f, 0.35f);
        [SerializeField] float successHold = 0.5f;

        // GDD: 60 sn'de 5-6 eşya (yol dahil); her minigame yaklaşık 5-7 sn sürmeli

        [Header("Vida sıkma")]
        // Vida sayısı ve düzeni eşyaya göre (VariantFor): 2-3 vida. Eskiden 4 vida yetişmiyordu, 1 vida çok kolaydı
        [Tooltip("Her vida için tornavidayla gereken tur (play testi 16: 1,25'ten biraz arttı)")]
        [SerializeField] float screwTurnsPerScrew = 1.5f;
        [Tooltip("Bundan hızlı çevirince uç vidadan kayar (tur / sn; 0 = kapalı). Play testi 16: uç arada kopuyordu, kapalı")]
        [SerializeField] float screwMaxTurnsPerSecond = 0f;
        [Tooltip("Ters yönde dönmenin geri alma katsayısı (1 = aynı hızda gevşer)")]
        [SerializeField] float screwReversePenalty = 1f;
        [Tooltip("Fare vidaya bundan yakınken dönüş sayılmaz (referans pikseli)")]
        [SerializeField] float screwMinRadius = 18f;

        [Header("Taşı ve yerleştir")]
        [SerializeField] float carrySnapRadius = 14f;
        [Tooltip("Duvardaki boşluğun yüksekliği (eşya 48; kayan boşluk +10)")]
        [SerializeField] float carryGapHeight = 64f;

        [Header("Görseller (Assets/Art/UI, prefab kurulurken bağlanır)")]
        [SerializeField] MinigameArt art = new MinigameArt();

        [Header("Kilitle")]
        // Play testi 8: mutfak dolabı geçilemiyordu; son isabette alan ~%15, hız ~1 tur/sn
        [SerializeField] float lockCyclesPerSecond = 0.75f;
        [SerializeField] float lockZoneWidth = 0.24f;
        [SerializeField] int lockRequiredHits = 3;
        [SerializeField] float lockShrinkPerHit = 0.8f;
        [SerializeField] float lockSpeedUpPerHit = 1.15f;
        [Tooltip("Her isabetten sonra yeşil alanın kayma hızı (bar eni / sn, isabet başına)")]
        [SerializeField] float lockDriftPerHit = 0.16f;

        CanvasGroup group;
        RawImage snapshot;
        RenderTexture snapshotBig;
        RenderTexture snapshotSmall;
        Image panel;
        RectTransform content;
        Image successFlash;
        Text titleText;
        RectTransform titlePanel;
        UiPrompt hint;
        UiPrompt cancel;
        string doneKey;

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
            if (snapshotBig) snapshotBig.Release();
            if (snapshotSmall) snapshotSmall.Release();
        }

        // Ana kamerayı bir kez küçük bir dokuya çizer, daha da küçültür: çift doğrusal büyütmeyle bulanık oda görüntüsü
        void TakeSnapshot()
        {
            var cam = Camera.main;
            if (!cam)
            {
                snapshot.enabled = false;
                return;
            }
            if (!snapshotBig)
            {
                snapshotBig = new RenderTexture(snapshotSize.x * 4, snapshotSize.y * 4, 24) { name = "MinigameSnapshot" };
                snapshotSmall = new RenderTexture(snapshotSize.x, snapshotSize.y, 0)
                {
                    name = "MinigameSnapshotBlur",
                    filterMode = FilterMode.Bilinear,
                };
            }
            var previous = cam.targetTexture;
            cam.targetTexture = snapshotBig;
            cam.Render();
            cam.targetTexture = previous; // PixelScreen kendi dokusuna geri döner
            Graphics.Blit(snapshotBig, snapshotSmall);
            snapshot.texture = snapshotSmall;
            snapshot.color = snapshotTint;
            snapshot.enabled = true;
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
        // title: başlıkta gösterilecek yazı (boşsa başlık yok); carryKind: taşınan şeyin çizimi, çeşidi de belirler
        public bool Open(MinigameType type, Action<bool> done, Sprite itemSprite = null, string title = null,
            string carryKind = null)
        {
            var variant = new MinigameVariant { Seed = UnityEngine.Random.Range(0, 100000) };
            if (!string.IsNullOrEmpty(carryKind))
            {
                variant.CarryKind = carryKind;
                variant.Carry = carryKind == "flashlight" ? CarryMode.Moving : CarryMode.Single;
            }
            ApplyTuning(variant);
            doneKey = "minigame.taken";
            if (!OpenPanel(type, itemSprite, variant, title, success =>
                {
                    GameEvents.RaiseMinigameClosed(ItemId.None, success);
                    done?.Invoke(success);
                }))
                return false;
            GameEvents.RaiseMinigameOpened(ItemId.None, type);
            return true;
        }

        bool OpenPanel(MinigameType type, Sprite itemSprite, MinigameVariant variant, string title, Action<bool> done)
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
            if (art.panel) UiBuilder.ApplyHudSprite(panel, art.panel);
            successFlash.enabled = false;
            current.Build(content, art, variant);
            var background = content.Find("Background");
            if (background && background.TryGetComponent<Image>(out var backgroundImage)) backgroundImage.color = contentTint;
            TakeSnapshot();

            titleText.text = Capitalize(title);
            titleText.color = UiBuilder.HudText;
            titlePanel.gameObject.SetActive(!string.IsNullOrEmpty(title));
            hint.Set(type == MinigameType.Lock
                ? Texts.Format(HintKey(type), lockRequiredHits)
                : Texts.Get(HintKey(type)));
            cancel.Set(Texts.Get("prompt.cancel"));
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
            var variant = VariantFor(id);
            string title = Texts.Format("minigame.title", Texts.Get($"item.{id}"));
            doneKey = "minigame.done";
            if (!item || item.IsSecured || !OpenPanel(type, CarrySpriteFor(id), variant, title, success => FinishItem(id, success)))
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
            successFlash.enabled = true;
            successFlash.color = successColor;
            if (titlePanel.gameObject.activeSelf)
            {
                titleText.text = Texts.Get(doneKey ?? "minigame.done");
                titleText.color = new Color(successColor.r, successColor.g, successColor.b, 1f);
            }
            yield return new WaitForSeconds(successHold);
            Close(true);
        }

        void Close(bool success)
        {
            StopAllCoroutines();
            if (!success && current != null) Audio.Sfx.Play("minigame.fail"); // vazgeçildi ya da süre bitti
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

        // "kitaplığı sabitle" → "Kitaplığı sabitle" (eşya adları cümle içinde kullanıldığı için küçük harfli)
        static string Capitalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var culture = new System.Globalization.CultureInfo(Texts.Current == Language.Turkish ? "tr-TR" : "en-US");
            return char.ToUpper(text[0], culture) + text.Substring(1);
        }

        // Her eşyanın kendi kurulumu (Berke'nin play testi: hepsi aynıydı); Seed her açılışta yeni
        MinigameVariant VariantFor(ItemId id)
        {
            var variant = new MinigameVariant { Seed = UnityEngine.Random.Range(0, 100000) };
            switch (id)
            {
                case ItemId.Bookcase: variant.Screws = ScrewLayout.Triangle3; break;   // ağır, uzun
                case ItemId.Wardrobe: variant.Screws = ScrewLayout.Triangle3; break;
                case ItemId.TvUnit: variant.Screws = ScrewLayout.Horizontal2; break;   // alçak, geniş
                case ItemId.ToyShelf: variant.Screws = ScrewLayout.Vertical2; break;
                case ItemId.ShoeRack: variant.Screws = ScrewLayout.Diagonal2; break;
                case ItemId.BedShelf: variant.Carry = CarryMode.Double; variant.CarryKind = "shelf"; break;
                case ItemId.StairMirror: variant.Carry = CarryMode.Moving; variant.CarryKind = "mirror"; break;
                case ItemId.BedFrame: variant.Carry = CarryMode.Single; variant.CarryKind = "frame"; break;
            }
            ApplyTuning(variant);
            return variant;
        }

        void ApplyTuning(MinigameVariant variant) => variant.LockDrift = lockDriftPerHit;

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
                    return new ScrewMinigame(screwTurnsPerScrew, screwReversePenalty, screwMinRadius, screwMaxTurnsPerSecond);
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

            snapshot = UiBuilder.CreateChild<RawImage>(canvas.transform, "Snapshot");
            snapshot.raycastTarget = false;
            UiBuilder.Stretch(snapshot.rectTransform, Vector2.zero, Vector2.one);
            snapshot.enabled = false;

            var overlay = UiBuilder.CreateImage(canvas.transform, "Overlay", overlayColor);
            UiBuilder.Stretch(overlay.rectTransform, Vector2.zero, Vector2.one);

            // Çerçeve: HUD'un pixel paneli (minigame_panel.png varsa o); içerik içinde, payla
            panel = UiBuilder.CreatePanel(canvas.transform, "Panel");
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = panelSize + Vector2.one * framePadding * 2f + new Vector2(0f, headerHeight);
            panelRect.anchoredPosition = new Vector2(0f, 14f + headerHeight * 0.5f);

            content = new GameObject("Content", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            content.SetParent(panel.transform, false);
            UiBuilder.Stretch(content, Vector2.zero, Vector2.one);
            content.offsetMin = Vector2.one * framePadding;
            content.offsetMax = -new Vector2(framePadding, framePadding + headerHeight);

            // İçeriğin dışında (OpenPanel içeriği her açılışta temizler), içeriğin üstünde
            successFlash = UiBuilder.CreateImage(panel.transform, "SuccessFlash", successColor);
            UiBuilder.Stretch(successFlash.rectTransform, Vector2.zero, Vector2.one);
            successFlash.rectTransform.offsetMin = Vector2.one * framePadding;
            successFlash.rectTransform.offsetMax = -new Vector2(framePadding, framePadding + headerHeight);
            successFlash.enabled = false;

            // Başlık: aynı çerçevenin içinde, üstte şerit ve altında ince ayırıcı ("Kitaplığı sabitle");
            // ayrı etiket paneli ve altın şerit yok (M28)
            titlePanel = new GameObject("Title", typeof(RectTransform)).GetComponent<RectTransform>();
            titlePanel.SetParent(panel.transform, false);
            titlePanel.anchorMin = new Vector2(0f, 1f);
            titlePanel.anchorMax = new Vector2(1f, 1f);
            titlePanel.pivot = new Vector2(0.5f, 1f);
            titlePanel.sizeDelta = new Vector2(-framePadding * 2f, headerHeight);
            titlePanel.anchoredPosition = new Vector2(0f, -framePadding * 0.5f);
            titleText = UiBuilder.CreateText(titlePanel, "Text", font, UiBuilder.HudFontSize, UiBuilder.HudText);
            UiBuilder.Stretch(titleText.rectTransform, Vector2.zero, Vector2.one);
            titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var separator = UiBuilder.CreateImage(titlePanel, "Separator", new Color(1f, 1f, 1f, 0.12f));
            separator.rectTransform.anchorMin = Vector2.zero;
            separator.rectTransform.anchorMax = new Vector2(1f, 0f);
            separator.rectTransform.pivot = new Vector2(0.5f, 0f);
            separator.rectTransform.sizeDelta = new Vector2(0f, 2f);
            separator.rectTransform.anchoredPosition = new Vector2(0f, 2f);

            hint = UiPrompt.Create(canvas.transform, "Hint", font, 14);
            hint.Rect.anchorMin = hint.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            hint.Rect.pivot = new Vector2(0.5f, 1f);
            hint.Rect.anchoredPosition = new Vector2(0f, 14f - panelRect.sizeDelta.y * 0.5f - 10f);

            cancel = UiPrompt.Create(canvas.transform, "Cancel", font, 13);
            cancel.Rect.anchorMin = cancel.Rect.anchorMax = Vector2.right;
            cancel.Rect.pivot = Vector2.right;
            cancel.Rect.anchoredPosition = new Vector2(-UiBuilder.HudMargin, UiBuilder.HudMargin);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;
    }
}
