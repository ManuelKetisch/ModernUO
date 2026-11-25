using System;
using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Items;

[Flippable(0x13E4, 0x13E3)]
[SerializationGenerator(0)]
public sealed partial class AncientTradeHammer : BaseTool
{
    [SerializableField(0, setter: "private")]
    private SkillName _tradeSkill;

    [SerializableField(1, setter: "private")]
    private int _bonus;

    private SkillMod _equippedBonus;

    [Constructible]
    public AncientTradeHammer(SkillName skill, int bonus, int uses = 600) : base(uses, 0x13E4)
    {
        if (skill is not (SkillName.Carpentry or SkillName.Fletching))
        {
            throw new ArgumentOutOfRangeException(nameof(skill));
        }

        if (bonus <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bonus));
        }

        TradeSkill = skill;
        Bonus = bonus;
        Layer = Layer.OneHanded;
        Hue = 0x482;
    }

    public override double DefaultWeight => 8.0;

    public override string DefaultName => TradeSkill == SkillName.Carpentry
        ? "ancient carpentry hammer"
        : "ancient fletching hammer";

    public override CraftSystem CraftSystem => TradeSkill == SkillName.Carpentry
        ? DefCarpentry.CraftSystem
        : DefBowFletching.CraftSystem;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        var skill = TradeSkill == SkillName.Carpentry ? "Carpentry" : "Fletching";
        list.Add($"{skill} skill bonus +{Bonus}");
    }

    public override void OnAdded(IEntity parent)
    {
        base.OnAdded(parent);
        ApplyBonus(parent);
    }

    public override void OnRemoved(IEntity parent)
    {
        base.OnRemoved(parent);
        _equippedBonus?.Remove();
        _equippedBonus = null;
    }

    private void ApplyBonus(IEntity parent)
    {
        if (parent is Mobile mobile && UsesRemaining > 0 && Bonus > 0)
        {
            _equippedBonus?.Remove();
            _equippedBonus = new DefaultSkillMod(TradeSkill, nameof(AncientTradeHammer), true, Bonus);
            mobile.AddSkillMod(_equippedBonus);
        }
    }

    [AfterDeserialization]
    private void AfterDeserialization() => ApplyBonus(Parent);
}
