using System;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.BioEngineering;

internal enum BioRecipe
{
    Vial = 1,
    VialSet,
    Deed
}

[SerializationGenerator(0)]
public partial class BioEngineeringTool : Item
{
    private static readonly Type[] _vialResources = [typeof(Bottle), typeof(IronIngot), typeof(BioOrganicMatter)];
    private static readonly int[] _vialAmounts = [1, 25, 1000];
    private static readonly int[] _setAmounts = [4, 50, 5000];
    private static readonly Type[] _deedResources = [typeof(IronIngot), typeof(BlankScroll)];
    private static readonly int[] _deedAmounts = [25, 5];

    [SerializableField(0, setter: "private")]
    [InvalidateProperties]
    private int _charges;

    [Constructible]
    public BioEngineeringTool() : this(20)
    {
    }

    [Constructible]
    public BioEngineeringTool(int charges) : base(0x1EB8)
    {
        Charges = Math.Max(0, charges);
        Hue = 0x59;
    }

    public override string DefaultName => "bio engineering tool";
    public override double DefaultWeight => 1.0;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.AddLocalized(1060584, Charges);
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player)
        {
            ShowRecipes(player);
        }
    }

    private void ShowRecipes(PlayerMobile player)
    {
        if (CanCraft(player))
        {
            player.SendGump(new RecipeGump(this));
        }
    }

    private bool CanCraft(PlayerMobile player)
    {
        if (!BioEngineeringBook.CanUseItem(player, this))
        {
            return false;
        }

        if (Charges <= 0)
        {
            player.SendMessage("The tool has no charges left.");
            return false;
        }

        return BioEngineeringBook.CheckTraining(player);
    }

    internal bool TryCraft(PlayerMobile player, BioRecipe recipe)
    {
        if (!CanCraft(player))
        {
            return false;
        }

        var pack = player.Backpack;
        Item result;
        Type[] resources;
        int[] amounts;
        switch (recipe)
        {
            case BioRecipe.Vial:
                result = new BioVial();
                resources = _vialResources;
                amounts = _vialAmounts;
                break;
            case BioRecipe.VialSet:
                result = new BioVialSet();
                resources = _vialResources;
                amounts = _setAmounts;
                break;
            case BioRecipe.Deed:
                result = new BioCreationDeed();
                resources = _deedResources;
                amounts = _deedAmounts;
                break;
            default:
                return false;
        }

        if (!pack.CheckHold(player, result, true))
        {
            result.Delete();
            return false;
        }

        // ConsumeTotal first checks every resource, then consumes them on the game loop.
        if (pack.ConsumeTotal(resources, amounts) != -1)
        {
            result.Delete();
            player.SendMessage("You are missing some of the materials.");
            return false;
        }

        pack.DropItem(result);
        Charges--;
        player.SendMessage($"You craft a {result.DefaultName}.");
        return true;
    }

    internal sealed class RecipeGump(BioEngineeringTool tool) : StaticGump<RecipeGump>(60, 55)
    {
        public override bool Singleton => true;

        protected override void BuildLayout(ref StaticGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 407, 241, 3500);
            builder.AddLabel(22, 17, 0x34, "Bio Engineering");

            AddRecipe(ref builder, 49, BioRecipe.Vial, "Sample vial", "1 bottle, 25 iron ingots, 1,000 organics");
            AddRecipe(ref builder, 98, BioRecipe.VialSet, "Vial set", "4 bottles, 50 iron ingots, 5,000 organics");
            AddRecipe(ref builder, 147, BioRecipe.Deed, "Creation deed", "25 iron ingots, 5 blank scrolls");
            builder.AddButton(22, 200, 4005, 4007, 4);
            builder.AddLabel(61, 200, 0, "Donor journal");
        }

        private static void AddRecipe(
            ref StaticGumpBuilder builder, int y, BioRecipe recipe, string name, string ingredients
        )
        {
            builder.AddButton(22, y, 4005, 4007, (int)recipe);
            builder.AddLabel(61, y, 0, name);
            builder.AddLabel(61, y + 21, 0, ingredients);
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile is PlayerMobile { } journalOwner && info.ButtonID == 4 &&
                BioEngineeringBook.CanUseItem(journalOwner, tool))
            {
                BioDonorJournal.DisplayTo(journalOwner);
                return;
            }

            if (sender.Mobile is PlayerMobile player && info.ButtonID >= (int)BioRecipe.Vial &&
                info.ButtonID <= (int)BioRecipe.Deed)
            {
                if (tool.TryCraft(player, (BioRecipe)info.ButtonID) && tool.Charges > 0)
                {
                    tool.ShowRecipes(player);
                }
            }
        }
    }
}

[SerializationGenerator(0)]
public partial class BioEngineeringBook : Item
{
    // Britannia Solo reserves this recipe ID for the character's study unlock.
    private const int StudyRecipeId = 100_000;

    [Constructible]
    public BioEngineeringBook() : base(0x0FF4)
    {
        Hue = 0x59;
    }

    public override string DefaultName => "bio engineering handbook";
    public override double DefaultWeight => 1.0;

    internal static bool HasStudied(PlayerMobile player) => player.HasRecipe(StudyRecipeId);

    private static bool CheckSkills(PlayerMobile player)
    {
        if (player.Skills.AnimalTaming.Base >= 100.0 && player.Skills.Tinkering.Base >= 100.0)
        {
            return true;
        }

        player.SendMessage("You need 100 Animal Taming and 100 Tinkering.");
        return false;
    }

    internal static bool CheckTraining(PlayerMobile player)
    {
        if (!HasStudied(player))
        {
            player.SendMessage("Read a bio engineering handbook first.");
            return false;
        }

        return CheckSkills(player);
    }

    internal static bool CanUseItem(PlayerMobile player, Item item)
    {
        if (player.Deleted || !player.CheckAlive())
        {
            return false;
        }

        if (!item.Deleted && player.Backpack != null && item.IsChildOf(player.Backpack) &&
            item.IsAccessibleTo(player))
        {
            return true;
        }

        player.SendMessage("Put the item in your backpack first.");
        return false;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile player || !CanUseItem(player, this) || !CheckSkills(player))
        {
            return;
        }

        if (HasStudied(player))
        {
            player.SendMessage("You already know bio engineering.");
            return;
        }

        player.AcquireRecipe(StudyRecipeId);
        player.MarkDirty();
        Delete();
        player.SendMessage("You learn how to craft bio engineering supplies and gather samples.");
    }
}

[SerializationGenerator(0)]
public partial class BioOrganicMatter : Item
{
    [Constructible]
    public BioOrganicMatter() : this(1)
    {
    }

    [Constructible]
    public BioOrganicMatter(int amount) : base(0x0F81)
    {
        Stackable = true;
        Amount = amount;
        Hue = 0x59;
    }

    public override string DefaultName => "organic matter";
    public override double DefaultWeight => 0.001;
}
