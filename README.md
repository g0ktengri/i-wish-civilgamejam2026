# Keşke

[Türkçe](#türkçe) · [English](#english)

## Türkçe

**Bir deprem hikâyesi.** Civil Game Jam 2026 (16–18 Ekim 2026, ODTÜ KALTEV) için 48 saatte geliştirildi.

Deprem gecesini tekrar tekrar yaşayan bir gencin hikâyesi. Her kayıp bir "keşke" bırakır. Zaman geri sarıldığında
oyuncu, öğrendiklerini bir sonraki geceye taşır: öncü sarsıntıyı ciddiye alır, depremde çök-kapan-tutun yapar, ruh
olarak evi hazırlar (eşyaları sabitler, deprem çantasını doldurur) ve ailesini kabine ulaştırmaya çalışır.

2.5D: 3D ev kesiti içinde 2D pixel art karakterler.

> Bu oyun deprem ve kayıp temalarını içerir. En iyi deneyim için kulaklıkla oynayın.

---

### Çalıştırma

| | |
|---|---|
| **Platform** | Windows 10 / 11, 64 bit |
| **Gereksinim** | DirectX 11 ya da 12 destekleyen ekran kartı; ek kurulum gerekmez |
| **Başlatma** | ZIP'i bir klasöre açın, `Keske.exe`'yi çalıştırın |
| **Ayarlar** | Ana menü → Ayarlar: çözünürlük, tam ekran, ses düzeyleri, ekran sarsıntısı |
| **Dil** | Ana menü → Dil: Türkçe / English |

### Kontroller

| Tuş | Eylem |
|---|---|
| **A / D** | Yürü |
| **Space** | Zıpla |
| **S** | Eğil |
| **S (basılı tut, depremde)** | Çök-Kapan-Tutun |
| **F** | Etkileşim: kalk, kapı aç, eşya sabitle, aileyi topla, kabine gir |
| **W / A / S / D** | Ruh (hayalet) olarak uç |
| **Fare** | Minigame'ler (vida sık, taşı ve yerleştir) |
| **Esc** | Duraklat |
| **Herhangi bir tuş** | Keşke ekranını hızlı geç |

Tuşlar ana menüdeki Kontroller ekranından değiştirilebilir.

### Nasıl oynanır

1. **İlk gece:** Okuldan eve dönersin, yatarsın. Gece bir öncü sarsıntı olur…
2. **Kaybedince keşke ekranı:** Ne olduğunu ve ne yapman gerektiğini söyler. Zaman geri sarılır, gece yeniden başlar.
3. **Ana deprem:** Parlayan masanın altına git ve **S'yi basılı tutarak** çök-kapan-tutun yap.
4. **Kurtarma:** Sol üstteki harita aileni, kabini ve malzemeleri gösterir. Aileni bul, gerekirse el feneri ve ilk
   yardım çantasını al, herkesi kabine götür. Babanın ayağındaki taşı **F'yi basılı tutarak** kaldır.
5. **Ruh fazı:** Biri kaybedilince sonraki gece ruh olarak açılır. Süre bitmeden **kırmızı konturlu** eşyaları
   sabitle (fareyle minigame) ve el feneri ile ilk yardım çantasını deprem çantasına taşı.
6. Herkes kabine ulaşınca final.

**Bölüm seç** (ana menü): Başlangıç, Çök-kapan, Ruh fazı ve Final bölümlerine doğrudan gidilebilir.

### Bilinen sorunlar

- Şu an bilinen, oynanışı engelleyen bir sorun yok.

---

### Kaynak koddan açma ve build alma

- **Motor:** Unity **6000.3.10f1** (Unity 6.3 LTS), Universal Render Pipeline.
- Projeyi Unity Hub ile açın. Oyun sahnesi: `Assets/Scenes/Main.unity`.
- **Windows build'i:** Unity menüsünde **Tools → Deprem → Windows build'i al**. Çıktı: `Build/Keske/Keske.exe`.
- Projeyi açmak için ek bir dosya indirmek gerekmez; gerekli bütün dosyalar repoda.

#### Proje yapısı

| Klasör | İçerik |
|---|---|
| `Assets/Scripts/Core` | Ortak durum ve olaylar (GameState, GameEvents) |
| `Assets/Scripts/Player`, `Quake`, `Family`, `Audio`, `Visual` | Karakter, deprem, aile, ses, görsel efektler |
| `Assets/Scripts/Loop`, `Spirit`, `Minigames`, `Bag`, `UI`, `Localization` | Döngü, ruh fazı, minigame'ler, deprem çantası, arayüz, Türkçe/İngilizce metinler |
| `Assets/Editor` | Evi, sahneyi ve prefab'ları kodla kuran editör araçları ve testler |
| `Assets/Art` | Ekip sanatçılarının çizimleri |
| `Assets/Resources/texts.csv` | Oyundaki bütün metinler (TR / EN) |

---

## English

**An earthquake story.** Made in 48 hours for Civil Game Jam 2026 (16–18 October 2026, METU KALTEV).

The game is called **Keşke**, Turkish for "if only" / "I wish"; the name is not translated.

A young man lives the night of an earthquake again and again. Every loss leaves a "keşke" behind. When time rewinds,
he carries what he learned into the next night: he takes the foreshock seriously, drops, covers and holds on during the
quake, prepares the house as a spirit (securing furniture, packing the earthquake bag) and tries to get his family to
the safe cabin.

2.5D: 2D pixel art characters inside a 3D cross-section of a house.

> This game contains themes of earthquakes and loss. Best played with headphones.

### Running the game

| | |
|---|---|
| **Platform** | Windows 10 / 11, 64-bit |
| **Requirements** | A graphics card with DirectX 11 or 12; nothing else to install |
| **Start** | Unzip into a folder and run `Keske.exe` |
| **Settings** | Main menu → Settings: resolution, fullscreen, volume, screen shake |
| **Language** | Main menu → Language: Türkçe / English |

### Controls

| Key | Action |
|---|---|
| **A / D** | Walk |
| **Space** | Jump |
| **S** | Crouch |
| **S (hold, during a quake)** | Drop, Cover, Hold On |
| **F** | Interact: get up, open doors, secure furniture, gather the family, enter the cabin |
| **W / A / S / D** | Fly as the spirit (ghost) |
| **Mouse** | Minigames (tighten screws, carry and place) |
| **Esc** | Pause |
| **Any key** | Skip quickly through the "keşke" screen |

Keys can be rebound from the Controls screen in the main menu.

### How to play

1. **The first night:** You come home from school and go to bed. During the night there is a foreshock…
2. **When you lose, the "keşke" screen** tells you what happened and what you should have done. Time rewinds and the
   night starts again.
3. **The main quake:** Go under the glowing table and **hold S** to drop, cover and hold on.
4. **Rescue:** The map in the top left shows your family, the cabin and the supplies. Find your family, take the
   flashlight and first aid kit if needed, and bring everyone to the cabin. Lift the rock off your father's foot by
   **holding F**.
5. **Spirit phase:** After a loss, the next night opens as a spirit. Before time runs out, secure the furniture with
   a **red outline** (mouse minigames) and carry the flashlight and first aid kit into the earthquake bag.
6. Once everyone reaches the cabin, the finale begins.

**Chapter select** (main menu): jump straight to Beginning, Drop and cover, Spirit phase or Finale.

### Known issues

- No known issues that block play at the moment.

### Opening the source and building

- **Engine:** Unity **6000.3.10f1** (Unity 6.3 LTS), Universal Render Pipeline.
- Open the project with Unity Hub. Game scene: `Assets/Scenes/Main.unity`.
- **Windows build:** in Unity, **Tools → Deprem → Windows build'i al**. Output: `Build/Keske/Keske.exe`.
- No extra downloads are needed to open the project; every required file is in the repository.

---

## Ekip / Team

| | |
|---|---|
| Melis Dönmezer | 2D Artist |
| Duygu İrem El | 2D Artist, 2D Animator |
| Berke Yaşar | Game Coder |
| Mehmet Kerim Artan | Game Developer, Game Designer |
