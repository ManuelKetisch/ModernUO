using System;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Tests;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests.Mobiles;

[Collection("Sequential UOContent Tests")]
public class PetTrainingPreviewTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void EquivalentFightBudgetDoesNotDependOnHitCount()
    {
        const int hits = 200;
        var full = PetExperiencePolicy.AwardForDamage(hits, 0, 10_000, out var fullCredit);

        var lightlyHitCredit = 0;
        var total = 0;
        for (var i = 0; i < hits; i++)
        {
            total += PetExperiencePolicy.AwardForDamage(hits, lightlyHitCredit, 1, out lightlyHitCredit);
        }

        Assert.Equal(62, full);
        Assert.Equal(full, total);
        Assert.Equal(hits, fullCredit);
        Assert.Equal(hits, lightlyHitCredit);
        Assert.Equal(0, PetExperiencePolicy.AwardForDamage(hits, lightlyHitCredit, 60, out var exhausted));
        Assert.Equal(hits, exhausted);
        Assert.Equal(120, PetExperiencePolicy.AwardForDamage(int.MaxValue, 0, int.MaxValue, out _));
    }

    [Fact]
    public void ConfirmSpendIsExactAndRefusesStaleQuotes()
    {
        var owner = _world.NewPlayer();
        owner.Player = true;
        owner.MoveToWorld(new Point3D(1100, 1100, 0), Map.Felucca);
        var pet = _world.NewCreature();
        pet.Tamable = true;
        pet.HitsMaxSeed = 998;
        pet.SetControlMaster(owner);
        pet.MoveToWorld(owner.Location, owner.Map);
        pet.AwardPetExperience(25);

        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = owner;
        owner.NetState = state;
        try
        {
            var initialPoints = pet.PetAbilityPoints;
            PetProgressionGump.DisplayTo(owner, pet);
            var main = owner.FindGump<PetProgressionGump>();
            Assert.NotNull(main);
            main.OnResponse(state, Reply(2));
            Assert.Equal(998, pet.HitsMaxSeed);
            Assert.Equal(initialPoints, pet.PetAbilityPoints);
            var stale = owner.FindGump<PetProgressionGump.ConfirmSpendGump>();
            Assert.NotNull(stale);

            Assert.Equal(1, pet.TrySpendPetAbilityPoints(owner, PetTrainingStat.Hits, 1));
            stale.OnResponse(state, Reply(1));
            Assert.Equal(999, pet.HitsMaxSeed);
            Assert.Equal(initialPoints - 1, pet.PetAbilityPoints);

            Assert.True(PetAbilitySpendQuote.TryCreate(pet, owner, PetTrainingStat.Hits, 10, out var quote));
            Assert.Equal(1, quote.Cost);
            Assert.Equal(1000, quote.Result);

            owner.FindGump<PetProgressionGump>().OnResponse(state, Reply(2));
            owner.FindGump<PetProgressionGump.ConfirmSpendGump>().OnResponse(state, Reply(1));
            Assert.Equal(1000, pet.HitsMaxSeed);
            Assert.Equal(initialPoints - 2, pet.PetAbilityPoints);
        }
        finally
        {
            owner.CloseGump<PetProgressionGump>();
            owner.CloseGump<PetProgressionGump.ConfirmSpendGump>();
            owner.NetState = null;
            state.Mobile = null;
        }
    }

    [Fact]
    public void TrainerInfoOpensOrdinaryBreedingTarget()
    {
        var owner = _world.NewPlayer();
        owner.Player = true;
        owner.MoveToWorld(new Point3D(1100, 1100, 0), Map.Felucca);
        var trainer = _world.Track(new AnimalTrainer());
        trainer.MoveToWorld(owner.Location, owner.Map);

        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = owner;
        owner.NetState = state;
        var entries = PooledRefList<ContextMenuEntry>.Create();
        try
        {
            trainer.GetContextMenuEntries(owner, ref entries);
            foreach (var entry in entries)
            {
                if (entry.Number == 3005031)
                {
                    entry.OnClick(owner, trainer);
                    break;
                }
            }

            var gump = owner.FindGump<BaseGump>();
            Assert.NotNull(gump);
            gump.OnResponse(state, Reply(1));
            Assert.NotNull(owner.Target);
        }
        finally
        {
            entries.Dispose();
            owner.CloseGump<BaseGump>();
            owner.NetState = null;
            state.Mobile = null;
        }
    }

    private static RelayInfo Reply(int button) => new(button, [], [], [], []);
}
