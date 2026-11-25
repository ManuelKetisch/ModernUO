using System;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.WeaponLeveling;
using Server.Mobiles;

namespace Server.Items;

public partial class BaseWeapon
{
    internal bool IsWeaponLevelingEnabled => WeaponLevelKind != WeaponLevelingClass.None;
    internal bool HasWeaponLevelingArtifactTag => ArtifactRarity > 0 && !ArtifactTagRemovedForLeveling;
    internal int WeaponLevel => IsWeaponLevelingEnabled ? WeaponLevelingRules.LevelForExperience(WeaponLevelExperience) : 0;
    internal int WeaponExperience => WeaponLevelExperience;
    internal int WeaponLevelingCostMultiplier => HasWeaponLevelingArtifactTag ? 2 : 1;
    internal int WeaponLevelingPointsEarned => WeaponLevelingRules.EarnedPoints(WeaponLevelKind, WeaponLevel) +
                                               WeaponLevelingRules.BoostBonus(WeaponLevelKind, WeaponLevelBoostUses);
    internal int WeaponLevelingPointsAvailable => Math.Max(0, WeaponLevelingPointsEarned - WeaponLevelSpent);

    internal WeaponLevelingSnapshot CaptureWeaponLeveling() =>
        new(WeaponLevelKind, WeaponLevelExperience, WeaponLevelSpent, ArtifactTagRemovedForLeveling, WeaponLevelBoostUses);

    internal bool CanManageWeaponLeveling(PlayerMobile player) =>
        player is { Deleted: false, Alive: true } && !Deleted && this is not Fists &&
        (Parent == player && player.Weapon == this || player.Backpack != null && IsChildOf(player.Backpack)) &&
        GetSecureTradeCont() == null && IsAccessibleTo(player);

    internal bool TryEnableWeaponLeveling(PlayerMobile player)
    {
        if (IsWeaponLevelingEnabled || !CanManageWeaponLeveling(player))
        {
            return false;
        }

        WeaponLevelKind = HasWeaponLevelingArtifactTag ? WeaponLevelingClass.Artifact :
            PlayerConstructed ? WeaponLevelingClass.Crafted : WeaponLevelingClass.Looted;
        return true;
    }

    internal bool TryRemoveWeaponLevelingArtifactTag(PlayerMobile player)
    {
        if (!HasWeaponLevelingArtifactTag || !CanManageWeaponLeveling(player))
        {
            return false;
        }

        ArtifactTagRemovedForLeveling = true;
        return true;
    }

    internal void CopyWeaponLevelingTo(BaseWeapon replacement, bool removeArtifactTag = false)
    {
        replacement.WeaponLevelKind = WeaponLevelKind;
        replacement.WeaponLevelExperience = WeaponLevelExperience;
        replacement.WeaponLevelSpent = WeaponLevelSpent;
        replacement.WeaponLevelBoostUses = WeaponLevelBoostUses;
        replacement.ArtifactTagRemovedForLeveling = ArtifactTagRemovedForLeveling || removeArtifactTag;
    }

    internal bool TryApplyWeaponLevelBoost(PlayerMobile player)
    {
        if (!CanManageWeaponLeveling(player) || !IsWeaponLevelingEnabled ||
            WeaponLevel >= WeaponLevelingRules.MaximumLevel)
        {
            return false;
        }

        var before = WeaponLevel;
        var after = Math.Min(before + WeaponLevelingRules.LevelsPerBoost, WeaponLevelingRules.MaximumLevel);
        var gained = WeaponLevelingRules.ExperienceForLevel(after) - WeaponLevelingRules.ExperienceForLevel(before);
        WeaponLevelBoostUses++;
        AwardWeaponExperience(gained, player);
        return true;
    }

    internal void AwardWeaponExperience(int amount, PlayerMobile player)
    {
        if (!IsWeaponLevelingEnabled || amount <= 0 || WeaponLevelExperience >= WeaponLevelingRules.MaximumExperience)
        {
            return;
        }

        var previousLevel = WeaponLevel;
        WeaponLevelExperience += Math.Min(amount, WeaponLevelingRules.MaximumExperience - WeaponLevelExperience);

        var newLevel = WeaponLevel;
        if (newLevel != previousLevel)
        {
            player.SendMessage($"Your weapon reached level {newLevel}.");
        }
    }

    internal WeaponUpgradeQuote? QuoteWeaponUpgrade(int index)
    {
        var upgrades = WeaponUpgradeCatalog.Upgrades;
        if (!IsWeaponLevelingEnabled || index < 0 || index >= upgrades.Length)
        {
            return null;
        }

        var upgrade = upgrades[index];
        var current = upgrade.Read(this);
        var rawCurrent = upgrade.ReadRaw(this);
        var cost = upgrade.Cost * WeaponLevelingCostMultiplier;
        if (!Core.AOS || current >= upgrade.Maximum || WeaponLevelingPointsAvailable < cost ||
            upgrade.Weapon == AosWeaponAttribute.MageWeapon && rawCurrent <= 0 ||
            upgrade.Weapon == AosWeaponAttribute.DurabilityBonus && MaxHitPoints <= 0)
        {
            return null;
        }

        return new WeaponUpgradeQuote(index, current, current + 1, cost, rawCurrent);
    }

    internal bool TrySpendWeaponLevelingPoints(PlayerMobile player, WeaponUpgradeQuote quote)
    {
        if (!CanManageWeaponLeveling(player) || QuoteWeaponUpgrade(quote.Index) != quote)
        {
            return false;
        }

        var upgrade = WeaponUpgradeCatalog.Upgrades[quote.Index];
        var nextRaw = quote.RawCurrent + 1;
        if (upgrade.General != 0)
        {
            Attributes[upgrade.General] = nextRaw;
            if (Parent == player)
            {
                RefreshEquippedStat(player, upgrade.General, nextRaw);
            }
        }
        else if (upgrade.Weapon == AosWeaponAttribute.DurabilityBonus)
        {
            ApplyWeaponLevelingDurability(nextRaw);
        }
        else
        {
            WeaponAttributes[upgrade.Weapon] = nextRaw;
            if (upgrade.Weapon == AosWeaponAttribute.MageWeapon && Parent == player)
            {
                RefreshEquippedMageWeapon(player, nextRaw);
            }
        }

        WeaponLevelSpent += quote.Cost;
        return true;
    }

    private void RefreshEquippedStat(PlayerMobile player, AosAttribute attribute, int value)
    {
        var (type, key) = attribute switch
        {
            AosAttribute.BonusStr => (StatType.Str, "Str"),
            AosAttribute.BonusDex => (StatType.Dex, "Dex"),
            AosAttribute.BonusInt => (StatType.Int, "Int"),
            _ => (default, null)
        };

        if (key != null)
        {
            var name = $"{Serial}{key}";
            player.RemoveStatMod(name);
            player.AddStatMod(new StatMod(type, name, value, TimeSpan.Zero));
        }
    }

    private void RefreshEquippedMageWeapon(PlayerMobile player, int value)
    {
        m_MageMod?.Remove();
        m_MageMod = null;
        if (Core.AOS && value is > 0 and < 30)
        {
            m_MageMod = new DefaultSkillMod(SkillName.Magery, "MageWeapon", true, value - 30);
            player.AddSkillMod(m_MageMod);
        }
    }

    private void ApplyWeaponLevelingDurability(int value)
    {
        var oldMaximum = MaxHitPoints;
        var oldHits = HitPoints;
        var oldScale = Math.Max(1L, 100L + GetDurabilityBonus());

        // Invert the native round-up once so repeated single-point purchases cannot inflate base durability.
        var baseMaximum = Math.Max(1L, oldMaximum * 100L / oldScale);
        WeaponAttributes.DurabilityBonus = value;
        var newScale = Math.Max(1L, 100L + GetDurabilityBonus());
        var newMaximum = (int)Math.Clamp((baseMaximum * newScale + 99) / 100, oldMaximum, int.MaxValue);

        MaxHitPoints = newMaximum;
        HitPoints = (int)Math.Clamp((long)oldHits + newMaximum - oldMaximum, 0, newMaximum);
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (from is PlayerMobile player && IsWeaponLevelingEnabled && CanManageWeaponLeveling(player))
        {
            list.Add(new WeaponLevelInformationEntry());
        }
    }

    private sealed class WeaponLevelInformationEntry() : ContextMenuEntry(3000098)
    {
        public override void OnClick(Mobile from, IEntity target)
        {
            if (from is PlayerMobile player && target is BaseWeapon weapon)
            {
                WeaponLevelingGump.DisplayTo(player, weapon);
            }
        }
    }
}
