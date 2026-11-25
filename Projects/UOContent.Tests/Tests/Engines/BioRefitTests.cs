using System;
using Server;
using Server.Engines.BioEngineering;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public sealed class BioRefitTests : IDisposable
{
    private readonly MobileTestHarness _harness = new();
    private readonly NotorietyHandler _previousNotoriety = Notoriety.Handler;

    public BioRefitTests() => Notoriety.Handler = NotorietyHandlers.MobileNotoriety;

    public void Dispose()
    {
        try
        {
            _harness.Dispose();
        }
        finally
        {
            Notoriety.Handler = _previousNotoriety;
        }
    }

    [Fact]
    public void CommandPreviewsThenChargesOnlyOnConfirmation()
    {
        var owner = CreateEngineer();
        var construct = CreateConstruct(owner);
        var bronze = PutInPack(owner, new BronzeIngot(35));
        BioRefit.Initialize();

        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = owner;
        owner.NetState = state;
        try
        {
            Assert.True(CommandSystem.Handle(owner, "[BioRefit"));
            owner.Target.Invoke(owner, construct);
            var options = Assert.IsType<BioRefit.RefitGump>(owner.FindGump<BioRefit.RefitGump>());
            options.OnResponse(state, new RelayInfo((int)BioRefitChange.BronzeFrame, [], [], [], []));
            var preview = Assert.IsType<BioRefit.RefitGump>(owner.FindGump<BioRefit.RefitGump>());
            Assert.NotSame(options, preview);
            Assert.Equal(BioFrameAlloy.Iron, construct.RefitState.Frame);
            Assert.Equal(35, bronze.Amount);

            preview.OnResponse(state, new RelayInfo(20, [], [], [], []));
            Assert.Equal(BioFrameAlloy.Bronze, construct.RefitState.Frame);
            Assert.Equal(15, bronze.Amount);
        }
        finally
        {
            owner.CloseGump<BioRefit.RefitGump>();
            owner.NetState = null;
            state.Mobile = null;
        }
    }

    [Fact]
    public void RefittingRechecksOwnerBenchCombatInventoryAndPreviousChoices()
    {
        var owner = CreateEngineer();
        var stranger = CreateEngineer();
        var construct = CreateConstruct(owner);
        var bronze = PutInPack(owner, new BronzeIngot(19));
        var agapite = PutInPack(owner, new AgapiteIngot(20));
        var bronzeQuote = Quote(construct, owner, BioRefitChange.BronzeFrame);
        var agapiteQuote = Quote(construct, owner, BioRefitChange.AgapiteFrame);

        Assert.False(construct.TryRefit(stranger, bronzeQuote));
        Assert.False(construct.TryRefit(owner, bronzeQuote));
        Assert.Equal(19, bronze.Amount);

        bronze.Amount = 25;
        var foe = CreateFoe(owner);
        owner.Combatant = foe;
        Assert.False(construct.TryRefit(owner, bronzeQuote));
        owner.Combatant = null;

        construct.AggressiveAction(foe);
        construct.Combatant = null;
        Assert.False(construct.TryRefit(owner, bronzeQuote));
        foe.Delete();

        construct.IsDeadPet = true;
        Assert.False(construct.TryRefit(owner, bronzeQuote));
        construct.IsDeadPet = false;

        var originalLocation = owner.Location;
        var remoteLocation = new Point3D(originalLocation.X + 20, originalLocation.Y, originalLocation.Z);
        owner.MoveToWorld(remoteLocation, owner.Map);
        construct.MoveToWorld(remoteLocation, owner.Map);
        Assert.False(construct.TryRefit(owner, bronzeQuote));
        construct.MoveToWorld(originalLocation, owner.Map);
        owner.MoveToWorld(originalLocation, owner.Map);
        Assert.Equal(25, bronze.Amount);

        Assert.True(construct.TryRefit(owner, bronzeQuote));
        Assert.False(construct.TryRefit(owner, agapiteQuote));
        Assert.Equal(20, agapite.Amount);
        Refit(construct, owner, BioRefitChange.IronFrame);
        Assert.False(construct.TryRefit(owner, bronzeQuote));
        Assert.Equal(5, bronze.Amount);
    }

    [Theory]
    [InlineData((int)BioRefitChange.BronzeFrame, 8, 0, 0)]
    [InlineData((int)BioRefitChange.AgapiteFrame, 0, 4, 0)]
    [InlineData((int)BioRefitChange.ShadowIronFrame, 0, 0, 8)]
    public void FrameEffectOnlyAppliesToWildCombatAndHasACooldown(
        int change, int expectedHealth, int expectedMana, int expectedDrain
    )
    {
        var owner = CreateEngineer();
        var construct = CreateConstruct(owner, BioTrait.None);
        var foe = CreateFoe(owner);
        GiveRefitMaterials(owner);
        Refit(construct, owner, (BioRefitChange)change);
        construct.Hits = 100;
        construct.Mana = 40;
        foe.Stam = 50;

        foe.NoKillAwards = true;
        construct.OnLandedMeleeAttack(foe);
        construct.OnReceivedMeleeHit(foe);
        Assert.Equal(100, construct.Hits);
        Assert.Equal(40, construct.Mana);
        Assert.Equal(50, foe.Stam);
        foe.NoKillAwards = false;

        for (var attack = 0; attack < 2; attack++)
        {
            construct.OnLandedMeleeAttack(foe);
            construct.OnReceivedMeleeHit(foe);
            Assert.Equal(100 + expectedHealth, construct.Hits);
            Assert.Equal(40 + expectedMana, construct.Mana);
            Assert.Equal(50 - expectedDrain, foe.Stam);
        }

        Refit(construct, owner, BioRefitChange.IronFrame);
        Refit(construct, owner, (BioRefitChange)change);
        construct.OnLandedMeleeAttack(foe);
        construct.OnReceivedMeleeHit(foe);
        Assert.Equal(100 + expectedHealth, construct.Hits);
        Assert.Equal(40 + expectedMana, construct.Mana);
        Assert.Equal(50 - expectedDrain, foe.Stam);
    }

    [Theory]
    [InlineData(25, 0)]
    [InlineData(30, 8)]
    public void BronzeFrameHealsAfterDonorRecoveryAndStartsCooldownOnlyWhenItHeals(
        int initialMissingHealth, int secondMissingHealth
    )
    {
        var owner = CreateEngineer();
        var construct = CreateConstruct(owner, BioTrait.QuickRecovery);
        var foe = CreateFoe(owner);
        GiveRefitMaterials(owner);
        Refit(construct, owner, BioRefitChange.BronzeFrame);
        Refit(construct, owner, BioRefitChange.InstallRegulator);

        construct.Hits = construct.HitsMax - initialMissingHealth;
        construct.Stam = 7;
        construct.OnReceivedMeleeHit(foe);
        Assert.Equal(construct.HitsMax, construct.Hits);
        Assert.Equal(0, construct.Stam);

        construct.Hits = construct.HitsMax - 8;
        construct.OnReceivedMeleeHit(foe);
        Assert.Equal(construct.HitsMax - secondMissingHealth, construct.Hits);
        Assert.Equal(0, construct.Stam);
    }

    [Fact]
    public void RefitAndBondedGhostSavePreserveBuildAndShareExistingMoveBudget()
    {
        var owner = CreateEngineer();
        var construct = CreateConstruct(owner);
        var foe = CreateFoe(owner);
        GiveRefitMaterials(owner);
        construct.IsBonded = true;
        construct.Hits = 100;
        construct.Stam = 9;
        construct.Mana = 40;

        Refit(construct, owner, BioRefitChange.AgapiteFrame);
        Refit(construct, owner, BioRefitChange.InstallCircuit);
        Refit(construct, owner, BioRefitChange.InstallRegulator);
        Assert.Equal((100, 9, 40), (construct.Hits, construct.Stam, construct.Mana));

        construct.OnLandedMeleeAttack(foe);
        Assert.Equal((100, 0, 44), (construct.Hits, construct.Stam, construct.Mana));
        construct.OnReceivedMeleeHit(foe);
        construct.OnReceivedMeleeHit(foe);
        Assert.Equal(5, construct.Stam);

        construct.IsDeadPet = true;
        var writer = new BufferWriter(true);
        construct.Serialize(writer);
        var restored = _harness.Track(new BioConstruct(World.NewMobile));
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        restored.Deserialize(reader);
        Assert.Equal(writer.Position, reader.Position);
        Assert.True(restored.IsBonded);
        Assert.True(restored.IsDeadPet);
        Assert.Same(owner, restored.ControlMaster);
        Assert.Equal(construct.Body, restored.Body);
        Assert.Equal(construct.Trait, restored.Trait);
        Assert.Equal(construct.HitsMax, restored.HitsMax);
        Assert.Equal(construct.Skills.Wrestling.Base, restored.Skills.Wrestling.Base);
        Assert.Equal(new BioRefitState(BioFrameAlloy.Agapite, true, true), restored.RefitState);

        restored.IsDeadPet = false;
        restored.MoveToWorld(owner.Location, owner.Map);
        restored.Stam = 30;
        restored.Mana = 40;
        restored.OnLandedMeleeAttack(foe);
        restored.OnReceivedMeleeHit(foe);
        Assert.Equal(30, restored.Stam);
        Assert.Equal(40, restored.Mana);
    }

    private PlayerMobile CreateEngineer()
    {
        var owner = _harness.Track(new PlayerMobile());
        owner.Player = true;
        owner.Race = Race.Human;
        var location = new Point3D(1800, 1800, 0);
        owner.MoveToWorld(location, Map.Felucca);
        _harness.TrackItem(new Anvil()).MoveToWorld(location, owner.Map);
        _harness.TrackItem(new Forge()).MoveToWorld(location, owner.Map);
        owner.AddItem(_harness.TrackItem(new Backpack()));
        owner.Skills.AnimalTaming.Base = 100;
        owner.Skills.Tinkering.Base = 100;
        PutInPack(owner, new BioEngineeringBook()).OnDoubleClick(owner);
        return owner;
    }

    private BioConstruct CreateConstruct(PlayerMobile owner, BioTrait trait = BioTrait.SearingStrike)
    {
        var construct = _harness.Track(new BioConstruct(owner));
        Assert.True(construct.SetControlMaster(owner));
        construct.MoveToWorld(owner.Location, owner.Map);
        Assert.True(construct.ApplySamples(
            [120, 250, 10, 14, 15, 20, 500, 500, 500],
            [100, 200, 0, 0, 0], [65, 100, 0, 0, 0, 0, 0, 0],
            [400, 0, 0, (int)trait, 0, 0, 0, 0, 0]
        ));
        return construct;
    }

    private TestCreature CreateFoe(PlayerMobile owner)
    {
        var foe = _harness.NewCreature();
        foe.MoveToWorld(new Point3D(owner.X + 1, owner.Y, owner.Z), owner.Map);
        foe.SetStr(150);
        foe.SetDex(100);
        foe.Hits = 150;
        return foe;
    }

    private T PutInPack<T>(PlayerMobile owner, T item) where T : Item
    {
        owner.Backpack.DropItem(_harness.TrackItem(item));
        return item;
    }

    private void GiveRefitMaterials(PlayerMobile owner)
    {
        PutInPack(owner, new BronzeIngot(50));
        PutInPack(owner, new AgapiteIngot(50));
        PutInPack(owner, new ShadowIronIngot(50));
    }

    private static BioRefitQuote Quote(BioConstruct construct, PlayerMobile owner, BioRefitChange change) =>
        Assert.IsType<BioRefitQuote>(construct.QuoteRefit(owner, change));

    private static void Refit(BioConstruct construct, PlayerMobile owner, BioRefitChange change) =>
        Assert.True(construct.TryRefit(owner, Quote(construct, owner, change)));
}
