using System;
using ModernUO.Serialization;
using Server.Engines.BuffIcons;

namespace Server;

[SerializationGenerator(0)]
public sealed partial class ClarityPotion : PowerFactionItem
{
    public ClarityPotion() : base(3628) => Hue = 1154;

    public override string DefaultName => "clarity potion";

    public override bool Use(Mobile from)
    {
        if (!from.BeginAction<ClarityPotion>())
        {
            return false;
        }

        var amount = Utility.Dice(3, 3, 3);
        var time = Utility.RandomMinMax(5, 30);

        from.PlaySound(0x2D6);

        if (from.Body.IsHuman)
        {
            from.Animate(34, 5, 1, true, false, 0);
        }

        from.FixedParticles(0x375A, 10, 15, 5011, EffectLayer.Head);
        from.PlaySound(0x1EB);

        var mod = from.GetStatMod("Concussion");

        if (mod != null)
        {
            from.RemoveStatMod("Concussion");
            from.Mana -= mod.Offset;
        }

        from.PlaySound(0x1EE);
        var duration = TimeSpan.FromMinutes(time);
        PlayerBuffPersistence.AddTimedStatMod(from, StatType.Int, "clarity-potion", amount, duration);

        OfflinePausableTimer.Start(from, duration, from.EndAction<ClarityPotion>);

        return true;
    }
}
