using System;
using Server;
using Server.Engines.BulkOrders;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public sealed class TamingBulkOrdersTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void OnlyMatchingFreshUnbondedPetMayBeSurrendered()
    {
        var owner = NewPlayer();
        var other = NewPlayer();
        var deed = Put(owner, new TamingSmallBOD(0, 1, typeof(Sheep)));
        var old = CreatePet(owner);
        var fresh = CreatePet(owner);
        var stranger = CreatePet(other);
        TamingBulkOrders.RecordFirstTame(fresh, owner);
        TamingBulkOrders.RecordFirstTame(stranger, other);

        Assert.False(TamingBulkOrders.TrySurrender(deed, owner, old));
        Assert.False(TamingBulkOrders.TrySurrender(deed, owner, stranger));
        fresh.IsBonded = true;
        Assert.False(TamingBulkOrders.TrySurrender(deed, owner, fresh));
        fresh.IsBonded = false;
        Assert.False(fresh.Deleted);
        Assert.True(TamingBulkOrders.TrySurrender(deed, owner, fresh));
        Assert.True(fresh.Deleted);
        Assert.True(deed.Complete);
        Assert.False(TamingBulkOrders.TrySurrender(deed, owner, fresh));
        Assert.Equal(1, deed.AmountCur);
    }

    [Fact]
    public void DeedClaimKeepsOfferClockAndTrainerHandInClearsIt()
    {
        var owner = NewPlayer();
        owner.Skills.AnimalTaming.Base = 20;
        var trainer = _world.Track(new AnimalTrainer());
        trainer.MoveToWorld(owner.Location, owner.Map);
        var offer = trainer.CreateBulkOrder(owner, true);
        _world.TrackItem(offer);
        Assert.IsType<TamingSmallBOD>(offer);
        try
        {
            var first = Put(owner, new TamingSmallBOD(1, 1, typeof(Sheep)));
            Assert.InRange(trainer.GetNextBulkOrder(owner), TimeSpan.FromMinutes(179), TimeSpan.FromHours(3));
            Assert.True(BulkOrderRedemption.TryClaim(owner, first));
            Assert.True(first.Deleted);
            Assert.True(trainer.GetNextBulkOrder(owner) > TimeSpan.Zero);
            Assert.False(BulkOrderRedemption.TryClaim(owner, first));

            owner.NextBODTurnInTime = Core.Now;
            var second = Put(owner, new TamingSmallBOD(1, 1, typeof(Sheep)));
            Assert.True(trainer.OnDragDrop(owner, second));
            Assert.True(second.Deleted);
            Assert.Equal(TimeSpan.Zero, trainer.GetNextBulkOrder(owner));
        }
        finally
        {
            TamingBulkOrders.Clear(owner);
        }
    }

    [Fact]
    public void LeashCanReturnItsPetOnlyToItsOwner()
    {
        var owner = NewPlayer();
        owner.Skills.AnimalTaming.Base = 75;
        var stranger = NewPlayer();
        stranger.Skills.AnimalTaming.Base = 100;
        var pet = CreatePet(owner);
        pet.IsBonded = true;
        var leash = Put(owner, new PetLeash());

        Assert.True(leash.TryShrink(owner, pet));
        Assert.Equal(Map.Internal, pet.Map);
        Assert.True(pet.IsStabled);
        Assert.Null(pet.ControlMaster);
        Assert.True(leash.Nontransferable);
        Assert.False(leash.TryRelease(stranger));

        Assert.True(leash.TryRelease(owner));
        Assert.Same(owner, pet.ControlMaster);
        Assert.Same(owner.Map, pet.Map);
        Assert.True(pet.IsBonded);
        Assert.False(leash.Nontransferable);
    }

    [Fact]
    public void OccupiedLeashRehydratesOnlyItsSavedUncontrolledPet()
    {
        using var timers = new TimerWheelScope();
        var owner = NewPlayer();
        owner.Skills.AnimalTaming.Base = 75;
        var pet = CreatePet(owner);
        pet.IsBonded = true;
        var leash = Put(owner, new PetLeash());
        Assert.True(leash.TryShrink(owner, pet));

        var writer = new BufferWriter(true);
        leash.Serialize(writer);
        pet.IsStabled = false;
        pet.StabledBy = null;

        var restored = _world.TrackItem(new PetLeash(World.NewItem));
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        restored.Deserialize(reader);
        Assert.Equal(writer.Position, reader.Position);
        Assert.True(TimerWheelScope.RunUntil(() => pet.IsStabled, 500));
        Assert.Same(owner, pet.StabledBy);
        Assert.Null(pet.ControlMaster);
        Assert.False(owner.Stabled?.Contains(pet) == true);

        Assert.True(restored.TryRelease(owner));
        Assert.Same(owner, pet.ControlMaster);
        Assert.Same(owner.Map, pet.Map);
        Assert.True(pet.IsBonded);
        Assert.False(pet.IsStabled);
    }

    [Fact]
    public void SeparatedOrDeletedLeashReturnsItsPetToOriginalTrainerRoster()
    {
        using var timers = new TimerWheelScope();
        var owner = NewPlayer();
        owner.Skills.AnimalTaming.Base = 75;
        var stranger = NewPlayer();
        stranger.Skills.AnimalTaming.Base = 100;
        var pet = CreatePet(owner);
        pet.IsBonded = true;
        var leash = Put(owner, new PetLeash());
        Assert.True(leash.TryShrink(owner, pet));

        stranger.Backpack.DropItem(leash);
        Assert.True(TimerWheelScope.RunUntil(() => owner.Stabled?.Contains(pet) == true, 500));
        Assert.Same(owner, pet.StabledBy);
        Assert.False(leash.Nontransferable);
        Assert.False(leash.TryRelease(stranger));

        var secondPet = CreatePet(owner);
        secondPet.IsBonded = true;
        var secondLeash = Put(owner, new PetLeash());
        Assert.True(secondLeash.TryShrink(owner, secondPet));
        secondLeash.Delete();

        Assert.Contains(secondPet, owner.Stabled);
        Assert.Same(owner, secondPet.StabledBy);
        Assert.Null(secondPet.ControlMaster);
    }

    [Fact]
    public void SkillScrollInvestmentProtectsAnOtherwiseFreshTame()
    {
        var owner = NewPlayer();
        var pet = CreatePet(owner);
        var deed = Put(owner, new TamingSmallBOD(0, 1, typeof(Sheep)));
        TamingBulkOrders.RecordFirstTame(pet, owner);
        Assert.True(TamingBulkOrders.CanSurrender(deed, owner, pet));

        var scroll = Put(owner, new TamingPetPowerScroll(SkillName.Wrestling, 105));
        Assert.True(scroll.TryUse(owner, pet));
        Assert.Equal(105, pet.Skills.Wrestling.Cap);
        Assert.False(TamingBulkOrders.CanSurrender(deed, owner, pet));
        Assert.False(pet.Deleted);
    }

    private Sheep CreatePet(PlayerMobile player)
    {
        var pet = _world.Track(new Sheep());
        pet.MoveToWorld(player.Location, player.Map);
        pet.Owners.Add(player);
        Assert.True(pet.SetControlMaster(player));
        return pet;
    }

    private PlayerMobile NewPlayer()
    {
        var player = _world.Track(new PlayerMobile { Player = true, Race = Race.Human });
        player.AddItem(_world.TrackItem(new Backpack()));
        player.MoveToWorld(new Point3D(1800, 1800, 0), Map.Felucca);
        return player;
    }

    private T Put<T>(PlayerMobile player, T item) where T : Item
    {
        player.Backpack.DropItem(_world.TrackItem(item));
        return item;
    }
}
