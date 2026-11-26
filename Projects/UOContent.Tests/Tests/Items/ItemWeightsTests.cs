using System;
using Server;
using Server.Engines.PetProgression;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Items;

[Collection("Sequential UOContent Tests")]
public class ItemWeightsTests : IDisposable
{
    private readonly MobileTestHarness _harness = new();

    public ItemWeightsTests()
    {
        ItemWeights.ResetDefaults();
    }

    public void Dispose()
    {
        ItemWeights.ResetDefaults();
        _harness.Dispose();
    }

    [Fact]
    public void AllSemanticTiersExposePositiveNormalizedDefaults()
    {
        foreach (var category in Enum.GetValues<ItemWeightCategory>())
        {
            var weight = ItemWeights.GetWeight(category);
            Assert.True(weight >= ItemWeights.MinWeight, $"Category {category} must remain strictly positive.");
            Assert.True(weight <= ItemWeights.MaxWeight, $"Category {category} must stay within bounds.");
        }

        Assert.Equal(ItemWeights.DefaultReagent, ItemWeights.Reagent);
        Assert.Equal(ItemWeights.DefaultBandage, ItemWeights.Bandage);
        Assert.Equal(ItemWeights.DefaultAmmunition, ItemWeights.Ammunition);
        Assert.Equal(ItemWeights.DefaultGem, ItemWeights.Gem);
        Assert.Equal(ItemWeights.DefaultScroll, ItemWeights.Scroll);
        Assert.Equal(ItemWeights.DefaultSmallSupply, ItemWeights.SmallSupply);
        Assert.Equal(ItemWeights.DefaultCookingIngredient, ItemWeights.CookingIngredient);
        Assert.Equal(ItemWeights.DefaultPotion, ItemWeights.Potion);
        Assert.Equal(ItemWeights.DefaultRefinedMaterial, ItemWeights.RefinedMaterial);
        Assert.Equal(ItemWeights.DefaultProcessedMaterial, ItemWeights.ProcessedMaterial);
        Assert.Equal(ItemWeights.DefaultRawWood, ItemWeights.RawWood);
        Assert.Equal(ItemWeights.DefaultRawFiber, ItemWeights.RawFiber);
        Assert.Equal(ItemWeights.DefaultRawHide, ItemWeights.RawHide);
        Assert.Equal(ItemWeights.DefaultRawSpecialOre, ItemWeights.RawSpecialOre);
    }

    [Fact]
    public void CastingAndRareReagentsUseLightweightReagentTier()
    {
        Item[] reagents =
        [
            _harness.TrackItem(new BlackPearl(100)),
            _harness.TrackItem(new Bloodmoss(100)),
            _harness.TrackItem(new Garlic(100)),
            _harness.TrackItem(new Ginseng(100)),
            _harness.TrackItem(new MandrakeRoot(100)),
            _harness.TrackItem(new Nightshade(100)),
            _harness.TrackItem(new SpidersSilk(100)),
            _harness.TrackItem(new SulfurousAsh(100)),
            _harness.TrackItem(new BatWing(100)),
            _harness.TrackItem(new DaemonBlood(100)),
            _harness.TrackItem(new GraveDust(100)),
            _harness.TrackItem(new NoxCrystal(100)),
            _harness.TrackItem(new PigIron(100)),
            _harness.TrackItem(new DeadWood(100)),
            _harness.TrackItem(new DragonsBlood(100)),
            _harness.TrackItem(new DaemonBone(100)),
            _harness.TrackItem(new Bone(100)),
            _harness.TrackItem(new FertileDirt(100)),
            _harness.TrackItem(new GreenThorns(100)),
            _harness.TrackItem(new OrangePetals(100)),
            _harness.TrackItem(new RedLeaves(100)),
            _harness.TrackItem(new ZoogiFungus(100)),
            _harness.TrackItem(new PowderOfTranslocation(100)),
            _harness.TrackItem(new EtherealPowder(100)),
            _harness.TrackItem(new Beeswax(100)),
            _harness.TrackItem(new TribalBerry(100)),
            _harness.TrackItem(new AncientWyrmScale(100)),
            _harness.TrackItem(new SwordsmanshipEssence(100)),
            _harness.TrackItem(new Blight(100)),
            _harness.TrackItem(new LuminescentFungi(100)),
            _harness.TrackItem(new CapturedEssence(100)),
            _harness.TrackItem(new Corruption(100)),
            _harness.TrackItem(new Putrefication(100)),
            _harness.TrackItem(new Taint(100)),
            _harness.TrackItem(new SwitchItem(100))
        ];

        foreach (var reagent in reagents)
        {
            Assert.Equal(ItemWeights.Reagent, reagent.Weight);
            Assert.Equal(1, reagent.PileWeight);
        }

        var fragment = _harness.TrackItem(new MeteoriteFragment { Amount = 100 });
        var volcanicGlass = _harness.TrackItem(new VolcanicGlass { Amount = 100 });
        Assert.Equal(ItemWeights.Reagent, fragment.Weight);
        Assert.Equal(1, fragment.PileWeight);
        Assert.Equal(ItemWeights.Reagent, volcanicGlass.Weight);
        Assert.Equal(1, volcanicGlass.PileWeight);

        var stack = _harness.TrackItem(new BlackPearl(1));
        Assert.Equal(1, stack.PileWeight);
        stack.Amount = 100;
        Assert.Equal(1, stack.PileWeight);
        stack.Amount = 101;
        Assert.Equal(2, stack.PileWeight);
        stack.Amount = 500;
        Assert.Equal(5, stack.PileWeight);
    }

    [Fact]
    public void BandagesAmmunitionGemsScrollsPotionsAndSuppliesMatchTiers()
    {
        var bandages = _harness.TrackItem(new Bandage(500));
        Assert.Equal(ItemWeights.Bandage, bandages.Weight);
        Assert.Equal(5, bandages.PileWeight);

        Item[] ammo =
        [
            _harness.TrackItem(new Arrow(1000)),
            _harness.TrackItem(new Bolt(1000)),
            _harness.TrackItem(new DippedArrow(ArrowDipKind.ArmorPiercing, 1000)),
            _harness.TrackItem(new Feather(1000)),
            _harness.TrackItem(new Shaft(1000))
        ];
        foreach (var item in ammo)
        {
            Assert.Equal(ItemWeights.Ammunition, item.Weight);
            Assert.Equal(10, item.PileWeight);
        }

        Item[] gems =
        [
            _harness.TrackItem(new Amber(100)),
            _harness.TrackItem(new Diamond(100)),
            _harness.TrackItem(new Ruby(100)),
            _harness.TrackItem(new BlueDiamond(100)),
            _harness.TrackItem(new FireRuby(100)),
            _harness.TrackItem(new PerfectEmerald(100)),
            _harness.TrackItem(new WhitePearl(100))
        ];
        foreach (var gem in gems)
        {
            Assert.Equal(ItemWeights.Gem, gem.Weight);
            Assert.Equal(1, gem.PileWeight);
        }

        var blankScrolls = _harness.TrackItem(new BlankScroll(100));
        var recallScrolls = _harness.TrackItem(new RecallScroll(50));
        Assert.Equal(ItemWeights.Scroll, blankScrolls.Weight);
        Assert.Equal(2, blankScrolls.PileWeight);
        Assert.Equal(ItemWeights.Scroll, recallScrolls.Weight);
        Assert.Equal(1, recallScrolls.PileWeight);

        Item[] smallSupplies =
        [
            _harness.TrackItem(new Bottle(20)),
            _harness.TrackItem(new Lockpick(20)),
            _harness.TrackItem(new RecallRune()),
            _harness.TrackItem(new OilCloth()),
            _harness.TrackItem(new SmokeBomb()),
            _harness.TrackItem(new EggBomb()),
            _harness.TrackItem(new PetOintment()),
            _harness.TrackItem(new RestorativePetOintment()),
            _harness.TrackItem(new Shuriken(10)),
            _harness.TrackItem(new FukiyaDarts(10)),
            _harness.TrackItem(new SpoolOfThread(20)),
            _harness.TrackItem(new DarkYarn(20)),
            _harness.TrackItem(new JinxedSkull()),
            _harness.TrackItem(new HellhoundRib())
        ];
        foreach (var supply in smallSupplies)
        {
            Assert.Equal(ItemWeights.SmallSupply, supply.Weight);
            Assert.Equal(1, supply.PileWeight);
        }

        var healPotions = _harness.TrackItem(new GreaterHealPotion { Amount = 25 });
        var curePotions = _harness.TrackItem(new GreaterCurePotion { Amount = 5 });
        Assert.Equal(ItemWeights.Potion, healPotions.Weight);
        Assert.Equal(5, healPotions.PileWeight);
        Assert.Equal(ItemWeights.Potion, curePotions.Weight);
        Assert.Equal(1, curePotions.PileWeight);
    }

    [Fact]
    public void CraftingAndCookingMaterialsPreserveProcessingIncentivesAndMassConservation()
    {
        var cloth = _harness.TrackItem(new Cloth(50));
        var bolt = _harness.TrackItem(new BoltOfCloth(1));
        Assert.Equal(cloth.Weight * 50, bolt.Weight, 6);
        Assert.Equal(ItemWeights.RefinedMaterial, cloth.Weight);
        Assert.Equal(ItemWeights.RefinedMaterial, _harness.TrackItem(new UncutCloth()).Weight);
        Assert.Equal(ItemWeights.RefinedMaterial, _harness.TrackItem(new IronIngot()).Weight);
        Assert.Equal(ItemWeights.RefinedMaterial, _harness.TrackItem(new BlazeIngot()).Weight);
        Assert.Equal(ItemWeights.RefinedMaterial, _harness.TrackItem(new ExcelsiorIronIngot()).Weight);
        Assert.Equal(ItemWeights.RefinedMaterial, _harness.TrackItem(new MeteoricBronzeIngot()).Weight);
        Assert.Equal(ItemWeights.RefinedMaterial, _harness.TrackItem(new ConductiveSilverIngot()).Weight);
        Assert.Equal(ItemWeights.RefinedMaterial, _harness.TrackItem(new RedScales()).Weight);

        var sackFlour = _harness.TrackItem(new SackFlour());
        var dough = _harness.TrackItem(new Dough());
        var honey = _harness.TrackItem(new JarHoney());
        var vanilla = _harness.TrackItem(new Vanilla(20));
        var sugar = _harness.TrackItem(new SackOfSugar(20));
        var rawFishSteak = _harness.TrackItem(new RawFishSteak(4));
        var wholeFish = _harness.TrackItem(new Fish(1));
        Assert.Equal(ItemWeights.CookingIngredient, dough.Weight);
        Assert.Equal(ItemWeights.CookingIngredient, honey.Weight);
        Assert.Equal(ItemWeights.CookingIngredient, vanilla.Weight);
        Assert.Equal(ItemWeights.CookingIngredient, sugar.Weight);
        Assert.Equal(1, vanilla.PileWeight);
        Assert.Equal(ItemWeights.CookingIngredient * ItemWeights.FlourUsesPerSack, sackFlour.Weight, 6);
        Assert.Equal(rawFishSteak.Weight * ItemWeights.SteaksPerFish, wholeFish.Weight, 6);

        var emptyKeg = _harness.TrackItem(new PotionKeg());
        var fullKeg = _harness.TrackItem(new PotionKeg { Type = PotionEffect.HealGreater, Held = 100 });
        var loosePotions = _harness.TrackItem(new GreaterHealPotion { Amount = 100 });
        Assert.Equal(ItemWeights.DefaultPotionKegEmpty, emptyKeg.Weight, 6);
        Assert.Equal(16, fullKeg.PileWeight);
        Assert.True(fullKeg.PileWeight < loosePotions.PileWeight);

        var hides = _harness.TrackItem(new Hides(10));
        var leather = _harness.TrackItem(new Leather(10));
        Assert.True(leather.Weight < hides.Weight);
        Assert.Equal(ItemWeights.RawHide, hides.Weight);
        Assert.Equal(ItemWeights.ProcessedMaterial, leather.Weight);
        Assert.Equal(ItemWeights.RawHide, _harness.TrackItem(new PolarHides()).Weight);
        Assert.Equal(ItemWeights.ProcessedMaterial, _harness.TrackItem(new PolarLeather()).Weight);

        var log = _harness.TrackItem(new Log(10));
        var board = _harness.TrackItem(new Board(10));
        var pineLog = _harness.TrackItem(new PineLog(10));
        var pineBoard = _harness.TrackItem(new PineBoard(10));
        Assert.True(board.Weight < log.Weight);
        Assert.True(pineBoard.Weight < pineLog.Weight);
        Assert.Equal(ItemWeights.RawWood, log.Weight);
        Assert.Equal(ItemWeights.ProcessedMaterial, board.Weight);
        Assert.Equal(ItemWeights.RawWood, pineLog.Weight);
        Assert.Equal(ItemWeights.ProcessedMaterial, pineBoard.Weight);
        Assert.Equal(ItemWeights.RawFiber, _harness.TrackItem(new Cotton()).Weight);
        Assert.Equal(ItemWeights.RawFiber, _harness.TrackItem(new Wool()).Weight);
        Assert.Equal(ItemWeights.RawFiber, _harness.TrackItem(new TaintedWool()).Weight);
        Assert.Equal(ItemWeights.ProcessedMaterial, _harness.TrackItem(new Sand()).Weight);
        Assert.Equal(ItemWeights.ProcessedMaterial, _harness.TrackItem(new Kindling()).Weight);
        Assert.Equal(ItemWeights.RawSpecialOre, _harness.TrackItem(new MeteoricIronOre()).Weight);
    }

    [Fact]
    public void FullAdventurerSupplyLoadoutRemainsLightweightInBackpack()
    {
        var player = _harness.NewPlayer();
        player.RawStr = 100;
        var pack = _harness.TrackItem(new Backpack());
        player.AddItem(pack);
        player.MoveToWorld(new Point3D(1100, 1100, 0), Map.Felucca);

        Assert.Same(pack, player.Backpack);

        Item[] loadout =
        [
            new BlackPearl(200),
            new Bloodmoss(200),
            new Garlic(200),
            new Ginseng(200),
            new MandrakeRoot(200),
            new Nightshade(200),
            new SpidersSilk(200),
            new SulfurousAsh(200),
            new BatWing(200),
            new DaemonBlood(200),
            new GraveDust(200),
            new NoxCrystal(200),
            new PigIron(200),
            new Bandage(500),
            new Arrow(500),
            new RecallScroll(50),
            new GreaterHealPotion { Amount = 25 },
            new Bottle(20),
            new Lockpick(40)
        ];

        foreach (var item in loadout)
        {
            _harness.TrackItem(item);
            Assert.True(pack.TryDropItem(player, item, false));
        }

        // 13 stacks * 2 st (26) + 500 bandages (5) + 500 arrows (5) + 50 scrolls (1)
        // + 25 potions (5) + 20 bottles (1) + 40 lockpicks (2) = 45 stones.
        Assert.Equal(45, pack.TotalWeight);
        Assert.False(StaminaSystem.IsOverloaded(player));
    }

    [Fact]
    public void GlobalScaleAndClampingPreventZeroOrInvalidWeights()
    {
        ItemWeights.ApplySettings(
            globalScale: 2.0,
            reagent: 0.0,
            bandage: -5.0,
            potion: double.NaN,
            rawHide: 999.0
        );

        Assert.Equal(2.0, ItemWeights.GlobalScale);
        Assert.Equal(ItemWeights.MinWeight, ItemWeights.Reagent);
        Assert.Equal(ItemWeights.MinWeight, ItemWeights.Bandage);
        Assert.Equal(ItemWeights.MinWeight, ItemWeights.Potion);
        Assert.Equal(ItemWeights.MaxWeight, ItemWeights.RawHide);
        Assert.Equal(ItemWeights.DefaultScroll * 2.0, ItemWeights.Scroll, 6);

        var reagent = _harness.TrackItem(new BlackPearl(100));
        Assert.Equal(ItemWeights.MinWeight, reagent.Weight);

        ItemWeights.ApplySettings(globalScale: double.PositiveInfinity);
        Assert.Equal(1.0, ItemWeights.GlobalScale);
    }
}
