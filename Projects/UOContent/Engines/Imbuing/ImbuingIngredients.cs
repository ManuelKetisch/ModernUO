using ModernUO.Serialization;
using Server.Engines.Imbuing;

namespace Server.Items;

[SerializationGenerator(0)]
public partial class MagicalResidue : Item
{
    [Constructible]
    public MagicalResidue(int amount = 1) : base(0x2DB1)
    {
        Stackable = true;
        Hue = 0x0;
        Amount = amount;
    }

    public override string DefaultName => "magical residue";
    public override double DefaultWeight => 0.1;
}

[SerializationGenerator(0)]
public partial class EnchantedEssence : Item
{
    [Constructible]
    public EnchantedEssence(int amount = 1) : base(0x2DB2)
    {
        Stackable = true;
        Amount = amount;
    }

    public override string DefaultName => "enchanted essence";
    public override double DefaultWeight => 0.1;
}

[SerializationGenerator(0)]
public partial class RelicFragment : Item
{
    [Constructible]
    public RelicFragment(int amount = 1) : base(0x2DB3)
    {
        Stackable = true;
        Amount = amount;
    }

    public override string DefaultName => "relic fragment";
    public override double DefaultWeight => 0.1;
}

[SerializationGenerator(0)]
public partial class SoulForge : Item
{
    [Constructible]
    public SoulForge() : base(0x2DB0)
    {
    }

    public override string DefaultName => "soulforge";
    public override double DefaultWeight => 10.0;

    public override void OnDoubleClick(Mobile from)
    {
        if (from == null || from.Deleted || !from.Alive)
        {
            return;
        }

        if (Parent != null)
        {
            from.SendMessage("The soulforge must be placed on the ground before you can use it.");
            return;
        }

        if (Map == null || Map == Map.Internal || Map != from.Map ||
            !from.InRange(GetWorldLocation(), ImbuingEngine.StationRange) || !from.InLOS(this))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        ImbuingGump.DisplayTo(from);
    }
}
