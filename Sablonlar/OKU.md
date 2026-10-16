# Sanatçı şablonları

Her PNG, oyundaki **gerçek boyutunda** ve **son dosya adıyla** üretildi. Ölçek: 64 piksel = 1 metre.
Bu klasörü Unity okumaz; çizdiğin dosyayı aynı yolla `Assets/Art/` altına koy:

`Sablonlar/Rooms/salon/arka.png` → `Assets/Art/Rooms/salon/arka.png`

Dosyayı koyduktan sonra Unity'de **Tools > Deprem > Evi Yeniden Kur** (ya da Mehmet'e haber ver); doku kendiliğinden yerine oturur.

## Çizgilerin anlamı

| Çizgi | Anlamı |
|---|---|
| Koyu gri | 1 metre (64 px) |
| Açık gri | 16 px |
| Kırmızı çerçeve | Dosyanın sınırı (karakter şeritlerinde kareler arası) |
| Kırmızı taralı alan | Buraya detay çizme: kapı boşluğu ya da merdiven boşluğu kodla oyulur |
| Mavi taralı alan | Ayrı görsel gelecek (salon penceresi, `Decor/pencere.png`) |
| Yeşil | Karakterlerde: dikey = pivot (alt orta), yatay = karakterin boyu, en alt satır = ayak hizası |
| Yeşil çerçeve (eşya, mobilya) | Eşyanın bir parçası (raf, kapak, ayak, yastık...). Çizimi bu çerçevelere oturt |
| Koyu gölgeli parça | Geride kalan parça (kitaplığın arkalığı, raftaki kitaplar); ön parçaların arasından görünür |
| Kırmızı tarama (eşya, mobilya) | Boşluk: oyunda orada parça yok (ayak araları, raf üstü), çizme ya da şeffaf bırak |

Şablonu alt katman olarak kullan; dışa aktarırken kılavuz katmanını gizle.

## Kurallar (GDD)

- **Odalar:** `arka` = arka duvar, `yan` = sol ve sağ duvar (aynı çizim iki duvara gider; sol kenar odanın ön tarafı), `zemin` / `tavan` (üst kenar = arka duvar, alt kenar = ön).
  Arka ve yan duvar aynı desense tek bir `duvar.png` da olur; yine de boyutu yüzeye uymazsa gerilir.
- **Eşyalar ve mobilyalar:** `_on` ön yüz (asıl detay burada), `_yan` yanlar ve arka (sol kenar = ön), `_ust` üst ve alt (alt kenar = ön). Ayna ve çerçevede sadece `_on`.
  Şekil kodda parçalardan kurulur; bütün parçaların ön yüzü aynı `_on` resmini görür. Yani kitaplığı tek resim olarak çiz: rafları, kitapları, kapakları yeşil çerçevelerin yerine çizersen geometriye birebir oturur.
  Mobilyalar (`Decor/`): tek kişilik yatak (oyuncu ve kardeş aynı), ebeveyn yatağı, komodin, kanepe, masa, sandalye, tezgah, buzdolabı.
  **Kapı** (`Decor/kapi`): asıl yüz `kapi_yan.png` (64 x 126 px), kapı açılınca kameraya bu yüz döner; sol kenar kulp tarafı.
  **Kabin** (`Items/kabin`): kapının üstündeki levhaya yazı çizme, "DEPREM KABİNİ" yazısını oyun koyar (dile göre değişir).
- **Karakterler:** 128 x 128 kareler yatay şerit; ilk gece sadece `durma` ve `yurume`.
- Dokulara ışık ve gölge çizilmez; ışığı Unity verir. Ortak Lospec paleti kullanılır.
- Hiçbir görsel Unity'de ölçeklenmez; boyut çizimde belirlenir.

Şablonlar ev ölçülerinden üretilir. Ev değişirse Mehmet **Tools > Deprem > Sanatçı Şablonlarını Üret** ile yeniler.
