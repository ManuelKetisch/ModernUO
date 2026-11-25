using System;
using System.Linq;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.PetProgression;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Tests;
using Server.Targeting;
using Server.Tests.Network;
using Server.Tests.Terrain;
using Xunit;

namespace UOContent.Tests.Mobiles;

[Collection("Sequential UOContent Tests")]
public class PetLevelingTests : IDisposable
{
    private readonly MobileTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    private (PlayerMobile Owner, TestCreature Pet) NewPet(TestCreature creature = null)
    {
        var owner = _harness.NewPlayer();
        owner.MoveToWorld(new Point3D(1100, 1100, 0), Map.Felucca);
        var pet = creature == null ? _harness.NewCreature() : _harness.Track(creature);
        pet.Tamable = true;
        pet.MoveToWorld(new Point3D(1101, 1100, 0), owner.Map);
        Assert.True(pet.SetControlMaster(owner));
        return (owner, pet);
    }

    [Fact]
    public void LandedMeleeUsesTheDefendersActualHitPointLoss()
    {
        var (_, pet) = NewPet();
        var weak = _harness.NewCreature();
        weak.HitsMaxSeed = 49;
        weak.Hits = 1;
        weak.MoveToWorld(new Point3D(1102, 1100, 0), pet.Map);
        var strong = _harness.NewCreature();
        strong.HitsMaxSeed = 200;
        strong.Hits = strong.HitsMax;
        strong.MoveToWorld(new Point3D(1101, 1101, 0), pet.Map);
        var weapon = _harness.TrackItem(new Longsword());
        pet.SetDamage(5);

        weapon.OnHit(pet, weak);
        Assert.Equal(0, weak.Hits);
        Assert.Equal(0, pet.PetExperience);

        weapon.OnHit(pet, strong);
        var lost = strong.HitsMax - strong.Hits;
        Assert.True(lost > 0);
        Assert.Equal(62 * lost / 200, pet.PetExperience);
    }

    [Fact]
    public void FightCreditIsBoundedAndSharedWhenTheOwnerLandsTheFinishingBlow()
    {
        var (owner, pet) = NewPet();
        var foe = _harness.NewCreature();
        foe.HitsMaxSeed = 100;

        pet.TrackPetMeleeHit(foe, 100, 20);
        Assert.Equal(7, pet.PetExperience);
        pet.TrackPetMeleeHit(foe, 100, 100);
        pet.TrackPetMeleeHit(foe, 100, 100);
        Assert.Equal(2, pet.PetLevel);
        Assert.Equal(12, pet.PetExperience);

        foe.LastKiller = owner;
        foe.Delete();
        foe.SettlePetTrainingDefeat();
        foe.SettlePetTrainingDefeat();
        Assert.Equal(14, pet.PetExperience);
    }

    [Fact]
    public void BreedingPausePersistsAndDiscardsExperienceBeyondTheBreedingLimit()
    {
        var candidate = new TestCreature { Tamable = true };
        candidate.InitializePetProgression(9, 45, false);
        var (owner, pet) = NewPet(candidate);
        var stranger = _harness.NewPlayer();
        stranger.MoveToWorld(owner.Location, owner.Map);

        Assert.True(pet.CanPausePetAtBreedingLimit);
        Assert.False(pet.TrySetPetBreedingPause(stranger, true));
        Assert.True(pet.TrySetPetBreedingPause(owner, true));
        pet.AwardPetExperience(100_000);
        Assert.Equal(42, pet.PetLevel);
        Assert.Equal(0, pet.PetExperience);
        var points = pet.PetAbilityPoints;
        pet.AwardPetExperience(100_000);
        Assert.Equal(points, pet.PetAbilityPoints);

        var writer = new BufferWriter(true);
        pet.Serialize(writer);
        var copy = _harness.Track(new TestCreature(World.NewMobile));
        copy.Deserialize(new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray()));
        copy.MoveToWorld(pet.Location, pet.Map);
        Assert.True(copy.PetBreedingPauseEnabled);
        Assert.Equal(0, copy.PetExperienceForNextLevel);
        Assert.True(copy.TrySetPetBreedingPause(owner, false));
        Assert.Equal(42, copy.PetLevel);
        Assert.Equal(0, copy.PetExperience);
        copy.AwardPetExperience(100_000);
        Assert.Equal(45, copy.PetLevel);
        Assert.False(copy.CanPausePetAtBreedingLimit);
    }

    [Fact]
    public void ControlledAndSummonedOpponentsAndRangedHitsEarnNothing()
    {
        var (_, pet) = NewPet();
        pet.SetDamage(5);
        var melee = _harness.TrackItem(new Longsword());
        var ranged = _harness.TrackItem(new Bow());
        var target = _harness.NewCreature();
        target.HitsMaxSeed = 200;
        target.Hits = target.HitsMax;
        target.MoveToWorld(new Point3D(1102, 1100, 0), pet.Map);

        ranged.OnHit(pet, target);
        Assert.Equal(0, pet.PetExperience);

        target.Summoned = true;
        melee.OnHit(pet, target);
        Assert.Equal(0, pet.PetExperience);

        target.Summoned = false;
        Assert.True(target.SetControlMaster(_harness.NewPlayer()));
        melee.OnHit(pet, target);
        Assert.Equal(0, pet.PetExperience);
    }

    [Fact]
    public void SpendingRechecksOwnerRangeUnlockAndCap()
    {
        var (owner, pet) = NewPet();
        var stranger = _harness.NewPlayer();
        stranger.MoveToWorld(owner.Location, owner.Map);
        pet.HitsMaxSeed = 999;

        Assert.InRange(pet.PetMaxLevel, 10, 30);
        Assert.Equal(1, pet.PetLevel);
        Assert.Equal(1, pet.PetGeneration);
        Assert.Equal(0, pet.TrySpendPetAbilityPoints(owner, PetTrainingStat.Hits, 10));

        pet.AwardPetExperience(12);
        pet.AwardPetExperience(12);
        pet.AwardPetExperience(12);

        Assert.Equal(2, pet.PetLevel);
        Assert.Equal(11, pet.PetExperience);
        Assert.InRange(pet.PetAbilityPoints, 3, 8);
        var balance = pet.PetAbilityPoints;
        Assert.Equal(0, pet.TrySpendPetAbilityPoints(owner, PetTrainingStat.PhysicalResistance, 10));
        Assert.Equal(0, pet.TrySpendPetAbilityPoints(stranger, PetTrainingStat.Hits, 10));
        owner.MoveToWorld(new Point3D(1150, 1150, 0), owner.Map);
        Assert.Equal(0, pet.TrySpendPetAbilityPoints(owner, PetTrainingStat.Hits, 10));
        owner.MoveToWorld(new Point3D(1100, 1100, 0), owner.Map);

        Assert.Equal(1, pet.TrySpendPetAbilityPoints(owner, PetTrainingStat.Hits, 10));
        Assert.Equal(1000, pet.HitsMaxSeed);
        Assert.Equal(balance - 1, pet.PetAbilityPoints);
        Assert.Equal(0, pet.TrySpendPetAbilityPoints(owner, PetTrainingStat.Hits, 10));
    }

    [Fact]
    public void NpcInfoGumpSpendsAnAbilityPointForTheOwner()
    {
        var (owner, pet) = NewPet();
        pet.HitsMaxSeed = 240;
        for (var i = 0; i < 3; i++)
        {
            pet.AwardPetExperience(12);
        }

        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = owner;
        owner.NetState = state;
        var entries = PooledRefList<ContextMenuEntry>.Create();
        try
        {
            pet.GetContextMenuEntries(owner, ref entries);
            foreach (var entry in entries)
            {
                if (entry.Number == 3005031)
                {
                    entry.OnClick(owner, pet);
                    break;
                }
            }

            var gump = owner.FindGump<PetProgressionGump>();
            Assert.NotNull(gump);
            var balance = pet.PetAbilityPoints;
            gump.OnResponse(state, new RelayInfo(1, [], [], [], []));
            Assert.Equal(240, pet.HitsMaxSeed);
            Assert.Equal(balance, pet.PetAbilityPoints);

            var confirm = owner.FindGump<PetProgressionGump.ConfirmSpendGump>();
            Assert.NotNull(confirm);
            confirm.OnResponse(state, new RelayInfo(1, [], [], [], []));

            Assert.Equal(241, pet.HitsMaxSeed);
            Assert.Equal(balance - 1, pet.PetAbilityPoints);
        }
        finally
        {
            entries.Dispose();
            owner.CloseGump<PetProgressionGump>();
            owner.NetState = null;
            state.Mobile = null;
        }
    }

    [Fact]
    public void OffspringProgressionRoundTripsAndMayReachLevelFortyFive()
    {
        var pet = _harness.NewCreature();
        pet.Tamable = true;
        pet.InitializePetProgression(10, 45, true);
        Assert.True(pet.SetControlMaster(_harness.NewPlayer()));
        pet.PetBreedingReadyAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < 6; i++)
        {
            pet.AwardPetExperience(12);
        }

        var writer = new BufferWriter(true);
        pet.Serialize(writer);
        var copy = _harness.Track(new TestCreature(World.NewMobile));
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        copy.Deserialize(reader);

        Assert.Equal(writer.Position, reader.Position);
        Assert.Equal(3, copy.PetLevel);
        Assert.Equal(45, copy.PetMaxLevel);
        Assert.Equal(10, copy.PetGeneration);
        Assert.True(copy.PetFemale);
        Assert.Equal(12, copy.PetExperience);
        Assert.Equal(pet.PetAbilityPoints, copy.PetAbilityPoints);
        Assert.Equal(pet.PetBreedingReadyAt, copy.PetBreedingReadyAt);

        for (var i = 0; i < 1000; i++)
        {
            copy.AwardPetExperience(12);
        }

        Assert.Equal(45, copy.PetLevel);
        Assert.Equal(0, copy.PetExperience);
        var cappedBalance = copy.PetAbilityPoints;
        copy.AwardPetExperience(12);
        Assert.Equal(cappedBalance, copy.PetAbilityPoints);
    }

    [Fact]
    public void TrainerSellsBondingResurrectionAndRecoverySupplies()
    {
        var trainer = _harness.Track(new AnimalTrainer());
        var player = _harness.NewPlayer();
        player.AddItem(new Backpack());
        player.Backpack.DropItem(new Gold(15000));
        var stock = trainer.GetBuyInfo().OfType<GenericBuyInfo>().ToArray();
        var deed = Assert.Single(stock, entry => entry.Type == typeof(PetBondingDeed));
        var draught = Assert.Single(stock, entry => entry.Type == typeof(PetResurrectionDraught));
        var ointment = Assert.Single(stock, entry => entry.Type == typeof(RestorativePetOintment));

        Assert.True(trainer.OnBuyItems(player, [
            new BuyItemResponse(deed.GetDisplayEntity().Serial, 1),
            new BuyItemResponse(draught.GetDisplayEntity().Serial, 1),
            new BuyItemResponse(ointment.GetDisplayEntity().Serial, 1)
        ]));

        Assert.NotNull(player.Backpack.FindItemByType<PetBondingDeed>());
        Assert.NotNull(player.Backpack.FindItemByType<PetResurrectionDraught>());
        Assert.NotNull(player.Backpack.FindItemByType<RestorativePetOintment>());
        Assert.Equal(3500, player.Backpack.GetAmount(typeof(Gold)));
    }

    [Fact]
    public void BondingAndResurrectionItemsRecheckOwnerAndPetState()
    {
        var (owner, pet) = NewPet(new BondableTestCreature());
        owner.AddItem(new Backpack());
        var stranger = _harness.NewPlayer();
        stranger.MoveToWorld(owner.Location, owner.Map);
        stranger.AddItem(new Backpack());
        var deed = _harness.TrackItem(new PetBondingDeed());
        stranger.Backpack.DropItem(deed);

        Assert.False(deed.TryUse(stranger, pet));
        Assert.False(deed.Deleted);
        owner.Backpack.DropItem(deed);
        deed.OnDoubleClick(owner);
        owner.Target.Invoke(owner, pet);
        Assert.True(pet.IsBonded);
        Assert.True(deed.Deleted);
        Assert.Equal(DateTime.MinValue, pet.BondingBegin);

        var draught = _harness.TrackItem(new PetResurrectionDraught());
        owner.Backpack.DropItem(draught);
        Assert.False(draught.TryUse(owner, pet));
        Assert.False(draught.Deleted);

        pet.Kill();
        Assert.True(pet.IsDeadPet);
        var previous = owner.Location;
        owner.MoveToWorld(new Point3D(1150, 1150, 0), owner.Map);
        Assert.False(draught.TryUse(owner, pet));
        Assert.False(draught.Deleted);
        owner.MoveToWorld(previous, pet.Map);
        draught.OnDoubleClick(owner);
        owner.Target.Invoke(owner, pet);
        Assert.False(pet.IsDeadPet);
        Assert.True(draught.Deleted);
        Assert.True(pet.Hits > 0);
    }

    [Fact]
    public void FeedingStillBondsPetsAfterAWeek()
    {
        var (owner, pet) = NewPet(new BondableTestCreature());

        Assert.True(pet.CheckFeed(owner, _harness.TrackItem(new RawRibs())));
        Assert.Equal(Core.Now, pet.BondingBegin);
        Assert.False(pet.IsBonded);

        pet.BondingBegin = Core.Now - TimeSpan.FromDays(7);
        Assert.True(pet.CheckFeed(owner, _harness.TrackItem(new RawRibs())));
        Assert.True(pet.IsBonded);
        Assert.Equal(DateTime.MinValue, pet.BondingBegin);
    }

    [Theory]
    [InlineData(0, true, false)]
    [InlineData(1, false, true)]
    [InlineData(3, false, false)]
    [InlineData(11, false, false)]
    public void DeathRollsAffectUnspentProgressAndPreserveEarnedLevels(int roll, bool losePoints, bool loseExperience)
    {
        var (_, pet) = NewPet();
        for (var i = 0; i < 3; i++)
        {
            pet.AwardPetExperience(12);
        }

        var points = pet.PetAbilityPoints;
        pet.ApplyPetDeathPenalty(roll);
        Assert.Equal(points, pet.PetAbilityPoints);
        Assert.Equal(11, pet.PetExperience);

        pet.IsBonded = true;
        pet.IsDeadPet = true;
        pet.ApplyPetDeathPenalty(roll);
        Assert.Equal(losePoints ? points / 2 : points, pet.PetAbilityPoints);
        Assert.Equal(loseExperience ? 5 : 11, pet.PetExperience);
        Assert.Equal(2, pet.PetLevel);
    }

    [Fact]
    public void DeathStatLossRoundTripsAndOintmentRestoresOnlyRecordedStats()
    {
        var (owner, pet) = NewPet();
        owner.AddItem(new Backpack());
        pet.HitsMaxSeed = 1000;
        pet.RawStr = 1200;
        pet.RawInt = 1;
        pet.RawDex = 110;
        pet.FireResistSeed = 70;
        pet.VirtualArmor = 60;
        pet.VirtualArmorMod = 20;
        pet.SetDamage(20, 27);

        for (var i = 0; i < 3; i++)
        {
            pet.AwardPetExperience(12);
        }

        pet.IsBonded = true;
        pet.IsDeadPet = true;
        pet.ApplyPetDeathPenalty(2);
        Assert.Equal(950, pet.HitsMaxSeed);
        Assert.Equal(1140, pet.RawStr);
        Assert.Equal(105, pet.RawDex);
        Assert.Equal(1, pet.RawInt);
        Assert.Equal(67, pet.FireResistSeed);
        Assert.Equal(57, pet.VirtualArmor);
        Assert.Equal(20, pet.VirtualArmorMod);
        Assert.Equal((19, 26), (pet.DamageMin, pet.DamageMax));

        var writer = new BufferWriter(true);
        pet.Serialize(writer);
        var copy = _harness.Track(new TestCreature(World.NewMobile));
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        copy.Deserialize(reader);
        Assert.Equal(writer.Position, reader.Position);
        copy.MoveToWorld(pet.Location, pet.Map);

        var ointment = _harness.TrackItem(new RestorativePetOintment());
        owner.Backpack.DropItem(ointment);
        Assert.False(ointment.TryUse(owner, copy));
        Assert.False(ointment.Deleted);
        copy.ResurrectPet();
        Assert.Equal(1, copy.TrySpendPetAbilityPoints(owner, PetTrainingStat.Hits, 1));

        ointment.OnDoubleClick(owner);
        owner.Target.Invoke(owner, copy);
        Assert.True(ointment.Deleted);
        Assert.Equal(1000, copy.HitsMaxSeed);
        Assert.Equal(1200, copy.RawStr);
        Assert.Equal(110, copy.RawDex);
        Assert.Equal(1, copy.RawInt);
        Assert.Equal(70, copy.FireResistSeed);
        Assert.Equal(60, copy.VirtualArmor);
        Assert.Equal((20, 27), (copy.DamageMin, copy.DamageMax));
        Assert.False(copy.TryRestorePetDeathStats());
    }

    [Fact]
    public void TrainingADerivedPoolKeepsItsRestorableLoss()
    {
        var (owner, pet) = NewPet();
        pet.RawStr = 120;
        pet.HitsMaxSeed = -1;

        for (var i = 0; i < 3; i++)
        {
            pet.AwardPetExperience(12);
        }

        pet.IsBonded = true;
        pet.IsDeadPet = true;
        pet.ApplyPetDeathPenalty(2);
        Assert.Equal(114, pet.RawStr);
        Assert.Equal(-1, pet.HitsMaxSeed);
        pet.ResurrectPet();
        Assert.Equal(1, pet.TrySpendPetAbilityPoints(owner, PetTrainingStat.Hits, 1));
        Assert.Equal(115, pet.HitsMaxSeed);
        Assert.True(pet.TryRestorePetDeathStats());
        Assert.Equal(120, pet.RawStr);
        Assert.Equal(121, pet.HitsMaxSeed);
    }

    [Fact]
    public void RepeatedDeathOnABondedGhostDoesNotApplyAnotherPenalty()
    {
        var (_, pet) = NewPet();
        pet.RawStr = 120;
        for (var i = 0; i < 3; i++)
        {
            pet.AwardPetExperience(12);
        }

        pet.IsBonded = true;
        pet.Kill();
        Assert.True(pet.IsDeadPet);
        var progression = (pet.PetAbilityPoints, pet.PetExperience, pet.RawStr);
        pet.OnDeath(null);
        Assert.Equal(progression, (pet.PetAbilityPoints, pet.PetExperience, pet.RawStr));
    }

    [Fact]
    public void ABrushCraftsOneDraughtOnlyWithTamingAndAllIngredients()
    {
        var owner = _harness.NewPlayer();
        owner.AddItem(new Backpack());
        var brush = _harness.TrackItem(new PetResurrectionBrush());
        owner.Backpack.DropItem(brush);
        owner.Backpack.DropItem(new Bottle(2));
        owner.Backpack.DropItem(new Ginseng(4));
        owner.Backpack.DropItem(new Garlic(2));
        owner.Skills.AnimalTaming.BaseFixedPoint = 799;

        Assert.False(brush.TryCraftDraught(owner));
        owner.Skills.AnimalTaming.BaseFixedPoint = 800;
        Assert.False(brush.TryCraftDraught(owner));
        Assert.Equal(2, owner.Backpack.GetAmount(typeof(Bottle)));
        Assert.Equal(4, owner.Backpack.GetAmount(typeof(Ginseng)));
        Assert.Equal(2, owner.Backpack.GetAmount(typeof(Garlic)));

        owner.Backpack.DropItem(new Garlic(2));
        brush.OnDoubleClick(owner);
        Assert.NotNull(owner.Backpack.FindItemByType<PetResurrectionDraught>());
        Assert.False(brush.Deleted);
        Assert.Equal(1, owner.Backpack.GetAmount(typeof(Bottle)));
        Assert.Equal(1, owner.Backpack.GetAmount(typeof(Ginseng)));
        Assert.Equal(1, owner.Backpack.GetAmount(typeof(Garlic)));
    }

    [Fact]
    public void AHouseTrainerAwardsOnlyTheSelectedPetsLandedMeleeAndStopsWhenRedeeded()
    {
        var (owner, pet) = NewPet();
        owner.AddItem(new Backpack());
        var origin = new Point3D(1400, 1400, 0);
        var house = _harness.TrackItem(new GuildHouse(owner));
        house.MoveToWorld(origin, Map.Felucca);
        owner.MoveToWorld(new Point3D(origin.X, origin.Y + 1, 1), house.Map);
        pet.MoveToWorld(new Point3D(origin.X + 1, origin.Y + 1, 1), house.Map);
        pet.SetDamage(5);
        var otherPet = _harness.NewCreature();
        otherPet.Tamable = true;
        otherPet.SetControlMaster(owner);
        otherPet.MoveToWorld(new Point3D(origin.X - 1, origin.Y + 1, 1), house.Map);
        otherPet.SetDamage(5);
        var stranger = _harness.NewPlayer();
        stranger.MoveToWorld(new Point3D(origin.X, origin.Y - 1, 1), house.Map);

        var deed = _harness.TrackItem(new PersonalPetTrainerDeed());
        owner.Backpack.DropItem(deed);
        deed.OnDoubleClick(owner);
        owner.Target.Invoke(owner, new StaticTarget(new Point3D(origin.X, origin.Y, 1), TestTiles.Floor));
        Assert.True(deed.Deleted);
        var addon = Assert.IsType<PersonalPetTrainerAddon>(Assert.Single(house.Addons));

        Assert.Null(addon.TryStartSparring(stranger, pet));
        addon.Components[0].OnDoubleClick(owner);
        owner.Target.Invoke(owner, pet);
        var trainer = Assert.IsType<PersonalPetTrainer>(pet.ControlTarget);
        Assert.Equal(OrderType.Attack, pet.ControlOrder);
        Assert.True(pet.CanBeHarmful(trainer, false));
        Assert.False(otherPet.CanBeHarmful(trainer, false));
        Assert.False(trainer.CanBeDamaged());

        var weapon = _harness.TrackItem(new Longsword());
        var hits = trainer.Hits;
        weapon.OnHit(otherPet, trainer);
        Assert.Equal(hits, trainer.Hits);
        Assert.Equal(0, otherPet.PetExperience);
        weapon.OnHit(pet, trainer);
        Assert.Equal(hits - 1, trainer.Hits);
        Assert.Equal(2, pet.PetExperience);

        addon.OnChop(owner);
        Assert.True(addon.Deleted);
        Assert.True(trainer.Deleted);
        Assert.Equal(owner, pet.ControlTarget);
        Assert.Equal(OrderType.Follow, pet.ControlOrder);
        Assert.NotNull(owner.Backpack.FindItemByType<PersonalPetTrainerDeed>());
    }

    private sealed class BondableTestCreature : TestCreature
    {
        public override bool IsBondable => !Summoned;
    }
}
