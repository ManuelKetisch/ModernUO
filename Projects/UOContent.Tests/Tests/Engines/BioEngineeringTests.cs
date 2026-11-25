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
public class BioEngineeringTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void StudyUsesBaseSkillAndPersistsOnTheCharacter()
    {
        var player = _world.Track(new PlayerMobile { Player = true });
        player.AddItem(new Backpack());
        player.Skills.AnimalTaming.Base = 100;
        var book = Pack(player, new BioEngineeringBook());
        player.Skills.Tinkering.Base = 99.9;

        book.OnDoubleClick(player);
        Assert.False(BioEngineeringBook.HasStudied(player));
        Assert.False(book.Deleted);

        player.Skills.Tinkering.Base = 100;
        book.OnDoubleClick(player);
        Assert.True(book.Deleted);

        var copy = _world.Track(new PlayerMobile(World.NewMobile));
        copy.DefaultMobileInit();
        RoundTrip(player.Serialize, copy.Deserialize);
        Assert.True(BioEngineeringBook.HasStudied(copy));
    }

    [Fact]
    public void CraftConsumesExactResourcesAndAChargeOnlyAfterSuccess()
    {
        var player = NewEngineer();
        var tool = Pack(player, new BioEngineeringTool(2));
        var bottle = Pack(player, new Bottle());
        var ingots = Pack(player, new IronIngot(25));

        Assert.False(tool.TryCraft(player, BioRecipe.Vial));
        Assert.False(bottle.Deleted);
        Assert.Equal(25, ingots.Amount);
        Assert.Equal(2, tool.Charges);

        Pack(player, new BioOrganicMatter(1000));
        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = player;
        player.NetState = state;
        try
        {
            tool.OnDoubleClick(player);
            var gump = Assert.IsType<BioEngineeringTool.RecipeGump>(player.FindGump<BioEngineeringTool.RecipeGump>());
            gump.OnResponse(state, new RelayInfo((int)BioRecipe.Vial, [], [], [], []));
        }
        finally
        {
            player.CloseGump<BioEngineeringTool.RecipeGump>();
            player.NetState = null;
            state.Mobile = null;
        }

        Assert.NotNull(player.Backpack.FindItemByType<BioVial>());
        Assert.True(bottle.Deleted);
        Assert.True(ingots.Deleted);
        Assert.Equal(1, tool.Charges);
        Assert.Equal(0, player.Backpack.GetAmount(typeof(BioOrganicMatter)));
    }

    [Fact]
    public void SamplingRejectsControlledCreaturesAndReportsLuckInfluence()
    {
        var player = NewEngineer();
        var vial = Pack(player, new BioVial());
        var donor = CreateDonor();
        donor.SetControlMaster(player);

        Assert.False(vial.TrySample(player, BioSampleKind.Prowess, donor, 0.0));
        Assert.False(vial.Deleted);
        Assert.False(vial.Filled);
        Assert.InRange(BioVial.SampleChance(0), 0.01, 0.50);
        Assert.InRange(BioVial.SampleChance(4000), 0.75, 0.99);
        Assert.Equal(BioVial.SampleChance(4000), BioVial.SampleChance(int.MaxValue));

        donor.SetControlMaster(null);
        Assert.False(vial.TrySample(player, BioSampleKind.Prowess, donor, 0.99));
        Assert.True(vial.Deleted);
    }

    [Fact]
    public void SamplesAndCreatureSurviveSaveAndReload()
    {
        var engineer = NewEngineer();
        var stranger = NewEngineer();
        var donor = CreateDonor();
        var rack = Pack(engineer, new BioVialSet());

        foreach (var kind in new[] { BioSampleKind.Prowess, BioSampleKind.Environment, BioSampleKind.Mental, BioSampleKind.Mimic })
        {
            var vial = Pack(engineer, new BioVial());
            Assert.True(vial.TrySample(engineer, kind, donor, 0.0));
            var saved = _world.TrackItem(new BioVial(World.NewItem));
            RoundTrip(vial.Serialize, saved.Deserialize);
            Assert.True(saved.Filled);
            Assert.Equal(kind, saved.Kind);
            engineer.Backpack.DropItem(saved);
            UseWorkbench(engineer, rack, 1, saved);
            Assert.True(saved.Deleted);
        }

        var duplicate = Pack(engineer, new BioVial());
        Assert.True(duplicate.TrySample(engineer, BioSampleKind.Prowess, donor, 0.0));
        Assert.False(rack.TryAdd(engineer, duplicate));
        Assert.False(duplicate.Deleted);

        var restoredRack = _world.TrackItem(new BioVialSet(World.NewItem));
        RoundTrip(rack.Serialize, restoredRack.Deserialize);
        engineer.Backpack.DropItem(restoredRack);
        Assert.True(restoredRack.Complete);

        var deed = Pack(engineer, new BioCreationDeed());
        engineer.FollowersMax = 2;
        Assert.Null(deed.TryCreate(engineer));
        Assert.False(deed.Deleted);
        engineer.FollowersMax = 5;
        var construct = _world.Track(deed.TryCreate(engineer));
        Assert.True(deed.Deleted);
        Assert.Equal(3, engineer.Followers);
        Assert.True(construct.IsInvulnerable);

        var fromDeed = _world.Track(new BioConstruct(World.NewMobile));
        RoundTrip(construct.Serialize, fromDeed.Deserialize);
        construct.Delete();
        fromDeed.MoveToWorld(engineer.Location, engineer.Map);
        Assert.True(fromDeed.CanBeControlledBy(engineer));
        Assert.False(fromDeed.CanBeControlledBy(stranger));

        stranger.Backpack.DropItem(restoredRack);
        Assert.False(restoredRack.TryInfuse(stranger, fromDeed));
        Assert.False(restoredRack.Deleted);

        engineer.Backpack.DropItem(restoredRack);
        UseWorkbench(engineer, restoredRack, 3, fromDeed);
        Assert.True(restoredRack.Deleted);
        Assert.False(fromDeed.IsInvulnerable);
        Assert.Equal(3000, fromDeed.HitsMax);
        Assert.Equal(1200, fromDeed.RawStr);
        Assert.Equal(300, fromDeed.RawDex);
        Assert.Equal(1000, fromDeed.StamMax);
        Assert.Equal(1200, fromDeed.RawInt);
        Assert.Equal(2000, fromDeed.ManaMax);
        Assert.Equal(18, fromDeed.DamageMin);
        Assert.Equal(26, fromDeed.DamageMax);
        Assert.Equal(70, fromDeed.PhysicalResistanceSeed);
        Assert.Equal(75, fromDeed.VirtualArmor);
        Assert.Equal(100.0, fromDeed.Skills.Magery.Base);
        Assert.Equal((int)donor.Body, (int)fromDeed.Body);

        var savedConstruct = _world.Track(new BioConstruct(World.NewMobile));
        RoundTrip(fromDeed.Serialize, savedConstruct.Deserialize);
        Assert.True(savedConstruct.Assembled);
        Assert.True(savedConstruct.CanBeControlledBy(stranger));
        Assert.Equal(fromDeed.ControlMaster, savedConstruct.ControlMaster);
        Assert.Equal(fromDeed.HitsMax, savedConstruct.HitsMax);
        Assert.Equal(BioTrait.SearingStrike, savedConstruct.Trait);

        var melee = _world.TrackItem(new Longsword());
        var ranged = _world.TrackItem(new Bow());
        fromDeed.Stam = 50;
        fromDeed.OnLandedMeleeAttack(stranger);
        Assert.Equal(50, fromDeed.Stam);

        donor.SetResistance(ResistanceType.Physical, 0);
        donor.Hits = donor.HitsMax;
        donor.Blessed = true;
        melee.OnHit(fromDeed, donor);
        Assert.Equal(donor.HitsMax, donor.Hits);
        Assert.Equal(50, fromDeed.Stam);

        donor.Blessed = false;
        ranged.OnHit(fromDeed, donor);
        Assert.Equal(50, fromDeed.Stam);

        var hitsBeforeStrike = donor.Hits;
        melee.OnHit(fromDeed, donor);
        Assert.True(donor.Hits < hitsBeforeStrike);
        Assert.Equal(38, fromDeed.Stam);

        melee.OnHit(fromDeed, donor);
        Assert.Equal(38, fromDeed.Stam);
    }

    [Fact]
    public void HumanConstructUsesOwnerStableAndKeepsGearAndSkillLocks()
    {
        var owner = NewPlayer();
        var outsider = NewPlayer();
        var trainer = _world.Track(new AnimalTrainer());
        trainer.MoveToWorld(owner.Location, owner.Map);
        var bio = NewConstruct(owner, 400, BioTrait.QuickRecovery);
        var sword = _world.TrackItem(new Longsword { Layer = Layer.OneHanded, LootType = LootType.Blessed });
        var gold = Pack(owner, new Gold(60));

        Assert.True(bio.AllowEquipFrom(owner));
        Assert.False(bio.AllowEquipFrom(outsider));
        Assert.True(bio.EquipItem(sword));
        Assert.False(bio.CanBeControlledBy(outsider));
        Assert.False(bio.TrySetSkillLock(outsider, SkillName.Swords, SkillLock.Locked));
        Assert.True(bio.TrySetSkillLock(owner, SkillName.Swords, SkillLock.Locked));

        trainer.EndStable(outsider, bio);
        Assert.Same(owner, bio.ControlMaster);
        Assert.False(bio.IsStabled);
        trainer.EndStable(owner, bio);
        Assert.True(bio.IsStabled);
        Assert.Same(owner, bio.StabledBy);
        Assert.Null(bio.ControlMaster);
        Assert.Contains(bio, owner.Stabled);
        var shield = _world.TrackItem(new MetalShield { Layer = Layer.TwoHanded });
        Assert.False(bio.EquipItem(shield));
        Assert.Equal(30, gold.Amount);
        Assert.Same(sword, bio.FindItemOnLayer(Layer.OneHanded));

        trainer.EndClaimList(outsider, bio);
        Assert.True(bio.IsStabled);
        trainer.EndClaimList(owner, bio);
        Assert.False(bio.IsStabled);
        Assert.Same(owner, bio.ControlMaster);
        Assert.Same(sword, bio.FindItemOnLayer(Layer.OneHanded));
        Assert.Equal(SkillLock.Locked, bio.Skills.Swords.Lock);

        var ordinaryHuman = _world.NewCreature();
        ordinaryHuman.Body = 400;
        Assert.True(ordinaryHuman.SetControlMaster(owner));
        ordinaryHuman.MoveToWorld(owner.Location, owner.Map);
        trainer.EndStable(owner, ordinaryHuman);
        Assert.False(ordinaryHuman.IsStabled);
        Assert.Equal(30, gold.Amount);

        var saved = _world.Track(new BioConstruct(World.NewMobile));
        RoundTrip(bio.Serialize, saved.Deserialize);
        Assert.True(saved.HasHumanForm);
        Assert.Equal(SkillLock.Locked, saved.Skills.Swords.Lock);
        Assert.Contains(sword, saved.Items);
    }

    [Fact]
    public void NamedHumanCommandsMountUndressAndUsePackedBandages()
    {
        var owner = NewPlayer();
        var outsider = NewPlayer();
        var bio = NewConstruct(owner, 400, BioTrait.QuickRecovery);
        bio.Name = "Iris";

        var horse = _world.Track(new Horse());
        Assert.True(horse.SetControlMaster(owner));
        horse.MoveToWorld(owner.Location, owner.Map);

        var rudeOrder = new SpeechEventArgs(outsider, "Iris ride", MessageType.Regular, 0, []);
        bio.OnSpeech(rudeOrder);
        Assert.False(rudeOrder.Handled);
        Assert.Null(outsider.Target);

        var ride = new SpeechEventArgs(owner, "Iris ride", MessageType.Regular, 0, []);
        bio.OnSpeech(ride);
        Assert.True(ride.Handled);
        owner.Target.Invoke(owner, horse);
        Assert.Same(horse, bio.Mount);
        Assert.Same(bio, horse.Rider);

        var trainer = _world.Track(new AnimalTrainer());
        trainer.MoveToWorld(owner.Location, owner.Map);
        var gold = Pack(owner, new Gold(30));
        trainer.EndStable(owner, bio);
        Assert.False(bio.IsStabled);
        Assert.Equal(30, gold.Amount);

        var dismount = new SpeechEventArgs(owner, "Iris dismount", MessageType.Regular, 0, []);
        bio.OnSpeech(dismount);
        Assert.True(dismount.Handled);
        Assert.Null(bio.Mount);
        Assert.Null(horse.Rider);
        Assert.Same(owner.Map, horse.Map);

        var sword = _world.TrackItem(new Longsword { Layer = Layer.OneHanded });
        Assert.True(bio.EquipItem(sword));
        Assert.False(bio.TryUndress(outsider));
        var undress = new SpeechEventArgs(owner, "Iris undress", MessageType.Regular, 0, []);
        bio.OnSpeech(undress);
        Assert.True(undress.Handled);
        Assert.True(sword.IsChildOf(bio.Backpack));
        Assert.True(bio.CheckNonlocalLift(owner, sword));
        Assert.False(bio.CheckNonlocalLift(outsider, sword));

        var bandages = _world.TrackItem(new Bandage(2));
        Assert.True(bio.OnDragDrop(owner, bandages));
        bio.Hits -= 35;
        Assert.True(bio.TryBandageSelf());
        Assert.Equal(1, bandages.Amount);
        Assert.False(bio.TryBandageSelf());

        bio.IssueOrder(OrderType.Release, owner);
        Assert.True(bio.Deleted);
        Assert.Null(BandageContext.GetContext(bio));
        Assert.True(sword.IsChildOf(owner.Backpack));
        Assert.True(bandages.IsChildOf(owner.Backpack));
    }

    [Fact]
    public void StableMapAndDeletionDismountWithoutLosingTheOwnedMount()
    {
        var owner = NewPlayer();
        var bio = NewConstruct(owner, 400, BioTrait.QuickRecovery);
        var horse = _world.Track(new Horse());
        Assert.True(horse.SetControlMaster(owner));
        horse.MoveToWorld(owner.Location, owner.Map);

        Assert.True(bio.TryRide(owner, horse));
        bio.Internalize();
        Assert.Null(horse.Rider);
        Assert.Null(bio.Mount);
        Assert.Same(owner.Map, horse.Map);
        Assert.Equal(owner.Location, horse.Location);

        bio.MoveToWorld(owner.Location, owner.Map);
        Assert.True(bio.TryRide(owner, horse));
        bio.Delete();
        Assert.False(horse.Deleted);
        Assert.Null(horse.Rider);
        Assert.Same(owner, horse.ControlMaster);
        Assert.Same(owner.Map, horse.Map);
    }

    [Fact]
    public void AcidBurstHitsOnlyItsTargetAndNearbyAggressiveWildCreatures()
    {
        var owner = NewPlayer();
        var bio = NewConstruct(owner, 211, BioTrait.AcidBurst);
        var target = CreateDonor();
        var aggressive = CreateDonor();
        aggressive.Combatant = bio;
        var neutral = CreateDonor();
        var controlled = CreateDonor();
        Assert.True(controlled.SetControlMaster(owner));
        controlled.Combatant = bio;

        var neutralHits = neutral.Hits;
        var controlledHits = controlled.Hits;
        var aggressiveHits = aggressive.Hits;
        var targetHits = target.Hits;
        var stamina = bio.Stam;
        bio.OnLandedMeleeAttack(target);

        Assert.True(target.Hits < targetHits);
        Assert.True(aggressive.Hits < aggressiveHits);
        Assert.Equal(neutralHits, neutral.Hits);
        Assert.Equal(controlledHits, controlled.Hits);
        Assert.Equal(stamina - 12, bio.Stam);
        bio.OnLandedMeleeAttack(target);
        Assert.Equal(stamina - 12, bio.Stam);
    }

    [Fact]
    public void HumanSkillsUseNewProwessDataAndOlderVialsStillWork()
    {
        var owner = NewPlayer();
        var donor = CreateDonor();
        donor.SetSkill(SkillName.Healing, 81.5);
        donor.SetSkill(SkillName.Swords, 95);
        Pack(owner, new BioEngineeringBook()).OnDoubleClick(owner);
        var sample = Pack(owner, new BioVial());
        Assert.True(sample.TrySample(owner, BioSampleKind.Prowess, donor, 0.0));

        var bio = NewConstruct(owner, 400, BioTrait.QuickRecovery, sample.SampleValues);
        Assert.Equal(81.5, bio.Skills.Healing.Base);
        Assert.Equal(95, bio.Skills.Swords.Base);
        Assert.Equal(0, NewConstruct(NewPlayer(), 400, BioTrait.QuickRecovery).Skills.Swords.Base);
    }

    [Fact]
    public void LowerTrainingUsesExplicitConfirmationAndRejectsStaleChanges()
    {
        var owner = NewPlayer();
        var outsider = NewPlayer();
        var bio = NewConstruct(owner, 400, BioTrait.QuickRecovery);
        bio.Name = "Iris";
        var skill = bio.Skills.Wrestling;
        skill.BaseFixedPoint = 14;

        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = owner;
        owner.NetState = state;
        try
        {
            BioConstruct.BioSkillDecreaseGump OpenDecrease()
            {
                bio.OnSpeech(new SpeechEventArgs(owner, "Iris skills", MessageType.Regular, 0, []));
                var skills = owner.FindGump<BioConstruct.BioSkillsGump>();
                Assert.NotNull(skills);
                skills.OnResponse(state, new RelayInfo(101, [], [], [], []));
                return Assert.IsType<BioConstruct.BioSkillDecreaseGump>(owner.FindGump<BioConstruct.BioSkillDecreaseGump>());
            }

            static RelayInfo Confirm() => new(1, [], [], [], []);

            bio.OnSpeech(new SpeechEventArgs(owner, "Iris skills", MessageType.Regular, 0, []));
            var initialGump = owner.FindGump<BioConstruct.BioSkillsGump>();
            Assert.NotNull(initialGump);
            initialGump.OnResponse(state, Confirm());
            Assert.Equal(SkillLock.Down, skill.Lock);
            Assert.Equal(14, skill.BaseFixedPoint);

            var pending = OpenDecrease();
            Assert.Equal(14, skill.BaseFixedPoint);
            var nearby = owner.Location;
            owner.MoveToWorld(new Point3D(1850, 1850, 0), owner.Map);
            pending.OnResponse(state, Confirm());
            Assert.Equal(14, skill.BaseFixedPoint);
            owner.MoveToWorld(nearby, owner.Map);

            pending = OpenDecrease();
            Assert.True(bio.TrySetSkillLock(owner, SkillName.Wrestling, SkillLock.Locked));
            pending.OnResponse(state, Confirm());
            Assert.Equal(14, skill.BaseFixedPoint);
            Assert.True(bio.TrySetSkillLock(owner, SkillName.Wrestling, SkillLock.Down));

            pending = OpenDecrease();
            Assert.True(bio.SetControlMaster(outsider));
            pending.OnResponse(state, Confirm());
            Assert.Equal(14, skill.BaseFixedPoint);
            Assert.True(bio.SetControlMaster(owner));

            OpenDecrease().OnResponse(state, Confirm());
            Assert.Equal(4, skill.BaseFixedPoint);
            OpenDecrease().OnResponse(state, Confirm());
            Assert.Equal(0, skill.BaseFixedPoint);
        }
        finally
        {
            owner.CloseGump<BioConstruct.BioSkillsGump>();
            owner.CloseGump<BioConstruct.BioSkillDecreaseGump>();
            owner.NetState = null;
            state.Mobile = null;
        }
    }

    private BioConstruct NewConstruct(PlayerMobile owner, int body, BioTrait trait, int[] prowess = null)
    {
        var bio = _world.Track(new BioConstruct(owner));
        Assert.True(bio.SetControlMaster(owner));
        bio.MoveToWorld(owner.Location, owner.Map);
        Assert.True(
            bio.ApplySamples(
                prowess ?? [120, 250, 10, 14, 15, 20, 500, 500, 500],
                [100, 200, 0, 0, 0], [65, 100, 0, 0, 0, 0, 0, 0],
                [body, 0, 0, (int)trait, 0, 0, 0, 0, 0]
            )
        );
        return bio;
    }

    private PlayerMobile NewPlayer()
    {
        var player = _world.NewPlayer();
        player.Player = true;
        player.AddItem(new Backpack());
        player.MoveToWorld(new Point3D(1800, 1800, 0), Map.Felucca);
        player.Skills.AnimalTaming.Base = 100;
        player.Skills.Tinkering.Base = 100;
        return player;
    }

    private PlayerMobile NewEngineer()
    {
        var player = NewPlayer();
        Pack(player, new BioEngineeringBook()).OnDoubleClick(player);
        return player;
    }

    private TestCreature CreateDonor()
    {
        var donor = _world.NewCreature();
        donor.MoveToWorld(new Point3D(1801, 1800, 0), Map.Felucca);
        donor.Body = 400;
        donor.SetStr(1800);
        donor.SetDex(400);
        donor.SetInt(1300);
        donor.HitsMaxSeed = 4000;
        donor.SetStam(1200);
        donor.SetMana(2500);
        donor.SetDamage(25, 40);
        donor.SetResistance(ResistanceType.Physical, 100);
        donor.VirtualArmor = 90;
        donor.SetSkill(SkillName.Magery, 125);
        return donor;
    }

    private T Pack<T>(PlayerMobile player, T item) where T : Item
    {
        _world.TrackItem(item);
        player.Backpack.DropItem(item);
        return item;
    }

    private static void UseWorkbench(PlayerMobile player, BioVialSet rack, int button, IEntity target)
    {
        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = player;
        player.NetState = state;
        try
        {
            rack.OnDoubleClick(player);
            var gump = player.FindGump<BioWorkbenchGump>();
            Assert.NotNull(gump);
            gump.OnResponse(state, new RelayInfo(button, [], [], [], []));
            Assert.NotNull(player.Target);
            player.Target.Invoke(player, target);
        }
        finally
        {
            player.CloseGump<BioWorkbenchGump>();
            player.NetState = null;
            state.Mobile = null;
        }
    }

    private static void RoundTrip(Action<IGenericWriter> serialize, Action<IGenericReader> deserialize)
    {
        var writer = new BufferWriter(true);
        serialize(writer);
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        deserialize(reader);
        Assert.Equal(writer.Position, reader.Position);
    }
}
