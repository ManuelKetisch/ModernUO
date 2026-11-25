using System;
using Server.Items;

namespace Server.Engines.WeaponLeveling;

internal enum WeaponUpgradeCategory
{
    Melee,
    Magic,
    Character,
    Resistances,
    WeaponHits,
    Miscellaneous
}

internal readonly record struct WeaponUpgrade(
    string Name,
    WeaponUpgradeCategory Category,
    int Cost,
    int Maximum,
    AosAttribute General,
    AosWeaponAttribute Weapon
)
{
    internal int ReadRaw(BaseWeapon weapon) =>
        General != 0 ? weapon.Attributes[General] : weapon.WeaponAttributes[Weapon];

    internal int Read(BaseWeapon weapon) => (General, Weapon) switch
    {
        (AosAttribute.Luck, _) => weapon.Attributes.Luck + weapon.GetLuckBonus(),
        (AosAttribute.WeaponDamage, _) => weapon.Attributes.WeaponDamage + weapon.GetDamageBonus(),
        (AosAttribute.AttackChance, _) => weapon.Attributes.AttackChance + weapon.GetHitChanceBonus(),
        (_, AosWeaponAttribute.LowerStatReq) => weapon.GetLowerStatReq(),
        (_, AosWeaponAttribute.DurabilityBonus) => weapon.GetDurabilityBonus(),
        _ => ReadRaw(weapon)
    };

    internal string DisplayValue(int value) => Weapon == AosWeaponAttribute.MageWeapon
        ? value == 0 ? "None" : (value - 30).ToString()
        : value.ToString();
}

internal static class WeaponUpgradeCatalog
{
    private static readonly WeaponUpgrade[] _upgrades =
    [
        General("Defense chance", WeaponUpgradeCategory.Melee, 3, 50, AosAttribute.DefendChance),
        General("Hit chance", WeaponUpgradeCategory.Melee, 3, 50, AosAttribute.AttackChance),
        General("Damage increase", WeaponUpgradeCategory.Melee, 4, 50, AosAttribute.WeaponDamage),
        General("Swing speed", WeaponUpgradeCategory.Melee, 4, 40, AosAttribute.WeaponSpeed),
        General("Reflect physical damage", WeaponUpgradeCategory.Melee, 3, 15, AosAttribute.ReflectPhysical),

        General("Spell damage", WeaponUpgradeCategory.Magic, 2, 20, AosAttribute.SpellDamage),
        General("Faster cast recovery", WeaponUpgradeCategory.Magic, 5, 6, AosAttribute.CastRecovery),
        General("Faster casting", WeaponUpgradeCategory.Magic, 50, 1, AosAttribute.CastSpeed),
        General("Lower mana cost", WeaponUpgradeCategory.Magic, 5, 10, AosAttribute.LowerManaCost),
        General("Lower reagent cost", WeaponUpgradeCategory.Magic, 5, 20, AosAttribute.LowerRegCost),
        General("Enhanced potions", WeaponUpgradeCategory.Magic, 2, 25, AosAttribute.EnhancePotions),
        General("Spell channeling", WeaponUpgradeCategory.Magic, 150, 1, AosAttribute.SpellChanneling),
        Weapon("Mage weapon", WeaponUpgradeCategory.Magic, 1, 30, AosWeaponAttribute.MageWeapon),

        General("Hit regeneration", WeaponUpgradeCategory.Character, 2, 6, AosAttribute.RegenHits),
        General("Stamina regeneration", WeaponUpgradeCategory.Character, 1, 10, AosAttribute.RegenStam),
        General("Mana regeneration", WeaponUpgradeCategory.Character, 6, 6, AosAttribute.RegenMana),
        General("Strength bonus", WeaponUpgradeCategory.Character, 4, 8, AosAttribute.BonusStr),
        General("Dexterity bonus", WeaponUpgradeCategory.Character, 4, 8, AosAttribute.BonusDex),
        General("Intelligence bonus", WeaponUpgradeCategory.Character, 4, 8, AosAttribute.BonusInt),
        General("Hit point bonus", WeaponUpgradeCategory.Character, 3, 8, AosAttribute.BonusHits),
        General("Stamina bonus", WeaponUpgradeCategory.Character, 3, 8, AosAttribute.BonusStam),
        General("Mana bonus", WeaponUpgradeCategory.Character, 3, 8, AosAttribute.BonusMana),
        Weapon("Lower requirements", WeaponUpgradeCategory.Character, 2, 100, AosWeaponAttribute.LowerStatReq),

        Weapon("Physical resistance", WeaponUpgradeCategory.Resistances, 5, 20, AosWeaponAttribute.ResistPhysicalBonus),
        Weapon("Fire resistance", WeaponUpgradeCategory.Resistances, 5, 20, AosWeaponAttribute.ResistFireBonus),
        Weapon("Cold resistance", WeaponUpgradeCategory.Resistances, 5, 20, AosWeaponAttribute.ResistColdBonus),
        Weapon("Poison resistance", WeaponUpgradeCategory.Resistances, 5, 20, AosWeaponAttribute.ResistPoisonBonus),
        Weapon("Energy resistance", WeaponUpgradeCategory.Resistances, 5, 20, AosWeaponAttribute.ResistEnergyBonus),

        Weapon("Hit life leech", WeaponUpgradeCategory.WeaponHits, 3, 60, AosWeaponAttribute.HitLeechHits),
        Weapon("Hit stamina leech", WeaponUpgradeCategory.WeaponHits, 3, 60, AosWeaponAttribute.HitLeechStam),
        Weapon("Hit mana leech", WeaponUpgradeCategory.WeaponHits, 3, 60, AosWeaponAttribute.HitLeechMana),
        Weapon("Hit lower attack", WeaponUpgradeCategory.WeaponHits, 3, 60, AosWeaponAttribute.HitLowerAttack),
        Weapon("Hit lower defense", WeaponUpgradeCategory.WeaponHits, 3, 60, AosWeaponAttribute.HitLowerDefend),
        Weapon("Hit magic arrow", WeaponUpgradeCategory.WeaponHits, 4, 60, AosWeaponAttribute.HitMagicArrow),
        Weapon("Hit harm", WeaponUpgradeCategory.WeaponHits, 4, 60, AosWeaponAttribute.HitHarm),
        Weapon("Hit fireball", WeaponUpgradeCategory.WeaponHits, 4, 60, AosWeaponAttribute.HitFireball),
        Weapon("Hit lightning", WeaponUpgradeCategory.WeaponHits, 4, 60, AosWeaponAttribute.HitLightning),
        Weapon("Hit dispel", WeaponUpgradeCategory.WeaponHits, 4, 60, AosWeaponAttribute.HitDispel),
        Weapon("Hit cold area", WeaponUpgradeCategory.WeaponHits, 4, 60, AosWeaponAttribute.HitColdArea),
        Weapon("Hit fire area", WeaponUpgradeCategory.WeaponHits, 4, 60, AosWeaponAttribute.HitFireArea),
        Weapon("Hit poison area", WeaponUpgradeCategory.WeaponHits, 4, 60, AosWeaponAttribute.HitPoisonArea),
        Weapon("Hit energy area", WeaponUpgradeCategory.WeaponHits, 4, 60, AosWeaponAttribute.HitEnergyArea),
        Weapon("Hit physical area", WeaponUpgradeCategory.WeaponHits, 4, 60, AosWeaponAttribute.HitPhysicalArea),

        General("Luck", WeaponUpgradeCategory.Miscellaneous, 3, 150, AosAttribute.Luck),
        General("Night sight", WeaponUpgradeCategory.Miscellaneous, 10, 1, AosAttribute.NightSight),
        Weapon("Self repair", WeaponUpgradeCategory.Miscellaneous, 12, 5, AosWeaponAttribute.SelfRepair),
        Weapon("Use best weapon skill", WeaponUpgradeCategory.Miscellaneous, 50, 1, AosWeaponAttribute.UseBestSkill),
        Weapon("Durability bonus", WeaponUpgradeCategory.Miscellaneous, 1, 255, AosWeaponAttribute.DurabilityBonus),
    ];

    internal static ReadOnlySpan<WeaponUpgrade> Upgrades => _upgrades;

    internal static string CategoryName(WeaponUpgradeCategory category) => category switch
    {
        WeaponUpgradeCategory.Character => "Character stats",
        WeaponUpgradeCategory.WeaponHits => "Weapon hits",
        _ => category.ToString()
    };

    private static WeaponUpgrade General(
        string name, WeaponUpgradeCategory category, int cost, int maximum, AosAttribute attribute
    ) => new(name, category, cost, maximum, attribute, 0);

    private static WeaponUpgrade Weapon(
        string name, WeaponUpgradeCategory category, int cost, int maximum, AosWeaponAttribute attribute
    ) => new(name, category, cost, maximum, 0, attribute);
}
