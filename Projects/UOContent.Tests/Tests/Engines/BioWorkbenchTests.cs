using System;
using Server;
using Server.Engines.BioEngineering;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public sealed class BioWorkbenchTests : IDisposable
{
    private readonly MobileTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void WorkbenchSwapsOneSampleInAFilledBackpackAndPersistsBothDonors()
    {
        var engineer = CreateEngineer();
        var stranger = CreateEngineer();
        var set = PutInPack(engineer, new BioVialSet());
        var oldVial = Sample(engineer, "first donor");
        Assert.True(set.TryAdd(engineer, oldVial));
        Assert.True(oldVial.Deleted);
        var replacement = Sample(engineer, "second donor");
        engineer.Backpack.MaxItems = engineer.Backpack.TotalItems;
        Assert.False(set.TrySwap(stranger, replacement));
        Assert.NotEqual("second donor", set.SampleStatus(BioSampleKind.Prowess));

        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = engineer;
        engineer.NetState = state;
        try
        {
            set.OnDoubleClick(engineer);
            var bench = engineer.FindGump<BioWorkbenchGump>();
            Assert.NotNull(bench);
            Assert.NotNull(engineer.Backpack);
            bench.OnResponse(state, new RelayInfo(2, [], [], [], []));
            engineer.Target.Invoke(engineer, replacement);
        }
        finally
        {
            engineer.CloseGump<BioWorkbenchGump>();
            engineer.NetState = null;
            state.Mobile = null;
        }

        Assert.Same(engineer.Backpack, replacement.Parent);
        Assert.False(replacement.Deleted);
        Assert.Equal("first donor", replacement.DonorName);
        Assert.Equal("second donor", set.SampleStatus(BioSampleKind.Prowess));
        Assert.True(set.SwapUsed);
        Assert.False(set.TrySwap(engineer, replacement));

        var writer = new BufferWriter(true);
        set.Serialize(writer);
        var copy = _harness.TrackItem(new BioVialSet(World.NewItem));
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        copy.Deserialize(reader);
        Assert.Equal(writer.Position, reader.Position);
        Assert.True(copy.SwapUsed);
        Assert.Equal("second donor", copy.SampleStatus(BioSampleKind.Prowess));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ModifiedSamplesRequireReviewBeforeInfusing(bool changeAfterOpeningTarget)
    {
        var engineer = CreateEngineer();
        var other = CreateEngineer();
        var set = PutInPack(engineer, new BioVialSet());
        for (var kind = BioSampleKind.Prowess; kind <= BioSampleKind.Mimic; kind++)
        {
            Assert.True(set.TryAdd(engineer, Sample(engineer, "original donor", kind)));
        }

        Assert.True(set.TryProject(out _));
        var construct = _harness.Track(Assert.IsType<BioConstruct>(
            PutInPack(engineer, new BioCreationDeed()).TryCreate(engineer)
        ));
        var replacement = Sample(other, "replacement donor");

        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = engineer;
        engineer.NetState = state;
        try
        {
            set.OnDoubleClick(engineer);
            var original = engineer.FindGump<BioWorkbenchGump>();
            Assert.NotNull(original);
            if (changeAfterOpeningTarget)
            {
                original.OnResponse(state, new RelayInfo(3, [], [], [], []));
                Assert.NotNull(engineer.Target);
            }

            other.Backpack.DropItem(set);
            Assert.True(set.TrySwap(other, replacement));
            engineer.Backpack.DropItem(set);

            if (changeAfterOpeningTarget)
            {
                engineer.Target.Invoke(engineer, construct);
            }
            else
            {
                original.OnResponse(state, new RelayInfo(3, [], [], [], []));
            }

            Assert.False(set.Deleted);
            Assert.False(construct.Assembled);
            Assert.Null(engineer.Target);
            var refreshed = engineer.FindGump<BioWorkbenchGump>();
            Assert.NotNull(refreshed);
            Assert.NotSame(original, refreshed);
            refreshed.OnResponse(state, new RelayInfo(3, [], [], [], []));
            engineer.Target.Invoke(engineer, construct);
            Assert.True(construct.Assembled);
            Assert.True(set.Deleted);
        }
        finally
        {
            engineer.CloseGump<BioWorkbenchGump>();
            engineer.NetState = null;
            state.Mobile = null;
        }
    }

    private PlayerMobile CreateEngineer()
    {
        var player = _harness.NewPlayer();
        player.Race = Race.Human;
        player.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);
        player.AddItem(_harness.TrackItem(new Backpack()));
        player.Skills.AnimalTaming.BaseFixedPoint = 1000;
        player.Skills.Tinkering.BaseFixedPoint = 1000;
        PutInPack(player, new BioEngineeringBook()).OnDoubleClick(player);
        return player;
    }

    private T PutInPack<T>(PlayerMobile owner, T item) where T : Item
    {
        owner.Backpack.DropItem(_harness.TrackItem(item));
        return item;
    }

    private BioVial Sample(PlayerMobile owner, string donorName, BioSampleKind kind = BioSampleKind.Prowess)
    {
        var creature = _harness.NewCreature();
        creature.Name = donorName;
        creature.MoveToWorld(new Point3D(1001, 1000, 0), owner.Map);
        var vial = PutInPack(owner, new BioVial());
        Assert.True(vial.TrySample(owner, kind, creature, 0.0));
        return vial;
    }
}
