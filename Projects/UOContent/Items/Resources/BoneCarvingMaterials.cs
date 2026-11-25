using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0)]
public sealed partial class JinxedSkull : Item
{
    [Constructible]
    public JinxedSkull() : base(0x1AE1)
    {
        Stackable = true;
        Hue = 0x481;
    }

    public override string DefaultName => "jinxed skull";
    public override double DefaultWeight => 1.0;
}

[SerializationGenerator(0)]
public sealed partial class HellhoundRib : Item
{
    [Constructible]
    public HellhoundRib() : base(0x1B17)
    {
        Stackable = true;
    }

    public override string DefaultName => "hellhound rib";
    public override double DefaultWeight => 1.0;
}
