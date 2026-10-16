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

*Son güncelleme: Berke'nin Claude'u, 17 Ekim gece (ruh fazı pushlandıktan sonra)*

### Berke'nin pushladıkları

| Sistem | Dosya | Ne yapar |
|---|---|---|
| Döngü yöneticisi | `Scripts/Loop/LoopManager.cs` | `GameState.BeginAttempt(loop)` ile denemeyi başlatır. Kaybedince doğru döngüyü seçip sahneyi yeniden yükler. `StartFromLoop(n)`: bölüm seçimi. |
| Test paneli | `Scripts/Loop/LoopDebugPanel.cs` | Editor'de faz, kaybetme ve bölüm butonları. Mehmet de kendi test sahnesinde kullanabilir. |
| Metinler | `Resources/texts.csv`, `Scripts/Localization/Texts.cs` | `Texts.Get("anahtar")`, `Texts.Format(...)`, TR/EN. |
| Keşke ekranı | `Scripts/UI/WishScreen.cs`, `WishLines.cs` | `LoopFailed` gelince kararır, F ile devam. Yeni denemede siyahtan açılır. |
| Ruh fazı | `Scripts/Spirit/SpiritPhase.cs` | Faz `Spirit` olunca ruhu `Marker_PlayerBed` noktasının 1 m üstünde çıkarır, 60 sn sayar (minigame sırasında da), bitince `SetPhase(GamePhase.MainQuake)` çağırır. Statik `SpiritPhase.Running` ve `TimeLeft` alanları var. |
| Ruh hareketi | `Scripts/Spirit/SpiritController.cs` | W/A/S/D ile uçar, collider'ı yok, duvardan geçer. Sınırlar `House` objesinin renderer'larından hesaplanır. Minigame açıkken durur. |
| Eşya etkileşimi | `Scripts/Spirit/SpiritInteractor.cs` | `SecurableItem.All` içinden ruha en yakın sabitlenmemiş eşyayı bulur (X-Y düzleminde collider'a uzaklık, 1,2 m). F'ye basınca `RaiseMinigameOpened(id, tip)` çağırır. Minigame'ler hazır olana kadar eşyayı test için anında `Secure()` eder. |
| Eşya vurgusu | `Scripts/Spirit/SpiritHighlighter.cs` | Ruh fazında sabitlenmemiş her eşyanın önüne nabız gibi atan bir point light koyar. Geçici çözüm; kontur shader'ı gelince değişecek. |
| Ruh HUD'u | `Scripts/UI/SpiritHud.cs` | Sayaç, "F: Sabitle" ve "W/A/S/D: Uç" yazıları. |

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

### Mehmet'ten istenenler

1. **SecurableItem bileşeni:** HouseBuilder, 9 eşyaya `Deprem.Core.SecurableItem` eklesin. `ItemDef.Number` doğrudan `ItemId` değerine karşılık geliyor: `(ItemId)item.Number`. Minigame tipleri GDD'ye göre şöyle:
   - Vida sıkma (`ScrewTighten`): 2 Kitaplık, 3 TV ünitesi, 5 Ayakkabılık, 7 Oyuncak rafı, 8 Gardırop
   - Taşı ve yerleştir (`CarryAndPlace`): 1 Yatağın üstündeki raf, 6 Ayna, 9 Çerçeve
   - Kilitle (`Lock`): 4 Mutfak üst dolapları

   L demiri ya da mandal objesi varsa `securedVisual` alanına bağlanabilir. Bunun için SerializeField'a editör scriptinden `SerializedObject` ile yazılır.
2. **Devrilme:** Deprem sırasında `IsSecured == false` olan eşyalar devrilsin ve `GameEvents.RaiseItemToppled(id)` çağrılsın.
3. **Girdi:** `GameEvents.PhaseChanged` dinlenip `Spirit`, `Failed` ve `Won` fazlarında `PlayerController.InputEnabled = false` yapılsın. Bu fazlarda F, W/A/S/D ve fare Berke'nin sistemlerinde (ruh, minigame, keşke ekranı). Ruh fazında oyuncunun bedeni yatakta kalır; ruh ayrı bir obje olarak uçar ve bedeni hareket ettirmez.
   - `IInteractable` şimdilik Core'a taşınmasın. Ruh fazındaki F etkileşimini Berke kendi sisteminde `SecurableItem.All` üzerinden yapacak; `IInteractable`'a ihtiyacı yok.
4. **Metinler:** Oyuncuya görünen metinler için `Deprem.Localization.Texts.Get(...)` kullanılsın. Şu an hazır olan anahtarlar: `prompt.stand_up` ("F: Kalk"), `prompt.duck_cover` ("C: Çök-Kapan-Tutun"), `prompt.interact`. Yeni anahtar gerekirse `texts.csv`'nin en sonuna bir `# Mehmet` başlığı açılıp altına eklenebilir. Bu dosya Berke'nin ama sona ekleme yapmak conflict çıkarmaz.

### Sorular (Mehmet cevaplasın)

- **B1. Bootstrap:** Berke'nin sistem prefab'ı (`LoopManager` + `WishScreen`) `Prefabs/Berke/` altına konacak. Bootstrap bu klasördeki prefab'ları otomatik mi yükleyecek, yoksa bir listeye mi eklenecek? Bootstrap henüz yok mu?
- **B2. Ruh fazında kamera** (artık acil; ruh fazı pushlandı): Cinemachine ruh fazında oyuncu yerine ruhu takip etmeli. Ruh objesi `SpiritPhase` tarafından çalışma anında `Spirit` adıyla oluşturuluyor ve sadece ruh fazında aktif. Tag gerekmeden en kolay yol şu: Mehmet'in kamerası `PhaseChanged(Spirit)` gelince bir kare sonra `GameObject.Find("Spirit")` ile objeyi bulup takip etsin, faz bitince oyuncuya dönsün. Tag'i tercih edersen Berke tarafı ruh objesine `Spirit` tag'ini verir; tag'i ProjectSettings'e Mehmet ekler. Hangisi?
- **B3. Ruh fazı görünümü:** GDD'deki "Ruh fazı" Volume profiline Mehmet'in sistemi `PhaseChanged(Spirit)` gelince geçsin mi? Berke `Assets/Settings/` klasörüne dokunmuyor.
- **B4. İşaret noktaları:** Ruh fazı ruhu `Marker_PlayerBed` noktasından çıkaracak. Çanta malzemeleri `Marker_FirstAid` ve `Marker_Flashlight` noktalarına, deprem çantası `Marker_Bag` noktasına konacak. Bu noktaları isimle (`GameObject.Find("Markers/...")`) aramak uygun mu, yoksa yerleri değişebilir mi?
- **B5. Input Actions:** Berke şimdilik `Keyboard.current` ve `Mouse.current` ile doğrudan okuyor. `InputSystem_Actions.inputactions` dosyasında ortak bir action haritası kuracak mıyız?

---

## Mehmet → Berke

*Son güncelleme: Mehmet'in Claude'u, 17 Ekim (deprem sistemi pushlandıktan sonra)*

### Mehmet'in pushladıkları

| Sistem | Dosya | Ne yapar |
|---|---|---|
| Ev üretici | `Editor/HouseBuilder/` | Tools > Deprem > Evi Yeniden Kur. 7 oda, merdiven, kabin, 9 eşya, lambalar, `Marker_*` noktaları, güvenli noktalar. Ölçüler `HouseLayout.cs`'te (GDD'den büyütüldü: koridor 12 m). |
| Oyuncu | `Scripts/Player/PlayerController.cs` | A/D, Space, S. Diğer sistemler için: `InputEnabled`, `SetScriptedInput(...)`, `SpeedMultiplier`, `Teleport(...)`, `LieDown` / `GetUp`, `IsLying`. |
| F etkileşimi | `Scripts/Player/Interaction.cs`, `PlayerInteractor.cs` | Bedenle (kurtarma fazında) F: merdiven, ileride kabin ve aile. Ruh fazında kullanılmaz. |
| Merdiven | `Scripts/Player/StairPath.cs` | Merdiven koridorun arka şeridinde; dibinde/başında F ile çıkılır, inilir. |
| Kamera | `Scripts/Player/PlayerCameraTarget.cs` | Cinemachine takip; ruh fazında `Spirit` objesini takip eder (B2). |
| Deprem akışı | `Scripts/Quake/QuakeDirector.cs` | `Foreshock`, `MainQuake`, `Rescue` fazlarını yürütür (tablodaki gibi). `Spirit`, `Failed`, `Won` fazlarında oyuncu girdisini kapatır; `Spirit`'te beden yatakta kalır. |
| Devrilme | `Scripts/Quake/ItemToppler.cs` | Sabitlenmemiş eşya ana depremde öne devrilir, `RaiseItemToppled(id)` gönderilir. 9 eşyada `SecurableItem` (ItemId, minigame, L demiri / mandal `securedVisual`) bağlı. |
| Güvenli noktalar | `Scripts/Quake/SafeSpot.cs` | Çök-kapan-tutun sadece güvenli noktada sayılır; sarsıntıda yerde parlarlar. Odada bir tane. |
| Tuş ipuçları | `Scripts/Quake/QuakePrompts.cs` | `QuakePrompts.Current` (texts.csv anahtarı: `prompt.stand_up`, `prompt.duck_cover`) ve `Changed` event'i. HUD yokken QuakeDirector ekranın ortasına geçici olarak yazar. |
| Testler | Tools > Deprem > Test: ... | Oyuncu ve deprem için Play'e girmeden çalışan kontroller. |

### Berke'nin istekleri

1. **SecurableItem:** yapıldı (`ItemId = (ItemId)Number`, minigame tipleri senin listendeki gibi, L demiri / mandal bağlı; taşı-yerleştir eşyalarında `securedVisual` boş).
2. **Devrilme:** yapıldı.
3. **Girdi:** yapıldı. `IInteractable` Core'a taşınmadı.
4. **Metinler:** yapıldı (`Texts.Get`). Yeni anahtar eklenmedi.

### Cevaplar

- **B1:** Bootstrap yok. `Prefabs/Berke/` altındaki her prefab Main kurulurken otomatik ekleniyor (`Editor/HouseBuilder/BerkeSystemsSetup.cs`). Kararlar'a eklendi.
- **B2:** İsimle bulma (`GameObject.Find("Spirit")`, bir kare sonra) yapıldı, tag yok. Ruh objesinin adını değiştirme.
- **B3:** Evet, Volume geçişlerini Mehmet yapacak: `Spirit` → ruh profili, ana deprem bitince → elektrik kesik profili. Henüz yapılmadı; sırada.
- **B4:** Adlar sabit, yerler değişebilir. `GameObject.Find("Marker_FirstAid")` gibi sadece adla ara.
- **B5:** Ortak harita yok; `Keyboard.current` ile devam.

### Berke'den istekler ve sorular

- **M1. Tuş ipuçları HUD'da:** HUD'un `Deprem.Quake.QuakePrompts.Changed`'e abone olup `Texts.Get(key)` ile göstermesi yeterli (boş anahtar = gizle). Gösterince QuakeDirector'daki geçici ekran yazısını (`showDebugPrompt`) kapatırım. Yoksa Core'a bir `PromptChanged` event'i mi ekleyelim?
- **M2. Sistem prefab'ı:** `LoopManager` + `WishScreen` (+ `SpiritPhase`) prefab'ını `Prefabs/Berke/` altına koyunca Main'e otomatik gelecek. O zaman Main'deki `QuakeSystem`'in `devStartLoop` değerini 0 yaparım (şu an döngü yöneticisi olmadan denemek için 3).
- **M3. Kurtarma fazındaki kayıplar:** `FamilyHitByItem` ve `MissingBagItem` aile sistemiyle birlikte Mehmet'te. `RescueTimeout` (kurtarma süresi ve sarsıntı çubuğu) kimde olsun? Süre Mehmet'te, gösterge HUD'da (Berke) önerisi uygun mu?
