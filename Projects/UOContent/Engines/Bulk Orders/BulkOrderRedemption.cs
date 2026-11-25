using Server.Mobiles;
using Server.Systems.FeatureFlags;

namespace Server.Engines.BulkOrders;

internal static class BulkOrderRedemption
{
    private const int VendorRange = 2;

    internal static bool TryClaim(PlayerMobile player, BaseBOD deed)
    {
        if (!ContentFeatureFlags.BulkOrders ||
            player is not { Alive: true, Map: not null, Deleted: false } || player.Map == Map.Internal ||
            deed is not { Complete: true, Deleted: false } ||
            !deed.IsChildOf(player.Backpack) || deed.GetSecureTradeCont() != null)
        {
            return false;
        }

        foreach (var vendor in player.GetMobilesInRange<BaseVendor>(VendorRange))
        {
            if (vendor.Alive && !vendor.Deleted && vendor.IsValidBulkOrder(deed) &&
                player.CanSee(vendor) && player.InLOS(vendor))
            {
                return vendor.RedeemBulkOrder(player, deed, false);
            }
        }

        player.SendMessage("Stand near a vendor who accepts this order.");
        return false;
    }
}
