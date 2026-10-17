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
        [SerializeField] Vector2 panelSize = new Vector2(360f, 256f); // GDD yakın çekim boyutu; M33: en uzun başlık ("Merdivendeki aynayı sabitle") tam boyda tek satır sığsın diye 256 → 360 en
        [Tooltip("Çerçevenin içerikten payı (referans pikseli)")]
        [SerializeField] float framePadding = 8f;
        [Tooltip("Başlık şeridinin yüksekliği: başlık çerçevenin içinde, içeriğin üstünde (M28: tek, sade çerçeve)")]
        [SerializeField] float headerHeight = 38f;
        [Tooltip("M33: ipucu panelin içinde, alt şeritte (ekranın altında küçük şerit değil)")]
        [SerializeField] float footerHeight = 58f;
        [SerializeField] int titleFontSize = 24;
        [SerializeField] int hintFontSize = 18;
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
        Text hintText;
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
            ReleaseCloseUp();
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
        // focus: dünyadaki nesne (M37: arka planda onun odası, taşınan şey onun modeli); boşsa eski pixel çizimler
        public bool Open(MinigameType type, Action<bool> done, Sprite itemSprite = null, string title = null,
            string carryKind = null, GameObject focus = null)
        {
            var variant = new MinigameVariant { Seed = UnityEngine.Random.Range(0, 100000) };
            if (!string.IsNullOrEmpty(carryKind))
            {
                variant.CarryKind = carryKind;
                // Play testi 23: ilk yardım çantasında duvar sabit kalıyordu; o da kayan boşluklu, diğerleri zikzak
                variant.Carry = carryKind == "flashlight" || carryKind == "firstaid" ? CarryMode.Moving : CarryMode.Double;
            }
            ApplyTuning(variant);
            doneKey = "minigame.taken";
            if (!OpenPanel(type, itemSprite, variant, title, success =>
                {
                    GameEvents.RaiseMinigameClosed(ItemId.None, success);
                    done?.Invoke(success);
                }, focus))
                return false;
            GameEvents.RaiseMinigameOpened(ItemId.None, type);
            return true;
        }

        bool OpenPanel(MinigameType type, Sprite itemSprite, MinigameVariant variant, string title, Action<bool> done,
            GameObject focus = null)
        {
            if (IsOpen) return false;
            ReleaseCloseUp();
            // M37: taşınan şey sanatçı görseli yoksa oyundaki modelinin görüntüsü
            if (type == MinigameType.CarryAndPlace && !itemSprite && focus)
            {
                closeUpItem = MinigameCloseUp.RenderObject(focus, CarryBox(variant.CarryKind), 1f);
                itemSprite = closeUpItem;
            }
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
            var backgroundImage = background ? background.GetComponent<Image>() : null;
            if (backgroundImage) backgroundImage.color = contentTint;
            if (focus) ShowCloseUp(focus, type == MinigameType.CarryAndPlace ? focus : null, backgroundImage);
            TakeSnapshot();

            titleText.text = Capitalize(title);
            titleText.color = UiBuilder.HudText;
            titlePanel.gameObject.SetActive(!string.IsNullOrEmpty(title));
            hintText.text = type == MinigameType.Lock
                ? Texts.Format(HintKey(type), lockRequiredHits)
                : Texts.Get(HintKey(type));
            cancel.Set(Texts.Get("prompt.cancel"));
            group.alpha = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            return true;
        }

        // ------------------------------------------------------------------ M37: oyundaki eşya ve odası

        [Header("M37: yakın çekim (Mehmet'in play testi 22)")]
        [Tooltip("Arka plan çekiminin piksel boyu (referans piksel / doku pikseli); oyunun pixel görünüşü")]
        [SerializeField] float closeUpPixel = 2f;
        [Tooltip("Eşyanın çevresindeki oda payı (1 = eşya paneli doldurur)")]
        [SerializeField] float closeUpMargin = 1.9f;
        [Tooltip("Arka plan rengi: üstündeki parçalar okunsun diye biraz kararır")]
        [SerializeField] Color closeUpTint = new Color(0.62f, 0.64f, 0.72f);

        RenderTexture closeUpTexture;
        Sprite closeUpItem;

        // Odanın eşyanın önünden görüntüsü, panelin en arkasında; soyut pixel zemin gizlenir
        void ShowCloseUp(GameObject focus, GameObject hide, Image pixelBackground)
        {
            closeUpTexture = MinigameCloseUp.RenderScene(MinigameCloseUp.BoundsOf(focus), content.rect.size, closeUpPixel,
                closeUpMargin, hide);
            if (!closeUpTexture) return;
            var image = UiBuilder.CreateChild<RawImage>(content, "SceneCloseUp");
            image.raycastTarget = false;
            image.texture = closeUpTexture;
            image.color = closeUpTint;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = content.rect.size;
            rect.anchoredPosition = Vector2.zero;
            rect.SetAsFirstSibling();
            if (pixelBackground) pixelBackground.enabled = false;
        }

        void ReleaseCloseUp()
        {
            if (closeUpTexture)
            {
                closeUpTexture.Release();
                Destroy(closeUpTexture);
            }
            if (closeUpItem)
            {
                Destroy(closeUpItem.texture);
                Destroy(closeUpItem);
            }
            closeUpTexture = null;
            closeUpItem = null;
        }

        // Taşınan şeyin kutusu: çarpışma dikdörtgeniyle aynı (CarryMinigame.ItemSize), oran korunur
        static Vector2 CarryBox(string kind) => (Vector2)MinigamePixels.CarrySize(kind) * MinigamePixels.Scale;

        void HandleMinigameOpened(ItemId id, MinigameType type)
        {
            if (id == ItemId.None) return; // Open() ile açılan, eşya olmayan minigame

            // Eşya yoksa, zaten sabitse ya da başka minigame açıksa hemen kapat ki ruh takılı kalmasın
            var item = SecurableItem.Find(id);
            var variant = VariantFor(id);
            string title = Texts.Format("minigame.title", Texts.Get($"item.{id}"));
            doneKey = "minigame.done";
            if (!item || item.IsSecured || !OpenPanel(type, CarrySpriteFor(id), variant, title, success => FinishItem(id, success),
                    item ? item.gameObject : null))
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

        /// <summary>Açık minigame varsa başarısız kapatır (ruh fazı süresi bitip ruh bedene dönerken)</summary>
        public void CloseIfOpen()
        {
            if (IsOpen) Close(false);
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
            panelRect.sizeDelta = panelSize + Vector2.one * framePadding * 2f + new Vector2(0f, headerHeight + footerHeight);
            panelRect.anchoredPosition = new Vector2(0f, 10f + (headerHeight - footerHeight) * 0.5f);

            content = new GameObject("Content", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            content.SetParent(panel.transform, false);
            UiBuilder.Stretch(content, Vector2.zero, Vector2.one);
            content.offsetMin = new Vector2(framePadding, framePadding + footerHeight);
            content.offsetMax = -new Vector2(framePadding, framePadding + headerHeight);

            // İçeriğin dışında (OpenPanel içeriği her açılışta temizler), içeriğin üstünde
            successFlash = UiBuilder.CreateImage(panel.transform, "SuccessFlash", successColor);
            UiBuilder.Stretch(successFlash.rectTransform, Vector2.zero, Vector2.one);
            successFlash.rectTransform.offsetMin = new Vector2(framePadding, framePadding + footerHeight);
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
            titleText = UiBuilder.Outlined(UiBuilder.CreateText(titlePanel, "Text", font, titleFontSize, UiBuilder.HudText));
            UiBuilder.Stretch(titleText.rectTransform, Vector2.zero, Vector2.one);
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Uzun başlık çerçeveden taşmasın: gerekirse küçülür
            titleText.resizeTextForBestFit = true;
            titleText.resizeTextMinSize = 16;
            titleText.resizeTextMaxSize = titleFontSize;
            var separator = UiBuilder.CreateImage(titlePanel, "Separator", new Color(1f, 1f, 1f, 0.12f));
            separator.rectTransform.anchorMin = Vector2.zero;
            separator.rectTransform.anchorMax = new Vector2(1f, 0f);
            separator.rectTransform.pivot = new Vector2(0.5f, 0f);
            separator.rectTransform.sizeDelta = new Vector2(0f, 2f);
            separator.rectTransform.anchoredPosition = new Vector2(0f, 2f);

            // İpucu: aynı çerçevenin içinde alt şeritte, büyük ve iki satıra kadar
            hintText = UiBuilder.Outlined(UiBuilder.CreateText(panel.transform, "Hint", font, hintFontSize, UiBuilder.HudText));
            var hintRect = hintText.rectTransform;
            hintRect.anchorMin = Vector2.zero;
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.sizeDelta = new Vector2(-framePadding * 2f - 12f, footerHeight - 4f);
            hintRect.anchoredPosition = new Vector2(0f, framePadding * 0.5f);
            hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            hintText.lineSpacing = 0.9f;
            hintText.resizeTextForBestFit = true;
            hintText.resizeTextMinSize = 14;
            hintText.resizeTextMaxSize = hintFontSize;

            cancel = UiPrompt.Create(canvas.transform, "Cancel", font, 18);
            cancel.Rect.anchorMin = cancel.Rect.anchorMax = Vector2.right;
            cancel.Rect.pivot = Vector2.right;
            cancel.Rect.anchoredPosition = new Vector2(-UiBuilder.HudMargin, UiBuilder.HudMargin);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;
    }
}
