using System;
using System.Collections.Generic;
using Server.Engines.Craft;
using Server.Engines.WeaponLevelingServices;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.Metallurgy;

internal enum MetallurgyRecipe
{
    MeteoricBronze = 1,
    MeteoricPickaxe,
    VolcanicGlassDagger,
    MeteoricIronShield,
    ConductiveSilver,
    ConductiveSilverSleeves
}

public static class Metallurgy
{
    internal const int SmithingRequired = 80;
    internal const int IronRequired = 8;
    internal const int BronzeRequired = 6;
    internal const int BenchRange = 2;

    internal readonly record struct Ingredient(Type Type, int Count, string Label);

    internal sealed record Recipe(
        string Label, int Skill, Ingredient[] Ingredients, string Benefit, Func<Item> Create
    );

    private static readonly Recipe[] _recipes =
    [
        new(
            "Refine meteoric bronze", SmithingRequired,
            [new(typeof(MeteoriteFragment), 1, "meteorite fragment"), new(typeof(IronIngot), IronRequired, "iron ingots"),
             new(typeof(BronzeIngot), BronzeRequired, "bronze ingots")],
            "Alloy for a meteoric pickaxe.", () => new MeteoricBronzeIngot()
        ),
        new(
            "Make a meteoric pickaxe", SmithingRequired, [new(typeof(MeteoricBronzeIngot), 1, "meteoric bronze ingot")],
            $"{MeteoricPickaxe.MiningUses} mining uses, {MeteoricPickaxe.WeightInStones} stones.",
            () => new MeteoricPickaxe()
        ),
        new(
            "Make a volcanic glass dagger", 85,
            [new(typeof(VolcanicGlass), 2, "volcanic glass"), new(typeof(IronIngot), 8, "iron ingots"),
             new(typeof(CopperIngot), 4, "copper ingots")],
            $"{VolcanicGlassDagger.FireConversion}% fire damage. Brittle edge.", () => new VolcanicGlassDagger()
        ),
        new(
            "Make a meteoric iron shield", 95,
            [new(typeof(MeteoricIronOre), 2, "meteoric iron ore"), new(typeof(IronIngot), 12, "iron ingots"),
             new(typeof(ShadowIronIngot), 4, "shadow iron ingots")],
            "Higher durability and physical resistance. Weighs 14 stones.", () => new MeteoricIronShield()
        ),
        new(
            "Refine conductive silver", 95,
            [new(typeof(SilverRing), 1, "plain silver ring"), new(typeof(MeteoricIronOre), 1, "meteoric iron ore"),
             new(typeof(CopperIngot), 4, "copper ingots"), new(typeof(DullCopperIngot), 4, "dull copper ingots")],
            "Alloy for silver conduit sleeves.", () => new ConductiveSilverIngot()
        ),
        new(
            "Make silver conduit sleeves", 95,
            [new(typeof(ConductiveSilverIngot), 1, "conductive silver ingot"), new(typeof(IronIngot), 8, "iron ingots")],
            $"{ConductiveSilverSleeves.ManaCostReduction}% lower mana cost. Allows meditation. Lower durability.",
            () => new ConductiveSilverSleeves()
        )
    ];

    internal static ReadOnlySpan<Recipe> Recipes => _recipes;

    public static void Initialize() => CommandSystem.Register(
        "Metallurgy", AccessLevel.Player, args =>
        {
            if (args.Mobile is PlayerMobile { Alive: true } owner)
            {
                owner.SendGump(new MetallurgyGump(owner));
            }
        }
    );

    private static bool CheckBench(PlayerMobile owner, int skill)
    {
        if (owner is not { Alive: true, Deleted: false, Map: not null, Backpack: not null } || owner.Map == Map.Internal)
        {
            return false;
        }

        if (owner.Skills.Blacksmith.Base < skill)
        {
            owner.SendMessage($"You need {skill} Blacksmithy to make this.");
            return false;
        }

        DefBlacksmithy.CheckAnvilAndForge(owner, BenchRange, out var anvil, out var forge);
        if (!anvil || !forge)
        {
            owner.SendMessage("Stand within two steps of an anvil and a forge.");
            return false;
        }

        return true;
    }

    internal static bool TryCraft(PlayerMobile owner, MetallurgyRecipe recipe)
    {
        var index = (int)recipe - 1;
        if ((uint)index >= (uint)_recipes.Length)
        {
            return false;
        }

        var definition = _recipes[index];
        if (!CheckBench(owner, definition.Skill))
        {
            return false;
        }

        var pack = owner.Backpack;
        var materials = new List<(Item Item, int Count)>(definition.Ingredients.Length);
        foreach (var ingredient in definition.Ingredients)
        {
            var remaining = ingredient.Count;
            foreach (var stack in pack.FindItemsByType(ingredient.Type))
            {
                if (!CanUseIngredient(owner, stack))
                {
                    continue;
                }

                var count = Math.Min(remaining, stack.Amount);
                materials.Add((stack, count));
                remaining -= count;
                if (remaining == 0)
                {
                    break;
                }
            }

            if (remaining != 0)
            {
                owner.SendMessage($"Put {ingredient.Count} {ingredient.Label} in your backpack.");
                return false;
            }
        }

        Item result = definition.Create();
        var resultName = result.DefaultName;
        if (!pack.TryDropItem(owner, result, true))
        {
            result.Delete();
            return false;
        }

        // Inputs are different types from the output. Delivery and charging run in the same game-loop turn.
        foreach (var (item, count) in materials)
        {
            item.Consume(count);
        }

        owner.PlaySound(0x2A);
        owner.SendMessage($"You made a {resultName}.");
        return true;
    }

    private static bool CanUseIngredient(PlayerMobile owner, Item item)
    {
        if (item is SilverRing ring)
        {
            return WeaponServiceTransfer.CanConsume(owner, ring) && ring.GemType == GemType.None &&
                   ring.Attributes.IsEmpty && ring.Resistances.IsEmpty && ring.SkillBonuses.IsEmpty;
        }

        return WeaponServiceTransfer.IsInBackpack(owner, item) && item is
        {
            Amount: > 0, Stackable: true, Movable: true, Nontransferable: false, LootType: LootType.Regular,
            BlessedFor: null, Insured: false, IsLockedDown: false, IsSecure: false, TotalItems: 0
        };
    }
}

internal sealed class MetallurgyGump(PlayerMobile owner) : DynamicGump(45, 45)
{
    private const int Hue = 0x481;

    public override bool Singleton => true;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 638, 568, 0x13BE);
        builder.AddLabel(22, 18, Hue, "Metallurgy");
        builder.AddLabel(22, 44, Hue, "Work at an anvil and forge. Each recipe shows its Blacksmithy requirement.");
        var y = 79;
        var recipes = Metallurgy.Recipes;
        for (var i = 0; i < recipes.Length; i++)
        {
            var recipe = recipes[i];
            builder.AddButton(22, y, 0xFA5, 0xFA7, i + 1);
            builder.AddLabel(58, y, Hue, $"{recipe.Label}   {recipe.Skill} Blacksmithy");
            var x = 58;
            foreach (var ingredient in recipe.Ingredients)
            {
                builder.AddLabel(x, y + 22, Hue, $"{ingredient.Count} {ingredient.Label}");
                x += 139;
            }
            builder.AddLabel(58, y + 43, Hue, recipe.Benefit);
            y += 66;
        }

        builder.AddLabel(22, 484, Hue, $"Volcanic glass at Fire, Trammel ({DeepOreSeam.FireSite.X}, {DeepOreSeam.FireSite.Y}). {DeepOreSeam.GlassMining} Mining.");
        builder.AddLabel(22, 507, Hue, $"Meteoric iron at Shame, Trammel ({DeepOreSeam.ShameSite.X}, {DeepOreSeam.ShameSite.Y}). {DeepOreSeam.IronMining} Mining.");
        builder.AddLabel(22, 531, Hue, "Use [RefineCraftGrade for Excelsior variants.");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender != owner.NetState || sender.Mobile != owner || !owner.Alive ||
            info.ButtonID < 1 || info.ButtonID > Metallurgy.Recipes.Length)
        {
            return;
        }

        Metallurgy.TryCraft(owner, (MetallurgyRecipe)info.ButtonID);
        owner.SendGump(new MetallurgyGump(owner));
    }
}
