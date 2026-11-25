using System;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.WeaponLeveling;

internal sealed class WeaponLevelingGump(
    PlayerMobile owner, BaseWeapon weapon, WeaponUpgradeCategory? category, WeaponUpgradeQuote? quote
) : DynamicGump(75, 65)
{
    internal const int FirstCategoryButton = 1;
    internal const int BackButton = 100;
    internal const int FirstUpgradeButton = 200;
    internal const int ConfirmButton = 400;
    private const int LabelHue = 0x480;
    private const int MutedHue = 0x3B2;

    private static readonly WeaponUpgradeCategory[] _categories = Enum.GetValues<WeaponUpgradeCategory>();

    public override bool Singleton => true;

    internal static void DisplayTo(
        PlayerMobile owner, BaseWeapon weapon, WeaponUpgradeCategory? category = null, WeaponUpgradeQuote? quote = null
    )
    {
        if (owner?.NetState != null && weapon is { IsWeaponLevelingEnabled: true } &&
            weapon.CanManageWeaponLeveling(owner))
        {
            owner.SendGump(new WeaponLevelingGump(owner, weapon, category, quote));
        }
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        if (category is not { } group)
        {
            BuildOverview(ref builder);
        }
        else if (quote is { } pending)
        {
            BuildConfirmation(ref builder, pending);
        }
        else
        {
            BuildUpgrades(ref builder, group);
        }
    }

    private void BuildOverview(ref DynamicGumpBuilder builder)
    {
        AddFrame(ref builder, 312);
        builder.AddLabelCropped(24, 19, 437, 24, LabelHue, weapon.Name ?? Localization.GetText(weapon.LabelNumber) ?? "Weapon");
        builder.AddLabel(24, 45, LabelHue,
            $"Level {weapon.WeaponLevel} / {WeaponLevelingRules.MaximumLevel}    Points: {weapon.WeaponLevelingPointsAvailable}");
        builder.AddLabel(24, 69, LabelHue,
            $"Experience: {weapon.WeaponExperience:N0} / {WeaponLevelingRules.MaximumExperience:N0}");

        if (weapon.WeaponLevel < WeaponLevelingRules.MaximumLevel)
        {
            var remaining = WeaponLevelingRules.ExperienceForLevel(weapon.WeaponLevel + 1) - weapon.WeaponExperience;
            builder.AddLabel(24, 94, LabelHue, $"Next level in {remaining:N0} XP");
        }
        else
        {
            builder.AddLabel(24, 94, LabelHue, "Maximum level reached");
        }

        var source = weapon.CaptureWeaponLeveling().Class;
        builder.AddLabel(24, 119, LabelHue, $"{source} weapon");
        if (weapon.WeaponLevelingCostMultiplier > 1)
        {
            builder.AddLabel(244, 119, LabelHue, "Artifact prices: double");
        }

        for (var i = 0; i < _categories.Length; i++)
        {
            var x = 24 + i / 3 * 233;
            var y = 158 + i % 3 * 37;
            AddChoice(ref builder, x, y, FirstCategoryButton + i, WeaponUpgradeCatalog.CategoryName(_categories[i]));
        }
    }

    private void BuildUpgrades(ref DynamicGumpBuilder builder, WeaponUpgradeCategory group)
    {
        var upgrades = WeaponUpgradeCatalog.Upgrades;
        var rows = 0;
        foreach (var upgrade in upgrades)
        {
            if (upgrade.Category == group)
            {
                rows++;
            }
        }

        AddFrame(ref builder, 137 + rows * 22);
        builder.AddLabel(24, 18, LabelHue, WeaponUpgradeCatalog.CategoryName(group));
        builder.AddLabel(307, 18, LabelHue, $"Points: {weapon.WeaponLevelingPointsAvailable}");
        builder.AddLabel(57, 52, LabelHue, "Attribute");
        builder.AddLabel(244, 52, LabelHue, "Current");
        builder.AddLabel(330, 52, LabelHue, "Limit");
        builder.AddLabel(414, 52, LabelHue, "Cost");

        var rowY = 78;
        for (var i = 0; i < upgrades.Length; i++)
        {
            var upgrade = upgrades[i];
            if (upgrade.Category != group)
            {
                continue;
            }

            var available = weapon.QuoteWeaponUpgrade(i) != null;
            var hue = available ? LabelHue : MutedHue;
            if (available)
            {
                builder.AddButton(24, rowY, 4005, 4007, FirstUpgradeButton + i);
            }

            builder.AddLabel(57, rowY, hue, upgrade.Name);
            builder.AddLabel(244, rowY, hue, upgrade.DisplayValue(upgrade.Read(weapon)));
            builder.AddLabel(330, rowY, hue, upgrade.DisplayValue(upgrade.Maximum));
            builder.AddLabel(414, rowY, hue, $"{upgrade.Cost * weapon.WeaponLevelingCostMultiplier}");
            rowY += 22;
        }

        AddChoice(ref builder, 24, rowY + 14, BackButton, "Back");
    }

    private void BuildConfirmation(ref DynamicGumpBuilder builder, WeaponUpgradeQuote pending)
    {
        var upgrade = WeaponUpgradeCatalog.Upgrades[pending.Index];
        AddFrame(ref builder, 174);
        builder.AddLabel(24, 19, LabelHue, $"Increase {upgrade.Name:L}?");
        builder.AddLabel(24, 51, LabelHue,
            $"{upgrade.DisplayValue(pending.Current)} to {upgrade.DisplayValue(pending.Next)}");
        builder.AddLabel(24, 83, LabelHue, $"Cost: {pending.Cost} points");
        AddChoice(ref builder, 24, 124, ConfirmButton, "Confirm");
        AddChoice(ref builder, 230, 124, BackButton, "Back");
    }

    private static void AddFrame(ref DynamicGumpBuilder builder, int height)
    {
        builder.AddBackground(0, 0, 485, height, 9270);
        builder.AddAlphaRegion(12, 12, 461, height - 24);
    }

    private static void AddChoice(ref DynamicGumpBuilder builder, int x, int y, int button, string label)
    {
        builder.AddButton(x, y, 4005, 4007, button);
        builder.AddLabel(x + 36, y, LabelHue, label);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile != owner || !weapon.CanManageWeaponLeveling(owner) || !weapon.IsWeaponLevelingEnabled)
        {
            return;
        }

        if (category is not { } group)
        {
            var selected = info.ButtonID - FirstCategoryButton;
            if (selected >= 0 && selected < _categories.Length)
            {
                DisplayTo(owner, weapon, _categories[selected]);
            }

            return;
        }

        if (info.ButtonID == BackButton)
        {
            DisplayTo(owner, weapon, quote == null ? null : group);
            return;
        }

        if (quote is { } pending)
        {
            if (info.ButtonID == ConfirmButton)
            {
                if (!weapon.TrySpendWeaponLevelingPoints(owner, pending))
                {
                    owner.SendMessage("The upgrade changed. Review the current values.");
                }

                DisplayTo(owner, weapon, group);
            }

            return;
        }

        var index = info.ButtonID - FirstUpgradeButton;
        var upgrades = WeaponUpgradeCatalog.Upgrades;
        if (index >= 0 && index < upgrades.Length && upgrades[index].Category == group)
        {
            if (weapon.QuoteWeaponUpgrade(index) is { } next)
            {
                DisplayTo(owner, weapon, group, next);
            }
            else
            {
                owner.SendMessage("That upgrade is not available.");
                DisplayTo(owner, weapon, group);
            }
        }
    }
}
