using ModernUO.Serialization;
using Server.Engines.BioEngineering;

namespace Server.Items;

[SerializationGenerator(0)]
public partial class BioOrganicMatterSack : Bag
{
    [Constructible]
    public BioOrganicMatterSack()
    {
        Hue = 0x59;
        DropItem(new BioOrganicMatter(1000));
    }

    public override string DefaultName => "organic matter sack";
}
