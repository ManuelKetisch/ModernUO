using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class RunicDovetailSaw : BaseRunicTool
{
    [Constructible]
    public RunicDovetailSaw(CraftResource resource) : base(resource, 0x1028) => Hue = CraftResources.GetHue(resource);

    [Constructible]
    public RunicDovetailSaw(CraftResource resource, int uses) : base(resource, uses, 0x1028) => Hue = CraftResources.GetHue(resource);

    public override double DefaultWeight => 2.0;

    public override CraftSystem CraftSystem => DefCarpentry.CraftSystem;

    public override int LabelNumber
    {
        get
        {
            var index = CraftResources.GetIndex(Resource);

            if (index >= 1 && index <= 6)
            {
                return 1072633 + index;
            }

            return 1024137; // dovetail saw
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
