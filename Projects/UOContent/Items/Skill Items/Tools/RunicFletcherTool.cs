using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class RunicFletcherTool : BaseRunicTool
{
    [Constructible]
    public RunicFletcherTool(CraftResource resource) : base(resource, 0x1022) => Hue = CraftResources.GetHue(resource);

    [Constructible]
    public RunicFletcherTool(CraftResource resource, int uses) : base(resource, uses, 0x1022) => Hue = CraftResources.GetHue(resource);

    public override double DefaultWeight => 2.0;

    public override CraftSystem CraftSystem => DefBowFletching.CraftSystem;

    public override int LabelNumber
    {
        get
        {
            var index = CraftResources.GetIndex(Resource);

            if (index >= 1 && index <= 6)
            {
                return 1072627 + index;
            }

            return 1044559; // Fletcher's Tools
        }
    }

    public override void AddNameProperties(IPropertyList list)
    {
        base.AddNameProperties(list);

        var index = CraftResources.GetIndex(Resource);
        if (index is >= 1 and <= 6)
        {
            return;
        }

        if (!CraftResources.IsStandard(Resource))
        {
            var num = CraftResources.GetLocalizationNumber(Resource);

            if (num > 0)
            {
                list.Add(num);
            }
            else
            {
                list.Add(CraftResources.GetName(Resource));
            }
        }
    }
}
