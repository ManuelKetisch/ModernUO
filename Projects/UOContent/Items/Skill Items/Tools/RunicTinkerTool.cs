using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Items;

[SerializationGenerator(0)]
public sealed partial class RunicTinkerTool : BaseRunicTool
{
    [Constructible]
    public RunicTinkerTool(CraftResource resource, int uses = 5) : base(resource, uses, 0x1EB8)
    {
    }

    public override string DefaultName => $"{CraftResources.GetName(Resource):L} runic tinker tool";
    public override double DefaultWeight => 1.0;
    public override CraftSystem CraftSystem => DefTinkering.CraftSystem;

    internal void ApplyToJewelry(BaseJewel jewel)
    {
        if (CraftResources.GetInfo(Resource)?.AttributeInfo is not { RunicMaxAttributes: > 0 } attributes)
        {
            return;
        }

        ApplyAttributesTo(jewel, true, 0, Utility.RandomMinMax(attributes.RunicMinAttributes, attributes.RunicMaxAttributes),
            attributes.RunicMinIntensity, attributes.RunicMaxIntensity);
    }
}
