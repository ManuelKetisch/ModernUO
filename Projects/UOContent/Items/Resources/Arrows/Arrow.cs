using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class Arrow : Item, ICommodity
{
    [Constructible]
    public Arrow(int amount = 1) : base(0xF3F)
    {
        Stackable = true;
        Amount = amount;
    }

    public override double DefaultWeight => ItemWeights.Ammunition;
    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}
