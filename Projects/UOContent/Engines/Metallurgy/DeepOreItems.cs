using System;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0)]
public sealed partial class VolcanicGlass : Item
{
    [Constructible]
    public VolcanicGlass() : base(0x19B7)
    {
        Stackable = true;
        Hue = 0x497;
    }

    public override string DefaultName => "volcanic glass";
    public override double DefaultWeight => ItemWeights.Reagent;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Make a volcanic glass dagger with [Metallurgy.");
    }
}

[SerializationGenerator(0)]
public sealed partial class MeteoricIronOre : Item
{
    [Constructible]
    public MeteoricIronOre() : base(0x19B7)
    {
        Stackable = true;
        Hue = 0x489;
    }

    public override string DefaultName => "meteoric iron ore";
    public override double DefaultWeight => ItemWeights.RawSpecialOre;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Make a meteoric iron shield or conductive silver with [Metallurgy.");
    }
}

[SerializationGenerator(0)]
public sealed partial class ConductiveSilverIngot : Item
{
    [Constructible]
    public ConductiveSilverIngot() : base(0x1BF2)
    {
        Stackable = true;
        Hue = 0x482;
    }

    public override string DefaultName => "conductive silver ingot";
    public override double DefaultWeight => ItemWeights.RefinedMaterial;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Make conduit sleeves with [Metallurgy.");
    }
}

[Flippable(0xF52, 0xF51)]
[SerializationGenerator(0)]
public sealed partial class VolcanicGlassDagger : Dagger
{
    internal const int FireConversion = 25;

    [Constructible]
    public VolcanicGlassDagger()
    {
        Hue = 0x497;
        PlayerConstructed = true;
        AosElementDamages.Fire = FireConversion;
    }

    public override string DefaultName => "volcanic glass dagger";
    public override int InitMinHits => 18;
    public override int InitMaxHits => 25;

    public override void GetDamageTypes(
        Mobile wielder, out int phys, out int fire, out int cold, out int pois, out int nrgy, out int chaos, out int direct
    )
    {
        base.GetDamageTypes(wielder, out phys, out fire, out cold, out pois, out nrgy, out chaos, out direct);
        if (wielder is BaseCreature)
        {
            var converted = Math.Clamp(phys, 0, FireConversion);
            phys -= converted;
            fire += converted;
        }
    }
}

[SerializationGenerator(0)]
public sealed partial class MeteoricIronShield : HeaterShield
{
    [Constructible]
    public MeteoricIronShield()
    {
        Hue = 0x489;
        PlayerConstructed = true;
    }

    public override string DefaultName => "meteoric iron shield";
    public override double DefaultWeight => 14.0;
    public override int InitMinHits => 95;
    public override int InitMaxHits => 110;
    public override int BasePhysicalResistance => 2;
    public override int BaseFireResistance => 0;
}

[Flippable(0x13EE, 0x13EF)]
[SerializationGenerator(0)]
public sealed partial class ConductiveSilverSleeves : RingmailArms
{
    internal const int ManaCostReduction = 5;

    [Constructible]
    public ConductiveSilverSleeves()
    {
        Hue = 0x482;
        PlayerConstructed = true;
        MeditationAllowance = ArmorMeditationAllowance.All;
        Attributes.LowerManaCost = ManaCostReduction;
    }

    public override string DefaultName => "silver conduit sleeves";
    public override int InitMinHits => 20;
    public override int InitMaxHits => 28;
    public override int BasePhysicalResistance => 2;
}
