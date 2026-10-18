using Deprem.Core;
using Deprem.Family;

namespace Deprem.Bag
{
    // M54 (Mehmet'in play testi 28): el feneri ve ilk yardım çantası, oyuncu onların eksikliğinden bir kez ölmeden
    // evde görünmez. Hangisinin öğrenildiğini Mehmet'in Deprem.Family.BagLessons sınıfı tutar (PlayerPrefs, "Devam et"te
    // kalır). Önce yansımayla okunuyordu (sınıf gelmeden derlensin diye); sınıf geldiği için artık doğrudan çağrılır.
    // Jüri temposu: AlwaysAvailable açıkken (BagSystem.suppliesFromFirstNight) ikisi de ilk geceden evde, alınabilir.
    public static class BagLessonGate
    {
        /// <summary>Açıksa ders beklenmez: el feneri ve ilk yardım ilk geceden evde</summary>
        public static bool AlwaysAvailable { get; set; }

        /// <summary>Malzeme evde görünsün mü (sadece el feneri ve ilk yardım derse bağlı)</summary>
        public static bool IsAvailable(BagItemId id)
        {
            if (AlwaysAvailable || (id != BagItemId.Flashlight && id != BagItemId.FirstAidKit)) return true;
            return BagLessons.IsLearned(id);
        }

        /// <summary>"Yeni oyun": öğrenilen dersler silinir</summary>
        public static void Clear() => BagLessons.Clear();
    }
}
