using System;
using Server.Items;

namespace Server.Engines.BulkOrders;

internal static class ExcelsiorBodRewardGroups
{
    private static readonly RewardItem _sturdyShovel = Reward(1, () => new SturdyShovel());
    private static readonly RewardItem _sturdyPickaxe = Reward(1, () => new SturdyPickaxe());
    private static readonly RewardItem _sturdyHammer = Reward(1, () => new SturdySmithHammer());
    private static readonly RewardItem _prospector = Reward(1, () => new ProspectorsTool());
    private static readonly RewardItem _gargoylePick = Reward(1, () => new GargoylesPickaxe());
    private static readonly RewardItem _smithDecor = Reward(1, () => Utility.Random(3) switch
    {
        0 => new DecorativeShieldSword1North(),
        1 => new SwordDisplay1NorthArtifact(),
        _ => new DecorativeShield4()
    });
    private static readonly RewardItem _coloredAnvil = Reward(1, () => new ColoredAnvil());
    private static readonly RewardItem _coloredForge = Reward(1, () => new ColoredForgeDeed());

    private static readonly RewardGroup[] _smith =
    [
        new(0, _sturdyShovel, _sturdyHammer),
        new(25, _sturdyPickaxe, _sturdyHammer),
        new(50, Weight(_sturdyShovel, 9), Armor(SkillName.Blacksmith, 1)),
        new(200, Weight(_sturdyPickaxe, 9), Armor(SkillName.Blacksmith, 1)),
        new(400, Weight(_prospector, 9), Armor(SkillName.Blacksmith, 1)),
        new(450, Reward(2, () => new PowderOfTemperament()), _gargoylePick, _smithDecor),
        new(500, Hammer(CraftResource.ExcelsiorDullCopper), _gargoylePick, _smithDecor),
        new(550, Weight(Hammer(CraftResource.ExcelsiorDullCopper), 3), Weight(Hammer(CraftResource.ExcelsiorShadowIron), 2)),
        new(600, Hammer(CraftResource.ExcelsiorShadowIron), _coloredForge),
        new(625, Weight(Hammer(CraftResource.ExcelsiorShadowIron), 3), _coloredAnvil),
        new(650, Hammer(CraftResource.ExcelsiorCopper), _smithDecor),
        new(675, Weight(Hammer(CraftResource.ExcelsiorCopper), 3), _coloredAnvil),
        new(700, Hammer(CraftResource.ExcelsiorBronze), _smithDecor),
        new(750, Reward(1, () => new AncientSmithyHammer(10)), Armor(SkillName.Blacksmith, 3)),
        new(800, _gargoylePick, Armor(SkillName.Blacksmith, 3)),
        new(850, Reward(1, () => new AncientSmithyHammer(15)), Armor(SkillName.Blacksmith, 3)),
        new(925, Hammer(CraftResource.ExcelsiorGold), Reward(1, () => new MetalDyeTub())),
        new(1000, Reward(1, () => new AncientSmithyHammer(30)), Reward(1, () => new MetalDyeTub())),
        new(1050, Hammer(CraftResource.ExcelsiorAgapite), Reward(1, () => new SmithersProtector())),
        new(1100, Reward(1, () => new AncientSmithyHammer(60)), Reward(1, () => new SmithersProtector())),
        new(1150, Hammer(CraftResource.ExcelsiorVerite), Armor(SkillName.Blacksmith, 5)),
        new(1200, Hammer(CraftResource.ExcelsiorValorite), Armor(SkillName.Blacksmith, 5)),
        new(1250, Hammer(CraftResource.Blaze), Armor(SkillName.Blacksmith, 5)),
        new(1300, Hammer(CraftResource.Ice), Reward(1, () => new BagOfResources())),
        new(1350, Hammer(CraftResource.Toxic), Reward(1, () => new BagOfResources())),
        new(1400, Hammer(CraftResource.Electrum), Reward(1, () => new SharpeningBlade())),
        new(1450, Hammer(CraftResource.Platinum), Reward(1, () => new SharpeningBlade()))
    ];

    private static readonly RewardItem _coloredLoom = Reward(1, () => new ColoredLoomDeed());
    private static readonly RewardItem _coloredScissors = Reward(1, () => new ColoredScissors());
    private static readonly RewardItem _tapestry = Reward(1, () => new LightFlowerTapestryEastDeed());
    private static readonly RewardItem _sturdyKit = Reward(1, () => new SturdySewingKit());
    private static readonly RewardItem _mastersKnife = Reward(1, () => new MastersKnife());
    private static readonly RewardItem _tailorDecor = Reward(1, () => Utility.Random(3) switch
    {
        0 => new LightFlowerTapestryEastDeed(),
        1 => new RoseOfTrinsic(),
        _ => new StretchedHideArtifact()
    });
    private static readonly RewardItem _gargoyleKnife = Reward(1, () => new GargoylesKnife());
    private static readonly RewardItem _leather = Reward(1, () => new SpinedLeather(100));
    private static readonly RewardItem _chargedDye = Reward(1, () => new ChargedDyeTub());
    private static readonly RewardItem _tailorsProtector = Reward(1, () => new TailorsProtector());

    private static readonly RewardGroup[] _tailor =
    [
        new(0, Cloth(1, 0x482), _coloredLoom),
        new(50, Cloth(1, 0x48D), _coloredLoom),
        new(100, Cloth(1, 0x489), _leather),
        new(150, Cloth(6, 0x47E), Weight(_leather, 3), Armor(SkillName.Tailoring, 1)),
        new(200, Cloth(2, 0x455), Weight(_leather, 2), Armor(SkillName.Tailoring, 1)),
        new(300, Reward(1, () => new StretchedHideArtifact()), _coloredScissors, Armor(SkillName.Tailoring, 1)),
        new(350, SewingKit(CraftResource.ExcelsiorSpinedLeather), _coloredScissors),
        new(400, Weight(_tapestry, 3), _coloredScissors, _sturdyKit),
        new(450, Reward(1, () => new BrownBearRugEastDeed()), _sturdyKit),
        new(500, _tailorDecor, _mastersKnife),
        new(550, Reward(1, () => new ClothingBlessDeed()), _tailorDecor, _mastersKnife),
        new(600, SewingKit(CraftResource.ExcelsiorHornedLeather), _mastersKnife),
        new(650, SewingKit(CraftResource.ExcelsiorHornedLeather), Armor(SkillName.Tailoring, 3)),
        new(700, SewingKit(CraftResource.ExcelsiorBarbedLeather), Armor(SkillName.Tailoring, 3)),
        new(750, SewingKit(CraftResource.ExcelsiorBarbedLeather), Armor(SkillName.Tailoring, 3), _gargoyleKnife),
        new(800, SewingKit(CraftResource.PolarLeather), _chargedDye, _gargoyleKnife),
        new(850, SewingKit(CraftResource.PolarLeather), _chargedDye),
        new(900, SewingKit(CraftResource.SyntheticLeather), Armor(SkillName.Tailoring, 5)),
        new(950, SewingKit(CraftResource.BlazeLeather), Armor(SkillName.Tailoring, 5)),
        new(1000, SewingKit(CraftResource.DaemonicLeather), _tailorsProtector, Armor(SkillName.Tailoring, 5)),
        new(1050, SewingKit(CraftResource.ShadowLeather), _tailorsProtector),
        new(1100, SewingKit(CraftResource.FrostLeather), Reward(1, () => new BagOfResources())),
        new(1150, SewingKit(CraftResource.EtherealLeather), Reward(1, () => new BagOfResources()))
    ];

    internal static RewardGroup Smith(int points) => Find(_smith, points);
    internal static RewardGroup Tailor(int points) => Find(_tailor, points);

    private static RewardGroup Find(RewardGroup[] groups, int points)
    {
        for (var i = groups.Length - 1; i > 0; i--)
        {
            if (points >= groups[i].Points)
            {
                return groups[i];
            }
        }

        return groups[0];
    }

    internal static RewardItem Reward(int weight, Func<Item> factory) => new(weight, _ => factory());

    internal static RewardItem Weight(RewardItem item, int weight) => new(weight, item.Constructor, item.Type);

    private static RewardItem Hammer(CraftResource material) => Reward(1, () => new RunicHammer(material, 15));

    private static RewardItem SewingKit(CraftResource material) => Reward(1, () => new RunicSewingKit(material, 15));

    private static RewardItem Cloth(int weight, int hue) => Reward(weight, () => new UncutCloth(100) { Hue = hue });

    internal static RewardItem Armor(SkillName skill, int amount) => Reward(1, () => new CraftSkillBonusArmor(skill, amount));
}
