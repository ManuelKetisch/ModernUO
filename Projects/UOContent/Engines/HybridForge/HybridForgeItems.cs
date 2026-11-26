using ModernUO.Serialization;
using Server.Engines.HybridForge;
using Server.Engines.WeaponLevelingServices;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class AncientWyrmScale : Item
{
    [Constructible]
    public AncientWyrmScale(int amount = 1) : base(0x26B4)
    {
        Stackable = true;
        Hue = 0x489;
        Amount = amount;
    }

    public override string DefaultName => "Ancient Wyrm scale";
    public override double DefaultWeight => ItemWeights.Reagent;
}

[SerializationGenerator(0, false)]
public partial class WyrmForgeTrophy : Item
{
    [Constructible]
    public WyrmForgeTrophy() : base(0x1D0F) => Hue = 0x489;

    public override string DefaultName => "Ancient Wyrm trophy";
}

[SerializationGenerator(0, false)]
public partial class WyrmForgeBlueprint : Item
{
    [SerializableField(0)]
    private HybridForgeRecipe _recipe;

    [Constructible]
    public WyrmForgeBlueprint(bool armor = false) : base(0xEF3)
    {
        _recipe = armor ? HybridForgeRecipe.ScaleLinedPlate : HybridForgeRecipe.Polehammer;
        Hue = 0x489;
    }

    public override string DefaultName => $"{HybridForge.NameOf(_recipe)} blueprint";

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile owner || !WeaponServiceTransfer.CanConsume(owner, this))
        {
            from.SendMessage("Put the blueprint in your backpack to use it.");
            return;
        }

        if (owner.HasRecipe(HybridForge.RecipeId(_recipe)))
        {
            var scales = new AncientWyrmScale(2);
            if (!owner.Backpack.TryDropItem(owner, scales, true))
            {
                scales.Delete();
                return;
            }

            Delete();
            owner.SendMessage("You exchanged the duplicate blueprint for two Ancient Wyrm scales.");
            return;
        }

        owner.AcquireRecipe(HybridForge.RecipeId(_recipe));
        owner.MarkDirty();
        Delete();
        owner.SendMessage($"You learned to forge {HybridForge.NameOf(_recipe)}. Use [HybridForge at an anvil and forge.");
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Double-click to learn this recipe.");
        list.Add("If you know it, exchange this for two Ancient Wyrm scales.");
    }
}

[Flippable(0x1439, 0x1438)]
[SerializationGenerator(0, false)]
public partial class Polehammer : Halberd
{
    [Constructible]
    public Polehammer()
    {
        // CrossUO uses the item art to choose its two special-move buttons.
        ItemID = 0x1439;
        Layer = Layer.TwoHanded;
    }

    public override string DefaultName => "polehammer";
    public override WeaponAbility SecondaryAbility => WeaponAbility.CrushingBlow;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Polehammer: Whirlwind Attack and Crushing Blow");
    }
}

[Flippable]
[SerializationGenerator(0, false)]
public partial class ScaleLinedPlateChest : PlateChest
{
    internal const int StockGraphic = 0x1415;
    internal const int StockAlternateGraphic = 0x1416;
    internal const int CustomGraphic = 0x7FAC;
    internal const int CustomAlternateGraphic = 0x7FAD;
    internal const string CustomTileName = "scale-lined plate";

    [Constructible]
    public ScaleLinedPlateChest() => SetGraphicFrom(StockGraphic);

    private static bool HasCustomTile(int id) =>
        TileData.ItemTable[id] is { Name: CustomTileName, Quality: (int)Layer.InnerTorso, Weight: 10 } tile &&
        (tile.Flags & TileFlag.Wearable) != 0;

    internal void SetGraphicFrom(int graphic)
    {
        var alternate = graphic is StockAlternateGraphic or CustomAlternateGraphic;
        ItemID = HasCustomTile(CustomGraphic) && HasCustomTile(CustomAlternateGraphic)
            ? alternate ? CustomAlternateGraphic : CustomGraphic
            : alternate ? StockAlternateGraphic : StockGraphic;
    }

    public void Flip() => SetGraphicFrom(
        ItemID is StockAlternateGraphic or CustomAlternateGraphic ? StockGraphic : StockAlternateGraphic
    );

    [AfterDeserialization]
    private void LoadAppearance() => SetGraphicFrom(ItemID);

    public override string DefaultName => "scale-lined plate chest";
    public override int BasePhysicalResistance => base.BasePhysicalResistance - HybridForge.ResistanceShift;
    public override int BaseFireResistance => base.BaseFireResistance + HybridForge.ResistanceShift;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Scale lining: 3 physical resistance moved to fire");
    }
}
