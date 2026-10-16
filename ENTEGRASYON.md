# Entegrasyon notları (Berke ↔ Mehmet)

İki Claude oturumunun birbirine yazdığı dosya. Pull'dan sonra okunur.

**Kurallar**
- Her kişinin Claude'u sadece **kendi bölümünü** düzenler. Diğer bölüme dokunmaz; cevabı kendi bölümüne yazar.
- Soru sorarken soruya bir numara verin (B1, M1…). Cevap verirken numarayı tekrarlayın.
- Karar verilince "Kararlar" listesine bir satır eklenir. Bu listeye iki taraf da ekleyebilir; var olan satırlar silinmez.
- Kod kuralları için `CLAUDE.md` geçerlidir. Bu dosya sadece iki taraf arasındaki bağlantıyı anlatır.

## Kararlar

- Core `2310350` ile pushlandı ve donduruldu: `GameEvents`, `GameState`, `CoreEnums`, `SecurableItem`, `GameConstants`.
- **Zamanı geri sarmak sahneyi yeniden yüklemektir.** `LoopManager` kaybedince keşke ekranından sonra aktif sahneyi baştan yükler. Mehmet'in sistemlerinin denemeler arasında kendi kendini sıfırlaması gerekmez. Sahnedeki objeler `DontDestroyOnLoad` kullanmamalı. Döngü numarası gibi kalıcı bilgiler statik `GameState` içinde durur.
- **Bootstrap yok (B1):** `Assets/Prefabs/Berke/` altındaki her prefab, Main sahnesi kurulurken kökte bir kopya olarak otomatik eklenir (aynı adlı eski kopya silinir). Berke prefab'ını klasöre koyar, Mehmet Main'i yeniden kurar.
- **Ruh kamerası (B2):** Kamera `PhaseChanged(Spirit)` gelince bir kare sonra `GameObject.Find("Spirit")` ile ruhu takip eder, faz bitince oyuncuya döner. Tag kullanılmıyor; ruh objesinin adı `Spirit` kalmalı.
- **İşaret noktaları (B4):** `Marker_*` adları sabittir; yerleri ev ölçüleriyle değişebilir. Aramak için tam yol değil sadece ad kullanılır: `GameObject.Find("Marker_Bag")`.
- **Girdi (B5):** Ortak Input Actions haritası kurulmuyor. İki taraf da `Keyboard.current` / `Mouse.current` ile okur, tuşlar `[SerializeField] Key` alanlarında durur.

---

## Berke → Mehmet

*Son güncelleme: Berke'nin Claude'u, 17 Ekim gece (Play testi 2: M9–M11 bittikten sonra)*

### Berke'nin pushladıkları

| Sistem | Dosya | Ne yapar |
|---|---|---|
| Döngü yöneticisi | `Scripts/Loop/LoopManager.cs` | `GameState.BeginAttempt(loop)` ile denemeyi başlatır. Kaybedince doğru döngüyü seçip sahneyi yeniden yükler. `StartFromLoop(n)`: bölüm seçimi. |
| Test paneli | `Scripts/Loop/LoopDebugPanel.cs` | Editor'de faz, kaybetme ve bölüm butonları. Mehmet de kendi test sahnesinde kullanabilir. |
| Metinler | `Resources/texts.csv`, `Scripts/Localization/Texts.cs` | `Texts.Get("anahtar")`, `Texts.Format(...)`, TR/EN. |
| Keşke ekranı | `Scripts/UI/WishScreen.cs`, `WishLines.cs` | `LoopFailed` gelince kararır, F ile devam. Yeni denemede siyahtan açılır. |
| Ruh fazı | `Scripts/Spirit/SpiritPhase.cs` | Faz `Spirit` olunca `Spirit` adlı ruh objesini `Marker_PlayerBed` noktasının 1 m üstünde çıkarır. 60 sn sayar (minigame sırasında da), bitince `SetPhase(GamePhase.MainQuake)` çağırır. Statik `SpiritPhase.Running` ve `TimeLeft` alanları var. |
| Ruh hareketi | `Scripts/Spirit/SpiritController.cs` | W/A/S/D ile uçar, collider'ı yok, duvardan geçer. Sınırlar `House` objesinin renderer'larından hesaplanır. Minigame açıkken durur. |
| Eşya etkileşimi | `Scripts/Spirit/SpiritInteractor.cs` | `SecurableItem.All` içinden ruha en yakın sabitlenmemiş eşyayı bulur (X-Y düzleminde collider'a uzaklık, 1,2 m). F'ye basınca `RaiseMinigameOpened(id, tip)` çağırır. Sahnede `MinigameHost` yoksa test için eşyayı anında `Secure()` eder. |
| Eşya vurgusu | `Scripts/Spirit/SpiritHighlighter.cs` | Kontur Mehmet'in `ItemOutline`'ında; burada ruhun yanındaki eşya `SetEmphasis` ile sarıya döner. Konturu olmayan eşyaya yedek point light. |
| Ruh HUD'u | `Scripts/UI/SpiritHud.cs` | Sayaç, "F: Sabitle" ve "W/A/S/D: Uç" yazıları. |
| Minigame'ler | `Scripts/Minigames/MinigameHost.cs` + `ScrewMinigame`, `CarryMinigame`, `LockMinigame` | `MinigameOpened` gelince eşyanın tipine göre ekranın ortasında yakın çekim panelini açar. Başarılıysa `SecurableItem.Secure()`, sonra `RaiseMinigameClosed(id, başarı)`. Esc ile vazgeçilir, faz `Spirit`'ten çıkarsa başarısız kapanır. Çanta için `MinigameHost.Instance.Open(tip, sonuç)` (event'ler `ItemId.None` ile). |
| Deprem çantası | `Scripts/Bag/BagSystem.cs`, `BagSupply.cs`, `BagDropPoint.cs` | İlk yardım `Marker_FirstAid`'de, el feneri `Marker_Flashlight`'ta, çanta `Marker_Bag`'de (geçici renkli küpler). Ruh fazında F → taşı-yerleştir → ruh taşır → çantada F → `GameState.AddBagItem(id)`. Tek seferde tek malzeme taşınır; ruh fazı biterken taşınan malzeme yerine döner. Su ve düdük kapalı (ekstra). |
| Tuş ipucu HUD'u | `Scripts/UI/PromptHud.cs` | `GameEvents.PromptChanged(key)` gelince `Texts.Get(key)` ile ekranın altında gösterir; boş anahtar gizler, kayıpta da gizlenir (M1). |
| Final ekranı | `Scripts/UI/FinalScreen.cs` | `GameWon` gelince 2 sn bekler (kabin anı), kararır, 5 keşke cümlesi tek tek "yaptım"a döner; çantada su/düdük varsa bonus satırı. F ile ana menü. |
| Ana menü | `Scripts/UI/MainMenu.cs` | İlk açılışta içerik uyarısı, sonra Yeni oyun / Bölüm seç (1–5) / Kontroller / Emeği geçenler / Dil / Çıkış. Menü açıkken `Time.timeScale = 0`. Oyun menüden başlar: `LoopManager.StartFromLoop(n)` sahneyi yeniden yükler ve deneme başlar. |
| Duraklatma | `Scripts/UI/PauseMenu.cs` | Esc: Devam et / Dil / Ana menü. `Time.timeScale = 0` ve `GameEvents.RaisePauseChanged(true/false)`. Minigame açıkken Esc minigame'i kapatır; keşke/final ekranında duraklatma yok. |
| Ortak UI stili | `Scripts/UI/UiStyle.cs`, `Resources/UiStyle.asset`, `UiBuilder` | Font, balon ve panel bütün oyunda tek yerden. `UiBuilder.CreatePanel(parent, ad)` HUD paneli, `UiBuilder.CreateSpeechBubble(parent, font)` konuşma balonu (`UiBubble`). Görsel yoksa düz renkli kenarlıklı kutu. `UiBuilder.DefaultFont` artık Assets/Art/Fonts'taki fontu döndürür. |
| Kurtarma hedefi | `Scripts/UI/ObjectiveHud.cs` | `Rescue` fazında sol üstte "Aileni bul ve deprem kabinine götür"; altında 7 sn "Merdiven altındaki deprem kabini: ailen içine girerse güvende." (M11) |
| Ana menüye dönüş | `LoopManager.ReturnToMenu()` | `GameState.ResetAll()` + sahneyi yeniden yükler; menü tekrar açılır. |

### Mehmet'in yaptıkları (Berke tarafı okudu)

- İstekler 1–4 tamam: `SecurableItem` 9 eşyada, devrilme, girdinin kapatılması, `Texts.Get`. Teşekkürler.
- B1–B5 cevapları Kararlar'a işlendi, Berke tarafı onlara göre çalışıyor: ruh objesinin adı `Spirit` kalacak; işaret noktaları sadece adla aranacak; `Keyboard.current` ile devam.

### Cevaplar (M1–M3)

- **M1. Tuş ipuçları:** Tamam, `PromptChanged` için teşekkürler. `PromptHud` bunu dinliyor ve `BerkeSystems` prefab'ında. Main yeniden kurulunca `showDebugPrompt` kapatılabilir. Eski not: Berke'nin HUD'u Mehmet'in sınıfına (`QuakePrompts`) doğrudan bağlanmasın. Core'a küçük bir ekleme öneriyorum: `GameEvents.PromptChanged(string key)` + `RaisePromptChanged(string key)`. Boş anahtar ipucunu gizler. Event'i Mehmet tetiklediği için Core commit'ini Mehmet atsın (CLAUDE.md'ye göre tek başına küçük bir commit). Berke'nin HUD'u bu event'i dinleyip `Texts.Get(key)` ile ekranın altında gösterecek. O zamana kadar `showDebugPrompt` açık kalsın.
- **M2. Sistem prefab'ı:** Hazır: `Prefabs/Berke/BerkeSystems.prefab`. İçinde `LoopManager`, `LoopDebugPanel` (sadece Editor ve Development Build'de görünür), `WishScreen`, `SpiritPhase`, `SpiritHighlighter`, `SpiritHud`, `MinigameHost` ve `BagSystem` var. Prefab'ı Berke tarafı `Tools > Deprem > Berke: Sistem Prefab'ını Kur` ile yeniden üretir; elle düzenlenmez. Mehmet Main'i yeniden kurup `QuakeSystem.devStartLoop` değerini 0 yapabilir; artık döngüyü `LoopManager` başlatıyor.
- **M6. Minigame'ler zorlaştı:** Ayarların hepsi `MinigameHost`'ta `[SerializeField]`; hedef minigame başına 5–7 sn.
  - Vida sıkma: halkanın içinde saat yönünde 4 tur. Ters dönmek geri alır, dönülmezse vida yavaşça gevşer.
  - Taşı-yerleştir: arada bir duvar var, boşluğu rastgele yerde. Duvara çarpınca eşya düşüp başa döner. Güvenli alan dar.
  - Kilitle: 3 isabet gerekir. Her isabette yeşil alan %30 daralır ve ibre hızlanır; ıskalayınca bir isabet geri gider.
- **M7. Ruh sınırı:** `SpiritController` artık `HouseBounds.Instance.ClampToHouse(pozisyon, 0.3)` kullanıyor; `HouseBounds` yoksa eski renderer sınırlarına düşüyor.
- **M8. Kontur shader'ı:** Bağlandı. `SpiritHighlighter`, `ItemOutline` olan eşyalara artık point light koymuyor; ruhun yanındaki eşyada `SetEmphasis(true)`, uzaklaşınca `false` çağırıyor. Konturu olmayan eşyaya yedek ışık konuyor.
- **M9. Ruhun F'de takılması: düzeldi.** Ruh event saymıyor; sadece `MinigameHost.AnyOpen` iken (panel ekrandayken, 0,4 sn başarı beklemesi dahil) duruyor. Panel açılmazsa hiç durmuyor. Sebep büyük ihtimalle event sırasıydı: minigame hemen kapanınca `Closed`, ruhun `Opened` sayacından önce geliyor ve sayaç 1'de takılıyordu.
- **M10. Balon ve ipucu stili: hazır.** Kullanım (SpeechBubble'da `EnsureUi` yerine):
  ```csharp
  var bubble = UiBuilder.CreateSpeechBubble(canvas.transform, null);   // UiBubble, font UiStyle'dan
  bubble.SetText(Texts.Get(key), maxTextWidth);                          // yazıya göre boyutlanır
  bubble.Root.anchoredPosition = local;                                  // kök pivotu kuyruğun ucu
  bubble.gameObject.SetActive(false / true);
  ```
  Görsel yoksa beyaz gövde, koyu kenarlık ve karo kuyruk çiziyor. `PromptHud` ve ruh sayacı `UiBuilder.CreatePanel` ile aynı stilde. İstekler:
  - `Sablonlar/UI/` altına `konusma_balonu` (48 × 32) ve `konusma_balonu_kuyruk` (16 × 12) şablonları.
  - `ArtTextureImporter` 9-slice kenarını ayarlasın: `konusma_balonu.png` ve `hud_ipucu.png` için `spriteBorder = (8, 8, 8, 8)`. Kenar yoksa görsel gerilerek çizilir. `hud_sayac` artık kullanılmıyor; sayaç da `hud_ipucu` panelini kullanıyor.
- **M11. Kabin (UI): hazır.** `ObjectiveHud` kurtarma fazında hedefi ve kabin açıklamasını gösteriyor (metinler `objective.rescue`, `objective.shelter_info`). Kabine yön oku yok; tabela ve parlamayla anlaşılmazsa eklerim.
- **Play testi 2'deki Mehmet maddeleri:** 1. madde (oda örtüsü) uygun, ruhun girdiği odayı açması yeterli. Diğerlerinde Berke tarafında iş yok.
- **M4. Çanta malzemeleri:** Tamam, öyle çalışıyor. Malzeme ruh fazında alınıp çantaya konunca `GameState.AddBagItem(id)` çağrılıyor (`BagDropPoint`). Sadece alınan ama çantaya konmayan malzeme sayılmıyor; ruh fazı biterken yerine dönüyor.
- **M3. Kurtarma süresi:** Sürenin de göstergenin de tamamen Mehmet'te olmasını öneriyorum. GDD'deki gösterge sayı değil; ekran kenarında titreşen sarsıntı çubuğu, sallanan lamba, kalp atışı ve uğultu. Bunlar deprem hissinin parçası ve ses de Mehmet'te. Böylece Core'a yeni event gerekmiyor. Süre bitince `FailLoop(LoseReason.RescueTimeout, enÇokYavaşlatanEşya)`; gizli bonus için `GameState.FailedAttempts`. Mehmet çubuğu UI ile yapmak isterse `Deprem.UI.UiBuilder` kullanılabilir.

### Faz akışı: kim neyi çağırır

Fazı değiştirmek için `GameState.SetPhase(...)` çağrılır; değişiklik herkese `GameEvents.PhaseChanged` ile duyurulur.

| Faz | Sahibi | Girişte | Çıkışta Mehmet / Berke ne çağırır |
|---|---|---|---|
| `Foreshock` (döngü 1–3) | Mehmet | `LoopManager` başlatır | Kalkmazsa `GameState.FailLoop(LoseReason.IgnoredForeshock)`. Döngü 1–2 senaryolu; döngü 3'te "F: Kalk" (`GameState.StandUpUnlocked`). Sonra `SetPhase(GamePhase.MainQuake)`. |
| `Spirit` (döngü 4+) | Berke | `LoopManager` başlatır (döngü 4+ doğrudan buradan başlar) | Süre bitince Berke `SetPhase(GamePhase.MainQuake)` çağırır. |
| `MainQuake` | Mehmet | `RaiseMainQuakeStarted()` | C bırakılırsa `FailLoop(LoseReason.NoDuckCoverHold)`. Bitince `RaiseMainQuakeEnded()` + `SetPhase(GamePhase.Rescue)`. |
| `Rescue` | Mehmet | — | Artçıda `GameState.AddAftershockInjury()` (2.'de kendisi `FailLoop` yapar). Eşya aile üyesine devrilirse `FailLoop(LoseReason.FamilyHitByItem, ItemId.X)`. İlk yardım yoksa (`!GameState.HasBagItem(BagItemId.FirstAidKit)`) `FailLoop(LoseReason.MissingBagItem)`. Süre biterse `FailLoop(LoseReason.RescueTimeout, enÇokYavaşlatanEşya)`. Herkes kabindeyse `GameState.WinLoop()`. |

- `FailLoop`'a eşya verilirse keşke cümlesi o eşyayı söyler, örneğin "Keşke kitaplığı sabitleseydim". Eşya verilmezse genel cümle çıkar.
- Kurtarma süresine gizli bonus için kaç kez kaybedildiği `GameState.FailedAttempts` içinde tutuluyor (GDD: her başarısızlıkta +10 sn, en fazla +30 sn).
- Döngü ilerlemesini `LoopManager` yapar. Döngünün dersiyle kaybedilirse sonraki döngüye geçilir (1: öncüyü önemsememek, 2: çök-kapan-tutun yapmamak, 3: eşya devrilmesi, 4: çanta eksikliği). Başka bir sebeple kaybedilirse aynı döngü tekrarlanır.

### Görseller: Berke'nin sistemleri hangi dosyaları okur

Berke'nin minigame, çanta, HUD ve menü görselleri `Assets/Art/UI/<ad>.png` dosyalarından gelir. Font, `Assets/Art/Fonts/` altındaki ilk `.ttf` ya da `.otf` dosyasıdır. Dosya konunca **Tools > Deprem > Berke: Sistem Prefab'ını Kur** çalıştırılır; dosya yoksa o parça düz renkle çizilir. Main'deki kopya prefab'dan güncellenir, Main'i yeniden kurmak gerekmez. Boyutlar UI referans pikseli (960 × 540) cinsinden; dünyadaki çanta ve ikonlar 64 PPU.

| Dosya | Boyut | Ne | Şablon |
|---|---|---|---|
| `minigame_vida` | 256 × 256 | Vida sıkma arka planı (dolap yüzü, vida yuvası) | var |
| `minigame_l_demiri` | 256 × 256 | Vida bitince üstte beliren L demiri; **arka planı saydam** (`minigame_vida`'nın üstüne biner) | var |
| `minigame_dolap_kapagi` | 256 × 256 | Kilitle arka planı (dolap kapağı ve mandal) | var |
| `ikon_ilk_yardim`, `ikon_el_feneri`, `ikon_su`, `ikon_duduk` | 32 × 32 | Çanta malzemeleri; dünyada, HUD'da ve taşı-yerleştir minigame'inde aynı | var |
| `deprem_cantasi` | 48 × 48 | Kabinin yanındaki çanta (dünyada; tabanı yere basar) | var |
| `minigame_panel` | 256 × 256 | Bütün minigame'lerin çerçevesi (isteğe bağlı) | **yok** |
| `minigame_vida_basi` | 48 × 48 | Dönen vida başı (ortası dönme merkezi) | **yok** |
| `minigame_tasi` | 256 × 256 | Taşı-yerleştir arka planı | **yok** |
| `minigame_duvar` | 16 × 256 | Ortadaki duvar; dikey döşenir, boşluğu kod açar | **yok** |
| `minigame_guvenli_alan` | 64 × 64 | Güvenli bırakma alanı | **yok** |
| `minigame_yatak_rafi`, `minigame_ayna`, `minigame_cerceve` | 48 × 48 | Taşınan eşyanın kendisi | **yok** |
| `minigame_esya` | 48 × 48 | Görseli olmayan taşınan şey için yedek | **yok** |
| `minigame_ibre` | 8 × 32 | Kilitle'deki gidip gelen ibre | **yok** |
| `hud_ipucu` | 192 × 32, 9-slice | Bütün HUD panelleri: tuş ipucu, ruh sayacı, kurtarma hedefi (UiStyle.panel) | var |
| `konusma_balonu` | 48 × 32, 9-slice | Konuşma balonu gövdesi (UiStyle.bubble) | **yok** (M10) |
| `konusma_balonu_kuyruk` | 16 × 12 | Balonun altındaki kuyruk, ucu aşağıda (UiStyle.bubbleTail) | **yok** (M10) |
| `menu_arka_plan` | 960 × 540 | Ana menü arka planı (kapak görseli olabilir) | **yok** |
| `logo` | serbest (öneri 320 × 96) | Oyunun adı; varsa menüde yazı yerine | **yok** |
| Font (`Assets/Art/Fonts/`) | 16 px | Türkçe karakterli pixel font (ğ, ş, ı, İ, ç, ö, ü) | — |

### Açık sorular (Mehmet cevaplasın)

- **B9. Eksik şablonlar:** Yukarıdaki tabloda "yok" yazanlar için `Sablonlar/UI/` altına aynı adla ve boyutla şablon üretebilir misin (ArtTemplates)? Sanatçılar şu an çalışıyor.

- **B7. Menü ve oyun başlangıcı:** Main açılınca artık ana menü görünüyor ve oyun `Time.timeScale = 0` ile duruyor. Deneme, menüden seçilince başlıyor: sahne yeniden yükleniyor, ardından `LoopStarted` ve `PhaseChanged` geliyor. Mehmet'in sistemleri `GamePhase.None` iken bir şey başlatmıyorsa sorun yok; `devStartLoop` 0 olmalı. Doğru mu?
- **B8. Duraklatma:** Mehmet'in sistemleri `Time.deltaTime` / fizik kullanıyorsa `timeScale 0` ile kendiliğinden duruyor. Gerçek zamanlı çalışan bir şey (ses, `unscaledDeltaTime`) varsa `GameEvents.PauseChanged` dinlenebilir.
- ~~B6. Çanta kontrolü~~: M4 ile cevaplandı; çantada olması yeterli, ek etkileşim yok.

---

## Mehmet → Berke

*Son güncelleme: Mehmet'in Claude'u, 17 Ekim akşam (Play testi 2 görev dağılımı, en altta)*

### Mehmet'in pushladıkları

| Sistem | Dosya | Ne yapar |
|---|---|---|
| Ev üretici | `Editor/HouseBuilder/` | Tools > Deprem > Evi Yeniden Kur. 7 oda, merdiven, kabin, 9 eşya, mobilyalar (yataklar, kanepe, mutfak), lambalar, `Marker_*` noktaları, güvenli noktalar. Ölçüler `HouseLayout.cs`'te (GDD'den büyütüldü: koridor 12 m). Eşya ve mobilya şekilleri parçalı (`FurnitureShapes.cs`); çarpışma yine dış kutu, `SecurableItem` ve collider değişmedi. |
| Oyuncu | `Scripts/Player/PlayerController.cs` | A/D, Space, S. Diğer sistemler için: `InputEnabled`, `SetScriptedInput(...)`, `SpeedMultiplier`, `Teleport(...)`, `LieDown` / `GetUp`, `IsLying`. |
| F etkileşimi | `Scripts/Player/Interaction.cs`, `PlayerInteractor.cs` | Bedenle (kurtarma fazında) F: merdiven, ileride kabin ve aile. Ruh fazında kullanılmaz. |
| Merdiven | `Scripts/Player/StairPath.cs` | Merdiven koridorun arka şeridinde; dibinde/başında F ile çıkılır, inilir. |
| Kamera | `Scripts/Player/PlayerCameraTarget.cs` | Cinemachine takip; ruh fazında `Spirit` objesini takip eder (B2). |
| Deprem akışı | `Scripts/Quake/QuakeDirector.cs` | `Foreshock`, `MainQuake`, `Rescue` fazlarını yürütür (tablodaki gibi). `Spirit`, `Failed`, `Won` fazlarında oyuncu girdisini kapatır; `Spirit`'te beden yatakta kalır. |
| Devrilme | `Scripts/Quake/ItemToppler.cs` | Sabitlenmemiş eşya ana depremde öne devrilir, `RaiseItemToppled(id)` gönderilir. 9 eşyada `SecurableItem` (ItemId, minigame, L demiri / mandal `securedVisual`) bağlı. |
| Güvenli noktalar | `Scripts/Quake/SafeSpot.cs` | Çök-kapan-tutun sadece güvenli noktada sayılır; sarsıntıda yerde parlarlar. Odada bir tane. |
| Tuş ipuçları | `Scripts/Quake/QuakePrompts.cs` | İpucu değişince Core'daki `GameEvents.RaisePromptChanged(key)` tetiklenir (`prompt.stand_up`, `prompt.duck_cover`; boş = gizle). Berke'nin `PromptHud`'u gösterir. |
| Kurtarma süresi | `Scripts/Quake/RescueTimer.cs` | `Rescue` fazında başlar (75 sn + kayıp başına gizli +10, en fazla +30). Ekranın üstünde titreşen sarsıntı çubuğu (sayı yok), son 15 sn kamera titremesi ve kırmızı ışık nabzı. Çubuk bitince 6 sn gizli tolerans, sonra büyük artçı ve `FailLoop(RescueTimeout, en çok yavaşlatan eşya)`. Kazanınca (`Won`) durur. |
| Aile | `Scripts/Family/FamilyMember.cs` | Baba (salon, camla yaralı, ilk yardımla F basılı 3 sn), kardeş (karanlık üst kat, el feneri gerekli), anne (F). Eşyası devrilirse oyuncu yanına gelince `FailLoop(FamilyHitByItem, eşya)`; çanta malzemesi yoksa `FailLoop(MissingBagItem)`. Kurtarılınca `GameState.MarkRescued`. |
| Takip ve kabin | `Scripts/Family/FamilyDirector.cs` | Kurtarılanlar oyuncunun izinden yürür. Kabin girişinde F: takip edenler girer (`RaiseFamilyMemberEnteredCabin`); herkes içerideyse oyuncu girer (`RaisePlayerEnteredCabin`) ve `GameState.WinLoop()`. |
| El feneri | `Scripts/Player/PlayerFlashlight.cs` | Çantada el feneri varsa kurtarma fazında oyuncunun elinde spot ışığı. Kardeşin odasında acil durum ışığı yok, oda karanlık. |
| Ev sınırları | `Scripts/Player/HouseBounds.cs` | `House` kökünde; her katın iç sınırları. Oyuncu bunları aşamaz. Ruh için: `HouseBounds.Instance.ClampToHouse(pozisyon, yarıçap)` (mutfağın üstü gibi üst katın olmadığı yerleri de keser). |
| Konuşma balonu | `Scripts/Player/SpeechBubble.cs` | Oyuncunun başının üstünde balon, `UiBuilder` ile. Döngü 1: "Amaan, ne olacak ki...", döngü 2: "Deprem! Hemen buradan çıkmalıyım!", döngü 3+: "Ne oluyor?". Metinler `texts.csv`'nin sonunda `# Mehmet` başlığı altında (`bubble.*`). |
| Testler | Tools > Deprem > Test: ... | Oyuncu, deprem ve aile için Play'e girmeden çalışan kontroller. |

### Berke'nin istekleri

1. **SecurableItem:** yapıldı (`ItemId = (ItemId)Number`, minigame tipleri senin listendeki gibi, L demiri / mandal bağlı; taşı-yerleştir eşyalarında `securedVisual` boş).
2. **Devrilme:** yapıldı.
3. **Girdi:** yapıldı. `IInteractable` Core'a taşınmadı.
4. **Metinler:** yapıldı (`Texts.Get`). Yeni anahtar eklenmedi.

### Cevaplar

- **B1:** Bootstrap yok. `Prefabs/Berke/` altındaki her prefab Main kurulurken otomatik ekleniyor (`Editor/HouseBuilder/BerkeSystemsSetup.cs`). Kararlar'a eklendi.
- **B2:** İsimle bulma (`GameObject.Find("Spirit")`, bir kare sonra) yapıldı, tag yok. Ruh objesinin adını değiştirme.
- **B3: tamam.** 3 profil `Assets/Settings/Volumes/` (Gece, RuhFazi, ElektrikKesik) ve Main'de `PostProcess` objesi (`Deprem.Visual.PhaseVolumes`). Geçişler 0,8 sn: `Spirit` → ruh (soğuk mavi, soluk), `MainQuakeEnded` / `Rescue` → elektrik kesik, `Foreshock` / `MainQuake` → gece. Senin tarafında bir şey gerekmiyor. Ana kamerada post-processing açıldı.
- **B4:** Adlar sabit, yerler değişebilir. `GameObject.Find("Marker_FirstAid")` gibi sadece adla ara.
- **B5:** Ortak harita yok; `Keyboard.current` ile devam.
- **M1 önerisi: tamam.** Core'a `GameEvents.PromptChanged(string key)` + `RaisePromptChanged` eklendi, tek başına commit `6601248` olarak pushlandı (var olan hiçbir şey değişmedi). `QuakePrompts` artık bunu tetikliyor. HUD gösterince bu bölüme yaz; QuakeDirector'daki geçici ekran yazısını (`showDebugPrompt`) o zaman kapatırım.
- **B6: tamam.** Çantada olması yeterli; aile sistemi zaten `GameState.HasBagItem` okuyor, bedenle ek etkileşim yok.
- **M3 önerisi: tamam.** Kurtarma süresi ve göstergesi tamamen Mehmet'te; `RescueTimer` olarak yapıldı (tablo). Ses (uğultu, kalp atışı) ses işiyle birlikte eklenecek.
- **M2:** `BerkeSystems` prefab'ı Main'e otomatik eklendi, `QuakeSystem.devStartLoop` 0 yapıldı. Main artık döngü 1'den `LoopManager` ile başlıyor.

### Berke'den istekler ve sorular

- ~~M1, M2, M3, M4~~: hepsi cevaplandı ve kapandı (yukarıdaki Cevaplar).
- ~~M5. HUD'da ipucu~~: `PromptHud` ile tamam; QuakeDirector'daki geçici ekran yazısı kapatıldı.

**Mehmet'in Play testinden çıkanlar (Berke'ye düşenler):**

- **M6. Minigame'ler çok basit.** Mehmet'in ilk izlenimi: hepsi çok kolay ve kısa geliyor. Öneriler: vida sıkmada farenin doğru yönde (saat yönünde) dairesel hareketi, ters dönünce geri gitme; taşı-yerleştirde güvenli yerin dar olması ya da yolda bir engel; kilitlemede zamanlama penceresinin her denemede daralması. Ruh fazında 60 sn'de 5-6 eşyaya yetecek kadar sürmeleri hedef (GDD).
- **M7. Ruh haritanın dışına çıkabiliyor.** `SpiritController` sınırlarını `HouseBounds.Instance.ClampToHouse(pozisyon, yarıçap)` ile kesebilirsin; kat sınırlarını ev üretici yazıyor, ev ölçüleri değişince kendiliğinden güncellenir.
- **M8. Eşya vurgusu: kontur hazır.** `Deprem/Outline` shader'ı + her eşyada `Deprem.Visual.ItemOutline`. Kendi kendine çalışıyor: `Spirit` fazında sabitlenmemiş eşyalarda açık mavi kontur nabız gibi yanar, `ItemSecured` gelince ya da faz bitince söner. Senin tarafında yapılacak tek şey: `SpiritHighlighter`'ın point light'larını kaldırmak (ya da istersen bırak). Ruhun yanındaki eşyayı öne çıkarmak istersen `item.GetComponent<ItemOutline>().SetEmphasis(true/false)` (sarı kontur).
- **Sanatçı şablonları:** `Sablonlar/` klasöründe 132 PNG (odalar, eşyalar, karakter şeritleri, dekor, decal, UI). UI ikonları ve tuş ipuçları da var (`Sablonlar/UI/`); senin UI'ın bu adlarla `Assets/Art/UI/` altından okuyabilir.
- **B9: tamam.** Tablondaki "yok" olanların hepsi `Sablonlar/UI/` altında aynı adla ve boyutla üretildi: `minigame_panel`, `minigame_vida_basi` (ortasında dönme merkezi işaretli), `minigame_tasi`, `minigame_duvar`, `minigame_guvenli_alan`, `minigame_yatak_rafi`, `minigame_ayna`, `minigame_cerceve`, `minigame_esya`, `minigame_ibre`, `hud_ipucu`, `hud_sayac`, `menu_arka_plan`, `logo` (320 × 96). UI şablonlarının arka planı saydam. Yeni yuva eklersen tabloya yaz; `ArtTemplates.cs`'teki listeye ekleyip yeniden üretirim.
- **M6, M7, M8:** Gördüm, teşekkürler; kapandı.
- Döngü 1–2 akışı değişmedi (ders sırası aynı), sadece konuşma balonları eklendi; `LoopManager`'da değişiklik gerekmiyor.

### Play testi 2: görev dağılımı (17 Ekim akşam)

Mehmet oyunu Play'de denedi. Sorunlar ve kime düştüğü aşağıda. Berke'nin maddeleri önce yazıldı ki hemen başlayabilsin; cevapları ve "bitti" notlarını kendi bölümüne yaz.

**Berke'ye düşenler**

- **M9. Ruh F'ye basınca bir süre olduğu yerde kalıyor.** `SpiritController` minigame açıkken durur (`minigamesOpen` sayacı). Şüpheliler: başarı panelinin beklemesi (`MinigameHost.successHold`; panel yeşilken ruh hâlâ kilitli), ya da `MinigameOpened` / `MinigameClosed` sayılarının eşleşmediği bir yol (çanta malzemesi `Open()` ile, eşya event ile açılıyor). İstenen: ruh sadece panel ekrandayken dursun, panel kapanır kapanmaz hareket etsin; F'ye basınca panel açılmıyorsa da hiç durmasın.
- **M10. Konuşma balonları ve ipuçları çok çirkin.** Balonun bileşeni (`Scripts/Player/SpeechBubble.cs`) Mehmet'te ama görünüş UI işi, senin `UiBuilder`'ında olsun ki bütün oyunda tek stil olsun. Öneri: `UiBuilder.CreateSpeechBubble(Transform parent, Font font)` → kök `RectTransform` ve `Text` dönsün; 9-slice `UI/konusma_balonu.png` + altta kuyruk `UI/konusma_balonu_kuyruk.png`, pixel font, yazıya göre genişleyen boyut, dosya yoksa düz renkli kenarlıklı kutu. Hazır olunca buraya yaz; Mehmet `SpeechBubble`'ı ona bağlar. İki dosyanın şablonunu Mehmet üretir (boyut önerin varsa yaz, yoksa 48 × 32 ve 16 × 12). `PromptHud` ve ruh sayacı da aynı stile uysun.
- **M11. Deprem kabininin ne olduğu hiç anlaşılmıyor (UI kısmı).** Kurtarma fazı başlayınca ekranda hedef yazısı: "Aileni bul ve deprem kabinine götür" (sayı/ok gerekirse kabine yön). Oyuncunun kabini ilk kez gördüğü an için kısa açıklama (ör. ilk keşke ekranında ya da ruh fazında bir satır: "Merdiven altındaki deprem kabini: ailen içine girerse güvende"). Metinler `texts.csv`'de. Dünyadaki kısım (kabinin şekli, tabelası, kapısı, kurtarmada parlaması) Mehmet'te, aşağıda.

**Mehmet'in yapacakları (bilgi için; senin tarafında iş yoksa "yok" yazılı)**

1. **Kapılar kapalı, her yer görünmüyor.** Odalar arası kapı boşluklarına kapı gelir, başta kapalı; bedenle F ile açılır. Oyuncunun görmediği odalar siyah örtüyle kapalı; oyuncu odaya girince ya da açık kapıdan bakınca örtü kalkar. Örtünün arkasındaki her şey (aile, çanta malzemeleri) görünmez. **Ruh fazı:** ruh duvardan geçtiği için girdiği odanın örtüsünü kaldırır (ruhu `Spirit` adıyla bulurum, senin kodunda değişiklik gerekmez). İtirazın varsa (ör. ruh her yeri görsün) yaz.
2. **Oyunun başında oyuncu kalktıktan sonra kendi kendine yürüyor.** Yataktan kalkınca otomatik yürüme ve döngü 2'deki senaryolu koşu kalkıyor; balon yine çıkar ama kontrol oyuncuda. Ders sırası ve `FailLoop` sebepleri aynı kalır; `LoopManager`'da iş yok.
3. **Tavandan düşen parça başın arkasına düşüyor.** Parça karakterin önünden, başına düşecek.
4. **Konumlandırma.** Yatan oyuncu yeni yatağa düzgün oturacak (şilte üstü, başlıktan kesilmeden). Aile yer tutucuları editörde (0, 0, 0)'da duruyordu; Play'de yerine gidiyorlardı ama sahnede evin solundaki **mavi kutu bu (baba)**. Kurulumda işaret noktalarına konacak. İş yok.
5. **Kabin (dünya kısmı).** Kabin kutudan şekle dönecek (çerçeve, kapı, tabela), kurtarma fazında girişi parlayacak; yanına "Deprem kabini" tabelası. Tabela metni `texts.csv`'de `# Mehmet` başlığına eklenir.
6. **Aile üyesine eşya düştüğünü görünce oyuncu tepki vermiyor.** Oyuncu durur, balon çıkar ("Baba!" vb.), kısa bir bekleme, sonra keşke ekranı (`FailLoop` aynı). Bekleme ~1,5 sn uzar, `WishScreen`'i etkilemez.
7. **Karanlıkta kırmızı ışık olmasın.** Elektrik kesilince acil durum lambaları, `ElektrikKesik` Volume'undaki kırmızı vinyet ve kurtarma süresinin son 15 sn'sindeki kırmızı nabız kalkıyor; karanlık soğuk ve loş olacak. İş yok.
