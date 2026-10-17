using System;
using Deprem.Core;
using UnityEngine;

namespace Deprem.Family
{
    /// <summary>
    /// Play testi 28 (ENTEGRASYON M54): el feneri ve ilk yardım çantası, oyuncu o malzemenin eksikliğinden bir kez
    /// ölmeden evde bulunamaz. Aile tarafı öğretir (kardeş fenersiz bulunamayınca Flashlight, baba vedasında
    /// FirstAidKit); Berke'nin çanta sistemi IsLearned ile malzemeyi gösterir / gizler. "Yeni oyun" Clear çağırır.
    /// PlayerPrefs'te tutulur: "Devam et"te kalır.
    /// </summary>
    public static class BagLessons
    {
        private const string Prefix = "lesson.";

        /// <summary>Bir malzeme ilk kez öğrenilince (çanta sistemi malzemeyi o an gösterebilir)</summary>
        public static event Action<BagItemId> Learned;

        public static bool IsLearned(BagItemId id) => id != BagItemId.None && PlayerPrefs.GetInt(Prefix + id, 0) == 1;

        /// <summary>
        /// Play testi 28: el feneri ve ilk yardımdan biri yüzünden ölününce ikisi birden açılır (oyuncu deprem
        /// çantasının önemini bir kez öğrenince çantaya konacak her şeyi bulabilsin)
        /// </summary>
        private static readonly BagItemId[] LinkedLessons = { BagItemId.Flashlight, BagItemId.FirstAidKit };

        public static void Learn(BagItemId id)
        {
            if (id == BagItemId.None) return;
            bool linked = Array.IndexOf(LinkedLessons, id) >= 0;
            foreach (var item in linked ? LinkedLessons : new[] { id })
            {
                if (IsLearned(item)) continue;
                PlayerPrefs.SetInt(Prefix + item, 1);
                PlayerPrefs.Save();
                Learned?.Invoke(item);
            }
        }

        public static void Clear()
        {
            foreach (BagItemId id in Enum.GetValues(typeof(BagItemId)))
                PlayerPrefs.DeleteKey(Prefix + id);
            PlayerPrefs.Save();
        }
    }
}
