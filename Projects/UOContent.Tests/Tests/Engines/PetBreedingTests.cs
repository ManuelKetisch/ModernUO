using System;
using System.Linq;
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
public class PetBreedingTests : IDisposable
{
    private static readonly DateTime _now = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);
    private readonly MobileTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void PlayerCommandTargetsBothParentsAndRequiresConfirmation()
    {
        var player = CreatePlayer();
        var mother = CreateParent<Horse>(player, true);
        var father = CreateParent<Horse>(player, false);
        PetBreedingSystem.Initialize();

        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = player;
        player.NetState = state;
        try
        {
            Assert.True(CommandSystem.Handle(player, "[PetBreed"));
            Assert.NotNull(player.Target);
            player.Target.Invoke(player, mother);
            Assert.NotNull(player.Target);
            player.Target.Invoke(player, father);

            Assert.Empty(player.Backpack.Items);
            var prompt = player.FindGump<BaseGump>();
            Assert.NotNull(prompt);
            prompt.OnResponse(state, new RelayInfo(1, [], [], [], []));
            Assert.Equal(2, player.Backpack.Items.OfType<PetBreedingClaimTicket>().Count());
        }
        finally
        {
            player.CloseGump<BaseGump>();
            player.NetState = null;
            state.Mobile = null;
        }
    }

    [Fact]
    public void BreederOpensSelectionAndAcceptsTicketThroughNormalItemDrop()
    {
        var player = CreatePlayer();
        var mother = CreateParent<Horse>(player, true);
        var father = CreateParent<Horse>(player, false);
        var breeder = CreateBreeder(player);
        breeder.OnDoubleClick(player);
        Assert.NotNull(player.Target);
        player.Target = null;

        Assert.True(PetBreedingSystem.TryBreed(player, mother, father, breeder, Core.Now.AddDays(-3)));
        var ticket = Assert.IsType<PetBreedingClaimTicket>(player.Backpack.Items[0]);
        player.Lift(ticket, 1, out var rejected, out _);
        Assert.False(rejected);

        bool accepted;
        try
        {
            accepted = player.Drop(breeder, breeder.Location);
        }
        finally
        {
            foreach (var follower in player.AllFollowers)
            {
                if (follower != mother && follower != father)
                {
                    _harness.Track(follower);
                }
            }
        }

        Assert.True(accepted);
        Assert.True(ticket.Deleted);
        Assert.Equal(3, player.Followers);
    }

    [Fact]
    public void BreederSellsDeedThatClampsReductionAndConsumesOneItem()
    {
        var player = CreatePlayer();
        var stranger = CreatePlayer();
        var pet = CreateParent<Horse>(player, true);
        var strangerPet = CreateParent<Horse>(stranger, true);
        var breeder = CreateBreeder(player);
        player.Backpack.DropItem(new Gold(5000));
        var stock = Assert.Single(breeder.GetBuyInfo().OfType<GenericBuyInfo>(),
            entry => entry.Type == typeof(PetBreedingDelayReductionDeed));
        Assert.True(breeder.OnBuyItems(player, [new BuyItemResponse(stock.GetDisplayEntity().Serial, 1)]));
        var deed = Assert.IsType<PetBreedingDelayReductionDeed>(Assert.Single(player.Backpack.Items));

        pet.PetBreedingReadyAt = _now.AddHours(12);
        strangerPet.PetBreedingReadyAt = _now.AddHours(30);
        Assert.False(deed.TryReduce(player, strangerPet, _now));
        Assert.False(deed.Deleted);
        Assert.Equal(_now.AddHours(30), strangerPet.PetBreedingReadyAt);
        Assert.True(deed.TryReduce(player, pet, _now));
        Assert.Equal(_now, pet.PetBreedingReadyAt);
        Assert.True(deed.Deleted);
        Assert.False(deed.TryReduce(player, pet, _now));
    }

    [Fact]
    public void PartnerBreedingRequiresBothConfirmationsAndIssuesOneTicketEach()
    {
        var firstOwner = CreatePlayer();
        var secondOwner = CreatePlayer();
        var first = CreateParent<Horse>(firstOwner, true, 2);
        var second = CreateParent<Horse>(secondOwner, false, 9);
        var breeder = CreateBreeder(firstOwner);
        using var firstState = AttachClient(firstOwner);
        using var secondState = AttachClient(secondOwner);
        try
        {
            breeder.OnDoubleClick(firstOwner);
            firstOwner.Target.Invoke(firstOwner, first);
            firstOwner.Target.Invoke(firstOwner, second);
            var offerPrompt = firstOwner.FindGump<BaseGump>();
            Assert.NotNull(offerPrompt);
            Assert.Null(secondOwner.FindGump<BaseGump>());
            offerPrompt.OnResponse(firstState, new RelayInfo(1, [], [], [], []));

            Assert.Empty(firstOwner.Backpack.Items);
            Assert.Empty(secondOwner.Backpack.Items);
            var acceptPrompt = secondOwner.FindGump<BaseGump>();
            Assert.NotNull(acceptPrompt);
            acceptPrompt.OnResponse(secondState, new RelayInfo(1, [], [], [], []));

            var firstTicket = Assert.IsType<PetBreedingClaimTicket>(Assert.Single(firstOwner.Backpack.Items));
            var secondTicket = Assert.IsType<PetBreedingClaimTicket>(Assert.Single(secondOwner.Backpack.Items));
            Assert.Same(firstOwner, firstTicket.BlessedFor);
            Assert.Same(secondOwner, secondTicket.BlessedFor);
            Assert.True(firstTicket.Female);
            Assert.False(secondTicket.Female);
            Assert.Equal(3, firstTicket.Generation);
            Assert.Equal(10, secondTicket.Generation);
        }
        finally
        {
            CloseClient(firstOwner, firstState);
            CloseClient(secondOwner, secondState);
        }
    }

    [Fact]
    public void PartnerCapacityRejectionLeavesBothPetsAndBothQuotasUntouched()
    {
        var firstOwner = CreatePlayer();
        var secondOwner = CreatePlayer();
        var first = CreateParent<Horse>(firstOwner, true);
        var second = CreateParent<Horse>(secondOwner, false);
        var breeder = CreateBreeder(firstOwner);
        using var firstState = AttachClient(firstOwner);
        using var secondState = AttachClient(secondOwner);
        try
        {
            secondOwner.Backpack.MaxItems = 1;
            var filler = _harness.TrackItem(new Gold());
            secondOwner.Backpack.DropItem(filler);

            Assert.False(PetBreedingSystem.TryPartnerBreed(firstOwner, first, secondOwner, second, breeder, _now));
            Assert.Empty(firstOwner.Backpack.Items);
            Assert.Same(filler, Assert.Single(secondOwner.Backpack.Items));
            Assert.Equal(DateTime.MinValue, first.PetBreedingReadyAt);
            Assert.Equal(DateTime.MinValue, second.PetBreedingReadyAt);

            filler.Delete();
            Assert.True(PetBreedingSystem.TryPartnerBreed(firstOwner, first, secondOwner, second, breeder, _now));
            first.PetBreedingReadyAt = second.PetBreedingReadyAt = _now;
            secondOwner.Backpack.MaxItems = 2;
            Assert.True(PetBreedingSystem.TryPartnerBreed(firstOwner, first, secondOwner, second, breeder, _now));
        }
        finally
        {
            CloseClient(firstOwner, firstState);
            CloseClient(secondOwner, secondState);
        }
    }

    [Theory]
    [InlineData("move")]
    [InlineData("transfer")]
    [InlineData("cancel")]
    public void StalePartnerOfferCannotIssueTickets(string change)
    {
        var firstOwner = CreatePlayer();
        var secondOwner = CreatePlayer();
        var first = CreateParent<Horse>(firstOwner, true);
        var second = CreateParent<Horse>(secondOwner, false);
        var breeder = CreateBreeder(firstOwner);
        using var firstState = AttachClient(firstOwner);
        using var secondState = AttachClient(secondOwner);
        try
        {
            PetBreedingOffer.Begin(firstOwner, first, secondOwner, second, breeder);
            var offer = secondOwner.FindGump<BaseGump>();
            Assert.NotNull(offer);

            switch (change)
            {
                case "move":
                    secondOwner.MoveToWorld(new Point3D(1020, 1000, 0), secondOwner.Map);
                    break;
                case "transfer":
                    Assert.True(second.SetControlMaster(firstOwner));
                    break;
                case "cancel":
                    firstOwner.FindGump<BaseGump>().OnResponse(firstState, new RelayInfo(2, [], [], [], []));
                    break;
            }

            offer.OnResponse(secondState, new RelayInfo(1, [], [], [], []));
            Assert.Empty(firstOwner.Backpack.Items);
            Assert.Empty(secondOwner.Backpack.Items);
            Assert.Equal(DateTime.MinValue, first.PetBreedingReadyAt);
            Assert.Equal(DateTime.MinValue, second.PetBreedingReadyAt);
        }
        finally
        {
            CloseClient(firstOwner, firstState);
            CloseClient(secondOwner, secondState);
        }
    }

    [Fact]
    public void OutingPersistsDistinctDefeatsAndReducesOneParentCooldownPerLitter()
    {
        var owner = CreatePlayer();
        var first = CreateParent<Horse>(owner, true);
        var second = CreateParent<Horse>(owner, false);
        var trainer = CreateBreeder(owner);
        Assert.True(PetBreedingSystem.TryBreed(owner, first, second, trainer, _now));
        Assert.True(PetBreedingOuting.TryStart(owner, first, trainer, _now));
        Assert.False(PetBreedingOuting.TryStart(owner, second, trainer, _now));

        var dragon = DefeatedBy(first, new Dragon());
        PetBreedingOuting.RecordDefeat(owner, first, dragon, _now);
        PetBreedingOuting.RecordDefeat(owner, first, dragon, _now);
        Assert.Equal((1, 3, true, false), PetBreedingOuting.GetStatus(owner, first, _now));

        var persistence = new PetBreedingOuting();
        try
        {
            var writer = new BufferWriter(true);
            persistence.Serialize(writer);
            PetBreedingOuting.OnPetDeleted(first);
            PetBreedingOuting.OnPetDeleted(second);
            var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
            persistence.Deserialize(reader);
            Assert.Equal(writer.Position, reader.Position);
            Assert.Equal((1, 3, true, false), PetBreedingOuting.GetStatus(owner, first, _now));
        }
        finally
        {
            persistence.Unregister();
        }

        var zombie = DefeatedBy(first, new Zombie());
        PetBreedingOuting.RecordDefeat(owner, first, zombie, _now);
        Assert.Equal((2, 3, true, false), PetBreedingOuting.GetStatus(owner, first, _now));

        first.PetBreedingReadyAt = _now.AddHours(30);
        var daemon = DefeatedBy(first, new Daemon());
        PetBreedingOuting.RecordDefeat(owner, first, daemon, _now);
        Assert.Equal(_now.AddHours(24), first.PetBreedingReadyAt);
        Assert.Equal(_now.AddDays(6), second.PetBreedingReadyAt);
        Assert.Equal((0, 3, false, true), PetBreedingOuting.GetStatus(owner, second, _now));
        Assert.False(PetBreedingOuting.TryStart(owner, second, trainer, _now));
    }

    [Fact]
    public void PartnerCanStartOutingAfterADeedMakesTheFirstParentsOutingIneligible()
    {
        var firstOwner = CreatePlayer();
        var secondOwner = CreatePlayer();
        var first = CreateParent<Horse>(firstOwner, true);
        var second = CreateParent<Horse>(secondOwner, false);
        var breeder = CreateBreeder(firstOwner);
        using var firstState = AttachClient(firstOwner);
        using var secondState = AttachClient(secondOwner);
        try
        {
            Assert.True(PetBreedingSystem.TryPartnerBreed(firstOwner, first, secondOwner, second, breeder, _now));
            Assert.True(PetBreedingOuting.TryStart(firstOwner, first, breeder, _now));
            var dragon = DefeatedBy(first, new Dragon());
            PetBreedingOuting.RecordDefeat(firstOwner, first, dragon, _now);
            Assert.Equal((1, 3, true, false), PetBreedingOuting.GetStatus(firstOwner, first, _now));

            var deed = _harness.TrackItem(new PetBreedingDelayReductionDeed());
            firstOwner.Backpack.DropItem(deed);
            var afterFourDays = _now.AddDays(4);
            Assert.True(deed.TryReduce(firstOwner, first, afterFourDays));
            Assert.Equal(afterFourDays.AddDays(1), first.PetBreedingReadyAt);
            Assert.Equal(_now.AddDays(6), second.PetBreedingReadyAt);
            Assert.Equal((0, 3, false, false), PetBreedingOuting.GetStatus(firstOwner, first, afterFourDays));
            Assert.True(PetBreedingOuting.TryStart(secondOwner, second, breeder, afterFourDays));
            Assert.Equal((0, 3, true, false), PetBreedingOuting.GetStatus(secondOwner, second, afterFourDays));
        }
        finally
        {
            CloseClient(firstOwner, firstState);
            CloseClient(secondOwner, secondState);
        }
    }

    private BaseCreature DefeatedBy(BaseCreature pet, BaseCreature enemy)
    {
        _harness.Track(enemy);
        enemy.LastKiller = pet;
        enemy.Delete();
        return enemy;
    }

    [Fact]
    public void LitterIssuesTwoOwnerBoundTicketsWithIndependentGenerations()
    {
        var player = CreatePlayer();
        var mother = CreateParent<Horse>(player, true, 2);
        var father = CreateParent<Horse>(player, false, 9);
        mother.RawStr = 100;
        father.RawStr = 100;
        mother.HitsMaxSeed = 1100;
        father.HitsMaxSeed = 1200;

        Assert.True(PetBreedingSystem.TryBreed(player, mother, father, null, _now));
        Assert.Equal(_now.AddDays(6), mother.PetBreedingReadyAt);
        Assert.Equal(_now.AddDays(6), father.PetBreedingReadyAt);

        var tickets = player.Backpack.Items.OfType<PetBreedingClaimTicket>().ToArray();
        Assert.Equal(2, tickets.Length);
        var femaleTicket = Assert.Single(tickets, ticket => ticket.Female);
        var maleTicket = Assert.Single(tickets, ticket => !ticket.Female);
        Assert.Equal(3, femaleTicket.Generation);
        Assert.Equal(10, maleTicket.Generation);

        var stranger = CreatePlayer();
        stranger.Backpack.DropItem(maleTicket);
        Assert.Null(maleTicket.TryClaim(stranger, _now.AddDays(3)));
        player.Backpack.DropItem(maleTicket);

        Assert.Null(femaleTicket.TryClaim(player, _now.AddDays(3).AddTicks(-1)));
        Assert.False(femaleTicket.Deleted);
        var offspring = _harness.Track(Assert.IsType<Horse>(femaleTicket.TryClaim(player, _now.AddDays(3))));
        Assert.True(femaleTicket.Deleted);
        Assert.Null(femaleTicket.TryClaim(player, _now.AddDays(3)));
        Assert.True(offspring.PetFemale);
        Assert.Equal(1, offspring.PetLevel);
        Assert.Equal(3, offspring.PetGeneration);
        Assert.InRange(offspring.PetMaxLevel, 10, 12);
        Assert.Same(player, offspring.ControlMaster);
        Assert.Equal(100, offspring.RawStr);
        Assert.Equal(1000, offspring.HitsMaxSeed);
    }

    [Fact]
    public void CharacterLimitIsRollingAndRejectionsLeaveParentsAvailable()
    {
        var player = CreatePlayer();
        var mother = CreateParent<Horse>(player, true);
        var father = CreateParent<Horse>(player, false);

        Assert.True(PetBreedingSystem.TryBreed(player, mother, father, null, _now));
        mother.PetBreedingReadyAt = father.PetBreedingReadyAt = _now;
        Assert.True(PetBreedingSystem.TryBreed(player, mother, father, null, _now.AddHours(1)));
        var readyAt = _now.AddHours(2);
        mother.PetBreedingReadyAt = father.PetBreedingReadyAt = readyAt;

        Assert.False(PetBreedingSystem.TryBreed(player, mother, father, null, _now.AddHours(12).AddTicks(-1)));
        Assert.Equal(readyAt, mother.PetBreedingReadyAt);
        Assert.Equal(4, player.Backpack.Items.OfType<PetBreedingClaimTicket>().Count());

        Assert.True(PetBreedingSystem.TryBreed(player, mother, father, null, _now.AddHours(12)));
    }

    [Fact]
    public void IssueAndClaimCheckCapacityWithoutConsumingTicketsOrCooldowns()
    {
        var player = CreatePlayer();
        var mother = CreateParent<Horse>(player, true);
        var father = CreateParent<Horse>(player, false);

        player.Backpack.MaxItems = 1;
        Assert.False(PetBreedingSystem.TryBreed(player, mother, father, null, _now));
        Assert.Empty(player.Backpack.Items);
        Assert.Equal(DateTime.MinValue, mother.PetBreedingReadyAt);
        Assert.Equal(DateTime.MinValue, father.PetBreedingReadyAt);

        player.Backpack.MaxItems = 2;
        Assert.True(PetBreedingSystem.TryBreed(player, mother, father, null, _now));
        var ticket = Assert.IsType<PetBreedingClaimTicket>(player.Backpack.Items[0]);

        player.FollowersMax = player.Followers;
        Assert.Null(ticket.TryClaim(player, _now.AddDays(3)));
        Assert.False(ticket.Deleted);

        player.FollowersMax++;
        Assert.IsType<Horse>(_harness.Track(ticket.TryClaim(player, _now.AddDays(3))));
        Assert.True(ticket.Deleted);
    }

    [Fact]
    public void CharacterLitterHistoryRoundTripsThroughPersistenceSerializer()
    {
        var player = CreatePlayer();
        var mother = CreateParent<Horse>(player, true);
        var father = CreateParent<Horse>(player, false);
        var persistence = new PetBreedingSystem();
        try
        {
            var now = Core.Now;
            Assert.True(PetBreedingSystem.TryBreed(player, mother, father, null, now));
            mother.PetBreedingReadyAt = father.PetBreedingReadyAt = now;
            Assert.True(PetBreedingSystem.TryBreed(player, mother, father, null, now.AddHours(1)));
            mother.PetBreedingReadyAt = father.PetBreedingReadyAt = now;
            Assert.False(PetBreedingSystem.TryBreed(player, mother, father, null, now.AddHours(2)));

            var writer = new BufferWriter(true);
            persistence.Serialize(writer);
            PetBreedingSystem.OnPlayerDeleted(player);
            var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
            persistence.Deserialize(reader);

            Assert.Equal(writer.Position, reader.Position);
            Assert.False(PetBreedingSystem.TryBreed(player, mother, father, null, now.AddHours(2)));
            Assert.True(PetBreedingSystem.TryBreed(player, mother, father, null, now.AddHours(12)));
        }
        finally
        {
            persistence.Unregister();
        }
    }

    [Fact]
    public void StabledHighSlotPairUsesTrainerAndChecksStableOwnership()
    {
        var player = CreatePlayer();
        var mother = CreateParent<Dragon>(player, true);
        StableFor(player, mother);
        var father = CreateParent<Dragon>(player, false);
        StableFor(player, father);
        var trainer = _harness.Track(new AnimalTrainer());
        trainer.MoveToWorld(new Point3D(1000, 1001, 0), player.Map);

        Assert.False(PetBreedingSystem.TryBreed(player, mother, father, null, _now));
        father.StabledBy = CreatePlayer();
        Assert.False(PetBreedingSystem.TryBreed(player, mother, father, trainer, _now));
        father.StabledBy = player;

        trainer.MoveToWorld(new Point3D(1010, 1000, 0), player.Map);
        Assert.False(PetBreedingSystem.TryBreed(player, mother, father, trainer, _now));
        trainer.MoveToWorld(new Point3D(1000, 1001, 0), player.Map);

        Assert.True(PetBreedingSystem.TryBreed(player, mother, father, trainer, _now));
        Assert.Contains(mother, player.Stabled);
        Assert.Contains(father, player.Stabled);
        Assert.True(mother.IsStabled);
        Assert.True(father.IsStabled);
        Assert.Equal(0, player.Followers);
    }

    [Fact]
    public void TicketKeepsSpeciesAndDelayAcrossSerialization()
    {
        var player = CreatePlayer();
        var mother = CreateParent<Horse>(player, true, 2);
        var father = CreateParent<Horse>(player, false);
        mother.Name = "Nora";
        father.Name = "Vasco";
        Assert.True(PetBreedingSystem.TryBreed(player, mother, father, null, _now));
        var ticket = Assert.Single(player.Backpack.Items.OfType<PetBreedingClaimTicket>(), candidate => candidate.Female);

        var writer = new BufferWriter(true);
        ticket.Serialize(writer);
        ticket.Delete();
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        var reloaded = _harness.TrackItem(new PetBreedingClaimTicket(World.NewItem));
        reloaded.Deserialize(reader);
        player.Backpack.DropItem(reloaded);

        Assert.Equal(writer.Position, reader.Position);
        Assert.Same(player, reloaded.BlessedFor);
        Assert.Equal(3, reloaded.Generation);
        Assert.True(reloaded.Female);
        Assert.Equal(_now.AddDays(3), reloaded.ReadyAt);
        Assert.Null(reloaded.TryClaim(player, _now.AddHours(12)));
        var pedigree = new PetPedigree("Nora", 9, 2, "Vasco", 9, 1);
        Assert.Equal(pedigree, reloaded.Pedigree);
        var offspring = Assert.IsType<Horse>(_harness.Track(reloaded.TryClaim(player, _now.AddDays(3))));
        Assert.Equal(pedigree, PetBreedingRecords.GetPedigree(offspring));
    }

    private PetBreeder CreateBreeder(PlayerMobile player)
    {
        var breeder = _harness.Track(new PetBreeder());
        breeder.MoveToWorld(new Point3D(1000, 1001, 0), player.Map);
        return breeder;
    }

    private static NetState AttachClient(PlayerMobile player)
    {
        var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = player;
        player.NetState = state;
        return state;
    }

    private static void CloseClient(PlayerMobile player, NetState state)
    {
        while (player.CloseGump<BaseGump>())
        {
        }

        player.NetState = null;
        state.Mobile = null;
    }

    private PlayerMobile CreatePlayer()
    {
        var player = _harness.NewPlayer();
        player.Race = Race.Human;
        player.AddItem(_harness.TrackItem(new Backpack()));
        player.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);
        return player;
    }

    private T CreateParent<T>(PlayerMobile player, bool female, int generation = 1) where T : BaseCreature, new()
    {
        var creature = _harness.Track(new T());
        creature.MoveToWorld(new Point3D(1001, 1000, 0), player.Map);
        creature.InitializePetProgression(generation, 30, female);
        Assert.True(creature.SetControlMaster(player));

        for (var i = 0; creature.PetLevel < 9 && i < 50; i++)
        {
            creature.AwardPetExperience(12);
        }

        Assert.Equal(9, creature.PetLevel);
        return creature;
    }

    private static void StableFor(PlayerMobile player, BaseCreature pet)
    {
        pet.Internalize();
        pet.SetControlMaster(null);
        pet.IsStabled = true;
        pet.StabledBy = player;
        player.AddStabled(pet);
    }
}
