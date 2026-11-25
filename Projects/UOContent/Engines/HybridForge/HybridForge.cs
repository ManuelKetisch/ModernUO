using System;
using Server.Commands;
using Server.Engines.Craft;
using Server.Engines.WeaponLevelingServices;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Engines.HybridForge;

public enum HybridForgeRecipe : byte
{
    Polehammer,
    ScaleLinedPlate
}

public static class HybridForge
{
    internal const int DragonScalesRequired = 10;
    internal const int ResistanceShift = 3;
    internal const int SmithingRequired = 100;

    public static void Initialize() => CommandSystem.Register(
        "HybridForge", AccessLevel.Player, e =>
        {
            if (e.Mobile is PlayerMobile { Alive: true } owner)
            {
                owner.SendGump(new HybridForgeMenu(owner));
            }
        }
    );

    internal static int RecipeId(HybridForgeRecipe recipe) => recipe switch
    {
        HybridForgeRecipe.Polehammer => 100010,
        HybridForgeRecipe.ScaleLinedPlate => 100011,
        _ => throw new ArgumentOutOfRangeException(nameof(recipe))
    };

    internal static string NameOf(HybridForgeRecipe recipe) => recipe switch
    {
        HybridForgeRecipe.Polehammer => "polehammer",
        HybridForgeRecipe.ScaleLinedPlate => "scale-lined plate chest",
        _ => throw new ArgumentOutOfRangeException(nameof(recipe))
    };

    internal static bool CanForge(PlayerMobile owner, HybridForgeRecipe recipe)
    {
        if (owner is not { Alive: true, Deleted: false, Map: { } map } || map == Map.Internal || !owner.HasRecipe(RecipeId(recipe)) ||
            owner.Skills.Blacksmith.Base < SmithingRequired)
        {
            return false;
        }

        DefBlacksmithy.CheckAnvilAndForge(owner, 2, out var anvil, out var forge);
        return anvil && forge;
    }

    private static bool CanConsumeStack(PlayerMobile owner, Item item, int quantity) =>
        WeaponServiceTransfer.IsInBackpack(owner, item) &&
        item is
        {
            Stackable: true, Movable: true, Nontransferable: false, LootType: LootType.Regular,
            BlessedFor: null, Insured: false, IsLockedDown: false, IsSecure: false, TotalItems: 0
        } && item.Amount >= quantity;

    private static bool IsBase(PlayerMobile owner, HybridForgeRecipe recipe, Item source) =>
        WeaponServiceTransfer.CanConsume(owner, source) && (recipe == HybridForgeRecipe.Polehammer
            ? source is Halberd { Layer: Layer.TwoHanded, Cursed: false, Consecrated: false, ArtifactRarity: 0 } &&
              source.GetType() == typeof(Halberd)
            : source is PlateChest { Layer: Layer.InnerTorso, ArtifactRarity: 0, PhysicalResistance: >= ResistanceShift } &&
              source.GetType() == typeof(PlateChest));

    private static bool IsDonor(PlayerMobile owner, HybridForgeRecipe recipe, Item donor) =>
        recipe == HybridForgeRecipe.Polehammer
            ? donor is WarHammer { Cursed: false, Consecrated: false, ArtifactRarity: 0 } &&
              donor.GetType() == typeof(WarHammer) && WeaponServiceTransfer.CanConsume(owner, donor)
            : donor is RedScales { Resource: CraftResource.RedScales } &&
              CanConsumeStack(owner, donor, DragonScalesRequired);

    internal static void ChooseInputs(PlayerMobile owner, HybridForgeRecipe recipe)
    {
        if (!CanForge(owner, recipe))
        {
            owner.SendMessage("Learn the recipe and stand within two steps of an anvil and forge. You need 100 Blacksmithy.");
            return;
        }

        owner.SendMessage(recipe == HybridForgeRecipe.Polehammer
            ? "Choose a halberd from your backpack."
            : "Choose a plate chest from your backpack.");

        owner.BeginTarget(-1, false, TargetFlags.None, (from, targeted) =>
        {
            if (from != owner || targeted is not Item source || !IsBase(owner, recipe, source))
            {
                owner.SendMessage("Choose an unbound base item for this recipe from your backpack.");
                return;
            }

            ChooseDonor(owner, recipe, source);
        });
    }

    private static void ChooseDonor(PlayerMobile owner, HybridForgeRecipe recipe, Item source)
    {
        owner.SendMessage(recipe == HybridForgeRecipe.Polehammer
            ? "Choose a war hammer from your backpack."
            : $"Choose a stack of at least {DragonScalesRequired} red dragon scales.");

        owner.BeginTarget(-1, false, TargetFlags.None, (from, targeted) =>
        {
            if (from != owner || targeted is not Item donor || !IsDonor(owner, recipe, donor))
            {
                owner.SendMessage("Choose the required unbound donor from your backpack.");
                return;
            }

            owner.SendMessage("Choose an Ancient Wyrm scale from your backpack.");
            owner.BeginTarget(-1, false, TargetFlags.None, (mobile, binding) =>
            {
                if (mobile == owner && binding is AncientWyrmScale scale &&
                    TryQuote(owner, recipe, source, donor, scale) is { } quote)
                {
                    owner.SendGump(new HybridForgePreview(owner, quote));
                }
                else
                {
                    owner.SendMessage("Check your inputs and your distance from the anvil and forge.");
                }
            });
        });
    }

    internal static HybridForgeQuote TryQuote(
        PlayerMobile owner, HybridForgeRecipe recipe, Item source, Item donor, AncientWyrmScale scale
    ) =>
        CanForge(owner, recipe) && IsBase(owner, recipe, source) && IsDonor(owner, recipe, donor) &&
        CanConsumeStack(owner, scale, 1)
            ? new HybridForgeQuote(recipe, source, donor, scale)
            : null;

    internal static Item Complete(PlayerMobile owner, HybridForgeQuote quote)
    {
        var current = TryQuote(owner, quote.Recipe, quote.Source, quote.Donor, quote.Scale);
        if (current == null)
        {
            owner.SendMessage("Check your ingredients and return to the anvil and forge.");
            return null;
        }

        if (!quote.Matches())
        {
            owner.SendMessage("An ingredient changed. Review the updated result.");
            owner.SendGump(new HybridForgePreview(owner, current));
            return null;
        }

        var result = quote.Source switch
        {
            Halberd halberd => CopyWeapon(halberd),
            PlateChest plate => (Item)CopyArmor(plate),
            _ => null
        };
        if (result == null)
        {
            owner.SendMessage("That base item cannot keep its properties in this recipe.");
            return null;
        }

        if (!owner.Backpack.TryDropItem(owner, result, true))
        {
            result.Delete();
            return null;
        }

        quote.Source.Delete();
        quote.Donor.Consume(quote.Recipe == HybridForgeRecipe.Polehammer ? 1 : DragonScalesRequired);
        quote.Scale.Consume();
        owner.SendMessage($"Your {NameOf(quote.Recipe)} is in your backpack.");
        return result;
    }

    private static Polehammer CopyWeapon(Halberd source)
    {
        var result = new Polehammer();
        if (!WeaponServiceTransfer.TryCopyWeapon(source, result, removeArtifactTag: false))
        {
            result.Delete();
            return null;
        }

        result.Weight = source.Weight;
        result.StrRequirement = source.StrRequirement;
        result.DexRequirement = source.DexRequirement;
        result.IntRequirement = source.IntRequirement;
        result.MinDamage = source.MinDamage;
        result.MaxDamage = source.MaxDamage;
        result.Speed = source.Speed;
        result.Skill = source.Skill;
        result.Type = source.Type;
        result.Animation = source.Animation;
        result.HitSound = source.HitSound;
        result.MissSound = source.MissSound;
        result.MaxRange = source.MaxRange;
        result.UsesRemaining = source.UsesRemaining;
        result.ShowUsesRemaining = source.ShowUsesRemaining;
        return result;
    }

    private static ScaleLinedPlateChest CopyArmor(PlateChest source)
    {
        var result = new ScaleLinedPlateChest
        {
            Name = source.Name == source.DefaultName ? null : source.Name,
            Weight = source.Weight,
            Resource = source.Resource,
            Quality = source.Quality,
            Crafter = source.Crafter,
            Durability = source.Durability,
            ProtectionLevel = source.ProtectionLevel,
            Identified = source.Identified,
            BaseArmorRating = source.BaseArmorRating,
            StrBonus = source.StrBonus,
            DexBonus = source.DexBonus,
            IntBonus = source.IntBonus,
            StrRequirement = source.StrRequirement,
            DexRequirement = source.DexRequirement,
            IntRequirement = source.IntRequirement,
            MeditationAllowance = source.MeditationAllowance,
            PhysicalBonus = source.PhysicalBonus,
            FireBonus = source.FireBonus,
            ColdBonus = source.ColdBonus,
            PoisonBonus = source.PoisonBonus,
            EnergyBonus = source.EnergyBonus
        };
        result.SetGraphicFrom(source.ItemID);
        source.OnAfterDuped(result);
        result.MaxHitPoints = source.MaxHitPoints;
        result.HitPoints = source.HitPoints;
        return result;
    }
}
