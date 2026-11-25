using System;
using Server;
using Server.Engines.BioEngineering;
using Server.Engines.CannedEvil;
using Server.Engines.WeaponLeveling;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public sealed class BioEquipmentProgressionTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void JewelryUsesTheSameOwnerControlsAndLifeCycleAsOtherHumanGear()
    {
        var owner = NewPlayer();
        var outsider = NewPlayer();
        var trainer = _world.Track(new AnimalTrainer());
        trainer.MoveToWorld(owner.Location, owner.Map);
        var bio = NewConstruct(owner);
        var otherForm = NewConstruct(outsider, 211);
        var ring = _world.TrackItem(new GoldRing { Layer = Layer.Ring });
        ring.Attributes.BonusStr = 8;
        var startingStrength = bio.Str;

        Assert.False(bio.AllowEquipFrom(outsider));
        Assert.False(otherForm.AllowEquipFrom(outsider));
        Assert.False(otherForm.EquipItem(ring));
        Assert.True(bio.AllowEquipFrom(owner));
        Assert.True(bio.EquipItem(ring));
        Assert.Same(ring, bio.FindItemOnLayer(Layer.Ring));
        Assert.Equal(startingStrength + (Core.AOS ? 8 : 0), bio.Str);
        Assert.False(bio.CanBeControlledBy(outsider));
        Assert.False(bio.CheckNonlocalLift(outsider, ring));
        Assert.True(bio.CheckNonlocalLift(owner, ring));

        owner.Backpack.DropItem(_world.TrackItem(new Gold(60)));
        trainer.EndStable(owner, bio);
        Assert.True(bio.IsStabled);
        Assert.False(bio.CheckNonlocalLift(owner, ring));
        Assert.Same(ring, bio.FindItemOnLayer(Layer.Ring));
        trainer.EndClaimList(owner, bio);
        Assert.False(bio.IsStabled);
        Assert.Same(ring, bio.FindItemOnLayer(Layer.Ring));

        Assert.False(bio.TryUndress(outsider));
        Assert.True(bio.TryUndress(owner));
        Assert.True(ring.IsChildOf(bio.Backpack));
        Assert.Equal(startingStrength, bio.Str);
        bio.Delete();
        Assert.True(ring.IsChildOf(owner.Backpack));
    }

    [Theory]
    [InlineData(false, false, 80)]
    [InlineData(true, false, 160)]
    [InlineData(true, true, 20)]
    public void ConstructDirectKillsUsePlayerRatesWithoutAwardingTheOwnersWeapon(
        bool felucca, bool champion, int expectedExperience
    )
    {
        var owner = NewPlayer(felucca ? Map.Felucca : Map.Trammel);
        var bio = NewConstruct(owner);
        var sword = NewEnabledWeapon(owner);
        var ownerSword = NewEnabledWeapon(owner);
        Assert.True(bio.EquipItem(sword));
        Assert.True(owner.EquipItem(ownerSword));
        ownerSword.AwardWeaponExperience(249_900, owner);
        var victim = NewVictim(bio);
        if (champion)
        {
            var spawn = _world.TrackItem(new ChampionSpawn());
            spawn.MoveToWorld(owner.Location, owner.Map);
            spawn.AddSpawnCreature(victim);
        }

        using var state = Connect(owner);
        try
        {
            sword.OnHit(bio, victim);
            Assert.True(victim.Deleted);
            Assert.Equal(expectedExperience, sword.WeaponExperience);
            Assert.Equal(249_900, ownerSword.WeaponExperience);

            var spellVictim = NewVictim(bio);
            AOS.Damage(spellVictim, bio, 20, 0, 100, 0, 0, 0);
            Assert.True(spellVictim.Deleted);
            Assert.Equal(expectedExperience, sword.WeaponExperience);
            Assert.Equal(felucca ? 249_920 : 249_910, ownerSword.WeaponExperience);
        }
        finally
        {
            Disconnect(owner, state);
        }
    }

    [Fact]
    public void DirectAbilityScopeRechecksTheWieldedWeaponAndOwnerAtDeath()
    {
        var owner = NewPlayer();
        var bio = NewConstruct(owner);
        var sword = NewEnabledWeapon(owner);
        Assert.True(bio.EquipItem(sword));

        using var state = Connect(owner);
        try
        {
            using var ability = WeaponLevelingCombat.BeginWeaponAbility(sword);
            var target = NewVictim(bio);
            using (WeaponLevelingCombat.BeginAbilityStrike(bio, target))
            {
                target.Damage(20, bio);
            }

            Assert.Equal(80, sword.WeaponExperience);

            target = NewVictim(bio);
            using (WeaponLevelingCombat.BeginAbilityStrike(bio, target))
            {
                owner.Backpack.DropItem(sword);
                target.Damage(20, bio);
            }

            Assert.Equal(80, sword.WeaponExperience);
            Assert.True(bio.EquipItem(sword));

            target = NewVictim(bio);
            using (WeaponLevelingCombat.BeginAbilityStrike(bio, target))
            {
                owner.Location = new Point3D(owner.X + 19, owner.Y, owner.Z);
                target.Damage(20, bio);
            }

            Assert.Equal(80, sword.WeaponExperience);
            sword.OnHit(bio, NewVictim(bio));
            Assert.Equal(80, sword.WeaponExperience);

            owner.Location = bio.Location;
            target = NewVictim(bio);
            using (WeaponLevelingCombat.BeginAbilityStrike(bio, target))
            {
                Disconnect(owner, state);
                target.Damage(20, bio);
            }

            Assert.Equal(80, sword.WeaponExperience);
            sword.OnHit(bio, NewVictim(bio));
            Assert.Equal(80, sword.WeaponExperience);
        }
        finally
        {
            Disconnect(owner, state);
        }
    }

    [Fact]
    public void NonhumanConstructRetainsLegacyPetCreditWithAFarAwayOfflineOwner()
    {
        var owner = NewPlayer();
        var bio = NewConstruct(owner, 211);
        var sword = NewEnabledWeapon(owner);
        Assert.True(owner.EquipItem(sword));
        sword.AwardWeaponExperience(249_900, owner);
        owner.Location = new Point3D(owner.X + 19, owner.Y, owner.Z);
        Assert.Null(owner.NetState);

        var target = NewVictim(bio);
        target.Damage(20, bio);

        Assert.True(target.Deleted);
        Assert.Equal(249_910, sword.WeaponExperience);
    }

    private PlayerMobile NewPlayer(Map map = null)
    {
        var player = _world.NewPlayer();
        player.Player = true;
        player.RawStr = 100;
        player.RawDex = 100;
        player.Hits = player.HitsMax;
        player.AddItem(_world.TrackItem(new Backpack()));
        player.Skills.AnimalTaming.Base = 100;
        player.MoveToWorld(new Point3D(1800, 1800, 0), map ?? Map.Trammel);
        return player;
    }

    private BioConstruct NewConstruct(PlayerMobile owner, int body = 400)
    {
        var bio = _world.Track(new BioConstruct(owner));
        Assert.True(bio.SetControlMaster(owner));
        bio.MoveToWorld(owner.Location, owner.Map);
        Assert.True(bio.ApplySamples(
            [120, 250, 10, 14, 15, 20, 500, 500, 500],
            [100, 200, 0, 0, 0], [65, 100, 0, 0, 0, 0, 0, 0],
            [body, 0, 0, (int)BioTrait.QuickRecovery, 0, 0, 0, 0, 0]
        ));
        return bio;
    }

    private Longsword NewEnabledWeapon(PlayerMobile owner)
    {
        var weapon = _world.TrackItem(new Longsword { Layer = Layer.OneHanded, MinDamage = 30, MaxDamage = 30 });
        owner.Backpack.DropItem(weapon);
        Assert.True(weapon.TryEnableWeaponLeveling(owner));
        return weapon;
    }

    private TestCreature NewVictim(BioConstruct bio)
    {
        var victim = _world.NewCreature();
        victim.Fame = 15_000;
        victim.Hits = 1;
        victim.MoveToWorld(new Point3D(bio.X + 1, bio.Y, bio.Z), bio.Map);
        return victim;
    }

    private static NetState Connect(PlayerMobile owner)
    {
        var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = owner;
        owner.NetState = state;
        return state;
    }

    private static void Disconnect(PlayerMobile owner, NetState state)
    {
        owner.NetState = null;
        state.Mobile = null;
    }
}
