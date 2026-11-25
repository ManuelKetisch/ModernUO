using System;
using Server.Mobiles;

namespace Server.Items;

internal static class WeaponTrainingRewards
{
    internal static void OnCreatureDeath(BaseCreature creature)
    {
        if (creature.Controlled || creature.Summoned || creature.NoKillAwards || creature.IsBonded ||
            creature is BaseVendor || creature.LastOwner != null || creature.Karma >= 0 ||
            creature.Map == null || creature.Map == Map.Internal)
        {
            return;
        }

        var killer = creature.LastKiller;
        var player = killer as PlayerMobile ?? (killer as BaseCreature)?.ControlMaster as PlayerMobile;
        if (player is not { Alive: true, Deleted: false, Backpack: not null } || player.Map != creature.Map ||
            !player.InRange(creature, 24))
        {
            return;
        }

        var amount = Math.Clamp(50L + creature.HitsMax / 4L + Math.Max(0, creature.Fame) / 100L, 50L, 500L);
        var tokens = new WeaponRecyclingToken((int)amount);
        if (!player.Backpack.TryDropItem(player, tokens, false))
        {
            tokens.Delete();
        }
    }
}
