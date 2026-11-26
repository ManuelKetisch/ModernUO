using System;
using ModernUO.Serialization;
using Server.Engines.BuffIcons;
using Server.Mobiles;
using Server.Spells;

namespace Server.Items;

[SerializationGenerator(0, false)]
public abstract partial class BaseAgilityPotion : BasePotion
{
    public BaseAgilityPotion(PotionEffect effect) : base(0xF08, effect)
    {
    }

    public abstract int DexOffset { get; }
    public abstract TimeSpan Duration { get; }

    public override bool CanDrink(Mobile from)
    {
        if (!base.CanDrink(from))
        {
            return false;
        }

        var offset = Scale(from, DexOffset);
        var duration = GetBuffDuration(from, Duration);

        if (!SpellHelper.AddStatOffset(from, StatType.Dex, offset, duration))
        {
            from.SendLocalizedMessage(502173); // You are already under a similar effect.
            return false;
        }

        var percent = from.RawDex > 0 ? (int)Math.Round(offset * 100.0 / from.RawDex) : offset;
        (from as PlayerMobile)?.AddBuff(
            new BuffInfo(
                BuffIcon.Agility,
                1075841,
                duration,
                percent.ToString(),
                pauseWhileOffline: true,
                persistAcrossRestart: true
            )
        );

        from.FixedEffect(0x375A, 10, 15);
        from.PlaySound(0x1E7);
        return true;
    }

    public override void Drink(Mobile from)
    {
        PlayDrinkEffect(from);
    }
}
