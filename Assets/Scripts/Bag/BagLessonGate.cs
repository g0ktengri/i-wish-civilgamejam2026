using Deprem.Core;
using Deprem.Family;

namespace Deprem.Bag
{
    // M54 (Mehmet'in play testi 28): el feneri ve ilk yardım çantası, oyuncu onların eksikliğinden bir kez ölmeden
    // evde görünmez. Hangisinin öğrenildiğini Mehmet'in Deprem.Family.BagLessons sınıfı tutar (PlayerPrefs, "Devam et"te
    // kalır). Önce yansımayla okunuyordu (sınıf gelmeden derlensin diye); sınıf geldiği için artık doğrudan çağrılır.
    public static class BagLessonGate
    {
        /// <summary>Malzeme evde görünsün mü (sadece el feneri ve ilk yardım derse bağlı)</summary>
        public static bool IsAvailable(BagItemId id)
        {
            if (id != BagItemId.Flashlight && id != BagItemId.FirstAidKit) return true;
            return BagLessons.IsLearned(id);
        }

        /// <summary>"Yeni oyun": öğrenilen dersler silinir</summary>
        public static void Clear() => BagLessons.Clear();
    }
}
