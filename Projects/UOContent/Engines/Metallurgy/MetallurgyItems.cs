using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0)]
public partial class MeteoriteFragment : Item
{
    [Constructible]
    public MeteoriteFragment() : base(0x19B7)
    {
        Stackable = true;
        Hue = 0x489;
    }

    public override string DefaultName => "meteorite fragment";
    public override double DefaultWeight => ItemWeights.Reagent;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Refine at an anvil and forge with [Metallurgy.");
    }
}

[SerializationGenerator(0)]
public partial class MeteoricBronzeIngot : Item
{
    [Constructible]
    public MeteoricBronzeIngot() : base(0x1BF2)
    {
        Stackable = true;
        Hue = 0x489;
    }

    public override string DefaultName => "meteoric bronze ingot";
    public override double DefaultWeight => ItemWeights.RefinedMaterial;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Make a meteoric pickaxe at an anvil and forge with [Metallurgy.");
    }
}

[Flippable(0xE86, 0xE85)]
[SerializationGenerator(0)]
public partial class MeteoricPickaxe : Pickaxe
{
    internal const int MiningUses = 240;
    internal const int WeightInStones = 5;

    [Constructible]
    public MeteoricPickaxe()
    {
        UsesRemaining = MiningUses;
        Hue = 0x489;
    }

    public override string DefaultName => "meteoric pickaxe";
    public override double DefaultWeight => WeightInStones;
}
