# CLAUDE.md — Deprem Gamejam Oyunu

Bu dosya iki yazılımcının (Mehmet ve Berke) Claude Code oturumları için ortak kurallardır.
Oturuma başlarken Claude'a kim olduğunu söyle: "Ben Mehmet'im" ya da "Ben Berke'yim".
Claude yalnızca o kişinin sahip olduğu klasörlerde dosya oluşturur ve düzenler.

## Proje
- Unity 6.3 LTS (6000.3.10f1), URP. Platform: Windows build.
- Tasarım kaynağı: GDD. Ölçek: 64 px = 1 m = 1 Unity birimi.
- Tür: 2.5D, 3D ev kesiti içinde 2D pixel art karakterler, zaman döngüsü.

## Klasör sahipliği

| Klasör | Sahibi | İçerik |
|---|---|---|
| `Assets/Scripts/Core/` | ORTAK (dondurulmuş) | GameEvents, GameState, ItemId, SecurableItem, sabitler |
| `Assets/Scripts/Player/` | Mehmet | Karakter kontrolcü, zıplama, eğilme, yaralanma |
| `Assets/Scripts/Quake/` | Mehmet | Öncü, ana deprem, artçılar, çök-kapan-tutun, eşya devrilme, kamera sarsıntısı |
| `Assets/Scripts/Family/` | Mehmet | Baba, anne, kardeş, takip, kabin |
| `Assets/Scripts/Audio/` | Mehmet | Ses yöneticisi ve ses tetikleyicileri |
| `Assets/Editor/HouseBuilder/` | Mehmet | Evi ve 9 eşyayı kodla kuran editör scripti, texture import scripti |
| `Assets/Settings/` (Volume, URP) | Mehmet | Işık, Volume profilleri |
| `Assets/Scripts/Loop/` | Berke | Döngü yöneticisi, kontrol noktaları, kaybetme durumları |
| `Assets/Scripts/Spirit/` | Berke | Ruh fazı, ruh hareketi, 60 sn sayaç, eşya vurgusu |
| `Assets/Scripts/Minigames/` | Berke | Vida sıkma, taşı ve yerleştir, kilitle |
| `Assets/Scripts/Bag/` | Berke | Deprem çantası ve malzemeler |
| `Assets/Scripts/UI/` | Berke | HUD, keşke ekranları, final listesi, menü, bölüm seçimi |
| `Assets/Scripts/Localization/` + `Assets/Resources/texts.csv` | Berke | Türkçe/İngilizce metinler |
| `Assets/Prefabs/Mehmet/`, `Assets/Prefabs/Berke/` | Adındaki kişi | Her kişinin kendi prefab'ları |
| `Assets/Art/` | Sanatçılar | Kod buraya dokunmaz |

## Ortak dosyalar (Core) kuralı
- `Core/` Cuma 22.00'de birlikte yazılır ve dondurulur.
- Sonradan değişiklik gerekirse: önce diğer kişiye (Mehmet ↔ Berke) sor, değişikliği tek başına ve küçük bir commit olarak yap, hemen push et.
- Var olan event/alan adlarını silme veya yeniden adlandırma; sadece yenisini ekle.

## Sistemler birbiriyle nasıl konuşur
- Doğrudan birbirinin sınıfını çağırmak yerine `Core/GameEvents` üzerindeki C# event'leri kullanılır.
- Ortak durum `Core/GameState` içinde tutulur (mevcut döngü, sabitlenen eşyalar, çantadaki malzemeler, kurtarılan aile üyeleri).
- Eşyalar: `SecurableItem` bileşeni (Core) her eşyada bulunur.
  - Berke (minigame) sadece `Secure()` çağırır.
  - Mehmet (deprem) sadece `IsSecured` okur ve sabitlenmemişse eşyayı devirir.

## Sahne kuralları
- `Assets/Scenes/Main.unity` sahibi Mehmet'tir. Berke ana sahneyi düzenlemez.
- Her kişinin kendi test sahnesi vardır: `Assets/Scenes/Dev/Mehmet_Test.unity`, `Assets/Scenes/Dev/Berke_Test.unity`.
- Sistemler prefab olarak kurulur. Ana sahnede tek bir `Bootstrap` objesi sistem prefab'larını yükler; Berke yeni bir sistem eklemek için kendi prefab'ını `Prefabs/Berke/` altına koyar ve Mehmet'ten Bootstrap listesine eklemesini ister (ya da Bootstrap klasördeki prefab'ları otomatik yükler).
- Claude `.unity` sahne dosyalarını elle düzenlemez; kurulum editör scripti ve prefab'larla yapılır.

## Proje ayarları
- `ProjectSettings/` ve `Packages/manifest.json` sadece Mehmet tarafından değiştirilir.
- Tag, Layer ve Input Actions ilk saatte birlikte tanımlanır.
- Paket eklemek gerekirse önce haber ver.

## Git kuralları
- Her oturuma başlamadan: `git pull --rebase`
- Sadece derlenen (Console'da kırmızı hata yok) kodu push et. Derlenmeyen kod diğer kişinin işini durdurur.
- Küçük ve sık commit: her çalışan özellik sonrası commit + push.
- Commit mesajı başına kişi: `[Mehmet] artçı tetikleyicileri eklendi`, `[Berke] vida minigame'i`.
- `.meta` dosyaları her zaman commit edilir; dosya taşırken/silerken meta ile birlikte.
- Conflict çıkarsa: kimin klasörüyse onun sürümü geçerlidir; Core'da çıkarsa birlikte çözülür.

## Kod stili
- C#, sınıf adları İngilizce PascalCase, oyuncuya görünen metinler sadece `texts.csv` içinde.
- Her sistem kendi namespace'inde: `Deprem.Player`, `Deprem.Quake`, `Deprem.Loop` vb.
- Ayarlanabilir değerler (süreler, hızlar) `[SerializeField]` olarak açılır; denge testinde koda dokunmadan değiştirilir.

## Beyan
- Kullanılan her hazır kaynak eklendiği anda `ASSETS.md`'ye, her AI kullanımı `AI.md`'ye yazılır.
