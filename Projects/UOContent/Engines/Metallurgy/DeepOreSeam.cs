using System;
using ModernUO.Serialization;
using Server.Engines.Harvest;
using Server.Mobiles;

namespace Server.Items;

public enum DeepOreKind
{
    VolcanicGlass,
    MeteoricIron
}

[SerializationGenerator(0)]
public sealed partial class DeepOreSeam : Item
{
    internal const int Capacity = 4;
    internal const int RefillMinutes = 25;
    internal const int GlassMining = 75;
    internal const int IronMining = 90;
    private const int Reach = 2;

    internal static readonly Point2D FireSite = new(5695, 1390);
    internal static readonly Point2D ShameSite = new(5396, 126);
    private static readonly TimeSpan _refillInterval = TimeSpan.FromMinutes(RefillMinutes);

    [SerializableField(0, setter: "private")]
    private DeepOreKind _kind;

    [SerializableField(1, setter: "private")]
    private int _units;

    [SerializableField(2, setter: "private")]
    private DateTime _refillAt;

    [Constructible]
    public DeepOreSeam(DeepOreKind kind = DeepOreKind.VolcanicGlass) : base(0x19B9)
    {
        Kind = kind;
        Units = Capacity;
        Hue = kind == DeepOreKind.MeteoricIron ? 0x489 : 0x497;
        Movable = false;
    }

    public override string DefaultName => Kind == DeepOreKind.MeteoricIron
        ? "meteoric iron seam"
        : "volcanic glass seam";

    internal int MiningRequired => Kind == DeepOreKind.MeteoricIron ? IronMining : GlassMining;

    public static void Initialize()
    {
        PlaceSeam(Map.Trammel, FireSite, DeepOreKind.VolcanicGlass);
        PlaceSeam(Map.Trammel, ShameSite, DeepOreKind.MeteoricIron);
    }

    private static void PlaceSeam(Map map, Point2D point, DeepOreKind kind)
    {
        if (map == null)
        {
            return;
        }

        var site = new Point3D(point.X, point.Y, map.GetAverageZ(point.X, point.Y));
        foreach (var seam in map.GetItemsInRange<DeepOreSeam>(site, 0))
        {
            if (seam is { Deleted: false, Movable: false, Parent: null } && seam.Kind == kind)
            {
                return;
            }
        }

        if (map.CanFit(site, 1, false, false))
        {
            new DeepOreSeam(kind).MoveToWorld(site, map);
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"Requires {MiningRequired} Mining.");
        list.Add("Use a pickaxe or shovel on this seam.");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (CanReach(from))
        {
            var units = Available(Core.Now);
            if (units > 0)
            {
                from.SendMessage($"{units} portions remain. Use a pickaxe or shovel to mine.");
            }
            else
            {
                from.SendMessage("This seam is depleted. Check back later.");
            }
        }
    }

    internal int Available(DateTime now) => Units > 0 ? Units : now >= RefillAt ? Capacity : 0;

    internal bool TryMine(Mobile from, Item tool) => TryMine(from, tool, Core.Now);

    internal bool TryMine(Mobile from, Item tool, DateTime now)
    {
        if (!CanReach(from))
        {
            from.SendMessage("Stand beside the seam to mine it.");
            return false;
        }

        if (from.Skills.Mining.Base < MiningRequired)
        {
            from.SendMessage($"You need {MiningRequired} Mining to excavate this seam.");
            return false;
        }

        if (from.Backpack is not { } pack || tool is not (Pickaxe or Shovel or GargoylesPickaxe) ||
            tool is not IUsesRemaining { UsesRemaining: > 0 } uses || tool.Deleted ||
            (tool.Parent != from && !tool.IsChildOf(pack)) || tool.GetSecureTradeCont() != null ||
            !tool.IsAccessibleTo(from))
        {
            from.SendMessage("Equip a usable mining tool or put one in your backpack.");
            return false;
        }

        if (!from.CanBeginAction(Mining.System) || !Mining.System.CheckHarvest(from, tool))
        {
            return false;
        }

        var units = Available(now);
        if (units == 0)
        {
            from.SendMessage("This seam is depleted. Check back later.");
            return false;
        }

        Item ore = Kind == DeepOreKind.MeteoricIron ? new MeteoricIronOre() : new VolcanicGlass();
        var material = ore.DefaultName;
        if (!pack.TryDropItem(from, ore, true))
        {
            ore.Delete();
            return false;
        }

        Units = units - 1;
        if (Units == 0)
        {
            RefillAt = now + _refillInterval;
        }

        uses.ShowUsesRemaining = true;
        if (--uses.UsesRemaining == 0)
        {
            tool.Delete();
            from.SendLocalizedMessage(1044038); // You have worn out your tool!
        }

        from.RevealingAction();
        from.PlaySound(0x125);
        from.SendMessage($"You mine one portion of {material}. Use [Metallurgy at an anvil and forge.");
        return true;
    }

    private bool CanReach(Mobile from)
    {
        var point = Kind == DeepOreKind.MeteoricIron ? ShameSite : FireSite;
        return from is PlayerMobile { Alive: true, Deleted: false } && !Deleted && !Movable && Parent == null &&
               Map == Map.Trammel && from.Map == Map && X == point.X && Y == point.Y && from.InRange(this, Reach) &&
               Math.Abs(from.Z - Z) <= 8 && from.CanSee(this) && from.InLOS(this) && IsAccessibleTo(from);
    }
}
