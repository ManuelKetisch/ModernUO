using System;
using Server;
using Server.Engines.PetBreeding;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public class PetBreedingFocusTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void FocusMovesOnlyRolledBonusAndKeepsEachSourceAboveItsBase()
    {
        var hits = PetBreedingFocus.IndexOf(PetTrainingStat.Hits);
        var strength = PetBreedingFocus.IndexOf(PetTrainingStat.Strength);
        var dexterity = PetBreedingFocus.IndexOf(PetTrainingStat.Dexterity);
        var minimumDamage = PetBreedingFocus.IndexOf(PetTrainingStat.MinimumDamage);
        var maximumDamage = PetBreedingFocus.IndexOf(PetTrainingStat.MaximumDamage);

        var baseline = new int[PetBreedingFocus.Stats.Length];
        baseline[hits] = 995;
        baseline[strength] = 100;
        baseline[dexterity] = 60;
        baseline[minimumDamage] = 12;
        baseline[maximumDamage] = 18;
        var rolled = (int[])baseline.Clone();
        rolled[hits] += 2;
        rolled[strength] += 5;
        rolled[dexterity] += 4;
        rolled[minimumDamage] += 3;
        rolled[maximumDamage] += 4;

        PetBreedingFocus.MoveBonus(rolled, baseline, hits, 1000);

        Assert.Equal(1000, rolled[hits]);
        Assert.Equal(103, rolled[strength]);
        Assert.Equal(63, rolled[dexterity]);
        Assert.Equal(15, rolled[minimumDamage]);
        Assert.Equal(22, rolled[maximumDamage]);
    }

    [Fact]
    public void FocusForecastAccountsForPeerBonusesAndTheSelectedCap()
    {
        var first = _world.NewCreature();
        var second = _world.NewCreature();
        first.HitsMaxSeed = second.HitsMaxSeed = 997;
        first.RawStr = 100;
        second.RawStr = 180;

        Assert.Equal(new PetBreedingFocus.Preview(997, 997, 997, 1000),
            PetBreedingFocus.GetPreview(first, second, PetTrainingStat.Hits));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void BreedingRefreshesTheAdvertisedRangeBeforeCommitting(bool partner, bool focused)
    {
        var owner = CreatePlayer();
        var other = partner ? CreatePlayer() : owner;
        var first = CreateParent(owner, true);
        var second = CreateParent(other, false);
        var breeder = _world.Track(new PetBreeder());
        breeder.MoveToWorld(owner.Location, owner.Map);

        using var ownerState = AttachClient(owner);
        using var otherState = partner ? AttachClient(other) : null;
        try
        {
            breeder.OnDoubleClick(owner);
            owner.Target.Invoke(owner, first);
            owner.Target.Invoke(owner, second);
            var confirmation = Assert.IsAssignableFrom<BaseGump>(owner.FindGump<BaseGump>());
            if (focused)
            {
                confirmation.OnResponse(ownerState, Reply(2));
                owner.FindGump<BaseGump>().OnResponse(ownerState, Reply(10 + PetBreedingFocus.IndexOf(PetTrainingStat.Hits)));
                confirmation = owner.FindGump<BaseGump>();
            }

            // Moving from 9 and 9 to 11 and 9 raises the offspring's max-level range.
            first.AwardPetExperience(220);
            Assert.Equal(11, first.PetLevel);
            confirmation.OnResponse(ownerState, Reply(1));

            Assert.Empty(owner.Backpack.Items);
            Assert.Empty(other.Backpack.Items);
            Assert.Equal(DateTime.MinValue, first.PetBreedingReadyAt);
            Assert.Equal(DateTime.MinValue, second.PetBreedingReadyAt);
            if (partner)
            {
                Assert.Null(other.FindGump<BaseGump>());
            }

            var refreshed = owner.FindGump<BaseGump>();
            Assert.NotNull(refreshed);
            Assert.NotSame(confirmation, refreshed);
            refreshed.OnResponse(ownerState, Reply(1));

            if (partner)
            {
                var offer = Assert.IsAssignableFrom<BaseGump>(other.FindGump<BaseGump>());
                offer.OnResponse(otherState, Reply(1));
                Assert.IsType<PetBreedingClaimTicket>(Assert.Single(owner.Backpack.Items));
                Assert.IsType<PetBreedingClaimTicket>(Assert.Single(other.Backpack.Items));
            }
            else
            {
                Assert.Equal(2, owner.Backpack.Items.Count);
            }
        }
        finally
        {
            DetachClient(owner, ownerState);
            if (partner)
            {
                DetachClient(other, otherState);
            }
        }
    }

    private PlayerMobile CreatePlayer()
    {
        var player = _world.NewPlayer();
        player.Player = true;
        player.AddItem(new Backpack());
        player.MoveToWorld(new Point3D(1100, 1100, 0), Map.Felucca);
        return player;
    }

    private Horse CreateParent(PlayerMobile owner, bool female)
    {
        var pet = _world.Track(new Horse());
        pet.InitializePetProgression(1, 45, female);
        Assert.True(pet.SetControlMaster(owner));
        pet.MoveToWorld(owner.Location, owner.Map);
        pet.AwardPetExperience(480);
        Assert.Equal(9, pet.PetLevel);
        return pet;
    }

    private static NetState AttachClient(PlayerMobile player)
    {
        var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = player;
        player.NetState = state;
        return state;
    }

    private static void DetachClient(PlayerMobile player, NetState state)
    {
        player.CloseGump<BaseGump>();
        player.NetState = null;
        state.Mobile = null;
    }

    private static RelayInfo Reply(int button) => new(button, [], [], [], []);
}
