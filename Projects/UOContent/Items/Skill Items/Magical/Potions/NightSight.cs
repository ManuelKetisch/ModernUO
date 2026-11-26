using System;
using ModernUO.Serialization;
using Server.Engines.BuffIcons;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class NightSightPotion : BasePotion
{
    [Constructible]
    public NightSightPotion() : base(0xF06, PotionEffect.Nightsight)
    {
    }

    public virtual TimeSpan Duration => TimeSpan.FromMinutes(20.0);

    public override bool CanDrink(Mobile from)
    {
        if (!base.CanDrink(from))
        {
            return false;
        }

        if (!from.BeginAction<LightCycle>())
        {
            from.SendMessage("You already have night sight.");
            return false;
        }

        return true;
    }

    public override void Drink(Mobile from)
    {
        var duration = GetBuffDuration(from, Duration);

        new LightCycle.NightSightTimer(from, duration).Start();
        from.LightLevel = LightCycle.DungeonLevel / 2;

        (from as PlayerMobile)?.AddBuff(new BuffInfo(BuffIcon.NightSight, 1075643, duration));

        from.FixedParticles(0x376A, 9, 32, 5007, EffectLayer.Waist);
        from.PlaySound(0x1E3);

        PlayDrinkEffect(from);
    }
}
