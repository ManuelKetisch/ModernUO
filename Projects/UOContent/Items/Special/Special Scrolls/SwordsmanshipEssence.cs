using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class SwordsmanshipEssence : Item
{
    private const int TrainingCap = SoloProgression.MaxSkill * 10;
    private const int EssenceCap = 1300;

    [Constructible]
    public SwordsmanshipEssence(int amount = 1) : base(0x318E)
    {
        Stackable = true;
        Amount = amount;
    }

    public override string DefaultName => "Essence of Swordsmanship";

    public override void OnDoubleClick(Mobile from)
    {
        if (!SoloProgression.AppliesTo(from) || !from.Alive)
        {
            return;
        }

        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        var skill = from.Skills[SkillName.Swords];
        if (skill.CapFixedPoint < TrainingCap || skill.BaseFixedPoint < TrainingCap)
        {
            from.SendMessage("Use a 120 Swordsmanship powerscroll and train Swordsmanship to 120 first.");
            return;
        }

        if (skill.BaseFixedPoint >= EssenceCap)
        {
            from.SendMessage("Your Swordsmanship has reached the essence limit.");
            return;
        }

        if (skill.Lock != SkillLock.Up)
        {
            from.SendMessage("Set Swordsmanship to Raise before using essences.");
            return;
        }

        // Skills are stored in tenths, so each consumed essence adds one fixed-point unit.
        var used = Math.Min(Amount, EssenceCap - skill.BaseFixedPoint);
        skill.BaseFixedPoint += used;
        Consume(used);
        from.SendMessage($"Your base Swordsmanship is now {skill.Base:F1}.");
    }
}
