using System;
using Server.Mobiles;

namespace Server.Engines.BioEngineering;

internal static class BioOrganicHarvest
{
    internal static void AddLoot(BaseCreature creature)
    {
        var amount = Yield(creature, Utility.RandomDouble());
        if (amount > 0)
        {
            creature.PackItem(new BioOrganicMatter(amount));
        }
    }

    internal static int Yield(BaseCreature creature, double roll)
    {
        if (!BioVial.IsWildCreature(creature) || creature.NoKillAwards || creature.IsBonded || creature.LastOwner != null ||
            creature.Karma >= 0 || creature.Fame < 100 || creature.Map is null || creature.Map == Map.Internal)
        {
            return 0;
        }

        var credited = creature.LastKiller is BaseCreature follower ? follower.GetDamageMaster(creature) :
            creature.LastKiller;
        if (credited is not PlayerMobile { Deleted: false } player || player.Map != creature.Map)
        {
            return 0;
        }

        var fame = Math.Clamp(creature.Fame, 0, 10_000);
        var chance = 0.45 + fame / 40_000.0;
        return roll < chance ? Math.Clamp(80 + fame / 35, 80, 350) : 0;
    }
}
