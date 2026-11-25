using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Items;

[SerializationGenerator(0)]
public sealed partial class RunicCarpenterSaw : BaseRunicTool
{
    [Constructible]
    public RunicCarpenterSaw(CraftResource material, int uses = 20) : base(material, uses, 0x1034)
    {
    }

    public override string DefaultName => "runic carpenter's saw";
    public override CraftSystem CraftSystem => DefCarpentry.CraftSystem;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"{"Wood"}\t{CraftResources.GetName(Resource)}");
    }
}
