using Deprem.Core;
using Deprem.Localization;

namespace Deprem.UI
{
    // Kaybetme sebebini keşke cümlesine ve altındaki bilgiye çevirir (GDD "Kaybetme durumları").
    public static class WishLines
    {
        public static string Wish(LoseReason reason, ItemId item)
        {
            switch (reason)
            {
                case LoseReason.IgnoredForeshock: return Texts.Get("wish.ignored_foreshock");
                case LoseReason.NoDuckCoverHold: return Texts.Get("wish.duck_cover");
                case LoseReason.MissingBagItem: return Texts.Get("wish.bag");
                case LoseReason.AftershockInjury: return Texts.Get("wish.aftershock");
                case LoseReason.FamilyHitByItem:
                case LoseReason.RescueTimeout:
                    return item == ItemId.None
                        ? Texts.Get("wish.secure_items")
                        : Texts.Format("wish.secure_item", Texts.Get($"item.{item}"));
                default: return string.Empty;
            }
        }

        // M47: final ekranında keşke cümlesinin bugüne dönmüş hali ("Keşke kitaplığı sabitleseydim" → "Kitaplığı sabitle.")
        public static string Advice(LoseReason reason, ItemId item)
        {
            switch (reason)
            {
                case LoseReason.IgnoredForeshock: return Texts.Get("advice.ignored_foreshock");
                case LoseReason.NoDuckCoverHold: return Texts.Get("advice.duck_cover");
                case LoseReason.MissingBagItem: return Texts.Get("advice.bag");
                case LoseReason.AftershockInjury: return Texts.Get("advice.aftershock");
                case LoseReason.FamilyHitByItem:
                case LoseReason.RescueTimeout:
                    return item == ItemId.None
                        ? Texts.Get("advice.secure_items")
                        : Capitalize(Texts.Format("advice.secure_item", Texts.Get($"item.{item}")));
                default: return string.Empty;
            }
        }

        // Türkçede "i" → "İ" (eşya adı cümle başına gelince)
        static string Capitalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var culture = Texts.Current == Language.Turkish
                ? new System.Globalization.CultureInfo("tr-TR")
                : System.Globalization.CultureInfo.InvariantCulture;
            return char.ToUpper(text[0], culture) + text.Substring(1);
        }

        // M47: oyun boyunca söylenen keşkeler (sırayla, tekrarsız); final ekranı hepsini gösterir. Zaman geri sarılınca
        // (sahne yeniden yüklenince) kalır; yeni oyunda ve kayıttan devamda temizlenir.
        static readonly System.Collections.Generic.List<(LoseReason reason, ItemId item)> history =
            new System.Collections.Generic.List<(LoseReason, ItemId)>();

        public static System.Collections.Generic.IReadOnlyList<(LoseReason reason, ItemId item)> History => history;

        public static void Record(LoseReason reason, ItemId item)
        {
            if (reason == LoseReason.None) return;
            if (reason != LoseReason.FamilyHitByItem && reason != LoseReason.RescueTimeout) item = ItemId.None;
            if (!history.Contains((reason, item))) history.Add((reason, item));
        }

        public static void ClearHistory() => history.Clear();

        /// <summary>M60: bu keşke daha önce gösterildi mi (Record'dan önce sorulur)</summary>
        public static bool WasShown(LoseReason reason, ItemId item)
        {
            if (reason != LoseReason.FamilyHitByItem && reason != LoseReason.RescueTimeout) item = ItemId.None;
            return history.Contains((reason, item));
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => history.Clear();

        public static string Info(LoseReason reason)
        {
            switch (reason)
            {
                case LoseReason.IgnoredForeshock: return Texts.Get("info.ignored_foreshock");
                case LoseReason.NoDuckCoverHold: return Texts.Get("info.duck_cover");
                case LoseReason.MissingBagItem: return MissingInfo();
                case LoseReason.AftershockInjury: return Texts.Get("info.aftershock");
                case LoseReason.FamilyHitByItem:
                case LoseReason.RescueTimeout: return Texts.Get("info.secure_items");
                default: return string.Empty;
            }
        }

        // M65 (Mehmet'in play testi 31): eksik malzeme yüzünden kaybedildiyse bilgi satırı neyin eksik olduğunu açıkça
        // söyler (kardeş: el feneri, baba: ilk yardım); Mehmet'in BagLessons.LastMissingItem / LastMissingMember'ı
        static string MissingInfo()
        {
            switch (Family.BagLessons.LastMissingItem)
            {
                case BagItemId.Flashlight: return Texts.Get("info.missing_flashlight");
                case BagItemId.FirstAidKit: return Texts.Get("info.missing_firstaid");
                default: return Texts.Get("info.bag");
            }
        }
    }

}
