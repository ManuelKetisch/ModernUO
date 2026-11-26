using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0)]
[Flippable(0xF43, 0xF44)]
public partial class SturdyAxe : Hatchet, IUsesRemaining
{
    [Constructible]
    public SturdyAxe(int uses = 180)
    {
        UsesRemaining = uses;
        ShowUsesRemaining = true;
        Hue = 0x973;
    }

    public override string DefaultName => "a sturdy axe";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Cuts Grade A logs for shipbuilding.");
    }
}

[SerializationGenerator(0)]
public partial class GradeALog : Item
{
    [Constructible]
    public GradeALog(int amount = 1) : base(0x1BDD)
    {
        Stackable = true;
        Amount = amount;
    }

    public override double DefaultWeight => ItemWeights.RawWood;
    public override string DefaultName => "grade A logs";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("For shipbuilding.");
    }
}
