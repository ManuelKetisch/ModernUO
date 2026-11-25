using System;
using System.Reflection;
using Server.Engines.WeaponLeveling;
using Server.Factions;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Server.Engines.WeaponLevelingServices;

internal enum WeaponServiceOperation
{
    WaiveTag,
    ChangeType,
    Relayer,
    OneHanded
}

internal static class WeaponServiceTransfer
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(WeaponServiceTransfer));
    private static readonly AosAttribute[] _aosAttributes = Enum.GetValues<AosAttribute>();
    private static readonly AosWeaponAttribute[] _weaponAttributes = Enum.GetValues<AosWeaponAttribute>();
    private static readonly AosElementAttribute[] _elementAttributes = Enum.GetValues<AosElementAttribute>();

    internal static bool IsInBackpack(PlayerMobile owner, Item item) =>
        owner is { Alive: true, Deleted: false, Backpack: { } pack } && item is { Deleted: false } &&
        item.IsChildOf(pack) && item.GetSecureTradeCont() == null && item.IsAccessibleTo(owner);

    internal static bool CanConsume(PlayerMobile owner, Item item) =>
        IsInBackpack(owner, item) && IsUnbound(item);

    private static bool IsUnbound(Item item) =>
        item is
        {
            Amount: 1, Stackable: false, Movable: true, Nontransferable: false, LootType: LootType.Regular,
            BlessedFor: null, Insured: false, IsLockedDown: false, IsSecure: false, TotalItems: 0
        } && item is not (IFactionItem { FactionItemState: not null } or IArcaneEquip { IsArcane: true });

    internal static bool CanPerform(
        PlayerMobile owner, BaseWeapon source, Item template, WeaponServiceOperation operation
    )
    {
        if (source?.CanManageWeaponLeveling(owner) != true)
        {
            return false;
        }

        if (operation == WeaponServiceOperation.WaiveTag)
        {
            return source.HasWeaponLevelingArtifactTag && template == null;
        }

        if (source == template || !CanConsume(owner, source) || !CanConsume(owner, template) ||
            source.Cursed || source.Consecrated || !IsBlank(template))
        {
            return false;
        }

        return operation switch
        {
            WeaponServiceOperation.ChangeType => template is BaseWeapon && template.GetType() != source.GetType() &&
                                                 (source.IsWeaponLevelingEnabled || source.HasWeaponLevelingArtifactTag),
            WeaponServiceOperation.Relayer => source.IsWeaponLevelingEnabled && template is
                                             BaseArmor or BaseClothing or BaseJewel,
            WeaponServiceOperation.OneHanded => source.IsWeaponLevelingEnabled &&
                                               source is not BaseRanged && source.Layer == Layer.TwoHanded &&
                                               template is BaseWeapon { Layer: Layer.OneHanded } and not BaseRanged,
            _ => false
        };
    }

    private static bool IsBlank(Item item)
    {
        if (item is not IAosItem { Attributes.IsEmpty: true } || item.Name != item.DefaultName ||
            item.Layer is < Layer.FirstValid or > Layer.LastUserValid or Layer.Backpack or Layer.Hair or Layer.FacialHair)
        {
            return false;
        }

        return item switch
        {
            BaseWeapon weapon => weapon is not IUsesRemaining && weapon.ArtifactRarity == 0 &&
                                 !weapon.IsWeaponLevelingEnabled && !weapon.CaptureWeaponLeveling().ArtifactTagRemoved &&
                                 weapon.WeaponAttributes.IsEmpty && weapon.SkillBonuses.IsEmpty &&
                                 weapon.AosElementDamages.IsEmpty &&
                                 weapon.Resource is CraftResource.Iron or CraftResource.RegularWood &&
                                 !HasRangedModifiers(weapon) &&
                                 weapon.DamageLevel == WeaponDamageLevel.Regular &&
                                 weapon.AccuracyLevel == WeaponAccuracyLevel.Regular &&
                                 weapon.DurabilityLevel == WeaponDurabilityLevel.Regular &&
                                 weapon.Slayer == SlayerName.None && weapon.Slayer2 == SlayerName.None &&
                                 weapon.Poison == null && weapon.PoisonCharges == 0 &&
                                 string.IsNullOrEmpty(weapon.EngravedText) && !weapon.Cursed && !weapon.Consecrated,
            BaseArmor armor => armor.ArtifactRarity == 0 && armor.ArmorAttributes.IsEmpty &&
                               armor.SkillBonuses.IsEmpty && armor.Resource == armor.DefaultResource &&
                               armor.PhysicalBonus == 0 && armor.FireBonus == 0 && armor.ColdBonus == 0 &&
                               armor.PoisonBonus == 0 && armor.EnergyBonus == 0,
            BaseClothing clothing => clothing.ArtifactRarity == 0 && clothing.ClothingAttributes.IsEmpty &&
                                     clothing.SkillBonuses.IsEmpty && clothing.Resistances.IsEmpty &&
                                     clothing.Resource == clothing.DefaultResource,
            BaseJewel jewel => jewel.ArtifactRarity == 0 && jewel.Resistances.IsEmpty && jewel.SkillBonuses.IsEmpty &&
                               jewel.GemType == GemType.None && jewel.Resource == CraftResource.Iron,
            _ => false
        };
    }

    internal static bool HasRangedModifiers(BaseWeapon weapon) =>
        weapon is BaseRanged { Balanced: true } or BaseRanged { Velocity: not 0 };

    internal static Item Complete(
        PlayerMobile owner, WeaponServiceDeed deed, BaseWeapon source, Item template, WeaponServiceOperation operation,
        PreviewStamp expected
    )
    {
        if (!deed.CanUse(owner) || !CanPerform(owner, source, template, operation) ||
            deed.IsRelayer != (operation is WeaponServiceOperation.Relayer or WeaponServiceOperation.OneHanded) ||
            !expected.Matches(source, template))
        {
            owner.SendMessage("Your items changed. Choose them again.");
            return null;
        }

        if (operation == WeaponServiceOperation.WaiveTag)
        {
            if (!source.TryRemoveWeaponLevelingArtifactTag(owner))
            {
                return null;
            }

            deed.Delete();
            return source;
        }

        if (operation == WeaponServiceOperation.OneHanded)
        {
            source.Layer = Layer.OneHanded;
            source.MarkDirty();
            source.InvalidateProperties();
            template.Delete();
            deed.Delete();
            owner.SendMessage("Your weapon now fits in one hand.");
            return source;
        }

        var result = CreateReplacement(template);
        if (result == null)
        {
            owner.SendMessage("That equipment type cannot be used as a template.");
            return null;
        }

        if (operation == WeaponServiceOperation.ChangeType)
        {
            if (!TryCopyWeapon(source, (BaseWeapon)result, removeArtifactTag: true))
            {
                result.Delete();
                owner.SendMessage("Choose a type with compatible built-in hit chance and damage bonuses.");
                return null;
            }
        }
        else
        {
            ApplyWearableStats(source, result);
        }

        if (!owner.Backpack.TryDropItem(owner, result, true))
        {
            result.Delete();
            return null;
        }

        source.Delete();
        template.Delete();
        deed.Delete();
        owner.SendMessage("Your new item is in your backpack.");
        return result;
    }

    private static bool IsPlayerConstructor(ConstructorInfo constructor) =>
        constructor.GetCustomAttribute<ConstructibleAttribute>()?.AccessLevel == AccessLevel.Player;

    private static Item CreateReplacement(Item template)
    {
        var type = template.GetType();
        var constructor = type.GetConstructor(out var count, IsPlayerConstructor);
        if (constructor == null)
        {
            return null;
        }

        Item result;
        try
        {
            var defaults = new object[count];
            Array.Fill(defaults, Type.Missing);
            result = (Item)constructor.Invoke(defaults);
        }
        catch (TargetInvocationException exception)
        {
            _logger.Warning(exception, "Weapon service cannot create a blank {Template}.", type);
            return null;
        }

        // The replacement uses the type's normal equipment slot and combat behavior.
        if (!IsUnbound(result) || !IsBlank(result) || result.Layer != template.Layer)
        {
            result.Delete();
            return null;
        }

        result.ItemID = template.ItemID;
        result.Hue = template.Hue;
        return result;
    }

    internal static bool TryCopyWeapon(BaseWeapon source, BaseWeapon result, bool removeArtifactTag)
    {
        result.Resource = source.Resource;
        result.Quality = source.Quality;
        result.DurabilityLevel = source.DurabilityLevel;
        result.AccuracyLevel = source.AccuracyLevel;
        result.DamageLevel = source.DamageLevel;

        // The weapon's built-in bonuses still apply in combat after changing type.
        var attackChance = (long)source.Attributes.AttackChance + source.GetHitChanceBonus() - result.GetHitChanceBonus();
        var damageBonus = (long)source.Attributes.WeaponDamage + source.GetDamageBonus() - result.GetDamageBonus();
        if (attackChance is < 0 or > int.MaxValue || damageBonus is < 0 or > int.MaxValue)
        {
            return false;
        }

        result.Crafter = source.Crafter;
        result.EngravedText = source.EngravedText;
        result.Identified = source.Identified;
        result.Slayer = source.Slayer;
        result.Slayer2 = source.Slayer2;
        result.Poison = source.Poison;
        result.PoisonCharges = source.PoisonCharges;
        result.PlayerConstructed = source.PlayerConstructed;
        result.Name = source.Name == source.DefaultName ? null : source.Name;
        if (source is BaseRanged rangedSource && result is BaseRanged rangedResult)
        {
            rangedResult.Balanced = rangedSource.Balanced;
            rangedResult.Velocity = rangedSource.Velocity;
        }

        source.OnAfterDuped(result);
        result.Attributes.AttackChance = (int)attackChance;
        result.Attributes.WeaponDamage = (int)damageBonus;
        result.MaxHitPoints = source.MaxHitPoints;
        result.HitPoints = source.HitPoints;
        source.CopyWeaponLevelingTo(result, removeArtifactTag);
        return true;
    }

    private static void ApplyWearableStats(BaseWeapon source, Item result)
    {
        var attributes = ((IAosItem)result).Attributes;
        foreach (var attribute in _aosAttributes)
        {
            attributes[attribute] = attribute switch
            {
                AosAttribute.AttackChance => source.Attributes.AttackChance + source.GetHitChanceBonus(),
                AosAttribute.WeaponDamage => source.Attributes.WeaponDamage + source.GetDamageBonus(),
                AosAttribute.Luck => source.Attributes.Luck + source.GetLuckBonus() - GetWearableMaterialLuck(result),
                _ => source.Attributes[attribute]
            };
        }

        switch (result)
        {
            case BaseArmor armor:
                armor.PhysicalBonus = source.PhysicalResistance;
                armor.FireBonus = source.FireResistance;
                armor.ColdBonus = source.ColdResistance;
                armor.PoisonBonus = source.PoisonResistance;
                armor.EnergyBonus = source.EnergyResistance;
                CopySkillBonuses(source.SkillBonuses, armor.SkillBonuses);
                CopyArmorAttributes(source, armor.ArmorAttributes, GetWearableMaterialRequirements(armor));
                break;
            case BaseClothing clothing:
                CopyElementResists(source, clothing.Resistances);
                CopySkillBonuses(source.SkillBonuses, clothing.SkillBonuses);
                CopyArmorAttributes(source, clothing.ClothingAttributes, GetWearableMaterialRequirements(clothing));
                break;
            case BaseJewel jewel:
                CopyElementResists(source, jewel.Resistances);
                CopySkillBonuses(source.SkillBonuses, jewel.SkillBonuses);
                break;
        }
    }

    private static void CopySkillBonuses(AosSkillBonuses source, AosSkillBonuses target)
    {
        for (var i = 0; i < 5; i++)
        {
            if (source.GetValues(i, out var skill, out var bonus))
            {
                target.SetValues(i, skill, bonus);
            }
        }
    }

    private static int GetWearableMaterialLuck(Item item) => item is BaseArmor armor ? armor.GetLuckBonus() : 0;

    private static int GetWearableMaterialRequirements(Item item) =>
        item is BaseArmor armor ? armor.GetLowerStatReq() : 0;

    private static void CopyArmorAttributes(BaseWeapon source, AosArmorAttributes target, int materialRequirements)
    {
        // Armor still applies its own material bonus when equipped.
        target.LowerStatReq = source.GetLowerStatReq() - materialRequirements;
        target.SelfRepair = source.WeaponAttributes.SelfRepair;
        target.DurabilityBonus = source.WeaponAttributes.DurabilityBonus;
    }

    private static void CopyElementResists(BaseWeapon source, AosElementAttributes target)
    {
        target.Physical = source.PhysicalResistance;
        target.Fire = source.FireResistance;
        target.Cold = source.ColdResistance;
        target.Poison = source.PoisonResistance;
        target.Energy = source.EnergyResistance;
    }

    private readonly record struct WeaponDetails(
        WeaponLevelingSnapshot Progress, string Name, Layer Layer, int ItemID, int Hue, int Hits, int MaximumHits,
        WeaponQuality Quality, WeaponDamageLevel Damage, WeaponAccuracyLevel Accuracy, WeaponDurabilityLevel Durability,
        CraftResource Resource, string Crafter, string Engraving, SlayerName Slayer, SlayerName Slayer2,
        Poison Poison, int PoisonCharges, bool Identified, bool Crafted, int MaterialLuck, int EffectiveRequirements,
        bool Balanced, int Velocity, int BuiltInHitChance, int BuiltInDamage
    )
    {
        public static WeaponDetails Read(BaseWeapon weapon) =>
            new(weapon.CaptureWeaponLeveling(), weapon.Name, weapon.Layer, weapon.ItemID, weapon.Hue, weapon.HitPoints,
                weapon.MaxHitPoints, weapon.Quality, weapon.DamageLevel, weapon.AccuracyLevel, weapon.DurabilityLevel,
                weapon.Resource, weapon.Crafter, weapon.EngravedText, weapon.Slayer, weapon.Slayer2, weapon.Poison,
                weapon.PoisonCharges, weapon.Identified, weapon.PlayerConstructed, weapon.GetLuckBonus(),
                weapon.GetLowerStatReq(), weapon is BaseRanged { Balanced: true }, (weapon as BaseRanged)?.Velocity ?? 0,
                weapon.GetHitChanceBonus(), weapon.GetDamageBonus());
    }

    internal sealed class PreviewStamp(BaseWeapon source, Item template)
    {
        private readonly WeaponDetails _details = WeaponDetails.Read(source);
        private readonly int[] _aos = Array.ConvertAll(_aosAttributes, attr => source.Attributes[attr]);
        private readonly int[] _weap = Array.ConvertAll(_weaponAttributes, attr => source.WeaponAttributes[attr]);
        private readonly int[] _elements = Array.ConvertAll(_elementAttributes, attr => source.AosElementDamages[attr]);
        private readonly (SkillName, double)[] _skills = ReadSkillBonuses(source.SkillBonuses);
        private readonly TemplateDetails _templateDetails = TemplateDetails.Read(template);

        internal bool Matches(BaseWeapon current, Item currentTemplate) =>
            _details == WeaponDetails.Read(current) && _templateDetails == TemplateDetails.Read(currentTemplate) &&
            _aos.AsSpan().SequenceEqual(Array.ConvertAll(_aosAttributes, attr => current.Attributes[attr])) &&
            _weap.AsSpan().SequenceEqual(Array.ConvertAll(_weaponAttributes, attr => current.WeaponAttributes[attr])) &&
            _elements.AsSpan().SequenceEqual(Array.ConvertAll(_elementAttributes, attr => current.AosElementDamages[attr])) &&
            _skills.AsSpan().SequenceEqual(ReadSkillBonuses(current.SkillBonuses));

        private readonly record struct TemplateDetails(
            Layer Layer, int ItemID, int Hue, int MaterialLuck, int MaterialRequirements, int BuiltInHitChance,
            int BuiltInDamage
        )
        {
            internal static TemplateDetails Read(Item item) => item == null
                ? default
                : new(item.Layer, item.ItemID, item.Hue, GetWearableMaterialLuck(item),
                    GetWearableMaterialRequirements(item), (item as BaseWeapon)?.GetHitChanceBonus() ?? 0,
                    (item as BaseWeapon)?.GetDamageBonus() ?? 0);
        }

        private static (SkillName, double)[] ReadSkillBonuses(AosSkillBonuses bonuses)
        {
            var skills = new (SkillName, double)[5];
            for (var i = 0; i < skills.Length; i++)
            {
                bonuses.GetValues(i, out var skill, out var value);
                skills[i] = (skill, value);
            }

            return skills;
        }
    }
}
