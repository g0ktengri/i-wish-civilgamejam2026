using System;
using System.Reflection;
using Deprem.Core;

namespace Deprem.Bag
{
    // M54 (Mehmet'in play testi 28): el feneri ve ilk yardım çantası, oyuncu onların eksikliğinden bir kez ölmeden
    // evde görünmez. Hangisinin öğrenildiğini Mehmet'in Deprem.Family.BagLessons sınıfı tutar (PlayerPrefs, "Devam et"te
    // kalır). O sınıf yansımayla okunur: gelmeden de derlenir; yokken her malzeme açık (eski davranış).
    public static class BagLessonGate
    {
        static bool lookedUp;
        static MethodInfo isLearned;
        static MethodInfo clear;

        /// <summary>Malzeme evde görünsün mü (sadece el feneri ve ilk yardım derse bağlı)</summary>
        public static bool IsAvailable(BagItemId id)
        {
            if (id != BagItemId.Flashlight && id != BagItemId.FirstAidKit) return true;
            LookUp();
            if (isLearned == null) return true;
            try
            {
                return (bool)isLearned.Invoke(null, new object[] { id });
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>"Yeni oyun": öğrenilen dersler silinir</summary>
        public static void Clear()
        {
            LookUp();
            clear?.Invoke(null, null);
        }

        static void LookUp()
        {
            if (lookedUp) return;
            lookedUp = true;
            var type = Type.GetType("Deprem.Family.BagLessons, Assembly-CSharp");
            if (type == null) return;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            isLearned = type.GetMethod("IsLearned", flags, null, new[] { typeof(BagItemId) }, null);
            clear = type.GetMethod("Clear", flags, null, Type.EmptyTypes, null);
        }
    }
}
