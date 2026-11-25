using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0)]
[Flippable(0xF43, 0xF44)]
public partial class GargoylesAxe : Hatchet, IUsesRemaining
{
    [Constructible]
    public GargoylesAxe(int uses = 50)
    {
        UsesRemaining = uses;
        ShowUsesRemaining = true;
        Hue = 0x973;
    }

    public override string DefaultName => "a gargoyle's axe";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Tries the next wood grade and may awaken a wood elemental.");
    }
}
