using System;
using Server;
using Server.Engines.BioEngineering;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public class BioStarterContractTests : IDisposable
{
    private readonly MobileTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void ThreeSurveyedFamiliesEarnAPersistentOneTimeKit()
    {
        var contract = new BioStarterContract();
        var player = _harness.NewPlayer();
        player.MoveToWorld(new Point3D(1100, 1100, 0), Map.Felucca);
        var tinker = _harness.Track(new Tinker());
        tinker.MoveToWorld(new Point3D(1101, 1100, 0), player.Map);

        try
        {
            var first = PrepareDonor(new TestCreature());
            var duplicate = PrepareDonor(new TestCreature());
            var second = PrepareDonor(new SecondFamily());
            var third = PrepareDonor(new ThirdFamily());

            Assert.False(BioDonorJournal.CanSurvey(player, first));
            Assert.False(BioStarterContract.TryRequest(player, tinker));
            player.Skills.AnimalTaming.BaseFixedPoint = 1000;
            player.Skills.Tinkering.BaseFixedPoint = 1000;
            Assert.True(BioStarterContract.TryRequest(player, tinker));
            Assert.True(BioDonorJournal.CanSurvey(player, first));

            BioStarterContract.RecordDiscovery(player, first);
            BioStarterContract.RecordDiscovery(player, duplicate);
            Assert.Equal((true, 1, false), BioStarterContract.GetStatus(player));
            Assert.False(BioStarterContract.TryRequest(player, tinker));

            BioDonorJournal.RecordSample(player, BioSampleKind.Mental, second);
            BioStarterContract.RecordDiscovery(player, third);
            Assert.Equal((true, 3, false), BioStarterContract.GetStatus(player));
            Assert.False(BioStarterContract.TryRequest(player, tinker)); // No backpack.
            Assert.Equal((true, 3, false), BioStarterContract.GetStatus(player));

            ReloadContract(contract, player);
            Assert.Equal((true, 3, false), BioStarterContract.GetStatus(player));
            player.AddItem(new Backpack());
            Assert.True(BioStarterContract.TryRequest(player, tinker));
            var kit = Assert.IsType<Bag>(Assert.Single(player.Backpack.Items));
            Assert.NotNull(kit.FindItemByType<BioEngineeringBook>());
            Assert.NotNull(kit.FindItemByType<BioEngineeringTool>());
            Assert.NotNull(kit.FindItemByType<BioVialSet>());
            Assert.NotNull(kit.FindItemByType<BioCreationDeed>());
            Assert.Equal(12, kit.GetAmount(typeof(BioVial)));

            ReloadContract(contract, player);
            Assert.Equal((true, 0, true), BioStarterContract.GetStatus(player));
            Assert.False(BioStarterContract.TryRequest(player, tinker));
            Assert.Single(player.Backpack.Items);
        }
        finally
        {
            BioStarterContract.OnPlayerDeleted(player);
        }
    }

    private BaseCreature PrepareDonor(TestCreature donor)
    {
        _harness.Track(donor);
        donor.MoveToWorld(new Point3D(1102, 1100, 0), Map.Felucca);
        return donor;
    }

    private static void ReloadContract(BioStarterContract contract, PlayerMobile player)
    {
        var writer = new BufferWriter(true);
        contract.Serialize(writer);
        BioStarterContract.OnPlayerDeleted(player);
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        contract.Deserialize(reader);
        Assert.Equal(writer.Position, reader.Position);
    }

    private sealed class SecondFamily : TestCreature;
    private sealed class ThirdFamily : TestCreature;
}
