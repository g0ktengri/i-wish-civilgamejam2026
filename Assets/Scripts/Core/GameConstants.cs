namespace Deprem.Core
{
    // Sadece tasarımda sabit olan değerler. Ayarlanabilir süreler ve hızlar
    // ilgili sistemde [SerializeField] olarak durur.
    public static class GameConstants
    {
        public const int PixelsPerUnit = 64; // 64 px = 1 m = 1 Unity birimi

        public const int LoopCount = 5;
        public const int StandUpUnlockLoop = 3;   // "F: Kalk" bu döngüden itibaren
        public const int SpiritPhaseUnlockLoop = 4; // Döngü 4'ten itibaren ruh fazından başlanır
        public const int MaxAftershockInjuries = 2; // İkinci yaralanmada döngü biter
    }
}
