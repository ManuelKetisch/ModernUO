using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Items;

[Forge]
[SerializationGenerator(0)]
public partial class MobileForge : Item
{
    [Constructible]
    public MobileForge() : base(0xFB1)
    {
    }

    public override string DefaultName => "mobile forge";
    public override double DefaultWeight => 10.0;

    public override void OnDoubleClick(Mobile from)
    {
        from.SendMessage(TradeToolAccess.InBackpack(from, this)
            ? "Double-click ore in your backpack, then target this forge."
            : "Keep the mobile forge in your backpack to use it.");
    }

    internal bool CanSmelt(Mobile from, BaseOre ore)
    {
        if (TradeToolAccess.InBackpack(from, this) && TradeToolAccess.InBackpack(from, ore))
        {
            return true;
        }

        from.SendMessage("Keep the mobile forge and ore in your backpack.");
        return false;
    }
}
