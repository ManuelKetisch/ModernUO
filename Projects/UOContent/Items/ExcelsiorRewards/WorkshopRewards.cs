using System;
using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Engines.WeaponLevelingServices;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0)]
public sealed partial class ColoredForgeAddon : SmallForgeAddon
{
    [Constructible]
    public ColoredForgeAddon(int hue = 0x7DA) => Hue = hue;

    public override bool RetainDeedHue => true;
    public override BaseAddonDeed Deed => new ColoredForgeDeed(Hue);
}

[SerializationGenerator(0)]
public sealed partial class ColoredForgeDeed : SmallForgeDeed
{
    [Constructible]
    public ColoredForgeDeed(int hue = 0x7DA) => Hue = hue;

    public override string DefaultName => "colored forge deed";
    public override BaseAddon Addon => new ColoredForgeAddon(Hue);
}

[SerializationGenerator(0)]
public sealed partial class ColoredLoomAddon : LoomSouthAddon
{
    [Constructible]
    public ColoredLoomAddon(int hue = 0x4A7) => Hue = hue;

    public override bool RetainDeedHue => true;
    public override BaseAddonDeed Deed => new ColoredLoomDeed(Hue);
}

[SerializationGenerator(0)]
public sealed partial class ColoredLoomDeed : LoomSouthDeed
{
    [Constructible]
    public ColoredLoomDeed(int hue = 0x4A7) => Hue = hue;

    public override string DefaultName => "colored loom deed";
    public override BaseAddon Addon => new ColoredLoomAddon(Hue);
}

[Flippable(0xF9F, 0xF9E)]
[SerializationGenerator(0)]
public sealed partial class ColoredScissors : Scissors
{
    [Constructible]
    public ColoredScissors(int hue = 0x4A7) => Hue = hue;

    public override string DefaultName => "colored scissors";
}

[SerializationGenerator(0)]
public sealed partial class SturdySmithHammer : SmithHammer
{
    [Constructible]
    public SturdySmithHammer(int uses = 180) : base(uses) => Hue = 0x973;

    public override string DefaultName => "sturdy smith's hammer";
}

[SerializationGenerator(0)]
public sealed partial class SturdySewingKit : SewingKit
{
    [Constructible]
    public SturdySewingKit(int uses = 180) : base(uses) => Hue = 0x973;

    public override string DefaultName => "sturdy sewing kit";
}

[SerializationGenerator(0)]
public sealed partial class BagOfResources : BaseContainer
{
    [Constructible]
    public BagOfResources() : base(0xE76) => Hue = 0x489;

    public override string DefaultName => "bag of resources";
    public override double DefaultWeight => 100.0;
    public override int DefaultMaxItems => 0;
    public override int DefaultMaxWeight => 0;

    public override bool CheckHold(Mobile mobile, Item item, bool message, bool checkItems, int plusItems, int plusWeight)
    {
        if (!Accepts(item))
        {
            if (message)
            {
                mobile?.SendMessage("This bag holds only raw ore, logs and hides.");
            }

            return false;
        }

        return base.CheckHold(mobile, item, message, checkItems, plusItems, plusWeight - item.PileWeight);
    }

    internal static bool Accepts(Item item) => item is
        BaseOre or BaseHides or Log or ExcelsiorLog or GradeALog or VolcanicGlass or MeteoricIronOre;

    public override int GetTotal(TotalType type) => type == TotalType.Weight ? 0 : base.GetTotal(type);

    public override void UpdateTotal(Item sender, TotalType type, int delta)
    {
        if (type != TotalType.Weight)
        {
            base.UpdateTotal(sender, type, delta);
        }
    }
}

[SerializationGenerator(0)]
public abstract partial class ChargedRewardDyeTub : DyeTub, IUsesRemaining
{
    [SerializableField(0)]
    [InvalidateProperties]
    private int _usesRemaining;

    protected ChargedRewardDyeTub(int uses) => UsesRemaining = Math.Max(1, uses);

    bool IUsesRemaining.ShowUsesRemaining
    {
        get => true;
        set { }
    }

    protected abstract string Prompt { get; }

    protected abstract bool ApplyHue(PlayerMobile owner, Item target);

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1060584, UsesRemaining);
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile owner || !WeaponServiceTransfer.IsInBackpack(owner, this))
        {
            from.SendMessage("Keep the dye tub in your backpack.");
            return;
        }

        owner.SendMessage(Prompt);
        owner.BeginTarget(1, false, TargetFlags.None, (_, targeted) => TryDye(owner, targeted as Item));
    }

    internal bool TryDye(PlayerMobile owner, Item target)
    {
        if (UsesRemaining <= 0 || !WeaponServiceTransfer.IsInBackpack(owner, this) ||
            !WeaponServiceTransfer.IsInBackpack(owner, target) || target.QuestItem || target.Hue == DyedHue ||
            !ApplyHue(owner, target))
        {
            owner.SendMessage("Choose eligible equipment in your backpack.");
            return false;
        }

        UsesRemaining--;
        owner.PlaySound(0x23E);
        if (UsesRemaining == 0)
        {
            Delete();
        }

        return true;
    }
}

[SerializationGenerator(0)]
public sealed partial class MetalDyeTub : ChargedRewardDyeTub
{
    [Constructible]
    public MetalDyeTub(int uses = 10) : base(uses)
    {
    }

    public override string DefaultName => "metal dye tub";
    protected override string Prompt => "Choose metal equipment in your backpack.";

    protected override bool ApplyHue(PlayerMobile owner, Item target)
    {
        var metal = target switch
        {
            BaseWeapon weapon => weapon is not VolcanicGlassDagger &&
                CraftResources.GetType(weapon.Resource) == CraftResourceType.Metal &&
                DefBlacksmithy.CraftSystem?.CraftItems.SearchForSubclass(weapon.GetType()) != null,
            BaseArmor armor => CraftResources.GetType(armor.Resource) == CraftResourceType.Metal,
            BaseJewel jewel => CraftResources.GetType(jewel.Resource) == CraftResourceType.Metal,
            _ => false
        };
        if (metal)
        {
            target.Hue = DyedHue;
        }

        return metal;
    }
}

[SerializationGenerator(0)]
public sealed partial class ChargedDyeTub : ChargedRewardDyeTub
{
    [Constructible]
    public ChargedDyeTub(int uses = 10) : base(uses)
    {
    }

    public override string DefaultName => "charged dye tub";
    protected override string Prompt => "Choose a dyeable item in your backpack.";

    protected override bool ApplyHue(PlayerMobile owner, Item target) =>
        target is IDyable dyeable && dyeable.Dye(owner, this);
}
