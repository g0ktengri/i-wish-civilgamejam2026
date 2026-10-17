using System;
using UnityEngine;

namespace Deprem.Minigames
{
    // Minigame görselleri. Her alan Assets/Art/UI/<dosya>.png ile eşleşir; BerkeSystems prefab'ı
    // kurulurken dosya varsa kendiliğinden bağlanır. Boş kalan parça düz renkle çizilir.
    // Boyutlar UI referans pikseli (960 x 540) cinsinden; görsel bu boyutta çizilir, ölçeklenmez.
    [Serializable]
    public class MinigameArt
    {
        [Tooltip("minigame_panel.png, 256 x 256: bütün minigame'lerin çerçevesi")]
        public Sprite panel;

        [Header("Vida sıkma")]
        [Tooltip("minigame_vida.png, 256 x 256: arka plan (dolabın duvara bakan yüzü)")]
        public Sprite screwBackground;
        [Tooltip("minigame_vida_panel.png, 600 x 512 (ekranda 300 x 256): oda görüntüsünün önünde vidalı pano (Play testi 24, ekip çizimi), arkası saydam")]
        public Sprite screwPanel;
        [Tooltip("minigame_levha.png, 168 x 112: 4 köşesinden vidalanan L demiri levhası")]
        public Sprite screwPlate;
        [Tooltip("minigame_vida_basi.png, 32 x 32: yuvarlak, dönen vida başı (ortası dönme merkezi)")]
        public Sprite screwHead;
        [Tooltip("minigame_tornavida.png, 96 x 16: tornavida, ucu SOLDA ortada, sağa doğru sap")]
        public Sprite screwdriver;
        [Tooltip("minigame_l_demiri.png, 256 x 256: sabitlenince görünen L demiri")]
        public Sprite screwDone;

        [Header("Taşı ve yerleştir")]
        [Tooltip("minigame_tasi.png, 256 x 256: arka plan")]
        public Sprite carryBackground;
        [Tooltip("minigame_duvar.png, 16 x 256: ortadaki duvar (boşluk kodla açılır, dikey döşenir)")]
        public Sprite carryWall;
        [Tooltip("minigame_tasi_panel.png, 720 x 512 (ekranda 360 x 256): ekip çizimi kutu (Panel2), çerçeve ve iç oda. " +
                 "Varsa arka planın yerine geçer; eşya çerçevenin içinde kalır, duvarlar çubuk çizimiyle")]
        public Sprite carryPanel;
        [Tooltip("minigame_tasi_cubuk.png, 32 x 16 (ekranda 16 genişlik): metal çubuğun gövdesi, dikey gerilir")]
        public Sprite carryRod;
        [Tooltip("minigame_tasi_cubuk_uc.png, 32 x 16: çubuğun yuvarlak ucu (aşağı bakan; alttaki çubukta ters çevrilir)")]
        public Sprite carryRodEnd;
        [Tooltip("minigame_guvenli_alan.png, 64 x 64: güvenli alan")]
        public Sprite carryTarget;
        [Tooltip("minigame_esya.png, 48 x 48: eşyaya özel görsel yoksa taşınan şey")]
        public Sprite carryItem;
        [Tooltip("minigame_yatak_rafi.png, 48 x 48")]
        public Sprite carryBedShelf;
        [Tooltip("minigame_ayna.png, 48 x 48")]
        public Sprite carryMirror;
        [Tooltip("minigame_cerceve.png, 48 x 48")]
        public Sprite carryFrame;

        [Header("Kilitle")]
        [Tooltip("minigame_dolap_kapagi.png, 256 x 256: arka plan (dolap kapağı ve mandal)")]
        public Sprite lockBackground;
        [Tooltip("minigame_ibre.png, 8 x 32: gidip gelen ibre")]
        public Sprite lockNeedle;
    }
}
