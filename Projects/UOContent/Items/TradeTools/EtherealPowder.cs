using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0)]
public sealed partial class EtherealPowder : Item
{
    [Constructible]
    public EtherealPowder(int amount = 1) : base(0x26B8)
    {
        Stackable = true;
        Amount = amount;
        Hue = 0x489;
    }

    public override string DefaultName => "ethereal powder";
    public override double DefaultWeight => ItemWeights.Reagent;
}
