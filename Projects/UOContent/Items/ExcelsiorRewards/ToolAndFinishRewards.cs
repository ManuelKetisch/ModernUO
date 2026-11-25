using System;
using ModernUO.Serialization;
using Server.Engines.WeaponLevelingServices;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Items;

[Flippable(0xE86, 0xE85)]
[SerializationGenerator(0)]
public sealed partial class SmithersProtector : Hatchet
{
    [Constructible]
    public SmithersProtector()
    {
        ItemID = 0xE86;
        Hue = 0x489;
        WeaponAttributes.UseBestSkill = 1;
    }

    public override string DefaultName => "smither's protector";
}

[Flippable(0xEC4, 0xEC5)]
[SerializationGenerator(0)]
public sealed partial class TailorsProtector : SkinningKnife
{
    [Constructible]
    public TailorsProtector()
    {
        Hue = 0x851;
        WeaponAttributes.UseBestSkill = 1;
    }

    public override string DefaultName => "tailor's protector";
}

[Flippable(0xEC4, 0xEC5)]
[SerializationGenerator(0)]
public sealed partial class MastersKnife : SkinningKnife
{
    [Constructible]
    public MastersKnife() => Hue = 0x44;

    public override string DefaultName => "master's knife";
    public override int InitMinHits => 70;
    public override int InitMaxHits => 80;
}

[SerializationGenerator(0)]
public abstract partial class RewardEquipmentFinish : Item
{
    protected RewardEquipmentFinish(int graphic, int hue) : base(graphic) => Hue = hue;

    protected abstract string Prompt { get; }
    protected abstract string Refusal { get; }
    protected abstract bool TryApply(PlayerMobile owner, Item target);

    public override double DefaultWeight => 1.0;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile owner || !WeaponServiceTransfer.IsInBackpack(owner, this))
        {
            from.SendMessage("Keep the finish in your backpack.");
            return;
        }

        owner.SendMessage(Prompt);
        owner.BeginTarget(1, false, TargetFlags.None, (_, targeted) => Apply(owner, targeted as Item));
    }

    internal bool Apply(PlayerMobile owner, Item target)
    {
        if (!WeaponServiceTransfer.IsInBackpack(owner, this) ||
            !WeaponServiceTransfer.IsInBackpack(owner, target) || target.QuestItem || !TryApply(owner, target))
        {
            owner.SendMessage(Refusal);
            return false;
        }

        Consume();
        owner.PlaySound(0x247);
        return true;
    }
}

[SerializationGenerator(0)]
public sealed partial class StainOfDurability : RewardEquipmentFinish
{
    [Constructible]
    public StainOfDurability() : base(0xE24, 0x59A)
    {
    }

    public override string DefaultName => "stain of durability";
    protected override string Prompt => "Choose wooden equipment in your backpack.";
    protected override string Refusal => "Choose wearable wooden equipment below maximum durability.";

    protected override bool TryApply(PlayerMobile owner, Item target)
    {
        if (target is not IDurability { CanFortify: true, MaxHitPoints: > 0 and < 255 } equipment)
        {
            return false;
        }

        var resource = target switch
        {
            BaseWeapon weapon => weapon.Resource,
            BaseArmor armor   => armor.Resource,
            _                 => CraftResource.None
        };
        if (CraftResources.GetType(resource) != CraftResourceType.Wood)
        {
            return false;
        }

        var increase = Math.Min(10, 255 - equipment.MaxHitPoints);
        equipment.MaxHitPoints += increase;
        equipment.HitPoints += increase;
        owner.SendMessage($"The stain adds {increase} durability.");
        return true;
    }
}

[SerializationGenerator(0)]
public sealed partial class SharpeningBlade : RewardEquipmentFinish
{
    [Constructible]
    public SharpeningBlade() : base(0xF52, 0x48E)
    {
    }

    public override string DefaultName => "sharpening blade";
    protected override string Prompt => "Choose a weapon in your backpack.";
    protected override string Refusal => "Choose a weapon with less than 50% damage increase.";

    protected override bool TryApply(PlayerMobile owner, Item target)
    {
        if (target is not BaseWeapon { Cursed: false, Consecrated: false } weapon ||
            weapon.Attributes.WeaponDamage >= 50)
        {
            return false;
        }

        var amount = Math.Clamp((int)(owner.Skills.Blacksmith.Value / 25), 1, 4);
        amount = Math.Min(amount, 50 - weapon.Attributes.WeaponDamage);
        weapon.Attributes.WeaponDamage += amount;
        owner.SendMessage($"The blade adds {amount}% weapon damage.");
        return true;
    }
}
