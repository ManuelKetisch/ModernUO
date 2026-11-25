using System;
using Server.Items;

namespace Server.Engines.BulkOrders;

internal sealed class WoodworkRewardCalculator : RewardCalculator
{
    private readonly BODType _profession;
    private static readonly WoodworkRewardCalculator _carpentry = new(BODType.Carpentry);
    private static readonly WoodworkRewardCalculator _fletching = new(BODType.Fletching);

    internal static WoodworkRewardCalculator For(BODType profession) =>
        profession == BODType.Carpentry ? _carpentry : _fletching;

    private WoodworkRewardCalculator(BODType profession)
    {
        _profession = profession;
        var skill = WoodworkBulkOrders.SkillFor(profession);
        var decor = ExcelsiorBodRewardGroups.Reward(1, BuildDecoration);
        var secondaryDecor = ExcelsiorBodRewardGroups.Reward(1, BuildSecondaryDecoration);
        var fletching = _profession == BODType.Fletching;
        var minorExtra = fletching ? Dip(5) : ExcelsiorBodRewardGroups.Reward(1, () => new Nails(80));
        var majorExtra = fletching ? Dip(25) : ExcelsiorBodRewardGroups.Reward(1, () => new Nails(150));

        Groups =
        [
            new(50, new RewardItem(3, _ => new SturdyAxe()), new RewardItem(1, _ => new Board(80))),
            new(200, new RewardItem(3, _ => new GargoylesAxe()), ExcelsiorBodRewardGroups.Armor(skill, 1)),
            new(400, new RewardItem(2, _ => new ProspectorsAxe()), ExcelsiorBodRewardGroups.Armor(skill, 1)),
            new(450, new RewardItem(1, _ => new StainOfDurability()), ExcelsiorBodRewardGroups.Armor(skill, 1)),
            new(500, ExcelsiorBodRewardGroups.Weight(Runic(CraftResource.PineWood), 3), decor),
            new(550, ExcelsiorBodRewardGroups.Weight(Runic(CraftResource.PineWood), 2), decor),
            new(600, Runic(CraftResource.ExcelsiorAshWood), decor),
            new(650, ExcelsiorBodRewardGroups.Weight(Runic(CraftResource.ExcelsiorAshWood), 2), minorExtra),
            new(700, Runic(CraftResource.MahoganyWood), minorExtra),
            new(750, Runic(CraftResource.MahoganyWood), new RewardItem(1, _ => new PowerScroll(skill, 120))),
            new(800, Runic(CraftResource.ExcelsiorYewWood), new RewardItem(1, _ => new PowerScroll(SkillName.Lumberjacking, 120)),
                ExcelsiorBodRewardGroups.Armor(skill, 3)),
            new(850, Runic(CraftResource.ExcelsiorYewWood), new RewardItem(1, _ => new AncientTradeHammer(skill, 10)),
                ExcelsiorBodRewardGroups.Armor(skill, 3)),
            new(900, Runic(CraftResource.ExcelsiorOakWood),
                fletching ? new RewardItem(1, _ => new AncientTradeHammer(skill, 10)) : secondaryDecor,
                ExcelsiorBodRewardGroups.Armor(skill, 3)),
            new(950, Runic(CraftResource.ZircoteWood), new RewardItem(1, _ => new AncientTradeHammer(skill, 20)), majorExtra),
            new(1000, Runic(CraftResource.EbonyWood),
                fletching ? new RewardItem(1, _ => new AncientTradeHammer(skill, 20)) : secondaryDecor, majorExtra),
            new(1050, Runic(CraftResource.BambooWood), new RewardItem(1, _ => new AncientTradeHammer(skill, 30))),
            new(1100, Runic(CraftResource.PurpleHeartWood),
                fletching ? new RewardItem(1, _ => new AncientTradeHammer(skill, 30)) : secondaryDecor),
            new(1150, Runic(CraftResource.Redwood), new RewardItem(1, _ => new AncientTradeHammer(skill, 40))),
            new(1200, Runic(CraftResource.PetrifiedWood),
                fletching ? new RewardItem(1, _ => new AncientTradeHammer(skill, 40)) : secondaryDecor),
            new(1250, fletching ? Dip(50) : new RewardItem(1, _ => new BagOfResources()), ExcelsiorBodRewardGroups.Armor(skill, 5))
        ];
    }

    private static RewardItem Dip(int uses) => new(1, _ => new ArrowDipTub((ArrowDipKind)Utility.Random(5), uses));

    private Item BuildDecoration() => _profession == BODType.Fletching
        ? Utility.Random(3) switch
        {
            0 => new ArcheryButteDeed(),
            1 => new Item(0xF41) { Name = "stack of arrows" },
            _ => new Item(0x1BD7) { Name = "stack of boards" }
        }
        : Utility.Random(3) switch
        {
            0 => new WoodenChair { Name = "unfinished wooden chair" },
            1 => new WoodenChest { Name = "unfinished wooden chest" },
            _ => new SmallCrate { Name = "unfinished crate" }
        };

    private static Item BuildSecondaryDecoration() => Utility.Random(3) switch
    {
        0 => new Item(0x2A5A) { Name = "bone table" },
        1 => new Item(0x2A7B) { Name = "stone coffin" },
        _ => new Item(0x2A93) { Name = "sacrificial altar" }
    };

    private RewardItem Runic(CraftResource wood) => new(1, _ => _profession == BODType.Carpentry
        ? new RunicCarpenterSaw(wood)
        : new RunicFletcherTool(wood, 20));

    internal RewardGroup GetRewards(SmallBOD deed) => LookupRewards(ComputePoints(deed));
    internal RewardGroup GetRewards(LargeBOD deed) => LookupRewards(ComputePoints(deed));

    public override int ComputePoints(int quantity, bool exceptional, BulkMaterialType material, int itemCount, Type type)
    {
        var amount = quantity switch
        {
            20 => 50,
            15 => 25,
            _  => 10
        };

        var size = itemCount switch
        {
            >= 6 => 400,
            5    => 300,
            4    => 200,
            3    => _profession == BODType.Fletching ? 200 : 100,
            2    => 50,
            _    => 0
        };

        var wood = material switch
        {
            BulkMaterialType.Pine       => 200,
            BulkMaterialType.Ash        => 250,
            BulkMaterialType.Mahogany   => 300,
            BulkMaterialType.Yew        => 350,
            BulkMaterialType.Oak        => 400,
            BulkMaterialType.Zircote    => 450,
            BulkMaterialType.Ebony      => 500,
            BulkMaterialType.Bamboo     => 550,
            BulkMaterialType.PurpleHeart => 600,
            BulkMaterialType.Redwood     => 650,
            BulkMaterialType.Petrified   => 700,
            BulkMaterialType.Heartwood   => 350,
            BulkMaterialType.Bloodwood   => 400,
            BulkMaterialType.Frostwood   => 450,
            _                          => 0
        };

        return amount + size + wood + (exceptional ? 200 : 0);
    }

    public override int ComputeGold(int quantity, bool exceptional, BulkMaterialType material, int itemCount, Type type) =>
        quantity * itemCount * 35 + ComputePoints(quantity, exceptional, material, itemCount, type) * 5;
}
