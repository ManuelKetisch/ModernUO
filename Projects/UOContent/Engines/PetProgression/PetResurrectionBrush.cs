using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.PetProgression;

[SerializationGenerator(0)]
public sealed partial class PetResurrectionBrush : Item
{
    private const double RequiredTaming = 80.0;
    private static readonly Type[] _ingredients = [typeof(Bottle), typeof(Ginseng), typeof(Garlic)];
    private static readonly int[] _amounts = [1, 3, 3];

    [Constructible]
    public PetResurrectionBrush() : base(0x0FC1) => Hue = 0x59;

    public override string DefaultName => "animal taming brush";
    public override double DefaultWeight => 1.0;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"{"Makes a pet resurrection draught"}");
        list.Add($"{"Needs 80 Animal Taming"}");
        list.Add($"{"Uses 1 empty bottle, 3 ginseng and 3 garlic"}");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player)
        {
            TryCraftDraught(player);
        }
    }

    internal bool TryCraftDraught(PlayerMobile player)
    {
        if (Deleted || !player.Alive || player.Deleted)
        {
            return false;
        }

        var pack = player.Backpack;
        if (pack == null || !IsChildOf(pack))
        {
            player.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return false;
        }

        if (player.Skills.AnimalTaming.Base < RequiredTaming)
        {
            player.SendMessage("You need 80 Animal Taming to make a pet resurrection draught.");
            return false;
        }

        var draught = new PetResurrectionDraught();
        if (!pack.CheckHold(player, draught, true))
        {
            draught.Delete();
            return false;
        }

        if (pack.ConsumeTotal(_ingredients, _amounts) != -1)
        {
            draught.Delete();
            player.SendMessage("You need one empty bottle, three ginseng and three garlic.");
            return false;
        }

        pack.DropItem(draught);
        player.SendMessage("You make a pet resurrection draught.");
        return true;
    }
}
