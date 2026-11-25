using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.HybridForge;

internal sealed class HybridForgeMenu(PlayerMobile owner) : DynamicGump(50, 50)
{
    public override bool Singleton => true;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 437, 220, 0x13BE);
        builder.AddLabel(23, 19, 0x481, "Hybrid forge");
        builder.AddLabel(23, 48, 0x481, "Stand by an anvil and forge with 100 Blacksmithy.");
        AddRecipe(ref builder, owner, HybridForgeRecipe.Polehammer, 77, 1, "Halberd, war hammer and Ancient Wyrm scale");
        AddRecipe(ref builder, owner, HybridForgeRecipe.ScaleLinedPlate, 133, 2, "Plate chest, ten red scales and Ancient Wyrm scale");
        builder.AddLabel(23, 188, 0x481, "Use [WyrmRewards to find blueprints.");
    }

    private static void AddRecipe(
        ref DynamicGumpBuilder builder, PlayerMobile owner, HybridForgeRecipe recipe, int y, int button, string inputs
    )
    {
        var name = HybridForge.NameOf(recipe);
        if (owner.HasRecipe(HybridForge.RecipeId(recipe)))
        {
            builder.AddButton(23, y, 0xFA5, 0xFA7, button);
            builder.AddLabel(57, y, 0x481, $"Make {name}");
        }
        else
        {
            builder.AddLabel(23, y, 0x481, $"{name} (blueprint needed)");
        }

        builder.AddLabel(23, y + 24, 0x481, inputs);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender == owner.NetState && sender.Mobile == owner && info.ButtonID is 1 or 2)
        {
            HybridForge.ChooseInputs(owner, info.ButtonID == 1 ? HybridForgeRecipe.Polehammer : HybridForgeRecipe.ScaleLinedPlate);
        }
    }
}

internal sealed class HybridForgePreview(PlayerMobile owner, HybridForgeQuote quote) : DynamicGump(50, 50)
{
    private const int Hue = 0x481;

    public override bool Singleton => true;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 486, 339, 0x13BE);
        builder.AddLabel(23, 19, Hue, $"Forge {HybridForge.NameOf(quote.Recipe)}");
        builder.AddLabel(23, 51, Hue, "These items will be used");
        AddInput(ref builder, quote.Source, 76, 1);
        AddInput(ref builder, quote.Donor, 99, quote.Recipe == HybridForgeRecipe.Polehammer ? 1 : HybridForge.DragonScalesRequired);
        AddInput(ref builder, quote.Scale, 122, 1);

        if (quote.Source is Halberd weapon)
        {
            builder.AddLabel(23, 152, Hue, "Keep Whirlwind Attack. Replace Concussion Blow with Crushing Blow.");
            builder.AddLabel(23, 175, Hue, $"Two-handed {weapon.Skill.ToString().ToLowerInvariant()} weapon, weight {weapon.Weight:0.##}");
            builder.AddLabel(23, 198, Hue, $"Damage {weapon.MinDamage}–{weapon.MaxDamage}, durability {weapon.HitPoints}/{weapon.MaxHitPoints}");
            var progress = weapon.CaptureWeaponLeveling();
            builder.AddLabel(23, 221, Hue, weapon.IsWeaponLevelingEnabled
                ? $"Keep {progress.Class.ToString().ToLowerInvariant()} level {weapon.WeaponLevel}, {progress.Experience} XP and {progress.Spent} spent points."
                : "The halberd has no Weapon Leveling progress.");
            builder.AddLabel(23, 244, Hue, progress.ArtifactTagRemoved
                ? "Keep the halberd's artifact waiver. War hammer upgrades are used up."
                : "War hammer upgrades are used up.");
        }
        else
        {
            var armor = (PlateChest)quote.Source;
            builder.AddLabel(23, 152, Hue, "Move 3 physical resistance to fire resistance.");
            builder.AddLabel(23, 175, Hue, $"Physical {armor.PhysicalResistance - HybridForge.ResistanceShift}, fire {armor.FireResistance + HybridForge.ResistanceShift}");
            builder.AddLabel(23, 198, Hue, $"Cold {armor.ColdResistance}, poison {armor.PoisonResistance}, energy {armor.EnergyResistance}");
            builder.AddLabel(23, 221, Hue, $"Chest armor, weight {armor.Weight:0.##}, durability {armor.HitPoints}/{armor.MaxHitPoints}");
            builder.AddLabel(23, 244, Hue, $"Strength required: {armor.ComputeStatReq(StatType.Str)}. Keeps other armor properties.");
        }

        builder.AddButton(23, 293, 0xFA5, 0xFA7, 1);
        builder.AddLabel(57, 293, Hue, "Forge item");
    }

    private static void AddInput(ref DynamicGumpBuilder builder, Item item, int y, int quantity)
    {
        builder.AddLabel(23, y, Hue, $"{quantity} ×");
        if (string.IsNullOrEmpty(item.Name))
        {
            builder.AddHtmlLocalized(64, y, 386, 22, item.LabelNumber, 0xFFFFFF);
        }
        else
        {
            builder.AddLabelCropped(64, y, 391, 24, Hue, item.Name);
        }

        builder.AddItemProperty(item.Serial);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender == owner.NetState && sender.Mobile == owner && info.ButtonID == 1)
        {
            HybridForge.Complete(owner, quote);
        }
    }
}
