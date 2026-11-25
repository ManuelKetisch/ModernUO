using ModernUO.Serialization;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0)]
[Flippable(0xEC4, 0xEC5)]
public partial class GargoylesKnife : SkinningKnife, IUsesRemaining
{
    [SerializableField(0)]
    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _usesRemaining;

    [Constructible]
    public GargoylesKnife(int uses = 50) => _usesRemaining = uses;

    public override string DefaultName => "a gargoyle's knife";

    bool IUsesRemaining.ShowUsesRemaining { get => true; set { } }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Improves hides and may awaken a leather elemental.");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (CheckUse(from))
        {
            from.BeginTarget(2, false, TargetFlags.None, (mobile, target) =>
            {
                if (target is Corpse { Owner: BaseCreature creature } corpse && CheckSkin(mobile, corpse, creature))
                {
                    corpse.Carve(mobile, this);
                }
                else if (target is not Corpse { Owner: BaseCreature })
                {
                    mobile.SendMessage("Choose a hide-bearing creature's corpse.");
                }
            });
        }
    }

    internal bool CheckSkin(Mobile from, Corpse corpse, BaseCreature creature)
    {
        if (!CheckUse(from))
        {
            return false;
        }

        if (corpse.Deleted || corpse.Carved || corpse.Owner != creature || corpse.OwnerWasSummoned ||
            creature.Alive || creature.Controlled || creature.LastOwner != null || creature.IsBonded ||
            corpse.Animated || creature.Hides <= 0 || corpse.Map != from.Map ||
            !from.InRange(corpse.GetWorldLocation(), 2) || !from.CanSee(corpse) || !from.InLOS(corpse) ||
            !corpse.IsAccessibleTo(from) || corpse.IsCriminalAction(from))
        {
            from.SendMessage("You must have rights to a nearby uncarved hide-bearing corpse.");
            return false;
        }

        return true;
    }

    internal void SpendUse(Mobile from)
    {
        if (--UsesRemaining <= 0)
        {
            from.SendMessage("Your gargoyle's knife wore out.");
            Delete();
        }
    }

    private bool CheckUse(Mobile from)
    {
        if (Deleted || UsesRemaining <= 0)
        {
            from.SendLocalizedMessage(1044038); // You have worn out your tool!
            return false;
        }

        if (from is not PlayerMobile { Alive: true } || InSecureTrade ||
            Parent != from && !IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return false;
        }

        return true;
    }
}
