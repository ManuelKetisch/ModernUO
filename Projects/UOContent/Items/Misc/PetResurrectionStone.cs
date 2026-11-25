using ModernUO.Serialization;
using Server.Engines.PetProgression;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0)]
public partial class PetResurrectionStone : Item
{
    private const int Reach = 2;

    [Constructible]
    public PetResurrectionStone() : base(0xED4)
    {
        Movable = false;
        Hue = 0x48E;
    }

    public override string DefaultName => "pet resurrection stone";

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile player || !CheckReach(player))
        {
            return;
        }

        player.SendMessage("Choose your bonded pet's ghost.");
        player.BeginTarget(Reach, false, TargetFlags.None, (user, target) =>
        {
            if (user is PlayerMobile owner && CheckReach(owner))
            {
                PetCare.TryResurrect(owner, target as BaseCreature);
            }
        });
    }

    private bool CheckReach(PlayerMobile player)
    {
        if (!player.CheckAlive())
        {
            return false;
        }

        if (!Deleted && Parent == null && Map != null && Map != Map.Internal && player.Map == Map &&
            player.InRange(Location, Reach) && player.CanSee(this) && player.InLOS(this) && IsAccessibleTo(player))
        {
            return true;
        }

        player.SendMessage("Move closer to the pet resurrection stone.");
        return false;
    }
}
