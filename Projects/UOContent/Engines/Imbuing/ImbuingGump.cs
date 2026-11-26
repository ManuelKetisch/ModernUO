using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Items;
using Server.Network;
using Server.Targeting;

namespace Server.Engines.Imbuing;

public sealed class ImbuingGump : DynamicGump
{
    private const int LabelHue = 0x480;
    private const int AccentHue = 0x35;
    private const int GoodHue = 0x44;
    private const int WarnHue = 0x22;
    private const int MutedHue = 0x3B2;
    private const int PageSize = 10;

    private const int BtnImbueItem = 1;
    private const int BtnUnravelItem = 2;
    private const int BtnUnravelBag = 3;
    private const int BtnBackToHub = 4;
    private const int BtnPrevPage = 5;
    private const int BtnNextPage = 6;
    private const int BtnDecLarge = 7;
    private const int BtnDecSmall = 8;
    private const int BtnIncSmall = 9;
    private const int BtnIncLarge = 10;
    private const int BtnMax = 11;
    private const int BtnExecuteImbue = 12;

    private const int BtnCategoryAll = 100;
    private const int BtnCategoryBase = 101;
    private const int BtnModBase = 200;

    private static readonly ImbuingCategory[] _categories = Enum.GetValues<ImbuingCategory>();

    private readonly Mobile _from;
    private readonly Item _targetItem;
    private readonly ImbuingCategory? _categoryFilter;
    private readonly int _page;
    private readonly int _selectedModId;
    private readonly int _targetIntensity;
    private readonly string _statusMessage;
    private readonly bool _statusIsError;

    public override bool Singleton => true;

    public static void Configure()
    {
        CommandSystem.Register("Imbue", AccessLevel.Player, Imbue_OnCommand);
    }

    [Usage("Imbue")]
    [Description("Opens the Soulforge Imbuing and Magical Unraveling menu.")]
    private static void Imbue_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        if (from == null || from.Deleted || !from.Alive)
        {
            return;
        }

        if (!ImbuingEngine.IsNearSoulForgeOrForge(from))
        {
            from.SendMessage("You must be near a soulforge, or near an anvil and forge, to use Imbuing.");
            return;
        }

        DisplayTo(from);
    }

    public static void DisplayTo(
        Mobile from,
        Item targetItem = null,
        ImbuingCategory? categoryFilter = null,
        int page = 0,
        int selectedModId = 0,
        int targetIntensity = 0,
        string statusMessage = null,
        bool statusIsError = false
    )
    {
        if (from?.NetState == null || from.Deleted || !from.Alive)
        {
            return;
        }

        if (targetItem != null &&
            (targetItem.Deleted ||
             from.Backpack == null ||
             !targetItem.IsChildOf(from.Backpack) ||
             targetItem.InSecureTrade ||
             !targetItem.IsAccessibleTo(from) ||
             !ImbuingEngine.CanImbue(targetItem, out _)))
        {
            targetItem = null;
            categoryFilter = null;
            page = 0;
            selectedModId = 0;
            targetIntensity = 0;
        }

        from.SendGump(
            new ImbuingGump(
                from,
                targetItem,
                categoryFilter,
                Math.Max(0, page),
                selectedModId,
                targetIntensity,
                statusMessage,
                statusIsError
            )
        );
    }

    private ImbuingGump(
        Mobile from,
        Item targetItem,
        ImbuingCategory? categoryFilter,
        int page,
        int selectedModId,
        int targetIntensity,
        string statusMessage,
        bool statusIsError
    ) : base(60, 50)
    {
        _from = from;
        _targetItem = targetItem;
        _categoryFilter = categoryFilter;
        _page = page;
        _selectedModId = selectedModId;
        _targetIntensity = targetIntensity;
        _statusMessage = statusMessage;
        _statusIsError = statusIsError;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();

        if (_targetItem == null)
        {
            BuildMainHub(ref builder);
        }
        else
        {
            BuildImbueView(ref builder);
        }
    }

    private void BuildMainHub(ref DynamicGumpBuilder builder)
    {
        const int width = 500;
        const int height = 410;

        builder.AddBackground(0, 0, width, height, 9270);
        builder.AddAlphaRegion(12, 12, width - 24, height - 24);

        builder.AddLabel(24, 20, AccentHue, "Soulforge & Magical Imbuing");
        builder.AddLabel(340, 20, LabelHue, $"Imbuing: {_from.Skills.Imbuing.Value:F1}");

        var nearSoulForge = ImbuingEngine.IsNearSoulForge(_from);
        if (nearSoulForge)
        {
            builder.AddLabel(24, 46, GoodHue, "Station: Soulforge (+10% imbuing success bonus)");
        }
        else if (ImbuingEngine.IsNearSoulForgeOrForge(_from))
        {
            builder.AddLabel(24, 46, LabelHue, "Station: Anvil & Forge (craft a Soulforge for +10% bonus)");
        }
        else
        {
            builder.AddLabel(24, 46, WarnHue, "Station: Move near a Soulforge or Anvil & Forge");
        }

        // Owned essence summary
        var pack = _from.Backpack;
        var residue = pack?.GetAmount(typeof(MagicalResidue)) ?? 0;
        var essence = pack?.GetAmount(typeof(EnchantedEssence)) ?? 0;
        var relics = pack?.GetAmount(typeof(RelicFragment)) ?? 0;

        builder.AddLabel(24, 78, AccentHue, "Imbuing Ingredients in Backpack");
        builder.AddItem(24, 102, 0x2DB1);
        builder.AddLabel(68, 108, LabelHue, $"Residue: {residue}");

        builder.AddItem(180, 102, 0x2DB2);
        builder.AddLabel(224, 108, LabelHue, $"Essence: {essence}");

        builder.AddItem(336, 102, 0x2DB3);
        builder.AddLabel(380, 108, LabelHue, $"Relics: {relics}");

        // Main actions
        AddActionRow(ref builder, 24, 154, BtnImbueItem, "Imbue Item", "Add or upgrade properties within item weight budget");
        AddActionRow(ref builder, 24, 204, BtnUnravelItem, "Unravel Single Item", "Salvage one magic item into Residue, Essence, or Relics");
        AddActionRow(ref builder, 24, 254, BtnUnravelBag, "Unravel Bag", "Safely unravel all eligible magic items in a bag");

        if (!string.IsNullOrEmpty(_statusMessage))
        {
            builder.AddHtml(24, 304, width - 48, 44, _statusMessage, _statusIsError ? "#FF6666" : "#66FF88");
        }

        builder.AddLabel(24, 362, MutedHue, "Budgets: Exceptional Crafted 550 | Crafted 500 | Looted 450 (Max 5 props)");
    }

    private void BuildImbueView(ref DynamicGumpBuilder builder)
    {
        const int width = 660;
        const int height = 500;

        builder.AddBackground(0, 0, width, height, 9270);
        builder.AddAlphaRegion(12, 12, width - 24, height - 24);

        var itemName = GetItemDisplayName(_targetItem);
        var currentWeight = ImbuingCatalog.ComputeTotalWeight(_targetItem);
        var maxWeight = ImbuingCatalog.GetMaxWeight(_targetItem);
        var tierLabel = ImbuingCatalog.GetBudgetTierLabel(_targetItem);
        var propCount = ImbuingCatalog.CountProperties(_targetItem);
        var timesImbued = ImbuingPersistence.GetTimesImbued(_targetItem);

        builder.AddLabelCropped(24, 18, 390, 22, AccentHue, itemName);
        builder.AddLabel(485, 18, LabelHue, $"Imbuing: {_from.Skills.Imbuing.Value:F1}");

        builder.AddLabel(24, 42, LabelHue, $"Weight: {currentWeight} / {maxWeight} ({tierLabel})");
        builder.AddLabel(355, 42, LabelHue, $"Props: {propCount} / {ImbuingCatalog.MaxProperties}");
        builder.AddLabel(485, 42, LabelHue, $"Imbued: {timesImbued} / {ImbuingCatalog.MaxImbueUses}");

        // Category filter bar
        var catX = 24;
        const int catY = 72;
        AddFilterButton(ref builder, catX, catY, BtnCategoryAll, "All", !_categoryFilter.HasValue);
        catX += 58;

        for (var i = 0; i < _categories.Length; i++)
        {
            var cat = _categories[i];
            if (ImbuingCatalog.GetEligibleMods(_targetItem, cat).Count == 0)
            {
                continue;
            }

            var catName = ImbuingCatalog.GetCategoryName(cat);
            var selected = _categoryFilter.HasValue && _categoryFilter.Value == cat;
            AddFilterButton(ref builder, catX, catY, BtnCategoryBase + i, catName, selected);
            catX += Math.Max(74, catName.Length * 8 + 24);
        }

        // Eligible mods list (left pane)
        var mods = ImbuingCatalog.GetEligibleMods(_targetItem, _categoryFilter);
        var totalPages = Math.Max(1, (mods.Count + PageSize - 1) / PageSize);
        var clampedPage = Math.Clamp(_page, 0, totalPages - 1);
        var startIndex = clampedPage * PageSize;
        var endIndex = Math.Min(mods.Count, startIndex + PageSize);

        var selectedMod = ResolveSelectedMod(mods);

        builder.AddLabel(24, 104, AccentHue, "Eligible Properties");
        var rowY = 128;

        for (var i = startIndex; i < endIndex; i++)
        {
            var mod = mods[i];
            var isSelected = selectedMod != null && mod.Id == selectedMod.Id;
            var cur = ImbuingCatalog.GetCurrentValue(_targetItem, mod);
            var max = mod.GetMaxValue(_targetItem);

            builder.AddButton(24, rowY, isSelected ? 4006 : 4005, 4007, BtnModBase + mod.Id);
            var hue = isSelected ? AccentHue : cur > 0 ? GoodHue : LabelHue;
            builder.AddLabelCropped(58, rowY, 178, 20, hue, mod.Name);

            if (mod.IsBoolean)
            {
                builder.AddLabel(242, rowY, hue, cur > 0 ? "Active" : "Off");
            }
            else
            {
                builder.AddLabel(242, rowY, hue, $"{cur}/{max}");
            }

            rowY += 26;
        }

        // Pagination controls
        if (clampedPage > 0)
        {
            builder.AddButton(24, 392, 4014, 4016, BtnPrevPage);
            builder.AddLabel(58, 392, LabelHue, "Prev");
        }

        builder.AddLabel(124, 392, MutedHue, $"Page {clampedPage + 1} / {totalPages}");

        if (clampedPage + 1 < totalPages)
        {
            builder.AddButton(220, 392, 4005, 4007, BtnNextPage);
            builder.AddLabel(254, 392, LabelHue, "Next");
        }

        // Right detail & imbuing pane
        const int rightX = 330;
        builder.AddLabel(rightX, 104, AccentHue, "Selected Property");

        if (selectedMod != null)
        {
            var currentVal = ImbuingCatalog.GetCurrentValue(_targetItem, selectedMod);
            var maxVal = selectedMod.GetMaxValue(_targetItem);
            var targetVal = ClampTargetIntensity(selectedMod, _targetItem, currentVal, _targetIntensity);

            builder.AddLabelCropped(rightX, 128, 305, 22, LabelHue, selectedMod.Name);

            if (selectedMod.IsBoolean)
            {
                builder.AddLabel(rightX, 152, LabelHue, currentVal > 0 ? "Current: Active (max)" : "Current: None -> Active");
            }
            else
            {
                var curFmt = selectedMod.FormatValue(currentVal);
                var tgtFmt = selectedMod.FormatValue(targetVal);
                var maxFmt = selectedMod.FormatValue(maxVal);
                builder.AddLabel(rightX, 152, LabelHue, $"Current: {curFmt}   Target: {tgtFmt} (Max {maxFmt})");

                var step = Math.Max(1, selectedMod.Step);
                var largeStep = Math.Max(step, ((10 + step - 1) / step) * step);

                builder.AddButton(rightX, 180, 4005, 4007, BtnDecLarge);
                builder.AddLabel(rightX + 32, 180, LabelHue, $"-{largeStep}");

                builder.AddButton(rightX + 64, 180, 4005, 4007, BtnDecSmall);
                builder.AddLabel(rightX + 96, 180, LabelHue, $"-{step}");

                builder.AddButton(rightX + 122, 180, 4005, 4007, BtnIncSmall);
                builder.AddLabel(rightX + 154, 180, LabelHue, $"+{step}");

                builder.AddButton(rightX + 180, 180, 4005, 4007, BtnIncLarge);
                builder.AddLabel(rightX + 212, 180, LabelHue, $"+{largeStep}");

                builder.AddButton(rightX + 244, 180, 4005, 4007, BtnMax);
                builder.AddLabel(rightX + 276, 180, LabelHue, "Max");
            }

            var projectedWeight = ImbuingCatalog.ComputeProjectedWeight(_targetItem, selectedMod, targetVal);
            var withinBudget = projectedWeight <= maxWeight;

            builder.AddLabel(
                rightX,
                214,
                withinBudget ? LabelHue : WarnHue,
                $"Projected Weight: {projectedWeight} / {maxWeight}"
            );

            var consumesSlot = currentVal <= 0 && selectedMod.ConsumesPropertySlot(_targetItem);
            var projectedProps = consumesSlot ? propCount + 1 : propCount;
            var slotsOk = !consumesSlot || propCount < ImbuingCatalog.MaxProperties;

            if (currentVal > 0)
            {
                builder.AddLabel(rightX, 238, GoodHue, "Slot: Upgrades existing property");
            }
            else if (!selectedMod.ConsumesPropertySlot(_targetItem))
            {
                builder.AddLabel(rightX, 238, GoodHue, "Slot: Resistance (no property slot used)");
            }
            else
            {
                builder.AddLabel(
                    rightX,
                    238,
                    slotsOk ? LabelHue : WarnHue,
                    $"Projected Props: {projectedProps} / {ImbuingCatalog.MaxProperties}"
                );
            }

            var cost = ImbuingCatalog.GetImbueCost(selectedMod, _targetItem, targetVal);
            var pack = _from.Backpack;
            var ownedPrimary = pack?.GetAmount(cost.PrimaryType) ?? 0;
            var ownedGem = pack?.GetAmount(cost.GemType) ?? 0;
            var primaryName = ImbuingCatalog.GetIngredientDisplayName(cost.PrimaryType);
            var gemName = ImbuingCatalog.GetIngredientDisplayName(cost.GemType);

            builder.AddLabel(rightX, 268, AccentHue, "Required Ingredients");
            builder.AddLabel(
                rightX,
                292,
                ownedPrimary >= cost.PrimaryAmount ? GoodHue : WarnHue,
                $"{primaryName}: {cost.PrimaryAmount} (have {ownedPrimary})"
            );
            builder.AddLabel(
                rightX,
                314,
                ownedGem >= cost.GemAmount ? GoodHue : WarnHue,
                $"{gemName}: {cost.GemAmount} (have {ownedGem})"
            );

            var chancePercent = (int)Math.Round(ImbuingEngine.GetImbueSuccessChance(_from, _targetItem, selectedMod, targetVal) * 100.0);
            var soulForgeActive = ImbuingEngine.IsNearSoulForge(_from);
            if (soulForgeActive)
            {
                builder.AddLabel(rightX, 344, GoodHue, $"Success Chance: {chancePercent}% (+10% Soulforge)");
            }
            else
            {
                builder.AddLabel(rightX, 344, LabelHue, $"Success Chance: {chancePercent}%");
            }

            var canExecute = currentVal < maxVal && targetVal > currentVal && withinBudget && slotsOk &&
                             ownedPrimary >= cost.PrimaryAmount && ownedGem >= cost.GemAmount;

            builder.AddButton(rightX, 376, 4005, 4007, BtnExecuteImbue);
            builder.AddLabel(rightX + 36, 376, canExecute ? GoodHue : MutedHue, "Imbue Property");
        }
        else
        {
            builder.AddLabel(rightX, 128, MutedHue, "No eligible properties in this category.");
        }

        // Bottom controls & full-width wrapped status bar
        builder.AddButton(24, 418, 4014, 4016, BtnBackToHub);
        builder.AddLabel(58, 418, LabelHue, "Main Menu");

        builder.AddButton(160, 418, 4005, 4007, BtnImbueItem);
        builder.AddLabel(194, 418, LabelHue, "Change Item");

        if (!string.IsNullOrEmpty(_statusMessage))
        {
            builder.AddHtml(24, 448, width - 48, 40, _statusMessage, _statusIsError ? "#FF6666" : "#66FF88");
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var mobile = sender.Mobile;
        if (mobile != _from || mobile.Deleted || !mobile.Alive)
        {
            return;
        }

        var buttonId = info.ButtonID;
        if (buttonId == 0)
        {
            return;
        }

        if (!ImbuingEngine.IsNearSoulForgeOrForge(mobile))
        {
            mobile.SendMessage("You must be near a soulforge, or near an anvil and forge, to use Imbuing.");
            return;
        }

        if (buttonId == BtnBackToHub)
        {
            DisplayTo(mobile);
            return;
        }

        switch (buttonId)
        {
            case BtnImbueItem:
            {
                mobile.SendMessage("Select the item in your backpack that you wish to imbue.");
                mobile.Target = new ImbueItemTarget();
                return;
            }
            case BtnUnravelItem:
            {
                mobile.SendMessage("Select the magic item in your backpack to unravel.");
                mobile.Target = new UnravelSingleTarget();
                return;
            }
            case BtnUnravelBag:
            {
                mobile.SendMessage("Select a bag inside your backpack to unravel its eligible magic items.");
                mobile.Target = new UnravelBagTarget();
                return;
            }
        }

        if (_targetItem == null || _targetItem.Deleted ||
            mobile.Backpack == null ||
            !_targetItem.IsChildOf(mobile.Backpack) ||
            _targetItem.InSecureTrade ||
            !_targetItem.IsAccessibleTo(mobile))
        {
            DisplayTo(mobile, statusMessage: "The item must remain accessible in your backpack.", statusIsError: true);
            return;
        }

        if (!ImbuingEngine.CanImbue(_targetItem, out var invalidReason))
        {
            DisplayTo(mobile, statusMessage: invalidReason ?? "Item is no longer valid.", statusIsError: true);
            return;
        }

        if (buttonId == BtnCategoryAll)
        {
            DisplayTo(mobile, _targetItem, null, 0);
            return;
        }

        if (buttonId >= BtnCategoryBase && buttonId < BtnCategoryBase + _categories.Length)
        {
            var cat = _categories[buttonId - BtnCategoryBase];
            DisplayTo(mobile, _targetItem, cat, 0);
            return;
        }

        if (buttonId == BtnPrevPage)
        {
            DisplayTo(mobile, _targetItem, _categoryFilter, Math.Max(0, _page - 1), _selectedModId, _targetIntensity);
            return;
        }

        if (buttonId == BtnNextPage)
        {
            DisplayTo(mobile, _targetItem, _categoryFilter, _page + 1, _selectedModId, _targetIntensity);
            return;
        }

        var mods = ImbuingCatalog.GetEligibleMods(_targetItem, _categoryFilter);

        if (buttonId >= BtnModBase)
        {
            var modId = buttonId - BtnModBase;
            var picked = ImbuingCatalog.GetMod(modId);
            if (picked != null && picked.IsEligibleFor(_targetItem))
            {
                var cur = ImbuingCatalog.GetCurrentValue(_targetItem, picked);
                var nextDefault = ClampTargetIntensity(picked, _targetItem, cur, 0);
                DisplayTo(mobile, _targetItem, _categoryFilter, _page, picked.Id, nextDefault);
                return;
            }
        }

        var activeMod = ResolveSelectedMod(mods);
        if (activeMod == null)
        {
            DisplayTo(mobile, _targetItem, _categoryFilter, _page);
            return;
        }

        var currentVal = ImbuingCatalog.GetCurrentValue(_targetItem, activeMod);
        var maxVal = activeMod.GetMaxValue(_targetItem);
        var currentTarget = ClampTargetIntensity(activeMod, _targetItem, currentVal, _targetIntensity);
        var step = Math.Max(1, activeMod.Step);
        var largeStep = Math.Max(step, ((10 + step - 1) / step) * step);

        switch (buttonId)
        {
            case BtnDecLarge:
            {
                var next = ClampTargetIntensity(activeMod, _targetItem, currentVal, currentTarget - largeStep);
                DisplayTo(mobile, _targetItem, _categoryFilter, _page, activeMod.Id, next);
                return;
            }
            case BtnDecSmall:
            {
                var next = ClampTargetIntensity(activeMod, _targetItem, currentVal, currentTarget - step);
                DisplayTo(mobile, _targetItem, _categoryFilter, _page, activeMod.Id, next);
                return;
            }
            case BtnIncSmall:
            {
                var next = ClampTargetIntensity(activeMod, _targetItem, currentVal, currentTarget + step);
                DisplayTo(mobile, _targetItem, _categoryFilter, _page, activeMod.Id, next);
                return;
            }
            case BtnIncLarge:
            {
                var next = ClampTargetIntensity(activeMod, _targetItem, currentVal, currentTarget + largeStep);
                DisplayTo(mobile, _targetItem, _categoryFilter, _page, activeMod.Id, next);
                return;
            }
            case BtnMax:
            {
                var next = ClampTargetIntensity(activeMod, _targetItem, currentVal, maxVal);
                DisplayTo(mobile, _targetItem, _categoryFilter, _page, activeMod.Id, next);
                return;
            }
            case BtnExecuteImbue:
            {
                var ok = ImbuingEngine.TryImbue(mobile, _targetItem, activeMod.Id, currentTarget, out var msg);
                mobile.SendMessage(ok ? GoodHue : WarnHue, msg);
                var updatedCur = ImbuingCatalog.GetCurrentValue(_targetItem, activeMod);
                var updatedTarget = ClampTargetIntensity(activeMod, _targetItem, updatedCur, currentTarget);
                DisplayTo(mobile, _targetItem, _categoryFilter, _page, activeMod.Id, updatedTarget, msg, !ok);
                return;
            }
        }
    }

    private ImbuingMod ResolveSelectedMod(List<ImbuingMod> mods)
    {
        if (mods.Count == 0)
        {
            return null;
        }

        if (_selectedModId > 0)
        {
            for (var i = 0; i < mods.Count; i++)
            {
                if (mods[i].Id == _selectedModId)
                {
                    return mods[i];
                }
            }
        }

        return mods[0];
    }

    private static int ClampTargetIntensity(ImbuingMod mod, Item item, int currentVal, int requested)
    {
        var maxVal = mod.GetMaxValue(item);
        if (mod.IsBoolean)
        {
            return 1;
        }

        var step = Math.Max(1, mod.Step);
        int minAllowed;

        if (currentVal < mod.MinValue)
        {
            minAllowed = mod.MinValue;
        }
        else if (currentVal >= maxVal)
        {
            return maxVal;
        }
        else
        {
            var stepsAboveMin = (currentVal - mod.MinValue) / step + 1;
            minAllowed = Math.Min(maxVal, mod.MinValue + stepsAboveMin * step);
        }

        if (requested <= 0)
        {
            return minAllowed;
        }

        var clamped = Math.Clamp(requested, minAllowed, maxVal);
        if (clamped < maxVal && step > 1)
        {
            var offset = clamped - mod.MinValue;
            var aligned = mod.MinValue + (offset / step) * step;
            if (aligned < minAllowed)
            {
                aligned = minAllowed;
            }

            clamped = Math.Clamp(aligned, minAllowed, maxVal);
        }

        return clamped;
    }

    private static string GetItemDisplayName(Item item)
    {
        if (!string.IsNullOrWhiteSpace(item.Name))
        {
            return item.Name;
        }

        var localized = Localization.GetText(item.LabelNumber);
        if (!string.IsNullOrWhiteSpace(localized))
        {
            return localized;
        }

        return item.GetType().Name;
    }

    private static void AddActionRow(
        ref DynamicGumpBuilder builder,
        int x,
        int y,
        int buttonId,
        string title,
        string subtitle
    )
    {
        builder.AddButton(x, y + 4, 4005, 4007, buttonId);
        builder.AddLabel(x + 36, y, AccentHue, title);
        builder.AddLabel(x + 36, y + 18, MutedHue, subtitle);
    }

    private static void AddFilterButton(
        ref DynamicGumpBuilder builder,
        int x,
        int y,
        int buttonId,
        string label,
        bool selected
    )
    {
        builder.AddButton(x, y + 2, selected ? 2153 : 2151, 2153, buttonId);
        builder.AddLabel(x + 28, y + 4, selected ? AccentHue : LabelHue, label);
    }

    private sealed class ImbueItemTarget : Target
    {
        public ImbueItemTarget() : base(3, false, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from == null || from.Deleted || !from.Alive)
            {
                return;
            }

            if (!ImbuingEngine.IsNearSoulForgeOrForge(from))
            {
                from.SendMessage("You must be near a soulforge, or near an anvil and forge, to use Imbuing.");
                return;
            }

            if (targeted is not Item item)
            {
                DisplayTo(from, statusMessage: "You must target an item in your backpack.", statusIsError: true);
                return;
            }

            if (from.Backpack == null || !item.IsChildOf(from.Backpack) || item.InSecureTrade || !item.IsAccessibleTo(from))
            {
                DisplayTo(from, statusMessage: "The item must be accessible in your backpack to imbue.", statusIsError: true);
                return;
            }

            if (!ImbuingEngine.CanImbue(item, out var reason))
            {
                DisplayTo(from, statusMessage: reason, statusIsError: true);
                return;
            }

            DisplayTo(from, item);
        }

        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
        {
            if (cancelType == TargetCancelType.Canceled && from is { Deleted: false, Alive: true })
            {
                DisplayTo(from);
            }
        }
    }

    private sealed class UnravelSingleTarget : Target
    {
        public UnravelSingleTarget() : base(3, false, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from == null || from.Deleted || !from.Alive)
            {
                return;
            }

            if (!ImbuingEngine.IsNearSoulForgeOrForge(from))
            {
                from.SendMessage("You must be near a soulforge, or near an anvil and forge, to use Imbuing.");
                return;
            }

            if (targeted is not Item item)
            {
                DisplayTo(from, statusMessage: "Target a magic item in your backpack.", statusIsError: true);
                return;
            }

            if (from.Backpack == null || !item.IsChildOf(from.Backpack) || item.InSecureTrade || !item.IsAccessibleTo(from))
            {
                DisplayTo(from, statusMessage: "The item must be accessible in your backpack to unravel.", statusIsError: true);
                return;
            }

            var ok = ImbuingEngine.TryUnravel(from, item, out _, out _, out var msg);
            from.SendMessage(ok ? GoodHue : WarnHue, msg);
            DisplayTo(from, statusMessage: msg, statusIsError: !ok);
        }

        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
        {
            if (cancelType == TargetCancelType.Canceled && from is { Deleted: false, Alive: true })
            {
                DisplayTo(from);
            }
        }
    }

    private sealed class UnravelBagTarget : Target
    {
        public UnravelBagTarget() : base(3, false, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from == null || from.Deleted || !from.Alive)
            {
                return;
            }

            if (!ImbuingEngine.IsNearSoulForgeOrForge(from))
            {
                from.SendMessage("You must be near a soulforge, or near an anvil and forge, to use Imbuing.");
                return;
            }

            if (targeted is not Container bag)
            {
                DisplayTo(from, statusMessage: "Target a bag inside your backpack.", statusIsError: true);
                return;
            }

            if (from.Backpack == null || bag == from.Backpack || !bag.IsChildOf(from.Backpack) ||
                bag.InSecureTrade || !bag.IsAccessibleTo(from))
            {
                DisplayTo(
                    from,
                    statusMessage: "Target an accessible separate bag inside your backpack (not your main pack).",
                    statusIsError: true
                );
                return;
            }

            var result = ImbuingEngine.TryUnravelContainer(from, bag);
            var summary = result.UnraveledCount == 0
                ? $"No eligible items unraveled ({result.SkippedCount} skipped)."
                : $"Unraveled {result.UnraveledCount} items ({result.ResidueAmount} Res, {result.EssenceAmount} Ess, {result.RelicAmount} Rel).";
            DisplayTo(from, statusMessage: summary, statusIsError: result.UnraveledCount == 0);
        }

        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
        {
            if (cancelType == TargetCancelType.Canceled && from is { Deleted: false, Alive: true })
            {
                DisplayTo(from);
            }
        }
    }
}
