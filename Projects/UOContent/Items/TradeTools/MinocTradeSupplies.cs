using ModernUO.Serialization;
using Server.Accounting;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0)]
public sealed partial class MinocTradeSupplies : Item
{
    private const string ClaimTag = "MinocSpellcasterKeys";
    private static readonly Point3D _moongate = new(2701, 692, 5);

    [Constructible]
    public MinocTradeSupplies() : base(0xE43) => Movable = false;

    public override string DefaultName => "free spell caster's keys";

    public static void Initialize()
    {
        EnsurePlaced(Map.Trammel);
        EnsurePlaced(Map.Felucca);
    }

    private static void EnsurePlaced(Map map)
    {
        if (map == null)
        {
            return;
        }

        foreach (var supplies in map.GetItemsInRange<MinocTradeSupplies>(_moongate, 4))
        {
            if (supplies is { Deleted: false, Parent: null })
            {
                return;
            }
        }

        Point3D[] candidates =
        [
            new(_moongate.X + 2, _moongate.Y, _moongate.Z),
            new(_moongate.X, _moongate.Y + 2, _moongate.Z),
            new(_moongate.X - 2, _moongate.Y, _moongate.Z),
            new(_moongate.X, _moongate.Y - 2, _moongate.Z)
        ];

        foreach (var candidate in candidates)
        {
            var position = new Point3D(candidate.X, candidate.Y, map.GetAverageZ(candidate.X, candidate.Y));

            if (map.CanFit(position, 16, false, false))
            {
                new MinocTradeSupplies().MoveToWorld(position, map);
                return;
            }
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player)
        {
            TryClaim(player);
        }
    }

    internal bool TryClaim(PlayerMobile player)
    {
        if (player is not { Alive: true, Deleted: false, Account: Account account, Backpack: { } pack } ||
            Deleted || Parent != null || Map == null || Map == Map.Internal || player.Map != Map ||
            !player.InRange(Location, 2) || !player.CanSee(this) || !player.InLOS(this) || !IsAccessibleTo(player))
        {
            player.SendMessage("Move closer to the supply chest.");
            return false;
        }

        if (account.GetTag(ClaimTag) != null)
        {
            player.SendMessage("You already claimed your free spell caster's keys.");
            return false;
        }

        var keys = new TradeResourceStorage(TradeSupplyKind.Spellcasting);
        if (!pack.TryDropItem(player, keys, true))
        {
            keys.Delete();
            return false;
        }

        account.SetTag(ClaimTag, "true");
        player.SendMessage("You receive a set of spell caster's keys.");
        return true;
    }
}
