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
| `Assets/Editor/Player/` | Mehmet | Oyuncu ve kamera kurulum scripti, oyuncu kontrolcüsü testi |
| `Assets/Editor/Quake/` | Mehmet | Deprem sistemi kurulum scripti ve testi |
| `Assets/Generated/` | Mehmet (otomatik) | HouseBuilder'ın ürettiği materyaller; elle düzenlenmez, her kurulumda yenilenir |
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
- Claude pull'dan önce `git fetch` yapar ve gelecek commit'leri `git log --stat HEAD..origin/main` ile inceler. Pull'dan sonra kullanıcıya diğer kişinin ne getirdiğini kısaca özetler. `CLAUDE.md`, `Core/`, `.claude/`, `Tools/`, `Packages/` ya da `ProjectSettings/` değiştiyse bunları ayrıca belirtir ve yeni bir kural geldiyse ona göre çalışır.
- Sadece derlenen (Console'da kırmızı hata yok) kodu push et. Derlenmeyen kod diğer kişinin işini durdurur.
- Küçük ve sık commit: her çalışan özellik sonrası commit + push.
- Commit mesajı başına kişi: `[Mehmet] artçı tetikleyicileri eklendi`, `[Berke] vida minigame'i`.
- `.meta` dosyaları her zaman commit edilir; dosya taşırken/silerken meta ile birlikte.
- Conflict çıkarsa: kimin klasörüyse onun sürümü geçerlidir; Core'da çıkarsa birlikte çözülür.

## Claude'un kod yazma döngüsü (yaz → kontrol et → düzelt)
Claude kod yazdıktan sonra kullanıcıya teslim etmeden önce şu döngüyü uygular:
1. **Derle:** `powershell -ExecutionPolicy Bypass -File Tools\quick-compile.ps1` (Editor açıkken de çalışır, ~3 sn).
2. **Hata varsa düzelt ve 1'e dön.** En fazla **3 düzeltme turu**. 3. turdan sonra hâlâ hata varsa durur, denediklerini ve kalan hatayı kullanıcıya yazar; aynı düzeltmeyi tekrar denemez.
3. **Kendi değişikliğini gözden geçir** (bir kez, döngüye girmeden): sadece kendi kişisinin klasörüne mi yazdı, namespace doğru mu, ayarlanabilir değerler `[SerializeField]` mı, oyuncuya görünen metin koda gömülü mü, oyun kodunda `UnityEditor` var mı, başka sistemi doğrudan çağırıyor mu (GameEvents kullanılmalı).
4. **Rapor et:** ne değişti, kontrol sonucu, Unity'de elle denenmesi gereken ne var.
5. **Commit/push sadece kullanıcı onayıyla.** `.claude/settings.json` içindeki hook, her commit/push öncesi hızlı kontrolü yeniden çalıştırır ve hata varsa engeller.

Hızlı kontrol asmdef'leri ve Unity'nin kod üretimini kapsamaz. Build almadan önce Editor kapalıyken `Tools\check-compile.ps1` (batchmode, kesin kontrol) çalıştırılır ya da Editor Console'a bakılır. Assets altında `.asmdef` kullanmayın.

## Kod stili
- C#, sınıf adları İngilizce PascalCase, oyuncuya görünen metinler sadece `texts.csv` içinde.
- Her sistem kendi namespace'inde: `Deprem.Player`, `Deprem.Quake`, `Deprem.Loop` vb.
- Ayarlanabilir değerler (süreler, hızlar) `[SerializeField]` olarak açılır; denge testinde koda dokunmadan değiştirilir.

## Beyan
- Hazır kaynak ve AI beyanı teslimde web formunda yapılır (görsel, ses, kod, metin, diğer). Eksik beyan diskalifiye sebebidir.
- Unutulmasın diye hazır kaynak (ses, font vb.) eklerken adını, kaynak bağlantısını ve lisansını commit mesajına yazın.
- Yeniden dağıtımı kısıtlı olduğu için repoya konmayan kaynakların nereden ve nasıl ekleneceği README'ye yazılır (kural 3.6).
