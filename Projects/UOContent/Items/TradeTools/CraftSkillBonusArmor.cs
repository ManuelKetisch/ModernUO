using System;
using ModernUO.Serialization;

namespace Server.Items;

[Flippable(0x13C6, 0x13CE)]
[SerializationGenerator(0)]
public sealed partial class CraftSkillBonusArmor : LeatherGloves
{
    [Constructible]
    public CraftSkillBonusArmor(SkillName skill, int bonus)
    {
        if (skill is not (SkillName.Tailoring or SkillName.Blacksmith or SkillName.Carpentry or
            SkillName.Fletching or SkillName.Lumberjacking or SkillName.Mining))
        {
            throw new ArgumentOutOfRangeException(nameof(skill));
        }

        if (bonus is not (1 or 3 or 5))
        {
            throw new ArgumentOutOfRangeException(nameof(bonus));
        }

        SkillBonuses.SetValues(0, skill, bonus);
    }

    public override string DefaultName => "artisan's gloves";
}
