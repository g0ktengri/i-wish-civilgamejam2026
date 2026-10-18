using System;
using System.Collections;
using System.Collections.Generic;
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

        [Header("M70: bitiş geri bildirimi (Mehmet'in play testi 32)")]
        [Tooltip("Parça oturunca içeriğin üstündeki kısa parlama")]
        [SerializeField] Color successFlashColor = new Color(1f, 1f, 1f, 0.55f);
        [SerializeField] float successFlashSeconds = 0.18f;
        [Tooltip("Damga basıldıktan sonra panel bu kadar sonra kapanır")]
        [SerializeField] float successSeconds = 0.6f;
        [SerializeField] Color stampColor = new Color(0.56f, 0.92f, 0.5f);
        [Tooltip("Mehmet'in play testi: eğik pixel yazıda B harfi bozuluyor, \"SAGİTLENDİ\" okunuyordu; düz")]
        [SerializeField] float stampAngle = 0f;
        [SerializeField] float stampPopSeconds = 0.14f;

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
        RectTransform stamp;
        CanvasGroup stampGroup;
        Text stampText;
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

        // ------------------------------------------------------------------ M61: ilk giriş öğreticisi

        [Header("M61: ilk giriş öğreticisi (Mehmet'in play testi 31)")]
        [Tooltip("Vurgulanan alanın dışındaki karartma")]
        [SerializeField] Color tutorialDim = new Color(0f, 0f, 0.02f, 0.62f);
        [SerializeField] Color tutorialFrameColor = new Color(1f, 0.82f, 0.4f);

        /// <summary>Öğretici ekranda mı: ruh sayacı bu sırada akmaz (SpiritPhase)</summary>
        public static bool TutorialActive { get; private set; }

        const string TutorialPrefix = "tutorial.minigame.";
        List<(string key, Rect area)> tutorialSteps;
        int tutorialStep;
        int tutorialStartFrame;
        string tutorialSavedHint;
        RectTransform tutorialRoot;
        readonly Image[] tutorialDims = new Image[4];
        readonly Image[] tutorialFrame = new Image[4];
        UiPrompt tutorialNext; // M67: "[F] Devam", panelin sağ altında

        /// <summary>"Yeni oyun": bütün minigame öğreticileri yeniden gösterilir</summary>
        public static void ClearTutorials()
        {
            foreach (MinigameType type in Enum.GetValues(typeof(MinigameType)))
                PlayerPrefs.DeleteKey(TutorialPrefix + type);
            PlayerPrefs.Save();
        }

        // Bu türe ilk girişse adım adım öğretici: vurgulanan alanın dışı kararır, çevresinde sarı çerçeve, adım metni
        // ipucu şeridinde; F / Boşluk / Enter / tık ile sonraki adım. Bu sırada minigame oynanmaz.
        void StartTutorial(MinigameType type)
        {
            if (PlayerPrefs.GetInt(TutorialPrefix + type, 0) == 1) return;
            tutorialSteps = current.TutorialSteps();
            if (tutorialSteps == null || tutorialSteps.Count == 0) return;
            TutorialActive = true;
            tutorialStep = 0;
            tutorialStartFrame = Time.frameCount;
            tutorialSavedHint = hintText.text;

            tutorialRoot = new GameObject("Tutorial", typeof(RectTransform)).GetComponent<RectTransform>();
            tutorialRoot.SetParent(content, false);
            UiBuilder.Stretch(tutorialRoot, Vector2.zero, Vector2.one);
            for (int i = 0; i < 4; i++)
            {
                tutorialDims[i] = UiBuilder.CreateImage(tutorialRoot, "Dim", tutorialDim);
                tutorialFrame[i] = UiBuilder.CreateImage(tutorialRoot, "Frame", tutorialFrameColor);
                tutorialFrame[i].gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1f, -1f);
            }
            ShowTutorialStep();
        }

        void ShowTutorialStep()
        {
            var size = content.rect.size;
            var half = size * 0.5f;
            var area = tutorialSteps[tutorialStep].area;
            float x0 = Mathf.Clamp(area.xMin, -half.x, half.x), x1 = Mathf.Clamp(area.xMax, -half.x, half.x);
            float y0 = Mathf.Clamp(area.yMin, -half.y, half.y), y1 = Mathf.Clamp(area.yMax, -half.y, half.y);
            // Vurgunun çevresinde dört karartma parçası (alanın kendisi açık kalır)
            Place(tutorialDims[0], -half.x, y1, half.x, half.y);
            Place(tutorialDims[1], -half.x, -half.y, half.x, y0);
            Place(tutorialDims[2], -half.x, y0, x0, y1);
            Place(tutorialDims[3], x1, y0, half.x, y1);
            const float t = 3f;
            Place(tutorialFrame[0], x0 - t, y1, x1 + t, y1 + t);
            Place(tutorialFrame[1], x0 - t, y0 - t, x1 + t, y0);
            Place(tutorialFrame[2], x0 - t, y0, x0, y1);
            Place(tutorialFrame[3], x1, y0, x1 + t, y1);

            string count = $"({tutorialStep + 1}/{tutorialSteps.Count}) ";
            hintText.text = Balanced(count + Texts.Get(tutorialSteps[tutorialStep].key));
            tutorialNext.Set(Texts.Get("minigame.tutorial.continue"));
        }

        // M67: sığmayan adım metni ortaya en yakın boşluktan iki dengeli satıra bölünür (tek kelime alta düşmesin)
        string Balanced(string text)
        {
            hintText.text = text;
            float width = hintText.rectTransform.rect.width;
            if (width <= 0f || hintText.preferredWidth <= width) return text;
            int middle = text.Length / 2, best = -1;
            for (int i = 0; i < text.Length; i++)
                if (text[i] == ' ' && (best < 0 || Mathf.Abs(i - middle) < Mathf.Abs(best - middle))) best = i;
            return best < 0 ? text : text.Substring(0, best) + "\n" + text.Substring(best + 1);
        }

        static void Place(Graphic image, float x0, float y0, float x1, float y1)
        {
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Mathf.Max(0f, x1 - x0), Mathf.Max(0f, y1 - y0));
            rect.anchoredPosition = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
        }

        // Öğretici sürerken her kare: çerçeve nabız gibi parlar, tuş ya da tıkla sonraki adım
        void TickTutorial()
        {
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
            foreach (var frame in tutorialFrame)
                if (frame) frame.color = new Color(tutorialFrameColor.r, tutorialFrameColor.g, tutorialFrameColor.b, pulse);
            if (Time.frameCount == tutorialStartFrame) return; // minigame'i açan tuş adımı geçmesin
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool next = (keyboard != null && (keyboard.fKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame ||
                                              keyboard.enterKey.wasPressedThisFrame))
                        || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (!next) return;
            Audio.Sfx.Play("ui.move");
            tutorialStep++;
            if (tutorialStep < tutorialSteps.Count) ShowTutorialStep();
            else EndTutorial(true);
        }

        // seen: öğretici bitti (bir daha gösterilmez); Esc ile kapanırsa bir dahaki girişte yine gösterilir
        void EndTutorial(bool seen)
        {
            if (!TutorialActive) return;
            TutorialActive = false;
            if (tutorialRoot) Destroy(tutorialRoot.gameObject);
            tutorialRoot = null;
            if (hintText) hintText.text = tutorialSavedHint;
            if (tutorialNext) tutorialNext.Set(null);
            if (seen)
            {
                PlayerPrefs.SetInt(TutorialPrefix + currentType, 1);
                PlayerPrefs.Save();
            }
        }

        MinigameType currentType;

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
            HideSuccess();
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
            currentType = type;
            StartTutorial(type); // M61: bu türe ilk girişse öğretici
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

            if (TutorialActive)
            {
                TickTutorial();
                return;
            }

            if (current.Tick(ReadInput())) StartCoroutine(Succeed());
        }

        // M70 (Mehmet'in play testi 32: bütün paneli kaplayan yeşil filtre kötüydü): parça oturur ("tık", kısa beyaz
        // parlama), panelin ortasına eğik "SABİTLENDİ" damgası ve onay işareti basılır, panel successSeconds sonra kapanır
        IEnumerator Succeed()
        {
            finishing = true;
            Audio.Sfx.Play("minigame.latch");
            stampText.text = Texts.Get(doneKey == "minigame.taken" ? "minigame.stamp.taken" : "minigame.stamp.secured");
            successFlash.enabled = true;
            stamp.gameObject.SetActive(true);
            stamp.localRotation = Quaternion.Euler(0f, 0f, stampAngle);
            for (float t = 0f; t < successSeconds; t += Time.deltaTime)
            {
                float flash = 1f - Mathf.Clamp01(t / successFlashSeconds);
                successFlash.color = new Color(successFlashColor.r, successFlashColor.g, successFlashColor.b,
                    successFlashColor.a * flash);
                float k = Mathf.Clamp01(t / stampPopSeconds);
                float ease = 1f - (1f - k) * (1f - k);
                stamp.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, ease); // yukarıdan basılır
                stampGroup.alpha = k;
                yield return null;
            }
            Close(true);
        }

        void HideSuccess()
        {
            successFlash.enabled = false;
            stamp.gameObject.SetActive(false);
        }

        void Close(bool success)
        {
            StopAllCoroutines();
            EndTutorial(false);
            HideSuccess();
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
                case ItemId.StairMirror: variant.Carry = CarryMode.Single; variant.CarryKind = "mirror"; break; // M62: isteğe bağlı eşya, kayan boşluk yerine tek duvar
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
            successFlash = UiBuilder.CreateImage(panel.transform, "SuccessFlash", successFlashColor);
            UiBuilder.Stretch(successFlash.rectTransform, Vector2.zero, Vector2.one);
            successFlash.rectTransform.offsetMin = new Vector2(framePadding, framePadding + footerHeight);
            successFlash.rectTransform.offsetMax = -new Vector2(framePadding, framePadding + headerHeight);
            successFlash.enabled = false;
            BuildStamp();

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

            // M67: öğretici devam ipucu HUD ipuçları gibi tuş kapaklı, panelin sağ alt köşesinin hemen altında;
            // öğretici F'yi sabit okuduğu için tuş ataması uygulanmaz
            tutorialNext = UiPrompt.Create(panel.transform, "TutorialNext", font, 14);
            tutorialNext.RemapKeys = false;
            tutorialNext.Rect.anchorMin = tutorialNext.Rect.anchorMax = Vector2.right;
            tutorialNext.Rect.pivot = Vector2.one;
            tutorialNext.Rect.anchoredPosition = new Vector2(0f, -6f);
        }

        // M70: içeriğin ortasında koyu zeminli, kalın çerçeveli damga; solda pixel onay işareti
        void BuildStamp()
        {
            stamp = new GameObject("Stamp", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            stamp.SetParent(panel.transform, false);
            stamp.anchorMin = stamp.anchorMax = new Vector2(0.5f, 0.5f);
            stamp.sizeDelta = new Vector2(236f, 64f);
            stamp.anchoredPosition = new Vector2(0f, (footerHeight - headerHeight) * 0.5f); // içeriğin ortası
            stampGroup = stamp.GetComponent<CanvasGroup>();
            stampGroup.blocksRaycasts = false;

            var fill = UiBuilder.CreateImage(stamp, "Fill", new Color(0.03f, 0.05f, 0.04f, 0.82f));
            UiBuilder.Stretch(fill.rectTransform, Vector2.zero, Vector2.one);
            const float border = 4f;
            Place(UiBuilder.CreateImage(stamp, "Border", stampColor), -118f, 32f - border, 118f, 32f);
            Place(UiBuilder.CreateImage(stamp, "Border", stampColor), -118f, -32f, 118f, -32f + border);
            Place(UiBuilder.CreateImage(stamp, "Border", stampColor), -118f, -32f, -118f + border, 32f);
            Place(UiBuilder.CreateImage(stamp, "Border", stampColor), 118f - border, -32f, 118f, 32f);

            var check = UiBuilder.CreateImage(stamp, "Check", stampColor);
            check.sprite = CheckSprite;
            Place(check, -102f, -16f, -62f, 16f); // 10x8 piksel, 4 kat

            stampText = UiBuilder.Outlined(UiBuilder.CreateText(stamp, "Text", font, 26, stampColor));
            Place(stampText, -54f, -28f, 110f, 28f);
            stampText.resizeTextForBestFit = true;
            stampText.resizeTextMinSize = 14;
            stampText.resizeTextMaxSize = 26;
            stamp.gameObject.SetActive(false);
        }

        static Sprite checkSprite;

        static Sprite CheckSprite
        {
            get
            {
                if (checkSprite) return checkSprite;
                string[] rows =
                {
                    "........##",
                    ".......###",
                    "......###.",
                    "##...###..",
                    "###.###...",
                    ".#####....",
                    "..###.....",
                    "...#......",
                };
                var texture = new Texture2D(10, 8) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 10; x++)
                    texture.SetPixel(x, 7 - y, rows[y][x] == '#' ? Color.white : Color.clear);
                texture.Apply();
                checkSprite = Sprite.Create(texture, new Rect(0f, 0f, 10f, 8f), new Vector2(0.5f, 0.5f), 1f);
                return checkSprite;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Instance = null;
            TutorialActive = false;
        }

    }
}
