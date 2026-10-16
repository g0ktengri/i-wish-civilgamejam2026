namespace Deprem.Core
{
    // GDD "Sabitlenebilir eşyalar" tablosu, aynı sırayla. Sıra değişmez; sadece sona eklenir.
    public enum ItemId
    {
        None = 0,
        BedShelf = 1,        // Yatağın üstündeki raf (oyuncunun odası)
        Bookcase = 2,        // Kitaplık (salon), babanın üstüne devrilir
        TvUnit = 3,          // TV ünitesi (salon), salon kapısını kapatır
        KitchenCabinets = 4, // Mutfak üst dolapları
        ShoeRack = 5,        // Ayakkabılık (koridor), kabinin önünü kapatır
        StairMirror = 6,     // Merdivendeki ayna
        ToyShelf = 7,        // Oyuncak rafı (kardeşin odası)
        Wardrobe = 8,        // Gardırop (ebeveyn odası)
        BedFrame = 9,        // Ebeveyn yatağının üstündeki çerçeve
    }

    public enum MinigameType
    {
        ScrewTighten = 0,  // Vida sıkma
        CarryAndPlace = 1, // Taşı ve yerleştir
        Lock = 2,          // Kilitle
    }

    public enum BagItemId
    {
        None = 0,
        FirstAidKit = 1, // Zorunlu: babayı kurtarmak için
        Flashlight = 2,  // Zorunlu: karanlık üst katta kardeşi bulmak için
        Water = 3,       // İsteğe bağlı, finalde bonus
        Whistle = 4,     // İsteğe bağlı, finalde bonus
    }

    public enum FamilyMemberId
    {
        None = 0,
        Father = 1,
        Mother = 2,
        Sibling = 3,
    }

    // Bir döngünün akışı: Foreshock → Spirit (döngü 4+) → MainQuake → Rescue → (Won ya da Failed)
    public enum GamePhase
    {
        None = 0,
        Foreshock = 1, // Öncü sarsıntı, yatak odası
        Spirit = 2,    // Ruh fazı, 60 sn
        MainQuake = 3, // Ana deprem, çök-kapan-tutun
        Rescue = 4,    // Artçılar ve kurtarma
        Failed = 5,    // Keşke ekranı
        Won = 6,       // Herkes kabinde, final listesi
    }

    // GDD "Kaybetme durumları" tablosu. Her biri bir keşke ekranına karşılık gelir.
    public enum LoseReason
    {
        None = 0,
        IgnoredForeshock = 1,  // Keşke dikkate alsaydım
        NoDuckCoverHold = 2,   // Keşke çök-kapan-tutun yapsaydım
        FamilyHitByItem = 3,   // Keşke [eşyayı] sabitleseydim
        MissingBagItem = 4,    // Keşke deprem çantası hazırlasaydım
        AftershockInjury = 5,  // Keşke artçıda da çök-kapan yapsaydım
        RescueTimeout = 6,     // Keşke [en çok yavaşlatan eşyayı] sabitleseydim
    }
}
