using ModernUO.Serialization;
using Server.Engines.Harvest;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0)]
[Flippable(0xF43, 0xF44)]
public partial class ProspectorsAxe : Hatchet, IUsesRemaining
{
    [Constructible]
    public ProspectorsAxe(int uses = 50)
    {
        UsesRemaining = uses;
        ShowUsesRemaining = true;
        Hue = 0x4A8;
    }

    public override string DefaultName => "a prospector's axe";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Improves one grove by a grade until it replenishes.");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (CanProspect(from))
        {
            from.BeginTarget(2, false, TargetFlags.None, (mobile, target) => Prospect(mobile, target));
        }
    }

    internal bool Prospect(Mobile from, object target)
    {
        if (!CanProspect(from))
        {
            return false;
        }

        var system = Lumberjacking.System;
        if (target is not Static and not StaticTarget ||
            !system.GetHarvestDetails(from, this, target, out var tile, out var map, out var location, out var isLand) ||
            map != from.Map || !from.InRange(location, 2) || !from.InLOS(target) ||
            system.GetDefinition(tile, isLand) is not { } definition ||
            definition.GetBank(map, location.X, location.Y) is not { } bank)
        {
            from.SendMessage("Choose a nearby standing tree.");
            return false;
        }

        if (bank.Vein != bank.DefaultVein)
        {
            from.SendMessage("This grove has already been prospected.");
            return false;
        }

        var next = definition.NextVein(map, bank.Vein);
        if (next == null)
        {
            from.SendMessage("This grove cannot be improved further.");
            return false;
        }

        bank.Vein = next;
        from.SendMessage($"This grove can now yield {CraftResources.GetName(CraftResources.GetFromType(next.PrimaryResource.Types[0]))} wood.");

        if (--UsesRemaining <= 0)
        {
            from.SendMessage("Your prospector's axe wore out.");
            Delete();
        }

        return true;
    }

    private bool CanProspect(Mobile from)
    {
        if (Deleted || UsesRemaining <= 0)
        {
            from.SendLocalizedMessage(1044038); // You have worn out your tool!
            return false;
        }

        if (from is not PlayerMobile { Alive: true } || InSecureTrade || Parent != from && !IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return false;
        }

        return true;
    }
}
