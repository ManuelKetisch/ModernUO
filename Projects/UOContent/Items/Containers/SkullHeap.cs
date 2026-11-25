using ModernUO.Serialization;
using Server.Engines.Quests.Doom;

namespace Server.Items;

[Furniture]
[SerializationGenerator(0)]
public sealed partial class SkullHeap : BaseContainer
{
    [Constructible]
    public SkullHeap() : base(6875)
    {
    }

    public override string DefaultName => "skull heap";
    public override double DefaultWeight => 8.0;
    public override int DefaultGumpID => 0x49;

    public override bool CheckHold(Mobile m, Item item, bool message, bool checkItems, int plusItems, int plusWeight)
    {
        if (item is not (JinxedSkull or ChampionSkull or GoldenSkull))
        {
            if (message)
            {
                m?.SendMessage("The skull heap only holds skulls.");
            }

            return false;
        }

        return base.CheckHold(m, item, message, checkItems, plusItems, plusWeight);
    }
}
