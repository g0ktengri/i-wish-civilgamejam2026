# AI Kullanım Beyanı

Civil Game Jam 2026 kural kitapçığı madde 4 ve EK A.5'e göre tutulur.
Her AI kullanımı **yapıldığı anda** buraya yazılır; kullanım ne kadar küçük olursa olsun.
Hazır kaynaklar burada değil, `ASSETS.md`'de beyan edilir.
Yanlış ya da eksik beyan diskalifiye sebebidir (madde 4.4).

## Kategori özeti (teslim formundaki seçimler)

| Kategori | Durum | Araç | Amaç ve oyundaki kullanım yeri |
|---|---|---|---|
| Görsel | Kullanılmadı | — | Bütün görseller el çizimi. AI ile üretilen geçici doku ya da şablon finalde kalırsa "Kullanıldı" yapılır |
| Ses | Kullanılmadı | — | |
| Kod | Kullanıldı | Claude Code (Anthropic) | Oyun kodu, editör scriptleri (ev üretici, texture import), shader'lar, yardımcı scriptler |
| Metin | Kullanıldı | Claude (Anthropic) | GDD taslağı, oyun içi metin taslakları, İngilizce çeviri |
| Diğer | Kullanıldı | Claude Code (Anthropic) | Proje kurulumu, beyan dosyalarının taslağı, derleme kontrol scripti |

## Kullanım günlüğü

Kısa ve sık yazın: kim, ne zaman, hangi araç, ne için, nerede kullanıldı.

| Zaman | Kişi | Kategori | Araç | Ne için / nerede |
|---|---|---|---|---|
| 16 Ekim | Mehmet, Berke | Metin | Claude | GDD taslağı, CLAUDE.md çalışma kuralları |
| 16 Ekim | Mehmet | Diğer | Claude Code | Cinemachine paketinin eklenmesi, ASSETS.md / AI.md taslakları, `Tools/check-compile.ps1`, `Tools/quick-compile.ps1`, commit öncesi derleme hook'u |
