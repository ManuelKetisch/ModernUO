using System;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.WeaponLeveling;
using Server.Engines.CannedEvil;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public sealed class WeaponLevelingTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Theory]
    [InlineData(0, 1)]
    [InlineData(299, 1)]
    [InlineData(300, 2)]
    [InlineData(800, 3)]
    [InlineData(62_400, 25)]
    [InlineData(249_899, 49)]
    [InlineData(249_900, 50)]
    [InlineData(562_400, 75)]
    [InlineData(980_000, 99)]
    [InlineData(999_899, 99)]
    [InlineData(999_900, 100)]
    [InlineData(int.MaxValue, 100)]
    public void LevelBoundariesMatchThePublishedTable(int experience, int level)
    {
        Assert.Equal(level, WeaponLevelingRules.LevelForExperience(experience));
        if (level < 100)
        {
            Assert.True(experience >= WeaponLevelingRules.ExperienceForLevel(level));
            Assert.True(experience < WeaponLevelingRules.ExperienceForLevel(level + 1));
        }
    }

    [Fact]
    public void DeedRequiresOwnBackpackAndWeaponAndDoesNotConsumeOnDuplicate()
    {
        var owner = NewPlayer();
        var other = NewPlayer();
        var weapon = _world.TrackItem(new Longsword());
        weapon.Attributes.AttackChance = 15;
        other.Backpack.DropItem(weapon);
        var deed = _world.TrackItem(new WeaponLevelDeed());
        owner.Backpack.DropItem(deed);

        deed.OnDoubleClick(owner);
        Assert.NotNull(owner.Target);
        owner.Target.Invoke(owner, weapon);
        Assert.False(deed.Deleted);
        Assert.False(weapon.IsWeaponLevelingEnabled);

        owner.Backpack.DropItem(weapon);
        deed.OnDoubleClick(owner);
        owner.Target.Invoke(owner, weapon);
        Assert.True(deed.Deleted);
        Assert.True(weapon.IsWeaponLevelingEnabled);
        Assert.Equal(1, weapon.WeaponLevel);
        Assert.Equal(15, weapon.Attributes.AttackChance);

        var spare = _world.TrackItem(new WeaponLevelDeed());
        owner.Backpack.DropItem(spare);
        Assert.False(spare.TryUse(owner, weapon));
        Assert.False(spare.Deleted);
    }

    [Fact]
    public void SourceBudgetsAndArtifactCostSurviveTagRemovalAndSave()
    {
        var owner = NewPlayer();
        var crafted = _world.TrackItem(new Longsword { PlayerConstructed = true });
        var looted = _world.TrackItem(new Longsword());
        var artifact = _world.TrackItem(new BoneCrusher());

        foreach (var weapon in new BaseWeapon[] { crafted, looted, artifact })
        {
            owner.Backpack.DropItem(weapon);
            Assert.True(weapon.TryEnableWeaponLeveling(owner));
            weapon.AwardWeaponExperience(int.MaxValue, owner);
        }

        Assert.Equal(295, crafted.WeaponLevelingPointsAvailable);
        Assert.Equal(495, looted.WeaponLevelingPointsAvailable);
        Assert.Equal(495, artifact.WeaponLevelingPointsAvailable);
        var index = FindUpgrade(AosWeaponAttribute.HitLowerAttack);
        var before = Assert.IsType<WeaponUpgradeQuote>(artifact.QuoteWeaponUpgrade(index));
        Assert.Equal(6, before.Cost);
        Assert.True(artifact.TrySpendWeaponLevelingPoints(owner, before));
        Assert.True(artifact.TryRemoveWeaponLevelingArtifactTag(owner));
        Assert.False(artifact.HasWeaponLevelingArtifactTag);
        Assert.Equal(11, artifact.ArtifactRarity);
        Assert.Equal(489, artifact.WeaponLevelingPointsAvailable);
        Assert.Equal(3, Assert.IsType<WeaponUpgradeQuote>(artifact.QuoteWeaponUpgrade(index)).Cost);

        var writer = new BufferWriter(true);
        artifact.Serialize(writer);
        var copy = _world.TrackItem(new BoneCrusher(World.NewItem));
        var reader = SnapshotReader(writer);
        copy.Deserialize(reader);
        Assert.Equal(writer.Position, reader.Position);
        Assert.Equal(artifact.CaptureWeaponLeveling(), copy.CaptureWeaponLeveling());
        Assert.Equal(artifact.WeaponAttributes.HitLowerAttack, copy.WeaponAttributes.HitLowerAttack);
        Assert.False(copy.HasWeaponLevelingArtifactTag);
    }

    [Fact]
    public void BoostAddsTenLevelsAndOnlyTheFirstNineGiveExtraPoints()
    {
        var owner = NewPlayer();
        var weapon = _world.TrackItem(new Longsword { PlayerConstructed = true });
        owner.Backpack.DropItem(weapon);
        Assert.True(weapon.TryEnableWeaponLeveling(owner));
        weapon.AwardWeaponExperience(120, owner);

        var deed = _world.TrackItem(new WeaponLevelBoostDeed());
        owner.Backpack.DropItem(deed);
        deed.OnDoubleClick(owner);
        owner.Target.Invoke(owner, weapon);
        Assert.True(deed.Deleted);
        Assert.Equal(11, weapon.WeaponLevel);
        Assert.Equal(12_120, weapon.WeaponExperience);
        Assert.Equal(33, weapon.WeaponLevelingPointsAvailable);

        for (var i = 1; i < 10; i++)
        {
            Assert.True(weapon.TryApplyWeaponLevelBoost(owner));
        }

        Assert.Equal(100, weapon.WeaponLevel);
        Assert.Equal(999_900, weapon.WeaponExperience);
        Assert.Equal(295 + 9 * 3, weapon.WeaponLevelingPointsAvailable);
        Assert.Equal(10, weapon.CaptureWeaponLeveling().BoostUses);
        Assert.False(weapon.TryApplyWeaponLevelBoost(owner));
    }

    [Fact]
    public void AllCatalogEntriesSetTheirNativeAttributes()
    {
        var owner = NewPlayer();
        var upgrades = WeaponUpgradeCatalog.Upgrades;
        Assert.Equal(48, upgrades.Length);
        for (var i = 0; i < upgrades.Length; i++)
        {
            var option = upgrades[i];
            var weapon = NewEnabledWeapon(owner);
            weapon.AwardWeaponExperience(999_900, owner);
            if (option.Weapon == AosWeaponAttribute.MageWeapon)
            {
                weapon.WeaponAttributes.MageWeapon = 10;
            }

            var before = option.Read(weapon);
            var quote = Assert.IsType<WeaponUpgradeQuote>(weapon.QuoteWeaponUpgrade(i));
            Assert.Equal(option.Cost, quote.Cost);
            Assert.True(weapon.TrySpendWeaponLevelingPoints(owner, quote), option.Name);
            Assert.Equal(before + 1, option.Read(weapon));
            Assert.Equal(495 - option.Cost, weapon.WeaponLevelingPointsAvailable);
            Assert.False(weapon.TrySpendWeaponLevelingPoints(owner, quote));
        }
    }

    [Fact]
    public void OverCapPropertiesAndWeaponsWithoutAMageryPenaltyCannotBeRaised()
    {
        var owner = NewPlayer();
        var weapon = NewEnabledWeapon(owner);
        weapon.AwardWeaponExperience(999_900, owner);
        weapon.Attributes.ReflectPhysical = 21;
        var reflect = FindUpgrade(AosAttribute.ReflectPhysical);
        var mage = FindUpgrade(AosWeaponAttribute.MageWeapon);
        Assert.Null(weapon.QuoteWeaponUpgrade(reflect));
        Assert.Equal(21, weapon.Attributes.ReflectPhysical);
        Assert.Null(weapon.QuoteWeaponUpgrade(mage));

        weapon.WeaponAttributes.MageWeapon = 29;
        Assert.True(weapon.TrySpendWeaponLevelingPoints(owner,
            Assert.IsType<WeaponUpgradeQuote>(weapon.QuoteWeaponUpgrade(mage))));
        Assert.Equal(30, weapon.WeaponAttributes.MageWeapon);
        Assert.Null(weapon.QuoteWeaponUpgrade(mage));
    }

    [Fact]
    public void ResourceBonusesCountTowardCapsWithoutBeingWrittenBackAndInvalidateQuotes()
    {
        var owner = NewPlayer();
        var weapon = NewEnabledWeapon(owner);
        weapon.AwardWeaponExperience(999_900, owner);
        var luck = FindUpgrade(AosAttribute.Luck);
        var requirements = FindUpgrade(AosWeaponAttribute.LowerStatReq);
        var oldQuote = Assert.IsType<WeaponUpgradeQuote>(weapon.QuoteWeaponUpgrade(requirements));

        weapon.Resource = CraftResource.Gold;
        Assert.False(weapon.TrySpendWeaponLevelingPoints(owner, oldQuote));
        Assert.Equal(0, weapon.WeaponAttributes.LowerStatReq);
        Assert.Equal(495, weapon.WeaponLevelingPointsAvailable);

        weapon.WeaponAttributes.LowerStatReq = 49;
        var reqQuote = Assert.IsType<WeaponUpgradeQuote>(weapon.QuoteWeaponUpgrade(requirements));
        Assert.Equal(99, reqQuote.Current);
        Assert.Equal(100, reqQuote.Next);
        Assert.True(weapon.TrySpendWeaponLevelingPoints(owner, reqQuote));
        Assert.Equal(50, weapon.WeaponAttributes.LowerStatReq);
        Assert.Equal(100, weapon.GetLowerStatReq());
        Assert.Null(weapon.QuoteWeaponUpgrade(requirements));

        weapon.Attributes.Luck = 109;
        var luckQuote = Assert.IsType<WeaponUpgradeQuote>(weapon.QuoteWeaponUpgrade(luck));
        Assert.Equal(149, luckQuote.Current);
        Assert.True(weapon.TrySpendWeaponLevelingPoints(owner, luckQuote));
        Assert.Equal(110, weapon.Attributes.Luck);
        Assert.Null(weapon.QuoteWeaponUpgrade(luck));
        Assert.Equal(490, weapon.WeaponLevelingPointsAvailable);

        weapon.Attributes.Luck = 160;
        Assert.Null(weapon.QuoteWeaponUpgrade(luck));
        Assert.Equal(160, weapon.Attributes.Luck);
    }

    [Fact]
    public void EquippingAndSpendingUpdatesLiveStatAndMageryMods()
    {
        var owner = NewPlayer();
        owner.RawStr = 100;
        owner.Skills.Magery.Base = 80;
        var weapon = NewEnabledWeapon(owner);
        weapon.AwardWeaponExperience(999_900, owner);
        weapon.WeaponAttributes.MageWeapon = 29;
        weapon.Attributes.BonusStr = 3;
        Assert.True(owner.EquipItem(weapon));
        Assert.Equal(103, owner.Str);
        var mageryBefore = owner.Skills.Magery.Value;
        var strength = FindUpgrade(AosAttribute.BonusStr);
        Assert.True(weapon.TrySpendWeaponLevelingPoints(owner,
            Assert.IsType<WeaponUpgradeQuote>(weapon.QuoteWeaponUpgrade(strength))));
        Assert.Equal(104, owner.Str);
        var mage = FindUpgrade(AosWeaponAttribute.MageWeapon);
        Assert.True(weapon.TrySpendWeaponLevelingPoints(owner,
            Assert.IsType<WeaponUpgradeQuote>(weapon.QuoteWeaponUpgrade(mage))));
        Assert.Equal(Core.AOS ? mageryBefore + 1 : mageryBefore, owner.Skills.Magery.Value);
    }

    [Fact]
    public void DurabilityUpgradesPreserveWearAndDoNotCompoundRounding()
    {
        var owner = NewPlayer();
        var weapon = NewEnabledWeapon(owner);
        weapon.AwardWeaponExperience(999_900, owner);
        weapon.MaxHitPoints = 50;
        weapon.HitPoints = 42;
        var durability = FindUpgrade(AosWeaponAttribute.DurabilityBonus);
        for (var i = 0; i < 100; i++)
        {
            var quote = Assert.IsType<WeaponUpgradeQuote>(weapon.QuoteWeaponUpgrade(durability));
            Assert.True(weapon.TrySpendWeaponLevelingPoints(owner, quote));
        }

        Assert.Equal(100, weapon.WeaponAttributes.DurabilityBonus);
        Assert.Equal(Core.AOS ? 100 : 50, weapon.MaxHitPoints);
        Assert.Equal(8, weapon.MaxHitPoints - weapon.HitPoints);
    }

    [Fact]
    public void EquippedWeaponLethalHitAwardsOnceButUnwieldedWeaponAndMagicDoNot()
    {
        var owner = NewPlayer();
        var sword = NewEnabledWeapon(owner);
        var other = NewEnabledWeapon(owner);
        Assert.True(owner.EquipItem(sword));
        var victim = NewVictim(owner);
        sword.OnHit(owner, victim);
        Assert.True(victim.Deleted);
        Assert.Equal(80, sword.WeaponExperience);
        Assert.Equal(0, other.WeaponExperience);

        victim = NewVictim(owner);
        other.OnHit(owner, victim);
        Assert.True(victim.Deleted);
        Assert.Equal(80, sword.WeaponExperience);
        Assert.Equal(0, other.WeaponExperience);

        victim = NewVictim(owner);
        AOS.Damage(victim, owner, 20, 0, 100, 0, 0, 0);
        Assert.True(victim.Deleted);
        Assert.Equal(80, sword.WeaponExperience);
        Assert.Equal(0, other.WeaponExperience);
    }

    [Fact]
    public void AChampionCreatureKeepsItsReducedAwardWhenLuredOutsideTheSpawn()
    {
        var owner = NewPlayer();
        var sword = NewEnabledWeapon(owner);
        Assert.True(owner.EquipItem(sword));
        var origin = new Point3D(owner.X + 40, owner.Y, owner.Z);
        var spawn = _world.TrackItem(new ChampionSpawn());
        spawn.MoveToWorld(origin, owner.Map);
        spawn.SpawnArea = new Rectangle2D(origin.X - 5, origin.Y - 5, 10, 10);
        var victim = NewVictim(owner);
        victim.MoveToWorld(origin, owner.Map);
        spawn.AddSpawnCreature(victim);

        Assert.Same(spawn, ChampionSpawn.FindSpawnOf(victim));
        victim.MoveToWorld(new Point3D(owner.X + 1, owner.Y, owner.Z), owner.Map);
        Assert.Null(ChampionSpawn.FindSpawnOf(victim));
        Assert.True(ChampionSpawn.HasSpawnProvenance(victim));

        sword.OnHit(owner, victim);
        Assert.True(victim.Deleted);
        Assert.Equal(10, sword.WeaponExperience);
        spawn.Delete();
        Assert.False(ChampionSpawn.HasSpawnProvenance(victim));
    }

    [Fact]
    public void LethalConcussionBlowCreditsTheWeaponExecutingIt()
    {
        var owner = NewPlayer();
        owner.Skills.Swords.Base = 100;
        owner.Skills.Tactics.Base = 100;
        owner.RawInt = 100;
        owner.Mana = owner.ManaMax;
        var sword = NewEnabledWeapon(owner);
        var unwielded = NewEnabledWeapon(owner);
        Assert.True(owner.EquipItem(sword));

        try
        {
            Assert.True(WeaponAbility.SetCurrentAbility(owner, WeaponAbility.ConcussionBlow));
            var victim = NewVictim(owner);
            unwielded.OnHit(owner, victim);
            Assert.True(victim.Deleted);
            Assert.Equal(0, sword.WeaponExperience);
            Assert.Equal(0, unwielded.WeaponExperience);

            owner.Mana = owner.ManaMax;
            Assert.True(WeaponAbility.SetCurrentAbility(owner, WeaponAbility.ConcussionBlow));
            victim = NewVictim(owner);
            sword.OnHit(owner, victim);
            Assert.True(victim.Deleted);
            Assert.InRange(victim.LastIncomingDamage, 10, 30);
            Assert.Equal(80, sword.WeaponExperience);
        }
        finally
        {
            WeaponAbility.ClearCurrentAbility(owner);
        }
    }

    [Fact]
    public void LethalVelocityArrowCreditsTheBowBeforeTheRegularHit()
    {
        var owner = NewPlayer();
        var weapon = _world.TrackItem(new Bow { Layer = Layer.TwoHanded, Velocity = 100 });
        owner.Backpack.DropItem(weapon);
        Assert.True(weapon.TryEnableWeaponLeveling(owner));
        Assert.True(owner.EquipItem(weapon));
        var victim = NewVictim(owner);
        victim.MoveToWorld(new Point3D(owner.X + 2, owner.Y, owner.Z), owner.Map);

        weapon.OnHit(owner, victim);
        Assert.True(victim.Deleted);
        Assert.Equal(6, victim.LastIncomingDamage);
        Assert.Equal(80, weapon.WeaponExperience);
    }

    [Fact]
    public void ActiveLightningArrowCreditsImmediateKillsWithoutAmmunition()
    {
        var owner = NewPlayer();
        owner.Player = true;
        owner.MoveToWorld(owner.Location, Map.Felucca);
        owner.Skills.Archery.Base = 100;
        owner.Skills.Tactics.Base = 100;
        owner.RawInt = 100;
        owner.Mana = owner.ManaMax;
        var bow = _world.TrackItem(new MagicalShortbow { Layer = Layer.TwoHanded });
        owner.Backpack.DropItem(bow);
        Assert.True(bow.TryEnableWeaponLeveling(owner));
        Assert.True(owner.EquipItem(bow));
        Assert.Null(owner.Backpack.FindItemByType<Arrow>());
        var victim = NewVictim(owner);
        var secondary = NewVictim(owner);
        secondary.WildInnocent = true;
        secondary.MoveToWorld(new Point3D(owner.X + 2, owner.Y, owner.Z), owner.Map);

        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = owner;
        owner.NetState = state;
        try
        {
            Assert.True(owner.CanBeHarmful(victim));
            Assert.True(WeaponAbility.SetCurrentAbility(owner, WeaponAbility.LightningArrow));
            owner.LastMoveTime = Core.TickCount - 1000;
            bow.OnSwing(owner, victim);

            Assert.True(victim.Deleted);
            Assert.InRange(victim.LastIncomingDamage, 29, 40);
            Assert.True(secondary.Deleted);
            Assert.InRange(secondary.LastIncomingDamage, 29, 40);
            Assert.Equal(320, bow.WeaponExperience);
        }
        finally
        {
            WeaponAbility.ClearCurrentAbility(owner);
            owner.NetState = null;
            state.Mobile = null;
        }
    }

    [Fact]
    public void PetKillOnlyCreditsTheOwnersEquippedWeaponFromLevelFifty()
    {
        var owner = NewPlayer();
        var sword = NewEnabledWeapon(owner);
        Assert.True(owner.EquipItem(sword));
        var pet = _world.NewCreature();
        pet.MoveToWorld(owner.Location, owner.Map);
        Assert.True(pet.SetControlMaster(owner));
        sword.AwardWeaponExperience(249_899, owner);

        NewVictim(owner).Damage(20, pet);
        Assert.Equal(249_899, sword.WeaponExperience);

        sword.AwardWeaponExperience(1, owner);
        NewVictim(owner).Damage(20, pet);
        Assert.Equal(249_910, sword.WeaponExperience);

        owner.Backpack.DropItem(sword);
        NewVictim(owner).Damage(20, pet);
        Assert.Equal(249_910, sword.WeaponExperience);
    }

    [Theory]
    [InlineData(false, false, false, 80)]
    [InlineData(true, false, false, 160)]
    [InlineData(true, true, false, 20)]
    [InlineData(false, true, false, 10)]
    [InlineData(false, false, true, 10)]
    [InlineData(true, true, true, 2)]
    public void FameAnchorAndRegionalModifiersCompose(bool felucca, bool champion, bool pet, int expected) =>
        Assert.Equal(expected, WeaponLevelingCombat.KillExperience(15_000, felucca, champion, pet));

    [Fact]
    public void InformationGumpWillNotConfirmAnUpgradeAfterWeaponChangesOwners()
    {
        var owner = NewPlayer();
        var other = NewPlayer();
        var weapon = NewEnabledWeapon(owner);
        weapon.AwardWeaponExperience(300, owner);
        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = owner;
        owner.NetState = state;
        try
        {
            var menu = PooledRefList<ContextMenuEntry>.Create();
            try
            {
                weapon.GetContextMenuEntries(owner, ref menu);
                foreach (var entry in menu)
                {
                    if (entry.Number == 3000098)
                    {
                        entry.OnClick(owner, weapon);
                        break;
                    }
                }
            }
            finally
            {
                menu.Dispose();
            }

            var gump = Assert.IsType<WeaponLevelingGump>(owner.FindGump<WeaponLevelingGump>());
            gump.OnResponse(state, Button(WeaponLevelingGump.FirstCategoryButton));
            gump = Assert.IsType<WeaponLevelingGump>(owner.FindGump<WeaponLevelingGump>());
            var hitChance = FindUpgrade(AosAttribute.AttackChance);
            gump.OnResponse(state, Button(WeaponLevelingGump.FirstUpgradeButton + hitChance));
            gump = Assert.IsType<WeaponLevelingGump>(owner.FindGump<WeaponLevelingGump>());
            Assert.Equal(0, weapon.Attributes.AttackChance);

            other.Backpack.DropItem(weapon);
            gump.OnResponse(state, Button(WeaponLevelingGump.ConfirmButton));
            Assert.Equal(0, weapon.Attributes.AttackChance);
            Assert.Equal(5, weapon.WeaponLevelingPointsAvailable);

            owner.Backpack.DropItem(weapon);
            gump.OnResponse(state, Button(WeaponLevelingGump.ConfirmButton));
            Assert.Equal(1, weapon.Attributes.AttackChance);
            Assert.Equal(2, weapon.WeaponLevelingPointsAvailable);
        }
        finally
        {
            owner.CloseGump<WeaponLevelingGump>();
            owner.NetState = null;
            state.Mobile = null;
        }
    }

    [Fact]
    public void VersionElevenWeaponRetainsItsOriginalDamageAndDurability()
    {
        var item = _world.TrackItem(new Item(0xF61));
        var weapon = _world.TrackItem(new MigrationWeapon(World.NewItem));
        var writer = new BufferWriter(true);
        item.Serialize(writer);
        writer.Write(11);
        writer.WriteEncodedInt(1 << 4 | 1 << 5 | 1 << 14 | 1 << 15);
        writer.Write(25);
        writer.Write(42);
        writer.Write(10);
        writer.Write(20);

        var reader = SnapshotReader(writer);
        weapon.Deserialize(reader);
        Assert.Equal(writer.Position, reader.Position);
        Assert.Equal(25, weapon.HitPoints);
        Assert.Equal(42, weapon.MaxHitPoints);
        Assert.Equal(10, weapon.MinDamage);
        Assert.Equal(20, weapon.MaxDamage);
        Assert.False(weapon.IsWeaponLevelingEnabled);
    }

    private sealed class MigrationWeapon(Serial serial) : BaseWeapon(serial);

    private sealed class RecordingVictim : TestCreature
    {
        internal int LastIncomingDamage { get; private set; }
        internal bool WildInnocent { get; set; }

        public override bool InitialInnocent => WildInnocent;

        public override void Damage(int amount, Mobile from = null, bool informMount = true, bool ignoreEvilOmen = false)
        {
            if (!Deleted)
            {
                LastIncomingDamage = amount;
            }

            base.Damage(amount, from, informMount, ignoreEvilOmen);
        }
    }

    private PlayerMobile NewPlayer()
    {
        var player = _world.Track(new PlayerMobile());
        player.RawStr = 100;
        player.RawDex = 100;
        player.Hits = player.HitsMax;
        player.AddItem(_world.TrackItem(new Backpack()));
        player.MoveToWorld(new Point3D(1100, 1100, 0), Map.Trammel);
        return player;
    }

    private Longsword NewEnabledWeapon(PlayerMobile owner)
    {
        var weapon = _world.TrackItem(new Longsword());
        weapon.Layer = Layer.OneHanded;
        weapon.MinDamage = 30;
        weapon.MaxDamage = 30;
        owner.Backpack.DropItem(weapon);
        Assert.True(weapon.TryEnableWeaponLeveling(owner));
        return weapon;
    }

    private RecordingVictim NewVictim(PlayerMobile owner)
    {
        var victim = _world.Track(new RecordingVictim());
        victim.Fame = 15_000;
        victim.Hits = 1;
        victim.MoveToWorld(new Point3D(owner.X + 1, owner.Y, owner.Z), owner.Map);
        return victim;
    }

    private static int FindUpgrade(AosAttribute general) => FindUpgrade(general, 0);

    private static int FindUpgrade(AosWeaponAttribute weapon) => FindUpgrade(0, weapon);

    private static int FindUpgrade(AosAttribute general, AosWeaponAttribute weapon)
    {
        var upgrades = WeaponUpgradeCatalog.Upgrades;
        for (var i = 0; i < upgrades.Length; i++)
        {
            if (upgrades[i].General == general && upgrades[i].Weapon == weapon)
            {
                return i;
            }
        }

        throw new InvalidOperationException("Weapon upgrade not found.");
    }

    private static BufferReader SnapshotReader(BufferWriter writer) =>
        new(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());

    private static RelayInfo Button(int id) => new(id, [], [], [], []);
}
