using System;
using Server;
using Server.Engines.BioEngineering;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public class BioPreviewTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void ProjectionResolvesBodyCapsAndSharesTheAssemblyRules()
    {
        int[] prowess = [1800, 4500, 40, 70, 100, 100, 1200, 1200, 1200];
        int[] environment = [500, 1300, 100, 100, 1000];
        int[] mental = [1400, 5000, 100, 100, 1000, 1000, 1000, 1000];
        int[] mimic = [400, 0, 0, (int)BioTrait.SearingStrike, 0, 0, 0, 0, 0];
        Assert.True(BioPreview.TryProject(prowess, environment, mental, mimic, out var human));
        Assert.Equal(3000, human.Hits);
        Assert.Equal(18, human.MinimumDamage);
        Assert.Equal(26, human.MaximumDamage);
        Assert.Equal(70, human.PhysicalResistance);
        Assert.Equal(70, human.FireResistance);
        Assert.Equal(70, human.ColdResistance);
        Assert.Equal(70, human.EnergyResistance);
        Assert.Equal(70, human.PoisonResistance);

        var bio = _world.Track(new BioConstruct(_world.NewPlayer()));
        Assert.True(bio.ApplySamples(prowess, environment, mental, mimic));
        Assert.Equal(human.Hits, bio.HitsMax);
        Assert.Equal(human.PhysicalResistance, bio.PhysicalResistanceSeed);
        Assert.Equal(human.Trait, bio.Trait);

        mimic[0] = 211;
        Assert.True(BioPreview.TryProject(prowess, environment, mental, mimic, out var animal));
        Assert.Equal(75, animal.PhysicalResistance);
        Assert.Equal(75, animal.EnergyResistance);
        Assert.False(BioPreview.TryProject([1], environment, mental, mimic, out _));
    }

    [Fact]
    public void AStaleSamplingPreviewDoesNotConsumeTheVial()
    {
        var player = NewEngineer();
        var donor = NewHostile();
        var vial = _world.TrackItem(new BioVial());
        player.Backpack.DropItem(vial);

        var captured = BioVial.Capture(BioSampleKind.Prowess, donor);
        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = player;
        player.NetState = state;
        try
        {
            BioVial.SamplePreviewGump.DisplayTo(player, vial, BioSampleKind.Prowess, donor, captured);
            var preview = player.FindGump<BioVial.SamplePreviewGump>();
            Assert.NotNull(preview);
            donor.RawStr += 1;
            preview.OnResponse(state, new RelayInfo(1, [], [], [], []));
            Assert.False(vial.Deleted);
            Assert.False(vial.Filled);
            Assert.NotSame(preview, player.FindGump<BioVial.SamplePreviewGump>());
        }
        finally
        {
            player.CloseGump<BioVial.SamplePreviewGump>();
            player.NetState = null;
            state.Mobile = null;
        }

        Assert.Equal(0.3501375, BioVial.SampleChance(1), 8);
        Assert.Equal(BioVial.SampleChance(4000), BioVial.SampleChance(int.MaxValue));
    }

    [Fact]
    public void OrganicYieldUsesPlayerCreditAndExcludesReleasedPets()
    {
        var player = NewEngineer();
        var wild = NewHostile();
        wild.LastKiller = player;
        Assert.Equal(108, BioOrganicHarvest.Yield(wild, 0.0));
        Assert.Equal(0, BioOrganicHarvest.Yield(wild, 0.99));

        var follower = _world.NewCreature();
        follower.SetControlMaster(player);
        wild.LastKiller = follower;
        Assert.Equal(108, BioOrganicHarvest.Yield(wild, 0.0));

        wild.SetControlMaster(player);
        wild.Owners.Add(player);
        wild.SetControlMaster(null);
        Assert.Equal(0, BioOrganicHarvest.Yield(wild, 0.0));

        var harmless = NewHostile();
        harmless.Karma = 0;
        harmless.LastKiller = player;
        Assert.Equal(0, BioOrganicHarvest.Yield(harmless, 0.0));
    }

    private PlayerMobile NewEngineer()
    {
        var player = _world.NewPlayer();
        player.Player = true;
        player.AddItem(new Backpack());
        player.MoveToWorld(new Point3D(1800, 1800, 0), Map.Felucca);
        player.Skills.AnimalTaming.Base = 100;
        player.Skills.Tinkering.Base = 100;
        var book = _world.TrackItem(new BioEngineeringBook());
        player.Backpack.DropItem(book);
        book.OnDoubleClick(player);
        return player;
    }

    private TestCreature NewHostile()
    {
        var creature = _world.NewCreature();
        creature.MoveToWorld(new Point3D(1801, 1800, 0), Map.Felucca);
        creature.Fame = 1_000;
        creature.Karma = -1_000;
        return creature;
    }
}
