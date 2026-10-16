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

        public static string Info(LoseReason reason)
        {
            switch (reason)
            {
                case LoseReason.IgnoredForeshock: return Texts.Get("info.ignored_foreshock");
                case LoseReason.NoDuckCoverHold: return Texts.Get("info.duck_cover");
                case LoseReason.MissingBagItem: return Texts.Get("info.bag");
                case LoseReason.AftershockInjury: return Texts.Get("info.aftershock");
                case LoseReason.FamilyHitByItem:
                case LoseReason.RescueTimeout: return Texts.Get("info.secure_items");
                default: return string.Empty;
            }
        }
    }
}
