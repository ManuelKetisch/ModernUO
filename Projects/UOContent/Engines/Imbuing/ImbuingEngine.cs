using System;
using Server.Collections;
using Server.Engines.Craft;
using Server.Factions;
using Server.Items;

namespace Server.Engines.Imbuing;

public readonly record struct UnravelBatchResult(
    int UnraveledCount,
    int SkippedCount,
    int ResidueAmount,
    int EssenceAmount,
    int RelicAmount
);

public static class ImbuingEngine
{
    public const int StationRange = 3;
    public const int EssenceWeightThreshold = 200;
    public const int RelicWeightThreshold = 450;
    public const int MaxUnravelableSpellbookSpells = 16;
    public const double SoulForgeSuccessBonus = 0.10;

    public static bool IsNearSoulForge(Mobile from)
    {
        if (from == null || from.Deleted)
        {
            return false;
        }

        var map = from.Map;
        if (map == null || map == Map.Internal)
        {
            return false;
        }

        foreach (var item in map.GetItemsInRange<SoulForge>(from.Location, StationRange))
        {
            if (!item.Deleted && item.Parent == null &&
                from.Z + 16 >= item.Z && item.Z + 16 >= from.Z &&
                from.InLOS(item))
            {
                return true;
            }
        }

        for (var x = -StationRange; x <= StationRange; x++)
        {
            for (var y = -StationRange; y <= StationRange; y++)
            {
                foreach (var tile in map.Tiles.GetStaticAndMultiTiles(from.X + x, from.Y + y))
                {
                    if (IsSoulForgeTileId(tile.ID) &&
                        from.Z + 16 >= tile.Z && tile.Z + 16 >= from.Z &&
                        from.InLOS(new Point3D(from.X + x, from.Y + y, tile.Z + tile.Height / 2 + 1)))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public static bool IsNearSoulForgeOrForge(Mobile from)
    {
        if (from == null || from.Deleted)
        {
            return false;
        }

        if (IsNearSoulForge(from))
        {
            return true;
        }

        DefBlacksmithy.CheckAnvilAndForge(from, StationRange, out var anvil, out var forge);
        return anvil && forge;
    }

    public static bool CanUnravel(Item item, out string reason)
    {
        if (item == null || item.Deleted)
        {
            reason = "That item is no longer valid.";
            return false;
        }

        if (item is Container)
        {
            reason = "Containers cannot be unraveled.";
            return false;
        }

        if (!item.Movable || item.IsLockedDown || item.IsSecure)
        {
            reason = "Locked or immovable items cannot be unraveled.";
            return false;
        }

        if (item.LootType != LootType.Regular)
        {
            reason = "Blessed, newbied, or cursed items cannot be unraveled.";
            return false;
        }

        if (item.Insured || item.BlessedFor != null)
        {
            reason = "Insured or blessed items cannot be unraveled.";
            return false;
        }

        if (item.Nontransferable || item.QuestItem)
        {
            reason = "Quest or nontransferable items cannot be unraveled.";
            return false;
        }

        if (FactionItem.Find(item) != null)
        {
            reason = "Faction items cannot be unraveled.";
            return false;
        }

        if (ItemRarity.IsFixedArtifact(item))
        {
            reason = "Artifacts cannot be unraveled.";
            return false;
        }

        switch (item)
        {
            case Fists:
            {
                reason = "That item cannot be unraveled.";
                return false;
            }
            case BaseWeapon weapon:
            {
                if (weapon.ArtifactRarity > 0)
                {
                    reason = "Artifacts cannot be unraveled.";
                    return false;
                }

                if (weapon.IsWeaponLevelingEnabled)
                {
                    reason = "Leveled weapons cannot be unraveled.";
                    return false;
                }

                break;
            }
            case BaseArmor armor:
            {
                if (armor.ArtifactRarity > 0)
                {
                    reason = "Artifacts cannot be unraveled.";
                    return false;
                }

                break;
            }
            case BaseClothing clothing:
            {
                if (clothing.ArtifactRarity > 0)
                {
                    reason = "Artifacts cannot be unraveled.";
                    return false;
                }

                break;
            }
            case BaseJewel jewel:
            {
                if (jewel.ArtifactRarity > 0)
                {
                    reason = "Artifacts cannot be unraveled.";
                    return false;
                }

                break;
            }
            case Spellbook spellbook:
            {
                if (spellbook.SpellCount > MaxUnravelableSpellbookSpells)
                {
                    reason = $"Spellbooks with more than {MaxUnravelableSpellbookSpells} spells cannot be unraveled.";
                    return false;
                }

                break;
            }
            default:
            {
                reason = "Only magic weapons, armor, clothing, jewelry, and spellbooks can be unraveled.";
                return false;
            }
        }

        var weight = ImbuingCatalog.ComputeTotalWeight(item);
        if (weight <= 0)
        {
            reason = "That item has no magical properties to unravel.";
            return false;
        }

        reason = null;
        return true;
    }

    public static bool TryUnravel(Mobile from, Item item, out Type ingredientType, out int amount) =>
        TryUnravel(from, item, out ingredientType, out amount, out _);

    public static bool TryUnravel(
        Mobile from,
        Item item,
        out Type ingredientType,
        out int amount,
        out string message
    )
    {
        ingredientType = null;
        amount = 0;

        if (!ValidateBackpackAndStation(from, item, out message))
        {
            return false;
        }

        if (!CanUnravel(item, out message))
        {
            return false;
        }

        var weight = ImbuingCatalog.ComputeTotalWeight(item);
        var imbuingSkill = from.Skills.Imbuing.Value;

        double minSkill;
        double maxSkill;
        Item ingredient;

        if (weight < EssenceWeightThreshold)
        {
            ingredientType = typeof(MagicalResidue);
            var baseQty = weight >= 140 ? 3 : weight >= 80 ? 2 : 1;
            var skillBonus = imbuingSkill >= 60.0 ? 1 : 0;
            amount = Math.Clamp(baseQty + skillBonus, 1, 4);
            minSkill = 0.0;
            maxSkill = 60.0;
            ingredient = new MagicalResidue(amount);
        }
        else if (weight < RelicWeightThreshold)
        {
            ingredientType = typeof(EnchantedEssence);
            var baseQty = weight >= 360 ? 3 : weight >= 280 ? 2 : 1;
            var skillBonus = imbuingSkill >= 85.0 ? 1 : 0;
            amount = Math.Clamp(baseQty + skillBonus, 1, 4);
            minSkill = 40.0;
            maxSkill = 95.0;
            ingredient = new EnchantedEssence(amount);
        }
        else
        {
            ingredientType = typeof(RelicFragment);
            var baseQty = weight >= 650 ? 3 : weight >= 540 ? 2 : 1;
            var skillBonus = imbuingSkill >= 105.0 ? 1 : 0;
            amount = Math.Clamp(baseQty + skillBonus, 1, 4);
            minSkill = 75.0;
            maxSkill = 120.0;
            ingredient = new RelicFragment(amount);
        }

        from.CheckSkill(SkillName.Imbuing, minSkill, maxSkill);

        ImbuingPersistence.Clear(item);
        item.Delete();

        if (!from.AddToBackpack(ingredient))
        {
            ingredient.MoveToWorld(from.Location, from.Map);
        }

        if (from.Map != null && from.Map != Map.Internal)
        {
            from.PlaySound(0x1ED);
            from.FixedParticles(0x376A, 9, 32, 5005, EffectLayer.Waist);
        }

        var ingredientName = ImbuingCatalog.GetIngredientDisplayName(ingredientType);
        message = $"You unravel the item into {amount} {ingredientName}.";
        return true;
    }

    public static UnravelBatchResult TryUnravelContainer(Mobile from, Container bag)
    {
        if (from == null || from.Deleted || !from.Alive || bag == null || bag.Deleted)
        {
            return default;
        }

        if (!IsNearSoulForgeOrForge(from))
        {
            from.SendMessage("You must be near a soulforge, or near an anvil and forge, to unravel items.");
            return default;
        }

        if (from.Backpack == null ||
            (bag != from.Backpack && !bag.IsChildOf(from.Backpack)) ||
            bag.InSecureTrade ||
            !bag.IsAccessibleTo(from))
        {
            from.SendMessage("That container must be accessible in your backpack.");
            return default;
        }

        using var snapshot = PooledRefList<Item>.Create();
        var items = bag.Items;
        for (var i = 0; i < items.Count; i++)
        {
            snapshot.Add(items[i]);
        }

        var unraveled = 0;
        var skipped = 0;
        var residue = 0;
        var essence = 0;
        var relics = 0;

        for (var i = 0; i < snapshot.Count; i++)
        {
            var candidate = snapshot[i];
            if (candidate == null || candidate.Deleted || candidate.Parent != bag)
            {
                continue;
            }

            if (!CanUnravel(candidate, out _))
            {
                skipped++;
                continue;
            }

            if (TryUnravel(from, candidate, out var ingredientType, out var amount))
            {
                unraveled++;

                if (ingredientType == typeof(MagicalResidue))
                {
                    residue += amount;
                }
                else if (ingredientType == typeof(EnchantedEssence))
                {
                    essence += amount;
                }
                else if (ingredientType == typeof(RelicFragment))
                {
                    relics += amount;
                }
            }
            else
            {
                skipped++;
            }
        }

        if (unraveled == 0)
        {
            from.SendMessage($"No eligible magic items were unraveled ({skipped} skipped).");
        }
        else
        {
            from.SendMessage(
                $"Unraveled {unraveled} items ({skipped} skipped): {residue} Magical Residue, {essence} Enchanted Essence, {relics} Relic Fragments."
            );
        }

        return new UnravelBatchResult(unraveled, skipped, residue, essence, relics);
    }

    public static bool CanImbue(Item item, out string reason)
    {
        if (item == null || item.Deleted)
        {
            reason = "That item is no longer valid.";
            return false;
        }

        if (item is Container)
        {
            reason = "Containers cannot be imbued.";
            return false;
        }

        if (!item.Movable || item.IsLockedDown || item.IsSecure)
        {
            reason = "Locked or immovable items cannot be imbued.";
            return false;
        }

        if (item.LootType == LootType.Cursed)
        {
            reason = "Cursed items cannot be imbued.";
            return false;
        }

        if (item.QuestItem)
        {
            reason = "Quest items cannot be imbued.";
            return false;
        }

        if (FactionItem.Find(item) != null)
        {
            reason = "Faction items cannot be imbued.";
            return false;
        }

        if (ItemRarity.IsFixedArtifact(item))
        {
            reason = "Artifacts cannot be imbued.";
            return false;
        }

        switch (item)
        {
            case Fists:
            {
                reason = "That item cannot be imbued.";
                return false;
            }
            case BaseWeapon weapon:
            {
                if (weapon.ArtifactRarity > 0)
                {
                    reason = "Artifacts cannot be imbued.";
                    return false;
                }

                if (weapon.IsWeaponLevelingEnabled)
                {
                    reason = "Leveled weapons cannot be imbued.";
                    return false;
                }

                break;
            }
            case BaseArmor armor:
            {
                if (armor.ArtifactRarity > 0)
                {
                    reason = "Artifacts cannot be imbued.";
                    return false;
                }

                break;
            }
            case BaseHat hat:
            {
                if (hat.ArtifactRarity > 0)
                {
                    reason = "Artifacts cannot be imbued.";
                    return false;
                }

                break;
            }
            case BaseJewel jewel:
            {
                if (jewel.ArtifactRarity > 0)
                {
                    reason = "Artifacts cannot be imbued.";
                    return false;
                }

                break;
            }
            case Spellbook:
            {
                break;
            }
            default:
            {
                reason = "Only weapons, armor, shields, hats, jewelry, and spellbooks can be imbued.";
                return false;
            }
        }

        if (ImbuingPersistence.GetTimesImbued(item) >= ImbuingCatalog.MaxImbueUses)
        {
            reason = $"That item has reached the maximum of {ImbuingCatalog.MaxImbueUses} imbuings.";
            return false;
        }

        reason = null;
        return true;
    }

    public static double GetImbueSuccessChance(Mobile from, Item item, ImbuingMod mod, int targetIntensity)
    {
        if (item == null || mod == null)
        {
            return 0.0;
        }

        var maxWeight = Math.Max(1, ImbuingCatalog.GetMaxWeight(item));
        var projectedTotal = ImbuingCatalog.ComputeProjectedWeight(item, mod, targetIntensity);

        var skill = from?.Skills.Imbuing.Value ?? 0.0;
        var weightLoad = Math.Clamp((double)projectedTotal / maxWeight, 0.0, 1.2);
        var intensityRatio = mod.IsBoolean
            ? 1.0
            : Math.Clamp((double)targetIntensity / Math.Max(1, mod.GetMaxValue(item)), 0.0, 1.0);

        // Solo-friendly curve: viable to train at low skill, reaches 90% at 100 Imbuing (100% at a SoulForge)
        // and 100% at 110+ Imbuing even at full 550 budget.
        var difficulty = 20.0 + weightLoad * 45.0 + intensityRatio * 20.0;
        var chance = 0.75 + (skill - difficulty) * 0.01;

        if (from != null && IsNearSoulForge(from))
        {
            chance += SoulForgeSuccessBonus;
        }

        return Math.Clamp(chance, 0.15, 1.0);
    }

    public static bool TryImbue(Mobile from, Item item, int modId, int targetIntensity, out string message)
    {
        if (!ValidateBackpackAndStation(from, item, out message))
        {
            return false;
        }

        if (!CanImbue(item, out message))
        {
            return false;
        }

        var mod = ImbuingCatalog.GetMod(modId);
        if (mod == null || !mod.IsEligibleFor(item))
        {
            message = "That property cannot be imbued onto this item.";
            return false;
        }

        var minVal = mod.MinValue;
        var maxVal = mod.GetMaxValue(item);
        if (targetIntensity < minVal || targetIntensity > maxVal)
        {
            message = $"Target intensity must be between {minVal} and {maxVal}.";
            return false;
        }

        var currentVal = ImbuingCatalog.GetCurrentValue(item, mod);
        if (targetIntensity <= currentVal)
        {
            message = "The item already has that property at an equal or higher intensity.";
            return false;
        }

        var isUpgrade = currentVal > 0;
        var currentProps = ImbuingCatalog.CountProperties(item);
        if (!isUpgrade && mod.ConsumesPropertySlot(item) && currentProps >= ImbuingCatalog.MaxProperties)
        {
            message = $"That item already has the maximum of {ImbuingCatalog.MaxProperties} properties.";
            return false;
        }

        var maxWeight = ImbuingCatalog.GetMaxWeight(item);
        var projectedTotal = ImbuingCatalog.ComputeProjectedWeight(item, mod, targetIntensity);

        if (projectedTotal > maxWeight)
        {
            message = $"That imbue exceeds the item's weight budget ({projectedTotal} / {maxWeight}).";
            return false;
        }

        var cost = ImbuingCatalog.GetImbueCost(mod, item, targetIntensity);
        var pack = from.Backpack;
        if (pack == null)
        {
            message = "You need a backpack containing the required imbuing ingredients.";
            return false;
        }

        var ownedPrimary = pack.GetAmount(cost.PrimaryType);
        var ownedGem = pack.GetAmount(cost.GemType);
        if (ownedPrimary < cost.PrimaryAmount || ownedGem < cost.GemAmount)
        {
            var primaryName = ImbuingCatalog.GetIngredientDisplayName(cost.PrimaryType);
            var gemName = ImbuingCatalog.GetIngredientDisplayName(cost.GemType);
            message = $"Insufficient ingredients. Need {cost.PrimaryAmount} {primaryName} and {cost.GemAmount} {gemName}.";
            return false;
        }

        var chance = GetImbueSuccessChance(from, item, mod, targetIntensity);
        var minSkill = Math.Clamp((double)projectedTotal / maxWeight * 100.0 - 25.0, 0.0, 100.0);
        var maxSkill = Math.Clamp(minSkill + 45.0, 45.0, 120.0);
        from.CheckSkill(SkillName.Imbuing, minSkill, maxSkill);

        var succeeded = chance >= 1.0 || Utility.RandomDouble() < chance;
        if (!succeeded)
        {
            var lostPrimary = Math.Max(1, (cost.PrimaryAmount + 1) / 2);
            var lostGem = Math.Max(1, (cost.GemAmount + 1) / 2);
            pack.ConsumeTotal(cost.PrimaryType, lostPrimary);
            pack.ConsumeTotal(cost.GemType, lostGem);

            if (from.Map != null && from.Map != Map.Internal)
            {
                from.PlaySound(0x5C);
            }

            message = "You fail to imbue the item. Half of the ingredients were lost, but the item is unharmed.";
            return false;
        }

        pack.ConsumeTotal(cost.PrimaryType, cost.PrimaryAmount);
        pack.ConsumeTotal(cost.GemType, cost.GemAmount);

        ImbuingCatalog.ApplyModValue(item, mod, targetIntensity);
        ImbuingPersistence.MarkImbued(item);

        if (from.Map != null && from.Map != Map.Internal)
        {
            from.PlaySound(0x1EA);
            from.FixedParticles(0x375A, 10, 15, 5017, EffectLayer.Waist);
        }

        var formatted = mod.FormatValue(targetIntensity);
        message = $"You successfully imbue {mod.Name} ({formatted}) onto the item.";
        return true;
    }

    private static bool ValidateBackpackAndStation(Mobile from, Item item, out string message)
    {
        if (from == null || from.Deleted || !from.Alive)
        {
            message = "You cannot do that right now.";
            return false;
        }

        if (!IsNearSoulForgeOrForge(from))
        {
            message = "You must be near a soulforge, or near an anvil and forge, to do that.";
            return false;
        }

        if (item == null || item.Deleted)
        {
            message = "That item is no longer valid.";
            return false;
        }

        if (item.Parent == from)
        {
            message = "Unequip the item and place it in your backpack first.";
            return false;
        }

        if (from.Backpack == null || !item.IsChildOf(from.Backpack) || item.InSecureTrade || !item.IsAccessibleTo(from))
        {
            message = "That item must be accessible in your backpack.";
            return false;
        }

        message = null;
        return true;
    }

    private static bool IsSoulForgeTileId(int tileId) =>
        tileId is 0x2DB0
            or >= 0x423B and <= 0x4243
            or >= 0x424B and <= 0x4257
            or >= 0x4263 and <= 0x4272
            or >= 0x4277 and <= 0x4286
            or >= 0x44C7 and <= 0x44CA;
}
