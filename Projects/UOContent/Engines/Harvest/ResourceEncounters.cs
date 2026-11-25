using System;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Harvest;

internal static class ResourceEncounters
{
    private static readonly Point2D[] _neighborOffsets =
    [
        new(-1, -1), new(-1, 0), new(-1, 1), new(0, -1),
        new(0, 1), new(1, -1), new(1, 0), new(1, 1)
    ];

    internal static CraftResource LeatherResource(HideType hideType) => CraftResource.RegularLeather + (int)hideType;

    internal static Item CreateHides(HideType hideType, int amount, bool cut)
    {
        var types = CraftResources.GetInfo(LeatherResource(hideType))?.ResourceTypes;
        if (types is not { Length: >= 2 })
        {
            return cut ? new Leather(amount) : new Hides(amount);
        }

        return types[cut ? 0 : 1].CreateEntityInstance<Item>(amount);
    }

    internal static HideType GetKnifeHideType(BaseCreature creature) => creature switch
    {
        // A first-generation elemental can be followed once. Its leather may change when carved with the knife.
        LeatherElemental { FollowUp: false } elemental => (HideType)Utility.RandomMinMax(
            Math.Max((int)HideType.Spined, (int)elemental.HideType - 1),
            Math.Min((int)HideType.Ethereal, (int)elemental.HideType + 1)
        ),
        LeatherElemental elemental => elemental.HideType,
        IceSerpent => Utility.RandomBool() ? HideType.Polar : HideType.Synthetic,
        Daemon => Utility.RandomBool() ? HideType.Blaze : HideType.Shadow,
        ShadowWyrm => Utility.RandomBool() ? HideType.Shadow : HideType.Frost,
        _ => creature.HideType < HideType.Ethereal ? creature.HideType + 1 : creature.HideType
    };

    internal static LeatherElemental TryAwakenLeatherElemental(
        Mobile from, BaseCreature source, HideType hideType, double roll
    )
    {
        if (roll >= 0.10 || source is LeatherElemental { FollowUp: true } ||
            hideType is < HideType.Spined or > HideType.Ethereal ||
            !TryFindNearbySpawn(from, out var map, out var point))
        {
            return null;
        }

        return Spawn(new LeatherElemental(LeatherResource(hideType), source is LeatherElemental), from, map, point);
    }

    internal static bool TryFindNearbySpawn(Mobile from, out Map map, out Point3D point)
    {
        map = from.Map;
        point = default;
        if (from is not PlayerMobile { Alive: true } || map == null || map == Map.Internal)
        {
            return false;
        }

        var start = Utility.Random(_neighborOffsets.Length);
        for (var i = 0; i < _neighborOffsets.Length; i++)
        {
            var offset = _neighborOffsets[(start + i) % _neighborOffsets.Length];
            var x = from.X + offset.X;
            var y = from.Y + offset.Y;
            if (!map.CanSpawnMobile(x, y, from.Z - 8, from.Z + 8, from.Z, false, false, out var z))
            {
                continue;
            }

            var candidate = new Point3D(x, y, z);
            if (!from.InLOS(candidate))
            {
                continue;
            }

            point = candidate;
            return true;
        }

        return false;
    }

    internal static T Spawn<T>(T elemental, Mobile from, Map map, Point3D point) where T : BaseCreature
    {
        elemental.OnBeforeSpawn(point, map);
        elemental.MoveToWorld(point, map);
        elemental.Combatant = from;
        return elemental;
    }
}
