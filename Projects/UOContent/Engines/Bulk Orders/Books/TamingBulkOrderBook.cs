using ModernUO.Serialization;

namespace Server.Engines.BulkOrders;

[SerializationGenerator(0)]
public partial class TamingBulkOrderBook : BulkOrderBook
{
    [Constructible]
    public TamingBulkOrderBook() => Hue = 0x471;

    public override string DefaultName => "animal taming bulk order book";

    protected override bool AcceptsProfession(BODType profession) => profession == BODType.Taming;
}
