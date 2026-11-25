using System;
using ModernUO.Serialization;
using Server.Engines.Harvest;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0)]
public partial class MinocProspectingMap : MapItem
{
    internal const int MiningRequired = 65;
    internal const int MapArt = 0x14EC;
    internal const int MapPrice = 200;
    internal const int MapStock = 5;
    internal const string MapName = "Minoc prospecting map";

    internal static readonly Point3D SurveySite = new(2558, 499, 0);
    private const int DigRange = 2;
    private const int SurveyRadius = 1;

    [Constructible]
    public MinocProspectingMap() : base(Map.Trammel)
    {
        Protected = true;
        SetDisplay(2398, 339, 2718, 659, 240, 240);
        AddWorldPin(SurveySite.X, SurveySite.Y);
    }

    public override string DefaultName => MapName;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Minoc Mine, Trammel");
        list.Add($"Requires {MiningRequired} Mining and a pickaxe or shovel");
        list.Add("Double-click to read the map or dig at the marked site.");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsInPack(from, this))
        {
            from.SendMessage("Keep the prospecting map in your backpack.");
            return;
        }

        if (from.Map != Map.Trammel || !from.InRange(SurveySite, DigRange + SurveyRadius))
        {
            if (from.NetState != null)
            {
                DisplayTo(from);
            }

            from.SendMessage("Travel to the marked entrance of Minoc Mine in Trammel.");
            return;
        }

        if (!CheckMining(from))
        {
            return;
        }

        if (FindTool(from) == null)
        {
            from.SendMessage("Put a usable pickaxe or shovel in your backpack.");
            return;
        }

        from.SendMessage("Target the cave floor at the marked site.");
        from.BeginTarget(DigRange, true, TargetFlags.None, (owner, targeted) => TryProspect(owner, targeted));
    }

    internal bool TryProspect(Mobile from, object target)
    {
        if (!IsInPack(from, this))
        {
            from.SendMessage("Keep the prospecting map in your backpack.");
            return false;
        }

        if (from.Map != Map.Trammel || target is not StaticTarget floor ||
            !from.InRange(floor, DigRange) || !from.InLOS(floor) || !IsSurveyGround(floor))
        {
            from.SendMessage("Dig in the cave floor at the marked Minoc Mine entrance in Trammel.");
            return false;
        }

        if (!CheckMining(from))
        {
            return false;
        }

        if (FindTool(from) is not Item tool)
        {
            from.SendMessage("Put a usable pickaxe or shovel in your backpack.");
            return false;
        }

        if (!Mining.System.CheckHarvest(from, tool))
        {
            return false;
        }

        var fragment = new MeteoriteFragment();
        if (!from.Backpack.TryDropItem(from, fragment, true))
        {
            fragment.Delete();
            return false;
        }

        var uses = (IUsesRemaining)tool;
        if (--uses.UsesRemaining == 0)
        {
            tool.Delete();
            from.SendMessage("Your mining tool broke.");
        }

        Delete();
        from.PlaySound(0x125);
        from.SendMessage("You found a meteorite fragment. Use [Metallurgy at an anvil and forge to refine it.");
        return true;
    }

    private static bool CheckMining(Mobile from)
    {
        if (from.Skills.Mining.Base >= MiningRequired)
        {
            return true;
        }

        from.SendMessage($"You need {MiningRequired} Mining to excavate the site.");
        return false;
    }

    private static bool IsSurveyGround(StaticTarget floor)
    {
        if (Math.Abs(floor.X - SurveySite.X) > SurveyRadius ||
            Math.Abs(floor.Y - SurveySite.Y) > SurveyRadius ||
            Mining.System.GetDefinition(floor.ItemID, false) != Mining.System.OreAndStone)
        {
            return false;
        }

        foreach (var tile in Map.Trammel.Tiles.GetStaticTiles(floor.X, floor.Y))
        {
            if (tile.ID == floor.ItemID && tile.Z == SurveySite.Z &&
                floor.Z == tile.Z + TileData.ItemTable[tile.ID].CalcHeight)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInPack(Mobile from, Item item) =>
        from is { Alive: true, Deleted: false, Backpack: { } pack } &&
        item is { Deleted: false } && item.IsChildOf(pack) &&
        item.GetSecureTradeCont() == null && item.IsAccessibleTo(from);

    private static Item FindTool(Mobile from)
    {
        var pack = from.Backpack;
        return (Item)pack.FindItemByType<Pickaxe>(predicate: pick =>
                   (pick.GetType() == typeof(Pickaxe) || pick is MeteoricPickaxe) &&
                   pick.UsesRemaining > 0 && IsInPack(from, pick)) ??
               pack.FindItemByType<Shovel>(predicate: shovel => shovel.UsesRemaining > 0 && IsInPack(from, shovel));
    }
}
