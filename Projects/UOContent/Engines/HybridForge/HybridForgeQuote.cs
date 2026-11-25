using System;
using Server.Engines.WeaponLevelingServices;
using Server.Items;

namespace Server.Engines.HybridForge;

internal sealed class HybridForgeQuote(HybridForgeRecipe recipe, Item source, Item donor, AncientWyrmScale scale)
{
    private static readonly AosAttribute[] _itemAttributes = Enum.GetValues<AosAttribute>();
    private static readonly AosArmorAttribute[] _armorAttributes = Enum.GetValues<AosArmorAttribute>();

    internal HybridForgeRecipe Recipe { get; } = recipe;
    internal Item Source { get; } = source;
    internal Item Donor { get; } = donor;
    internal AncientWyrmScale Scale { get; } = scale;

    private readonly WeaponServiceTransfer.PreviewStamp _weapon = source is BaseWeapon weapon ? new(weapon, donor) : null;
    private readonly WeaponServiceTransfer.PreviewStamp _donorWeapon = donor is BaseWeapon weapon ? new(weapon, null) : null;
    private readonly WeaponFrame _weaponFrame = source is Halberd halberd ? WeaponFrame.Read(halberd) : default;
    private readonly ArmorFrame _armorFrame = source is BaseArmor armor ? ArmorFrame.Read(armor) : default;
    private readonly int[] _attributes = source is BaseArmor armor
        ? Array.ConvertAll(_itemAttributes, attr => armor.Attributes[attr])
        : null;
    private readonly int[] _armor = source is BaseArmor armor
        ? Array.ConvertAll(_armorAttributes, attr => armor.ArmorAttributes[attr])
        : null;
    private readonly (SkillName, double)[] _skills = source is BaseArmor armor ? ReadSkills(armor.SkillBonuses) : null;
    private readonly StackFrame _donorStack = StackFrame.Read(donor);
    private readonly StackFrame _scaleStack = StackFrame.Read(scale);

    internal bool Matches()
    {
        if (_donorStack != StackFrame.Read(Donor) || _scaleStack != StackFrame.Read(Scale))
        {
            return false;
        }

        if (Source is Halberd weapon)
        {
            return _weaponFrame == WeaponFrame.Read(weapon) && _weapon.Matches(weapon, Donor) &&
                   _donorWeapon.Matches((BaseWeapon)Donor, null);
        }

        var armor = (BaseArmor)Source;
        return _armorFrame == ArmorFrame.Read(armor) &&
               _attributes.AsSpan().SequenceEqual(Array.ConvertAll(_itemAttributes, attr => armor.Attributes[attr])) &&
               _armor.AsSpan().SequenceEqual(Array.ConvertAll(_armorAttributes, attr => armor.ArmorAttributes[attr])) &&
               _skills.AsSpan().SequenceEqual(ReadSkills(armor.SkillBonuses));
    }

    private static (SkillName, double)[] ReadSkills(AosSkillBonuses skillBonuses)
    {
        var result = new (SkillName, double)[5];
        for (var i = 0; i < result.Length; i++)
        {
            skillBonuses.GetValues(i, out var skill, out var bonus);
            result[i] = (skill, bonus);
        }

        return result;
    }

    private readonly record struct StackFrame(int Amount, int Hue, string Name)
    {
        internal static StackFrame Read(Item item) => new(item.Amount, item.Hue, item.Name);
    }

    private readonly record struct WeaponFrame(
        double Weight, int Strength, int Dexterity, int Intelligence, int MinDamage, int MaxDamage, float Speed,
        SkillName Skill, WeaponType Type, WeaponAnimation Animation, int HitSound, int MissSound, int Range,
        int UsesRemaining, bool ShowUsesRemaining
    )
    {
        internal static WeaponFrame Read(Halberd weapon) =>
            new(weapon.Weight, weapon.StrRequirement, weapon.DexRequirement, weapon.IntRequirement, weapon.MinDamage,
                weapon.MaxDamage, weapon.Speed, weapon.Skill, weapon.Type, weapon.Animation, weapon.HitSound,
                weapon.MissSound, weapon.MaxRange, weapon.UsesRemaining, weapon.ShowUsesRemaining);
    }

    private readonly record struct ArmorFrame(
        int ItemId, int Hue, string Name, double Weight, CraftResource Resource, ArmorQuality Quality,
        string Crafter, ArmorDurabilityLevel Durability, ArmorProtectionLevel Protection, bool Identified,
        int ArmorRating, int Hits, int MaxHits, int StrBonus, int DexBonus, int IntBonus, int Strength,
        int Dexterity, int Intelligence, ArmorMeditationAllowance Meditation, int Physical, int Fire, int Cold, int Poison, int Energy
    )
    {
        internal static ArmorFrame Read(BaseArmor armor) =>
            new(armor.ItemID, armor.Hue, armor.Name, armor.Weight, armor.Resource, armor.Quality, armor.Crafter,
                armor.Durability, armor.ProtectionLevel, armor.Identified, armor.BaseArmorRating, armor.HitPoints,
                armor.MaxHitPoints, armor.StrBonus, armor.DexBonus, armor.IntBonus, armor.StrRequirement,
                armor.DexRequirement, armor.IntRequirement, armor.MeditationAllowance, armor.PhysicalResistance, armor.FireResistance,
                armor.ColdResistance, armor.PoisonResistance, armor.EnergyResistance);
    }
}
