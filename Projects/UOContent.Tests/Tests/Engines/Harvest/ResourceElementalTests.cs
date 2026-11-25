using System;
using Server;
using Server.Engines.Harvest;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public sealed class ResourceElementalTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void GargoyleAxeRaisesOneChopAndLeavesBankGradeAlone()
    {
        var normal = new HarvestResource(0, 0, 100, 0, typeof(Log));
        var oak = new HarvestResource(65, 25, 105, 0, typeof(OakLog));
        var ash = new HarvestResource(80, 40, 120, 0, typeof(AshLog));
        var definition = new HarvestDefinition
        {
            Veins = [new(500, 0, normal, null), new(300, 0, oak, normal), new(200, 0, ash, oak)]
        };
        var bank = new HarvestBank(definition, definition.Veins[0]) { Vein = definition.Veins[1] };
        var cutter = _world.NewPlayer();
        var regularAxe = _world.TrackItem(new Hatchet());
        var gargoyleAxe = _world.TrackItem(new GargoylesAxe(3));
        var system = Lumberjacking.System;

        Assert.Same(system, gargoyleAxe.HarvestSystem);
        Assert.Same(definition.Veins[1], system.MutateVein(cutter, regularAxe, definition, bank, null, bank.Vein));
        Assert.Same(definition.Veins[2], system.MutateVein(cutter, gargoyleAxe, definition, bank, null, bank.Vein));
        Assert.Same(definition.Veins[1], bank.Vein);
        Assert.Same(definition.Veins[2], system.MutateVein(cutter, gargoyleAxe, definition, bank, null, definition.Veins[2]));
        Assert.Equal(3, gargoyleAxe.UsesRemaining);
    }

    [Fact]
    public void WoodEncounterRequiresPrimaryMatchingWoodAndValidRoll()
    {
        var ordinary = new HarvestResource(0, 0, 100, 0, typeof(Log));
        var ash = new HarvestResource(80, 40, 120, 0, typeof(AshLog));
        var vein = new HarvestVein(20, 0.5, ash, ordinary);
        var cutter = _world.Track(new PlayerMobile { Player = true, Race = Race.Human });
        cutter.MoveToWorld(new Point3D(1800, 1800, 0), Map.Felucca);

        Assert.Null(Lumberjacking.TryAwakenWoodElemental(cutter, vein, ash, typeof(AshLog), 0.10));
        Assert.Null(Lumberjacking.TryAwakenWoodElemental(cutter, vein, ordinary, typeof(Log), 0.00));
        Assert.Null(Lumberjacking.TryAwakenWoodElemental(cutter, vein, ash, typeof(OakLog), 0.00));

        var elemental = Assert.IsType<WoodElemental>(
            Lumberjacking.TryAwakenWoodElemental(cutter, vein, ash, typeof(AshLog), 0.00)
        );
        _world.Track(elemental);
        Assert.Same(cutter, elemental.Combatant);
        Assert.Same(cutter.Map, elemental.Map);
        Assert.True(cutter.InRange(elemental.Location, 1));
        Assert.NotEqual(cutter.Location, elemental.Location);
        Assert.Equal(CraftResource.AshWood, elemental.Resource);
        Assert.Equal(50, elemental.Backpack.FindItemByType<AshLog>().Amount);

        var runic = elemental.Backpack.FindItemByType<RunicFletcherTool>();
        Assert.Equal(CraftResource.AshWood, runic.Resource);
        Assert.Equal(5, runic.UsesRemaining);
    }

    [Fact]
    public void WoodGradePersistsAndTheFletcherKitProducesMagicalProperties()
    {
        var elemental = _world.Track(new WoodElemental(CraftResource.Frostwood));
        var copy = _world.Track(new WoodElemental(World.NewMobile));
        var writer = new BufferWriter(true);
        elemental.Serialize(writer);
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        copy.Deserialize(reader);
        Assert.Equal(writer.Position, reader.Position);
        Assert.Equal(CraftResource.Frostwood, copy.Resource);
        Assert.Contains("frostwood", copy.DefaultName);

        var runic = elemental.Backpack.FindItemByType<RunicFletcherTool>();
        var bow = _world.TrackItem(new Bow());
        runic.ApplyAttributesTo(bow);
        Assert.True(!bow.Attributes.IsEmpty || !bow.WeaponAttributes.IsEmpty || !bow.SkillBonuses.IsEmpty);
    }

    [Fact]
    public void OreEncounterRequiresPrimaryOreAndAwardsMatchingLimitedRunicHammer()
    {
        var ordinary = new HarvestResource(0, 0, 100, 0, typeof(IronOre), typeof(Granite));
        var resource = new HarvestResource(
            99, 59, 139, 0, typeof(ValoriteOre), typeof(ValoriteGranite), typeof(ValoriteElemental)
        );
        var vein = new HarvestVein(14, 0.5, resource, ordinary);
        var miner = _world.Track(new PlayerMobile { Player = true, Race = Race.Human });
        miner.MoveToWorld(new Point3D(1800, 1800, 0), Map.Felucca);

        Assert.Null(Mining.TryAwakenOreElemental(miner, vein, resource, typeof(ValoriteOre), 0.10));
        Assert.Null(Mining.TryAwakenOreElemental(miner, vein, ordinary, typeof(IronOre), 0.00));
        Assert.Null(Mining.TryAwakenOreElemental(miner, vein, resource, typeof(ValoriteGranite), 0.00));
        var elemental = Assert.IsType<ValoriteElemental>(
            Mining.TryAwakenOreElemental(miner, vein, resource, typeof(ValoriteOre), 0.00)
        );
        _world.Track(elemental);
        Assert.Same(miner, elemental.Combatant);
        Assert.Equal(25, elemental.Backpack.FindItemByType<ValoriteOre>().Amount);

        var hammer = elemental.Backpack.FindItemByType<RunicHammer>();
        Assert.Equal(CraftResource.Valorite, hammer.Resource);
        Assert.Equal(5, hammer.UsesRemaining);
        Assert.Null(_world.Track(new ValoriteElemental(25)).Backpack.FindItemByType<RunicHammer>());

        var legacy = Assert.IsType<ValoriteElemental>(
            Mining.TryAwakenOreElemental(miner, vein, resource, null, 0.00, true)
        );
        _world.Track(legacy);
        Assert.Null(legacy.Backpack.FindItemByType<RunicHammer>());
        Assert.Null(legacy.Backpack.FindItemByType<RunicTinkerTool>());
    }


    [Fact]
    public void IlshenarMetalRanksExtendWithoutChangingOrdinarySelectionWeights()
    {
        var definition = Mining.System.OreAndStone;
        var regular = definition.GetVeins(Map.Trammel);
        var regional = definition.GetVeins(Map.Ilshenar);
        var routineMetal = regular[8];
        var exoticMetal = regional[^1];
        Assert.Equal(1000u, definition.VeinWeights);
        Assert.Equal(0u, regular[9].VeinChance);
        Assert.Equal(typeof(BlazeOre), definition.NextVein(Map.Trammel, routineMetal).PrimaryResource.Types[0]);
        Assert.Equal(typeof(PlatinumOre), exoticMetal.PrimaryResource.Types[0]);
        Assert.Equal(119, exoticMetal.PrimaryResource.ReqSkill);
        Assert.Equal(1000u, definition.GetVeinWeights(Map.Ilshenar));
        Assert.Null(definition.NextVein(Map.Ilshenar, exoticMetal));
        Assert.All(regional, element => Assert.True(element.VeinChance > 0));
    }

    [Fact]
    public void LeatherEncounterUsesPromotedGradeAndAllowsOnlyOneFollowUp()
    {
        var cutter = _world.Track(new PlayerMobile { Player = true, Race = Race.Human });
        cutter.MoveToWorld(new Point3D(1820, 1800, 0), Map.Felucca);
        var source = _world.NewCreature();
        Assert.Null(ResourceEncounters.TryAwakenLeatherElemental(cutter, source, HideType.Synthetic, 0.10));
        var elemental = Assert.IsType<LeatherElemental>(
            ResourceEncounters.TryAwakenLeatherElemental(cutter, source, HideType.Synthetic, 0.00)
        );
        _world.Track(elemental);
        Assert.Equal(50, elemental.Hides);
        Assert.Equal(HideType.Synthetic, elemental.HideType);
        Assert.False(elemental.FollowUp);
        var kit = elemental.Backpack.FindItemByType<RunicSewingKit>();
        Assert.Equal(CraftResource.SyntheticLeather, kit.Resource);
        Assert.Equal(5, kit.UsesRemaining);
        Assert.Null(elemental.Backpack.FindItemByType<SyntheticHides>());

        var followUp = Assert.IsType<LeatherElemental>(
            ResourceEncounters.TryAwakenLeatherElemental(cutter, elemental, HideType.Blaze, 0.00)
        );
        _world.Track(followUp);
        Assert.True(followUp.FollowUp);
        Assert.Null(ResourceEncounters.TryAwakenLeatherElemental(cutter, followUp, HideType.Ethereal, 0.00));

        var restored = _world.Track(new LeatherElemental(World.NewMobile));
        var writer = new BufferWriter(true);
        followUp.Serialize(writer);
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        restored.Deserialize(reader);
        Assert.Equal(writer.Position, reader.Position);
        Assert.True(restored.FollowUp);
        Assert.Equal(CraftResource.BlazeLeather, restored.Resource);
    }

    [Fact]
    public void GargoyleKnifeRequiresPossessionAndSturdyLogsCannotSubstituteForOrdinaryMaterial()
    {
        var carver = _world.NewPlayer();
        var stranger = _world.NewPlayer();
        carver.AddItem(_world.TrackItem(new Backpack()));
        var knife = _world.TrackItem(new GargoylesKnife(2));
        carver.Backpack.DropItem(knife);
        knife.OnDoubleClick(stranger);
        Assert.Null(stranger.Target);
        knife.OnDoubleClick(carver);
        Assert.NotNull(carver.Target);
        Assert.Equal(2, knife.UsesRemaining);

        var axe = _world.TrackItem(new SturdyAxe());
        Assert.Equal(180, axe.UsesRemaining);
        Assert.False(typeof(Log).IsAssignableFrom(typeof(GradeALog)));
        Assert.Equal(CraftResource.None, CraftResources.GetFromType(typeof(GradeALog)));

        carver.MoveToWorld(new Point3D(1800, 1800, 0), Map.Felucca);
        var lumber = Lumberjacking.System;
        var definition = lumber.GetDefinition();
        Assert.Equal(typeof(GradeALog), lumber.MutateType(typeof(Log), carver, axe,
            definition, carver.Map, carver.Location, null));
        Assert.Equal(typeof(GradeALog), lumber.MutateType(typeof(PineLog), carver, axe,
            definition, carver.Map, carver.Location, null));
        var ordinary = _world.TrackItem(new Hatchet());
        Assert.Equal(typeof(PineLog), lumber.MutateType(typeof(PineLog), carver, ordinary,
            definition, carver.Map, carver.Location, null));
    }
}
