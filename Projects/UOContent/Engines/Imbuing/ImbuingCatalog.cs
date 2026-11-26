using System;
using System.Collections.Generic;
using Server.Items;

namespace Server.Engines.Imbuing;

public enum ImbuingCategory
{
    Combat,
    Caster,
    StatsAndRegen,
    WeaponHits,
    Resistances,
    Defenses
}

[Flags]
public enum ImbuingItemMask
{
    None = 0,
    MeleeWeapon = 1 << 0,
    RangedWeapon = 1 << 1,
    Armor = 1 << 2,
    Shield = 1 << 3,
    Clothing = 1 << 4,
    Jewelry = 1 << 5,
    Spellbook = 1 << 6,

    AllWeapons = MeleeWeapon | RangedWeapon,
    ArmorAndClothing = Armor | Clothing,
    AllArmor = Armor | Shield | Clothing
}

public enum ImbuingIngredientTier
{
    MagicalResidue,
    EnchantedEssence,
    RelicFragment
}

public enum ImbuingModKind
{
    AosAttribute,
    AosWeaponAttribute,
    AosArmorAttribute,
    Resistance,
    RangedBalanced,
    RangedVelocity
}

public readonly record struct ImbuingCost(Type PrimaryType, int PrimaryAmount, Type GemType, int GemAmount);

public sealed class ImbuingMod
{
    public ImbuingMod(
        int id,
        string name,
        ImbuingCategory category,
        ImbuingItemMask allowedItems,
        ImbuingModKind kind,
        int minValue,
        int maxValue,
        int step,
        int weight,
        bool isPercentage,
        ImbuingIngredientTier primaryTier,
        Type secondaryGemType,
        AosAttribute aosAttr = 0,
        AosWeaponAttribute weaponAttr = 0,
        AosArmorAttribute armorAttr = 0,
        ResistanceType resistType = ResistanceType.Physical,
        int jewelryMaxValue = 0
    )
    {
        Id = id;
        Name = name;
        Category = category;
        AllowedItems = allowedItems;
        Kind = kind;
        MinValue = minValue;
        MaxValue = maxValue;
        Step = step;
        Weight = weight;
        IsPercentage = isPercentage;
        PrimaryTier = primaryTier;
        SecondaryGemType = secondaryGemType;
        AosAttr = aosAttr;
        WeaponAttr = weaponAttr;
        ArmorAttr = armorAttr;
        ResistType = resistType;
        JewelryMaxValue = jewelryMaxValue;
    }

    public int Id { get; }
    public string Name { get; }
    public ImbuingCategory Category { get; }
    public ImbuingItemMask AllowedItems { get; }
    public ImbuingModKind Kind { get; }
    public int MinValue { get; }
    public int MaxValue { get; }
    public int Step { get; }
    public int Weight { get; }
    public bool IsPercentage { get; }
    public ImbuingIngredientTier PrimaryTier { get; }
    public Type SecondaryGemType { get; }
    public AosAttribute AosAttr { get; }
    public AosWeaponAttribute WeaponAttr { get; }
    public AosArmorAttribute ArmorAttr { get; }
    public ResistanceType ResistType { get; }
    public int JewelryMaxValue { get; }

    public bool IsBoolean => MinValue == 1 && MaxValue == 1;

    public int GetMaxValue(Item item) =>
        JewelryMaxValue > 0 && item is BaseJewel ? JewelryMaxValue : MaxValue;

    public bool IsEligibleFor(Item item)
    {
        var mask = ImbuingCatalog.GetItemMask(item);
        if (mask == ImbuingItemMask.None || (AllowedItems & mask) == 0)
        {
            return false;
        }

        if (Kind == ImbuingModKind.RangedBalanced)
        {
            return item is BaseRanged and not BaseThrown;
        }

        if (Kind == ImbuingModKind.AosArmorAttribute && ArmorAttr == AosArmorAttribute.MageArmor)
        {
            return item is BaseArmor armor && armor.MeditationAllowance != ArmorMeditationAllowance.All;
        }

        return true;
    }

    public bool ConsumesPropertySlot(Item item)
    {
        // Armor and clothing resistances are innate defense pools (crafted exceptional pieces
        // distribute points across up to 5 elements) and do not consume the 5-property slot cap.
        if (Kind == ImbuingModKind.Resistance && item is BaseArmor or BaseClothing)
        {
            return false;
        }

        return true;
    }

    public int ComputeWeight(Item item, int value)
    {
        if (value <= 0)
        {
            return 0;
        }

        if (IsBoolean)
        {
            return Weight;
        }

        var maxVal = Math.Max(1, GetMaxValue(item));
        return Math.Max(1, (int)Math.Round((double)value / maxVal * Weight));
    }

    public string FormatValue(int value)
    {
        if (value <= 0)
        {
            return "None";
        }

        if (IsBoolean)
        {
            return "Active";
        }

        return IsPercentage ? $"{value}%" : $"{value}";
    }
}

public static class ImbuingCatalog
{
    public const int ExceptionalCraftedMaxWeight = 550;
    public const int StandardCraftedMaxWeight = 500;
    public const int LootedMaxWeight = 450;
    public const int MaxProperties = 5;
    public const int MaxImbueUses = 20;
    public const int ExceptionalArmorResistAllowance = 15;

    private static readonly ImbuingMod[] _mods =
    [
        // Combat
        new(1, "Damage Increase", ImbuingCategory.Combat, ImbuingItemMask.AllWeapons | ImbuingItemMask.Jewelry,
            ImbuingModKind.AosAttribute, 1, 50, 1, 100, true, ImbuingIngredientTier.EnchantedEssence, typeof(Ruby),
            aosAttr: AosAttribute.WeaponDamage, jewelryMaxValue: 25),
        new(2, "Swing Speed Increase", ImbuingCategory.Combat, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosAttribute, 5, 30, 5, 110, true, ImbuingIngredientTier.RelicFragment, typeof(Tourmaline),
            aosAttr: AosAttribute.WeaponSpeed),
        new(3, "Hit Chance Increase", ImbuingCategory.Combat,
            ImbuingItemMask.AllWeapons | ImbuingItemMask.Shield | ImbuingItemMask.Jewelry,
            ImbuingModKind.AosAttribute, 1, 15, 1, 130, true, ImbuingIngredientTier.EnchantedEssence, typeof(Diamond),
            aosAttr: AosAttribute.AttackChance),
        new(4, "Defense Chance Increase", ImbuingCategory.Combat,
            ImbuingItemMask.AllWeapons | ImbuingItemMask.Shield | ImbuingItemMask.Jewelry,
            ImbuingModKind.AosAttribute, 1, 15, 1, 130, true, ImbuingIngredientTier.EnchantedEssence, typeof(Tourmaline),
            aosAttr: AosAttribute.DefendChance),
        new(5, "Reflect Physical Damage", ImbuingCategory.Combat, ImbuingItemMask.AllArmor,
            ImbuingModKind.AosAttribute, 1, 15, 1, 100, true, ImbuingIngredientTier.MagicalResidue, typeof(Citrine),
            aosAttr: AosAttribute.ReflectPhysical),
        new(6, "Balanced", ImbuingCategory.Combat, ImbuingItemMask.RangedWeapon,
            ImbuingModKind.RangedBalanced, 1, 1, 1, 150, false, ImbuingIngredientTier.RelicFragment, typeof(Diamond)),
        new(7, "Velocity", ImbuingCategory.Combat, ImbuingItemMask.RangedWeapon,
            ImbuingModKind.RangedVelocity, 10, 50, 2, 130, true, ImbuingIngredientTier.EnchantedEssence, typeof(Tourmaline)),
        new(8, "Battle Lust", ImbuingCategory.Combat, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 1, 1, 1, 130, false, ImbuingIngredientTier.RelicFragment, typeof(Ruby),
            weaponAttr: AosWeaponAttribute.BattleLust),
        new(9, "Blood Drinker", ImbuingCategory.Combat, ImbuingItemMask.MeleeWeapon,
            ImbuingModKind.AosWeaponAttribute, 1, 1, 1, 125, false, ImbuingIngredientTier.RelicFragment, typeof(Ruby),
            weaponAttr: AosWeaponAttribute.BloodDrinker),
        new(10, "Bane", ImbuingCategory.Combat, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 1, 1, 1, 140, false, ImbuingIngredientTier.RelicFragment, typeof(Amethyst),
            weaponAttr: AosWeaponAttribute.Bane),

        // Caster
        new(11, "Spell Damage Increase", ImbuingCategory.Caster, ImbuingItemMask.Jewelry | ImbuingItemMask.Spellbook,
            ImbuingModKind.AosAttribute, 1, 12, 1, 100, true, ImbuingIngredientTier.EnchantedEssence, typeof(Emerald),
            aosAttr: AosAttribute.SpellDamage),
        new(12, "Faster Casting", ImbuingCategory.Caster,
            ImbuingItemMask.AllWeapons | ImbuingItemMask.Shield | ImbuingItemMask.Jewelry | ImbuingItemMask.Spellbook,
            ImbuingModKind.AosAttribute, 1, 1, 1, 140, false, ImbuingIngredientTier.RelicFragment, typeof(Sapphire),
            aosAttr: AosAttribute.CastSpeed),
        new(13, "Faster Cast Recovery", ImbuingCategory.Caster, ImbuingItemMask.Jewelry | ImbuingItemMask.Spellbook,
            ImbuingModKind.AosAttribute, 1, 3, 1, 120, false, ImbuingIngredientTier.EnchantedEssence, typeof(StarSapphire),
            aosAttr: AosAttribute.CastRecovery),
        new(14, "Lower Mana Cost", ImbuingCategory.Caster,
            ImbuingItemMask.ArmorAndClothing | ImbuingItemMask.Jewelry | ImbuingItemMask.Spellbook,
            ImbuingModKind.AosAttribute, 1, 8, 1, 110, true, ImbuingIngredientTier.EnchantedEssence, typeof(Sapphire),
            aosAttr: AosAttribute.LowerManaCost),
        new(15, "Lower Reagent Cost", ImbuingCategory.Caster,
            ImbuingItemMask.ArmorAndClothing | ImbuingItemMask.Jewelry | ImbuingItemMask.Spellbook,
            ImbuingModKind.AosAttribute, 1, 20, 1, 100, true, ImbuingIngredientTier.MagicalResidue, typeof(Amber),
            aosAttr: AosAttribute.LowerRegCost),
        new(16, "Spell Channeling", ImbuingCategory.Caster, ImbuingItemMask.AllWeapons | ImbuingItemMask.Shield,
            ImbuingModKind.AosAttribute, 1, 1, 1, 100, false, ImbuingIngredientTier.MagicalResidue, typeof(Diamond),
            aosAttr: AosAttribute.SpellChanneling),
        new(17, "Mage Armor", ImbuingCategory.Caster, ImbuingItemMask.Armor,
            ImbuingModKind.AosArmorAttribute, 1, 1, 1, 100, false, ImbuingIngredientTier.EnchantedEssence, typeof(Diamond),
            armorAttr: AosArmorAttribute.MageArmor),
        new(18, "Casting Focus", ImbuingCategory.Caster,
            ImbuingItemMask.ArmorAndClothing | ImbuingItemMask.Jewelry | ImbuingItemMask.Spellbook,
            ImbuingModKind.AosAttribute, 1, 6, 1, 120, true, ImbuingIngredientTier.EnchantedEssence, typeof(Amethyst),
            aosAttr: AosAttribute.CastingFocus, jewelryMaxValue: 4),
        new(19, "Mana Phase", ImbuingCategory.Caster, ImbuingItemMask.ArmorAndClothing,
            ImbuingModKind.AosArmorAttribute, 1, 1, 1, 140, false, ImbuingIngredientTier.RelicFragment, typeof(StarSapphire),
            armorAttr: AosArmorAttribute.ManaPhase),
        new(20, "Soul Charge", ImbuingCategory.Caster, ImbuingItemMask.AllArmor,
            ImbuingModKind.AosArmorAttribute, 5, 25, 5, 125, true, ImbuingIngredientTier.RelicFragment, typeof(StarSapphire),
            armorAttr: AosArmorAttribute.SoulCharge),

        // Stats, Pools, Regen & Utility
        new(21, "Hit Point Regeneration", ImbuingCategory.StatsAndRegen,
            ImbuingItemMask.ArmorAndClothing | ImbuingItemMask.Spellbook,
            ImbuingModKind.AosAttribute, 1, 2, 1, 100, false, ImbuingIngredientTier.EnchantedEssence, typeof(Ruby),
            aosAttr: AosAttribute.RegenHits),
        new(22, "Stamina Regeneration", ImbuingCategory.StatsAndRegen,
            ImbuingItemMask.ArmorAndClothing | ImbuingItemMask.Spellbook,
            ImbuingModKind.AosAttribute, 1, 3, 1, 100, false, ImbuingIngredientTier.EnchantedEssence, typeof(Diamond),
            aosAttr: AosAttribute.RegenStam),
        new(23, "Mana Regeneration", ImbuingCategory.StatsAndRegen,
            ImbuingItemMask.ArmorAndClothing | ImbuingItemMask.Spellbook,
            ImbuingModKind.AosAttribute, 1, 2, 1, 100, false, ImbuingIngredientTier.EnchantedEssence, typeof(Sapphire),
            aosAttr: AosAttribute.RegenMana),
        new(24, "Strength Bonus", ImbuingCategory.StatsAndRegen, ImbuingItemMask.Jewelry | ImbuingItemMask.Clothing,
            ImbuingModKind.AosAttribute, 1, 8, 1, 110, false, ImbuingIngredientTier.EnchantedEssence, typeof(Ruby),
            aosAttr: AosAttribute.BonusStr),
        new(25, "Dexterity Bonus", ImbuingCategory.StatsAndRegen, ImbuingItemMask.Jewelry | ImbuingItemMask.Clothing,
            ImbuingModKind.AosAttribute, 1, 8, 1, 110, false, ImbuingIngredientTier.EnchantedEssence, typeof(Emerald),
            aosAttr: AosAttribute.BonusDex),
        new(26, "Intelligence Bonus", ImbuingCategory.StatsAndRegen,
            ImbuingItemMask.Jewelry | ImbuingItemMask.Clothing | ImbuingItemMask.Spellbook,
            ImbuingModKind.AosAttribute, 1, 8, 1, 110, false, ImbuingIngredientTier.EnchantedEssence, typeof(Sapphire),
            aosAttr: AosAttribute.BonusInt),
        new(27, "Hit Point Increase", ImbuingCategory.StatsAndRegen, ImbuingItemMask.ArmorAndClothing,
            ImbuingModKind.AosAttribute, 1, 5, 1, 110, false, ImbuingIngredientTier.EnchantedEssence, typeof(Ruby),
            aosAttr: AosAttribute.BonusHits),
        new(28, "Stamina Increase", ImbuingCategory.StatsAndRegen, ImbuingItemMask.ArmorAndClothing,
            ImbuingModKind.AosAttribute, 1, 8, 1, 110, false, ImbuingIngredientTier.EnchantedEssence, typeof(Diamond),
            aosAttr: AosAttribute.BonusStam),
        new(29, "Mana Increase", ImbuingCategory.StatsAndRegen,
            ImbuingItemMask.ArmorAndClothing | ImbuingItemMask.Spellbook,
            ImbuingModKind.AosAttribute, 1, 8, 1, 110, false, ImbuingIngredientTier.EnchantedEssence, typeof(Sapphire),
            aosAttr: AosAttribute.BonusMana),
        new(30, "Enhance Potions", ImbuingCategory.StatsAndRegen, ImbuingItemMask.Jewelry,
            ImbuingModKind.AosAttribute, 5, 25, 5, 100, true, ImbuingIngredientTier.MagicalResidue, typeof(Citrine),
            aosAttr: AosAttribute.EnhancePotions),
        new(31, "Luck", ImbuingCategory.StatsAndRegen,
            ImbuingItemMask.AllWeapons | ImbuingItemMask.ArmorAndClothing | ImbuingItemMask.Jewelry,
            ImbuingModKind.AosAttribute, 5, 100, 5, 100, false, ImbuingIngredientTier.MagicalResidue, typeof(Citrine),
            aosAttr: AosAttribute.Luck),

        // Weapon Hits
        new(32, "Hit Life Leech", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 2, 50, 2, 110, true, ImbuingIngredientTier.EnchantedEssence, typeof(Ruby),
            weaponAttr: AosWeaponAttribute.HitLeechHits),
        new(33, "Hit Stamina Leech", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 2, 50, 2, 100, true, ImbuingIngredientTier.MagicalResidue, typeof(Diamond),
            weaponAttr: AosWeaponAttribute.HitLeechStam),
        new(34, "Hit Mana Leech", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 2, 50, 2, 110, true, ImbuingIngredientTier.EnchantedEssence, typeof(Sapphire),
            weaponAttr: AosWeaponAttribute.HitLeechMana),
        new(35, "Hit Lower Attack", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 2, 50, 2, 110, true, ImbuingIngredientTier.EnchantedEssence, typeof(Emerald),
            weaponAttr: AosWeaponAttribute.HitLowerAttack),
        new(36, "Hit Lower Defense", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 2, 50, 2, 130, true, ImbuingIngredientTier.RelicFragment, typeof(Tourmaline),
            weaponAttr: AosWeaponAttribute.HitLowerDefend),
        new(37, "Hit Magic Arrow", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 2, 50, 2, 120, true, ImbuingIngredientTier.RelicFragment, typeof(Amber),
            weaponAttr: AosWeaponAttribute.HitMagicArrow),
        new(38, "Hit Harm", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 2, 50, 2, 110, true, ImbuingIngredientTier.EnchantedEssence, typeof(Emerald),
            weaponAttr: AosWeaponAttribute.HitHarm),
        new(39, "Hit Fireball", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 2, 50, 2, 140, true, ImbuingIngredientTier.RelicFragment, typeof(Ruby),
            weaponAttr: AosWeaponAttribute.HitFireball),
        new(40, "Hit Lightning", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 2, 50, 2, 140, true, ImbuingIngredientTier.RelicFragment, typeof(Amethyst),
            weaponAttr: AosWeaponAttribute.HitLightning),
        new(41, "Hit Dispel", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 2, 50, 2, 100, true, ImbuingIngredientTier.MagicalResidue, typeof(Amber),
            weaponAttr: AosWeaponAttribute.HitDispel),
        new(42, "Hit Physical Area", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 2, 50, 2, 100, true, ImbuingIngredientTier.MagicalResidue, typeof(Diamond),
            weaponAttr: AosWeaponAttribute.HitPhysicalArea),
        new(43, "Hit Fire Area", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 2, 50, 2, 100, true, ImbuingIngredientTier.MagicalResidue, typeof(Ruby),
            weaponAttr: AosWeaponAttribute.HitFireArea),
        new(44, "Hit Cold Area", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 2, 50, 2, 100, true, ImbuingIngredientTier.MagicalResidue, typeof(Sapphire),
            weaponAttr: AosWeaponAttribute.HitColdArea),
        new(45, "Hit Poison Area", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 2, 50, 2, 100, true, ImbuingIngredientTier.MagicalResidue, typeof(Emerald),
            weaponAttr: AosWeaponAttribute.HitPoisonArea),
        new(46, "Hit Energy Area", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 2, 50, 2, 100, true, ImbuingIngredientTier.MagicalResidue, typeof(Amethyst),
            weaponAttr: AosWeaponAttribute.HitEnergyArea),
        new(47, "Splintering Weapon", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 5, 30, 5, 140, true, ImbuingIngredientTier.RelicFragment, typeof(Diamond),
            weaponAttr: AosWeaponAttribute.SplinteringWeapon),
        new(48, "Hit Curse", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 2, 50, 2, 130, true, ImbuingIngredientTier.EnchantedEssence, typeof(Amethyst),
            weaponAttr: AosWeaponAttribute.HitCurse),
        new(49, "Hit Fatigue", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 2, 50, 2, 115, true, ImbuingIngredientTier.EnchantedEssence, typeof(Citrine),
            weaponAttr: AosWeaponAttribute.HitFatigue),
        new(50, "Hit Mana Drain", ImbuingCategory.WeaponHits, ImbuingItemMask.AllWeapons,
            ImbuingModKind.AosWeaponAttribute, 2, 50, 2, 120, true, ImbuingIngredientTier.EnchantedEssence, typeof(Sapphire),
            weaponAttr: AosWeaponAttribute.HitManaDrain),

        // Resistances
        new(51, "Physical Resist", ImbuingCategory.Resistances,
            ImbuingItemMask.AllWeapons | ImbuingItemMask.AllArmor | ImbuingItemMask.Jewelry,
            ImbuingModKind.Resistance, 1, 15, 1, 100, true, ImbuingIngredientTier.MagicalResidue, typeof(Diamond),
            resistType: ResistanceType.Physical),
        new(52, "Fire Resist", ImbuingCategory.Resistances,
            ImbuingItemMask.AllWeapons | ImbuingItemMask.AllArmor | ImbuingItemMask.Jewelry,
            ImbuingModKind.Resistance, 1, 15, 1, 100, true, ImbuingIngredientTier.MagicalResidue, typeof(Ruby),
            resistType: ResistanceType.Fire),
        new(53, "Cold Resist", ImbuingCategory.Resistances,
            ImbuingItemMask.AllWeapons | ImbuingItemMask.AllArmor | ImbuingItemMask.Jewelry,
            ImbuingModKind.Resistance, 1, 15, 1, 100, true, ImbuingIngredientTier.MagicalResidue, typeof(Sapphire),
            resistType: ResistanceType.Cold),
        new(54, "Poison Resist", ImbuingCategory.Resistances,
            ImbuingItemMask.AllWeapons | ImbuingItemMask.AllArmor | ImbuingItemMask.Jewelry,
            ImbuingModKind.Resistance, 1, 15, 1, 100, true, ImbuingIngredientTier.MagicalResidue, typeof(Emerald),
            resistType: ResistanceType.Poison),
        new(55, "Energy Resist", ImbuingCategory.Resistances,
            ImbuingItemMask.AllWeapons | ImbuingItemMask.AllArmor | ImbuingItemMask.Jewelry,
            ImbuingModKind.Resistance, 1, 15, 1, 100, true, ImbuingIngredientTier.MagicalResidue, typeof(Amethyst),
            resistType: ResistanceType.Energy),

        // Defenses (SA/ToL Eaters, Reactive Paralyze, Resonances)
        new(56, "Damage Eater", ImbuingCategory.Defenses, ImbuingItemMask.AllArmor | ImbuingItemMask.Jewelry,
            ImbuingModKind.AosAttribute, 1, 6, 1, 140, true, ImbuingIngredientTier.RelicFragment, typeof(Diamond),
            aosAttr: AosAttribute.DamageEater, jewelryMaxValue: 4),
        new(57, "Kinetic Eater", ImbuingCategory.Defenses, ImbuingItemMask.AllArmor,
            ImbuingModKind.AosAttribute, 1, 15, 1, 115, true, ImbuingIngredientTier.EnchantedEssence, typeof(Diamond),
            aosAttr: AosAttribute.KineticEater),
        new(58, "Fire Eater", ImbuingCategory.Defenses, ImbuingItemMask.AllArmor,
            ImbuingModKind.AosAttribute, 1, 15, 1, 115, true, ImbuingIngredientTier.EnchantedEssence, typeof(Ruby),
            aosAttr: AosAttribute.FireEater),
        new(59, "Cold Eater", ImbuingCategory.Defenses, ImbuingItemMask.AllArmor,
            ImbuingModKind.AosAttribute, 1, 15, 1, 115, true, ImbuingIngredientTier.EnchantedEssence, typeof(Sapphire),
            aosAttr: AosAttribute.ColdEater),
        new(60, "Poison Eater", ImbuingCategory.Defenses, ImbuingItemMask.AllArmor,
            ImbuingModKind.AosAttribute, 1, 15, 1, 115, true, ImbuingIngredientTier.EnchantedEssence, typeof(Emerald),
            aosAttr: AosAttribute.PoisonEater),
        new(61, "Energy Eater", ImbuingCategory.Defenses, ImbuingItemMask.AllArmor,
            ImbuingModKind.AosAttribute, 1, 15, 1, 115, true, ImbuingIngredientTier.EnchantedEssence, typeof(Amethyst),
            aosAttr: AosAttribute.EnergyEater),
        new(62, "Reactive Paralyze", ImbuingCategory.Defenses, ImbuingItemMask.Armor | ImbuingItemMask.Shield,
            ImbuingModKind.AosArmorAttribute, 1, 1, 1, 135, false, ImbuingIngredientTier.RelicFragment, typeof(Tourmaline),
            armorAttr: AosArmorAttribute.ReactiveParalyze),
        new(63, "Kinetic Resonance", ImbuingCategory.Defenses, ImbuingItemMask.ArmorAndClothing,
            ImbuingModKind.AosArmorAttribute, 5, 20, 5, 110, true, ImbuingIngredientTier.EnchantedEssence, typeof(Amber),
            armorAttr: AosArmorAttribute.KineticResonance),
        new(64, "Fire Resonance", ImbuingCategory.Defenses, ImbuingItemMask.ArmorAndClothing,
            ImbuingModKind.AosArmorAttribute, 5, 20, 5, 110, true, ImbuingIngredientTier.EnchantedEssence, typeof(Ruby),
            armorAttr: AosArmorAttribute.FireResonance),
        new(65, "Cold Resonance", ImbuingCategory.Defenses, ImbuingItemMask.ArmorAndClothing,
            ImbuingModKind.AosArmorAttribute, 5, 20, 5, 110, true, ImbuingIngredientTier.EnchantedEssence, typeof(Sapphire),
            armorAttr: AosArmorAttribute.ColdResonance),
        new(66, "Poison Resonance", ImbuingCategory.Defenses, ImbuingItemMask.ArmorAndClothing,
            ImbuingModKind.AosArmorAttribute, 5, 20, 5, 110, true, ImbuingIngredientTier.EnchantedEssence, typeof(Emerald),
            armorAttr: AosArmorAttribute.PoisonResonance),
        new(67, "Energy Resonance", ImbuingCategory.Defenses, ImbuingItemMask.ArmorAndClothing,
            ImbuingModKind.AosArmorAttribute, 5, 20, 5, 110, true, ImbuingIngredientTier.EnchantedEssence, typeof(Amethyst),
            armorAttr: AosArmorAttribute.EnergyResonance)
    ];

    private static readonly Dictionary<int, ImbuingMod> _modsById = BuildIdLookup();

    private static Dictionary<int, ImbuingMod> BuildIdLookup()
    {
        var map = new Dictionary<int, ImbuingMod>(_mods.Length);
        for (var i = 0; i < _mods.Length; i++)
        {
            map[_mods[i].Id] = _mods[i];
        }

        return map;
    }

    public static ReadOnlySpan<ImbuingMod> AllMods => _mods;

    public static ImbuingMod GetMod(int modId) =>
        _modsById.TryGetValue(modId, out var mod) ? mod : null;

    public static ImbuingMod FindMod(AosAttribute attr)
    {
        for (var i = 0; i < _mods.Length; i++)
        {
            if (_mods[i].Kind == ImbuingModKind.AosAttribute && _mods[i].AosAttr == attr)
            {
                return _mods[i];
            }
        }

        return null;
    }

    public static ImbuingMod FindMod(AosWeaponAttribute attr)
    {
        for (var i = 0; i < _mods.Length; i++)
        {
            if (_mods[i].Kind == ImbuingModKind.AosWeaponAttribute && _mods[i].WeaponAttr == attr)
            {
                return _mods[i];
            }
        }

        return null;
    }

    public static ImbuingMod FindMod(AosArmorAttribute attr)
    {
        for (var i = 0; i < _mods.Length; i++)
        {
            if (_mods[i].Kind == ImbuingModKind.AosArmorAttribute && _mods[i].ArmorAttr == attr)
            {
                return _mods[i];
            }
        }

        return null;
    }

    public static ImbuingMod FindResistanceMod(ResistanceType resist)
    {
        for (var i = 0; i < _mods.Length; i++)
        {
            if (_mods[i].Kind == ImbuingModKind.Resistance && _mods[i].ResistType == resist)
            {
                return _mods[i];
            }
        }

        return null;
    }

    public static ImbuingMod FindRangedBalancedMod() => GetMod(6);

    public static ImbuingMod FindRangedVelocityMod() => GetMod(7);

    public static ImbuingMod FindModByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        for (var i = 0; i < _mods.Length; i++)
        {
            if (string.Equals(_mods[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return _mods[i];
            }
        }

        return null;
    }

    public static ImbuingItemMask GetItemMask(Item item) => item switch
    {
        Fists => ImbuingItemMask.None,
        BaseRanged => ImbuingItemMask.RangedWeapon,
        BaseWeapon => ImbuingItemMask.MeleeWeapon,
        BaseShield => ImbuingItemMask.Shield,
        BaseArmor => ImbuingItemMask.Armor,
        BaseHat => ImbuingItemMask.Clothing,
        BaseJewel => ImbuingItemMask.Jewelry,
        Spellbook => ImbuingItemMask.Spellbook,
        _ => ImbuingItemMask.None
    };

    public static bool IsPlayerCrafted(Item item) => item switch
    {
        null => false,
        BaseWeapon w => w.PlayerConstructed || !string.IsNullOrEmpty(w.Crafter),
        BaseArmor a => a.PlayerConstructed || !string.IsNullOrEmpty(a.Crafter),
        BaseClothing c => c.PlayerConstructed || !string.IsNullOrEmpty(c.Crafter),
        Spellbook s => s.PlayerConstructed || !string.IsNullOrEmpty(s.Crafter),
        BaseJewel j => j.PlayerConstructed,
        _ => item.PlayerConstructed
    };

    public static bool IsExceptional(Item item) => item switch
    {
        BaseWeapon w => w.Quality == WeaponQuality.Exceptional,
        BaseArmor a => a.Quality == ArmorQuality.Exceptional,
        BaseClothing c => c.Quality == ClothingQuality.Exceptional,
        Spellbook s => s.Quality == BookQuality.Exceptional,
        _ => false
    };

    public static int GetMaxWeight(Item item)
    {
        if (item == null)
        {
            return 0;
        }

        if (IsPlayerCrafted(item))
        {
            // BaseJewel does not serialize Quality, so crafted jewelry receives the full 550 budget.
            if (item is BaseJewel || IsExceptional(item))
            {
                return ExceptionalCraftedMaxWeight;
            }

            return StandardCraftedMaxWeight;
        }

        return LootedMaxWeight;
    }

    public static string GetBudgetTierLabel(Item item)
    {
        if (IsPlayerCrafted(item))
        {
            return item is BaseJewel || IsExceptional(item) ? "Exceptional Crafted" : "Crafted";
        }

        return "Looted";
    }

    public static string GetCategoryName(ImbuingCategory category) => category switch
    {
        ImbuingCategory.Combat => "Combat",
        ImbuingCategory.Caster => "Caster",
        ImbuingCategory.StatsAndRegen => "Stats",
        ImbuingCategory.WeaponHits => "Weapon Hits",
        ImbuingCategory.Resistances => "Resists",
        ImbuingCategory.Defenses => "Defenses",
        _ => category.ToString()
    };

    public static List<ImbuingMod> GetEligibleMods(Item item, ImbuingCategory? categoryFilter = null)
    {
        var result = new List<ImbuingMod>();
        if (item == null || item.Deleted)
        {
            return result;
        }

        for (var i = 0; i < _mods.Length; i++)
        {
            var mod = _mods[i];
            if (categoryFilter.HasValue && mod.Category != categoryFilter.Value)
            {
                continue;
            }

            if (mod.IsEligibleFor(item))
            {
                result.Add(mod);
            }
        }

        return result;
    }

    public static int GetCurrentValue(Item item, ImbuingMod mod)
    {
        if (item == null || mod == null)
        {
            return 0;
        }

        return mod.Kind switch
        {
            ImbuingModKind.AosAttribute => GetAosAttributeValue(item, mod.AosAttr),
            ImbuingModKind.AosWeaponAttribute => item is BaseWeapon w ? w.WeaponAttributes[mod.WeaponAttr] : 0,
            ImbuingModKind.AosArmorAttribute => item switch
            {
                BaseArmor a => a.ArmorAttributes[mod.ArmorAttr],
                BaseClothing c => c.ClothingAttributes[mod.ArmorAttr],
                _ => 0
            },
            ImbuingModKind.Resistance => GetResistanceValue(item, mod.ResistType),
            ImbuingModKind.RangedBalanced => item is BaseRanged { Balanced: true } ? 1 : 0,
            ImbuingModKind.RangedVelocity => item is BaseRanged r ? r.Velocity : 0,
            _ => 0
        };
    }

    public static void ApplyModValue(Item item, ImbuingMod mod, int value)
    {
        if (item == null || item.Deleted || mod == null)
        {
            return;
        }

        switch (mod.Kind)
        {
            case ImbuingModKind.AosAttribute:
            {
                if (item is IAosItem aos)
                {
                    if (mod.AosAttr == AosAttribute.SpellChanneling)
                    {
                        if (value > 0 && aos.Attributes.SpellChanneling == 0)
                        {
                            aos.Attributes.SpellChanneling = 1;
                            aos.Attributes.CastSpeed -= 1;
                        }
                    }
                    else if (mod.AosAttr == AosAttribute.CastSpeed && aos.Attributes.SpellChanneling > 0)
                    {
                        // Spell Channeling carries a -1 CastSpeed penalty; Faster Casting offsets it to 0, never +1.
                        aos.Attributes.CastSpeed = Math.Min(0, value - 1);
                    }
                    else
                    {
                        aos.Attributes[mod.AosAttr] = value;
                    }
                }

                break;
            }
            case ImbuingModKind.AosWeaponAttribute:
            {
                if (item is BaseWeapon weapon)
                {
                    weapon.WeaponAttributes[mod.WeaponAttr] = value;
                }

                break;
            }
            case ImbuingModKind.AosArmorAttribute:
            {
                if (item is BaseArmor armor)
                {
                    armor.ArmorAttributes[mod.ArmorAttr] = value;
                }
                else if (item is BaseClothing clothing)
                {
                    clothing.ClothingAttributes[mod.ArmorAttr] = value;
                }

                break;
            }
            case ImbuingModKind.Resistance:
            {
                SetResistanceValue(item, mod.ResistType, value);
                break;
            }
            case ImbuingModKind.RangedBalanced:
            {
                if (item is BaseRanged ranged and not BaseThrown)
                {
                    ranged.Balanced = value != 0;
                }

                break;
            }
            case ImbuingModKind.RangedVelocity:
            {
                if (item is BaseRanged ranged)
                {
                    ranged.Velocity = value;
                }

                break;
            }
        }

        item.InvalidateProperties();
    }

    public static int ComputeProjectedWeight(Item item, ImbuingMod mod, int targetIntensity)
    {
        if (item == null || mod == null)
        {
            return 0;
        }

        var currentTotal = ComputeTotalWeight(item);
        var currentVal = GetCurrentValue(item, mod);

        if (mod.Kind == ImbuingModKind.Resistance && item is BaseArmor armor && IsExceptionalCraftedArmor(armor))
        {
            var currentResistPts = GetTotalArmorResistBonusPoints(armor);
            var projectedResistPts = currentResistPts - Math.Max(0, currentVal) + Math.Max(0, targetIntensity);
            var oldResistWeight = ComputeExceptionalArmorResistWeight(currentResistPts);
            var newResistWeight = ComputeExceptionalArmorResistWeight(projectedResistPts);
            return currentTotal - oldResistWeight + newResistWeight;
        }

        var oldModWeight = mod.ComputeWeight(item, currentVal);
        var newModWeight = mod.ComputeWeight(item, targetIntensity);
        return currentTotal - oldModWeight + newModWeight;
    }

    public static int ComputeTotalWeight(Item item)
    {
        if (item == null || item.Deleted || (GetItemMask(item) == ImbuingItemMask.None && item is not BaseClothing))
        {
            return 0;
        }

        var total = 0;
        var exceptionalCraftedArmor = item is BaseArmor exArmor && IsExceptionalCraftedArmor(exArmor) ? exArmor : null;

        for (var i = 0; i < _mods.Length; i++)
        {
            var mod = _mods[i];
            if (exceptionalCraftedArmor != null && mod.Kind == ImbuingModKind.Resistance)
            {
                continue;
            }

            var value = GetCurrentValue(item, mod);
            if (value > 0)
            {
                total += mod.ComputeWeight(item, value);
            }
        }

        if (exceptionalCraftedArmor != null)
        {
            total += ComputeExceptionalArmorResistWeight(GetTotalArmorResistBonusPoints(exceptionalCraftedArmor));
        }

        // Non-imbuable properties that still contribute to item weight for unraveling and budget accounting.
        if (item is IAosItem aos && aos.Attributes.NightSight > 0)
        {
            total += 50;
        }

        if (item is BaseWeapon weapon)
        {
            var wAttrs = weapon.WeaponAttributes;
            if (wAttrs.LowerStatReq > 0)
            {
                total += Math.Max(1, wAttrs.LowerStatReq);
            }

            if (wAttrs.SelfRepair > 0)
            {
                total += wAttrs.SelfRepair * 20;
            }

            if (wAttrs.UseBestSkill > 0)
            {
                total += 140;
            }

            if (wAttrs.MageWeapon > 0)
            {
                total += Math.Max(1, (int)Math.Round(wAttrs.MageWeapon / 10.0 * 100.0));
            }

            if (wAttrs.DurabilityBonus > 0)
            {
                total += Math.Max(1, wAttrs.DurabilityBonus / 2);
            }

            if (weapon.Slayer != SlayerName.None)
            {
                total += 120;
            }

            if (weapon.Slayer2 != SlayerName.None)
            {
                total += 120;
            }

            total += ComputeSkillBonusWeight(weapon.SkillBonuses);
        }
        else if (item is BaseArmor armor)
        {
            var aAttrs = armor.ArmorAttributes;
            if (aAttrs.LowerStatReq > 0)
            {
                total += Math.Max(1, aAttrs.LowerStatReq);
            }

            if (aAttrs.SelfRepair > 0)
            {
                total += aAttrs.SelfRepair * 20;
            }

            if (aAttrs.DurabilityBonus > 0)
            {
                total += Math.Max(1, aAttrs.DurabilityBonus / 2);
            }

            total += ComputeSkillBonusWeight(armor.SkillBonuses);
        }
        else if (item is BaseClothing clothing)
        {
            var cAttrs = clothing.ClothingAttributes;
            if (cAttrs.LowerStatReq > 0)
            {
                total += Math.Max(1, cAttrs.LowerStatReq);
            }

            if (cAttrs.SelfRepair > 0)
            {
                total += cAttrs.SelfRepair * 20;
            }

            if (cAttrs.DurabilityBonus > 0)
            {
                total += Math.Max(1, cAttrs.DurabilityBonus / 2);
            }

            total += ComputeSkillBonusWeight(clothing.SkillBonuses);
        }
        else if (item is BaseJewel jewel)
        {
            total += ComputeSkillBonusWeight(jewel.SkillBonuses);
        }
        else if (item is Spellbook spellbook)
        {
            if (spellbook.Slayer != SlayerName.None)
            {
                total += 120;
            }

            if (spellbook.Slayer2 != SlayerName.None)
            {
                total += 120;
            }

            total += ComputeSkillBonusWeight(spellbook.SkillBonuses);
        }

        return total;
    }

    public static int CountProperties(Item item)
    {
        if (item == null || item.Deleted || (GetItemMask(item) == ImbuingItemMask.None && item is not BaseClothing))
        {
            return 0;
        }

        var count = 0;

        for (var i = 0; i < _mods.Length; i++)
        {
            var mod = _mods[i];
            if (!mod.ConsumesPropertySlot(item))
            {
                continue;
            }

            if (GetCurrentValue(item, mod) > 0)
            {
                count++;
            }
        }

        if (item is IAosItem aos && aos.Attributes.NightSight > 0)
        {
            count++;
        }

        if (item is BaseWeapon weapon)
        {
            var wAttrs = weapon.WeaponAttributes;
            if (wAttrs.LowerStatReq > 0)
            {
                count++;
            }

            if (wAttrs.SelfRepair > 0)
            {
                count++;
            }

            if (wAttrs.UseBestSkill > 0)
            {
                count++;
            }

            if (wAttrs.MageWeapon > 0)
            {
                count++;
            }

            if (wAttrs.DurabilityBonus > 0)
            {
                count++;
            }

            if (weapon.Slayer != SlayerName.None)
            {
                count++;
            }

            if (weapon.Slayer2 != SlayerName.None)
            {
                count++;
            }

            count += CountSkillBonuses(weapon.SkillBonuses);
        }
        else if (item is BaseArmor armor)
        {
            var aAttrs = armor.ArmorAttributes;
            if (aAttrs.LowerStatReq > 0)
            {
                count++;
            }

            if (aAttrs.SelfRepair > 0)
            {
                count++;
            }

            if (aAttrs.DurabilityBonus > 0)
            {
                count++;
            }

            count += CountSkillBonuses(armor.SkillBonuses);
        }
        else if (item is BaseClothing clothing)
        {
            var cAttrs = clothing.ClothingAttributes;
            if (cAttrs.LowerStatReq > 0)
            {
                count++;
            }

            if (cAttrs.SelfRepair > 0)
            {
                count++;
            }

            if (cAttrs.DurabilityBonus > 0)
            {
                count++;
            }

            count += CountSkillBonuses(clothing.SkillBonuses);
        }
        else if (item is BaseJewel jewel)
        {
            count += CountSkillBonuses(jewel.SkillBonuses);
        }
        else if (item is Spellbook spellbook)
        {
            if (spellbook.Slayer != SlayerName.None)
            {
                count++;
            }

            if (spellbook.Slayer2 != SlayerName.None)
            {
                count++;
            }

            count += CountSkillBonuses(spellbook.SkillBonuses);
        }

        return count;
    }

    public static ImbuingCost GetImbueCost(ImbuingMod mod, Item item, int targetIntensity)
    {
        var maxVal = Math.Max(1, mod.GetMaxValue(item));
        var ratio = mod.IsBoolean ? 1.0 : Math.Clamp((double)targetIntensity / maxVal, 0.1, 1.0);
        var highWeightBonus = mod.Weight >= 130 && ratio >= 0.8 ? 1 : 0;

        var (primaryType, primaryAmount) = mod.PrimaryTier switch
        {
            ImbuingIngredientTier.MagicalResidue => (
                typeof(MagicalResidue),
                Math.Clamp((int)Math.Ceiling(ratio * 6.0) + 1 + highWeightBonus, 2, 9)
            ),
            ImbuingIngredientTier.EnchantedEssence => (
                typeof(EnchantedEssence),
                Math.Clamp((int)Math.Ceiling(ratio * 5.0) + 1 + highWeightBonus, 2, 7)
            ),
            _ => (
                typeof(RelicFragment),
                Math.Clamp((int)Math.Ceiling(ratio * 3.0) + 1 + highWeightBonus, 1, 5)
            )
        };

        var gemAmount = Math.Clamp((int)Math.Ceiling(ratio * 4.0) + highWeightBonus, 1, 6);
        return new ImbuingCost(primaryType, primaryAmount, mod.SecondaryGemType, gemAmount);
    }

    public static string GetIngredientDisplayName(Type type)
    {
        if (type == typeof(MagicalResidue))
        {
            return "Magical Residue";
        }

        if (type == typeof(EnchantedEssence))
        {
            return "Enchanted Essence";
        }

        if (type == typeof(RelicFragment))
        {
            return "Relic Fragment";
        }

        if (type == typeof(StarSapphire))
        {
            return "Star Sapphire";
        }

        return type?.Name ?? "Ingredient";
    }

    private static int GetAosAttributeValue(Item item, AosAttribute attr)
    {
        if (item is not IAosItem aos)
        {
            return 0;
        }

        if (attr == AosAttribute.CastSpeed && aos.Attributes.SpellChanneling > 0)
        {
            return Math.Max(0, aos.Attributes.CastSpeed + 1);
        }

        return aos.Attributes[attr];
    }

    private static bool IsExceptionalCraftedArmor(BaseArmor armor) =>
        IsPlayerCrafted(armor) && armor.Quality == ArmorQuality.Exceptional;

    private static int GetTotalArmorResistBonusPoints(BaseArmor armor) =>
        Math.Max(0, armor.PhysicalBonus) +
        Math.Max(0, armor.FireBonus) +
        Math.Max(0, armor.ColdBonus) +
        Math.Max(0, armor.PoisonBonus) +
        Math.Max(0, armor.EnergyBonus);

    private static int ComputeExceptionalArmorResistWeight(int totalBonusPoints)
    {
        var excess = totalBonusPoints - ExceptionalArmorResistAllowance;
        if (excess <= 0)
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Round(excess / 15.0 * 100.0));
    }

    private static int GetResistanceValue(Item item, ResistanceType resist)
    {
        if (item is BaseWeapon weapon)
        {
            return resist switch
            {
                ResistanceType.Physical => weapon.WeaponAttributes.ResistPhysicalBonus,
                ResistanceType.Fire => weapon.WeaponAttributes.ResistFireBonus,
                ResistanceType.Cold => weapon.WeaponAttributes.ResistColdBonus,
                ResistanceType.Poison => weapon.WeaponAttributes.ResistPoisonBonus,
                ResistanceType.Energy => weapon.WeaponAttributes.ResistEnergyBonus,
                _ => 0
            };
        }

        if (item is BaseArmor armor)
        {
            return resist switch
            {
                ResistanceType.Physical => armor.PhysicalBonus,
                ResistanceType.Fire => armor.FireBonus,
                ResistanceType.Cold => armor.ColdBonus,
                ResistanceType.Poison => armor.PoisonBonus,
                ResistanceType.Energy => armor.EnergyBonus,
                _ => 0
            };
        }

        if (item is BaseClothing clothing)
        {
            return resist switch
            {
                ResistanceType.Physical => clothing.Resistances.Physical,
                ResistanceType.Fire => clothing.Resistances.Fire,
                ResistanceType.Cold => clothing.Resistances.Cold,
                ResistanceType.Poison => clothing.Resistances.Poison,
                ResistanceType.Energy => clothing.Resistances.Energy,
                _ => 0
            };
        }

        if (item is BaseJewel jewel)
        {
            return resist switch
            {
                ResistanceType.Physical => jewel.Resistances.Physical,
                ResistanceType.Fire => jewel.Resistances.Fire,
                ResistanceType.Cold => jewel.Resistances.Cold,
                ResistanceType.Poison => jewel.Resistances.Poison,
                ResistanceType.Energy => jewel.Resistances.Energy,
                _ => 0
            };
        }

        return 0;
    }

    private static void SetResistanceValue(Item item, ResistanceType resist, int value)
    {
        if (item is BaseWeapon weapon)
        {
            switch (resist)
            {
                case ResistanceType.Physical:
                    weapon.WeaponAttributes.ResistPhysicalBonus = value;
                    break;
                case ResistanceType.Fire:
                    weapon.WeaponAttributes.ResistFireBonus = value;
                    break;
                case ResistanceType.Cold:
                    weapon.WeaponAttributes.ResistColdBonus = value;
                    break;
                case ResistanceType.Poison:
                    weapon.WeaponAttributes.ResistPoisonBonus = value;
                    break;
                case ResistanceType.Energy:
                    weapon.WeaponAttributes.ResistEnergyBonus = value;
                    break;
            }
        }
        else if (item is BaseArmor armor)
        {
            switch (resist)
            {
                case ResistanceType.Physical:
                    armor.PhysicalBonus = value;
                    break;
                case ResistanceType.Fire:
                    armor.FireBonus = value;
                    break;
                case ResistanceType.Cold:
                    armor.ColdBonus = value;
                    break;
                case ResistanceType.Poison:
                    armor.PoisonBonus = value;
                    break;
                case ResistanceType.Energy:
                    armor.EnergyBonus = value;
                    break;
            }

            if (armor.Parent is Mobile owner)
            {
                owner.UpdateResistances();
            }
        }
        else if (item is BaseClothing clothing)
        {
            switch (resist)
            {
                case ResistanceType.Physical:
                    clothing.Resistances.Physical = value;
                    break;
                case ResistanceType.Fire:
                    clothing.Resistances.Fire = value;
                    break;
                case ResistanceType.Cold:
                    clothing.Resistances.Cold = value;
                    break;
                case ResistanceType.Poison:
                    clothing.Resistances.Poison = value;
                    break;
                case ResistanceType.Energy:
                    clothing.Resistances.Energy = value;
                    break;
            }
        }
        else if (item is BaseJewel jewel)
        {
            switch (resist)
            {
                case ResistanceType.Physical:
                    jewel.Resistances.Physical = value;
                    break;
                case ResistanceType.Fire:
                    jewel.Resistances.Fire = value;
                    break;
                case ResistanceType.Cold:
                    jewel.Resistances.Cold = value;
                    break;
                case ResistanceType.Poison:
                    jewel.Resistances.Poison = value;
                    break;
                case ResistanceType.Energy:
                    jewel.Resistances.Energy = value;
                    break;
            }
        }
    }

    private static int ComputeSkillBonusWeight(AosSkillBonuses bonuses)
    {
        if (bonuses == null || bonuses.IsEmpty)
        {
            return 0;
        }

        var weight = 0;
        for (var i = 0; i < 5; i++)
        {
            if (bonuses.GetValues(i, out _, out var bonus) && bonus > 0)
            {
                weight += Math.Max(1, (int)Math.Round(bonus / 15.0 * 140.0));
            }
        }

        return weight;
    }

    private static int CountSkillBonuses(AosSkillBonuses bonuses)
    {
        if (bonuses == null || bonuses.IsEmpty)
        {
            return 0;
        }

        var count = 0;
        for (var i = 0; i < 5; i++)
        {
            if (bonuses.GetValues(i, out _, out var bonus) && bonus > 0)
            {
                count++;
            }
        }

        return count;
    }
}
