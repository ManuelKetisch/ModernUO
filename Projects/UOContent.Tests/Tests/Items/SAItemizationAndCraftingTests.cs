using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Server;
using Server.Engines.BuffIcons;
using Server.Engines.Craft;
using Server.Engines.Imbuing;
using Server.Engines.WeaponLeveling;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.SkillHandlers;
using Server.Spells;
using Server.Spells.Bushido;
using Server.Spells.Chivalry;
using Server.Spells.Fourth;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Items;

[Collection("Sequential UOContent Tests")]
public sealed class SAItemizationAndCraftingTests : IDisposable
{
    private readonly MobileTestHarness _world = new();
    private readonly Expansion _previousExpansion = Core.Expansion;
    private readonly bool _previousSoloEnabled = ServerConfiguration.GetSetting("progression.soloEnabled", false);
    private readonly int _previousAttackChanceCap = ServerConfiguration.GetSetting(
        "progression.soloAttackChanceCap",
        SoloProgression.DefaultAttackChanceCap
    );
    private readonly bool _previousOplEnabled = ObjectPropertyList.Enabled;
    private readonly IWeapon _previousDefaultWeapon = Mobile.DefaultWeapon;
    private readonly RegenRateHandler _previousHitsRegen = Mobile.HitsRegenRateHandler;
    private readonly RegenRateHandler _previousStamRegen = Mobile.StamRegenRateHandler;
    private readonly RegenRateHandler _previousManaRegen = Mobile.ManaRegenRateHandler;

    public SAItemizationAndCraftingTests()
    {
        Core.Expansion = Expansion.EJ;
        ObjectPropertyList.Enabled = true;
        Mobile.HitsRegenRateHandler = _ => TimeSpan.FromHours(1);
        Mobile.StamRegenRateHandler = _ => TimeSpan.FromHours(1);
        Mobile.ManaRegenRateHandler = _ => TimeSpan.FromHours(1);
        if (Mobile.DefaultWeapon == null)
        {
            Mobile.DefaultWeapon = _world.TrackItem(new Fists());
        }

        ImbuingPersistence.ResetForTesting();
        SetSoloMode(true);
    }

    public void Dispose()
    {
        ImbuingPersistence.ResetForTesting();
        Core.Expansion = _previousExpansion;
        ObjectPropertyList.Enabled = _previousOplEnabled;
        Mobile.HitsRegenRateHandler = _previousHitsRegen;
        Mobile.StamRegenRateHandler = _previousStamRegen;
        Mobile.ManaRegenRateHandler = _previousManaRegen;
        Mobile.DefaultWeapon = _previousDefaultWeapon;
        ServerConfiguration.SetSetting("progression.soloAttackChanceCap", _previousAttackChanceCap);
        SetSoloMode(_previousSoloEnabled);
        TreasuresOfTokuno.Configure();
        _world.Dispose();
    }

    private static void SetSoloMode(bool enabled)
    {
        ServerConfiguration.SetSetting("progression.soloEnabled", enabled);
        SoloProgression.Configure();
    }

    private static (int Cliloc, string Arg)[] DecodeOpl(Item item)
    {
        using var opl = new ObjectPropertyList(item);
        item.GetProperties(opl);
        opl.Terminate();

        var buffer = opl.Buffer;
        var entries = new List<(int, string)>();
        var pos = 15; // 0xD6 packet header is 15 bytes.

        while (true)
        {
            var cliloc = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(pos));
            pos += 4;
            if (cliloc == 0)
            {
                break;
            }

            var byteLen = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(pos));
            pos += 2;
            var arg = Encoding.Unicode.GetString(buffer, pos, byteLen);
            pos += byteLen;
            entries.Add((cliloc, arg));
        }

        return entries.ToArray();
    }

    private static void AssertHasCliloc((int Cliloc, string Arg)[] entries, int expectedCliloc, string expectedArg = null)
    {
        var found = entries.Any(e => e.Cliloc == expectedCliloc && (expectedArg == null || e.Arg == expectedArg));
        Assert.True(
            found,
            $"Expected cliloc {expectedCliloc}{(expectedArg != null ? $" with arg '{expectedArg}'" : "")} in OPL."
        );
    }

    private static BufferReader SnapshotReader(BufferWriter writer) =>
        new(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());

    private PlayerMobile CreatePlayer(int x = 1400, int y = 1400, Map map = null)
    {
        var player = _world.NewPlayer();
        player.Player = true;
        player.Race = Race.Human;
        player.RawStr = 125;
        player.RawDex = 125;
        player.RawInt = 125;
        player.Hits = player.HitsMax;
        player.Stam = player.StamMax;
        player.Mana = player.ManaMax;
        player.AddItem(_world.TrackItem(new Backpack()));
        player.MoveToWorld(new Point3D(x, y, 0), map ?? Map.Felucca);
        return player;
    }

    private static void EnsureCraftSystemsInitialized()
    {
        if (DefBlacksmithy.CraftSystem == null)
        {
            DefBlacksmithy.Initialize();
        }

        if (DefTailoring.CraftSystem == null)
        {
            DefTailoring.Initialize();
        }

        if (DefBowFletching.CraftSystem == null)
        {
            DefBowFletching.Initialize();
        }

        if (DefTinkering.CraftSystem == null)
        {
            DefTinkering.Initialize();
        }

        if (DefInscription.CraftSystem == null)
        {
            DefInscription.Initialize();
        }

        if (DefCarpentry.CraftSystem == null)
        {
            DefCarpentry.Initialize();
        }
    }

    // =========================================================================
    // 1. Phase 2: SA / HS / ToL Attributes — Serialization & OPL Clilocs
    // =========================================================================

    [Fact]
    public void Phase2_AllNewAosAttributes_SerializeRoundTripAndEmitExpectedClilocs()
    {
        var armor = _world.TrackItem(new PlateChest());
        armor.Attributes.CastingFocus = 6;
        armor.Attributes.DamageEater = 5;
        armor.Attributes.KineticEater = 11;
        armor.Attributes.FireEater = 12;
        armor.Attributes.ColdEater = 13;
        armor.Attributes.PoisonEater = 14;
        armor.Attributes.EnergyEater = 15;

        armor.ArmorAttributes.ReactiveParalyze = 1;
        armor.ArmorAttributes.SoulCharge = 25;
        armor.ArmorAttributes.ManaPhase = 1;
        armor.ArmorAttributes.KineticResonance = 10;
        armor.ArmorAttributes.FireResonance = 12;
        armor.ArmorAttributes.ColdResonance = 14;
        armor.ArmorAttributes.PoisonResonance = 16;
        armor.ArmorAttributes.EnergyResonance = 20;

        var armorEntries = DecodeOpl(armor);
        AssertHasCliloc(armorEntries, 1113696, "6");  // Casting Focus
        AssertHasCliloc(armorEntries, 1113598, "5");  // Damage Eater
        AssertHasCliloc(armorEntries, 1113597, "11"); // Kinetic Eater
        AssertHasCliloc(armorEntries, 1113593, "12"); // Fire Eater
        AssertHasCliloc(armorEntries, 1113594, "13"); // Cold Eater
        AssertHasCliloc(armorEntries, 1113595, "14"); // Poison Eater
        AssertHasCliloc(armorEntries, 1113596, "15"); // Energy Eater

        AssertHasCliloc(armorEntries, 1112364);       // reactive paralyze
        AssertHasCliloc(armorEntries, 1113630, "25"); // Soul Charge
        AssertHasCliloc(armorEntries, 1116158);       // Mana Phase
        AssertHasCliloc(armorEntries, 1113695, "10"); // Kinetic Resonance
        AssertHasCliloc(armorEntries, 1113691, "12"); // Fire Resonance
        AssertHasCliloc(armorEntries, 1113692, "14"); // Cold Resonance
        AssertHasCliloc(armorEntries, 1113693, "16"); // Poison Resonance
        AssertHasCliloc(armorEntries, 1113694, "20"); // Energy Resonance

        var armorWriter = new BufferWriter(true);
        armor.Serialize(armorWriter);
        var armorCopy = _world.TrackItem(new PlateChest(World.NewItem));
        var armorReader = SnapshotReader(armorWriter);
        armorCopy.Deserialize(armorReader);

        Assert.Equal(armorWriter.Position, armorReader.Position);
        Assert.Equal(6, armorCopy.Attributes.CastingFocus);
        Assert.Equal(5, armorCopy.Attributes.DamageEater);
        Assert.Equal(11, armorCopy.Attributes.KineticEater);
        Assert.Equal(12, armorCopy.Attributes.FireEater);
        Assert.Equal(13, armorCopy.Attributes.ColdEater);
        Assert.Equal(14, armorCopy.Attributes.PoisonEater);
        Assert.Equal(15, armorCopy.Attributes.EnergyEater);
        Assert.Equal(1, armorCopy.ArmorAttributes.ReactiveParalyze);
        Assert.Equal(25, armorCopy.ArmorAttributes.SoulCharge);
        Assert.Equal(1, armorCopy.ArmorAttributes.ManaPhase);
        Assert.Equal(10, armorCopy.ArmorAttributes.KineticResonance);
        Assert.Equal(12, armorCopy.ArmorAttributes.FireResonance);
        Assert.Equal(14, armorCopy.ArmorAttributes.ColdResonance);
        Assert.Equal(16, armorCopy.ArmorAttributes.PoisonResonance);
        Assert.Equal(20, armorCopy.ArmorAttributes.EnergyResonance);

        // Weapon attributes including bit 31 (Bane = 0x80000000) alongside lower bits.
        var weapon = _world.TrackItem(new Katana());
        weapon.WeaponAttributes.HitLeechHits = 30;
        weapon.WeaponAttributes.BattleLust = 1;
        weapon.WeaponAttributes.SplinteringWeapon = 25;
        weapon.WeaponAttributes.HitCurse = 35;
        weapon.WeaponAttributes.HitFatigue = 40;
        weapon.WeaponAttributes.HitManaDrain = 45;
        weapon.WeaponAttributes.BloodDrinker = 1;
        weapon.WeaponAttributes.Bane = 1;

        var weaponEntries = DecodeOpl(weapon);
        AssertHasCliloc(weaponEntries, 1113710);       // Battle Lust
        AssertHasCliloc(weaponEntries, 1112857, "25"); // splintering weapon
        AssertHasCliloc(weaponEntries, 1113712, "35"); // Hit Curse
        AssertHasCliloc(weaponEntries, 1113700, "40"); // Hit Fatigue
        AssertHasCliloc(weaponEntries, 1113699, "45"); // Hit Mana Drain
        AssertHasCliloc(weaponEntries, 1113591);       // Blood Drinker
        AssertHasCliloc(weaponEntries, 1154671);       // Bane

        var weaponWriter = new BufferWriter(true);
        weapon.Serialize(weaponWriter);
        var weaponCopy = _world.TrackItem(new Katana(World.NewItem));
        var weaponReader = SnapshotReader(weaponWriter);
        weaponCopy.Deserialize(weaponReader);

        Assert.Equal(weaponWriter.Position, weaponReader.Position);
        Assert.Equal(30, weaponCopy.WeaponAttributes.HitLeechHits);
        Assert.Equal(1, weaponCopy.WeaponAttributes.BattleLust);
        Assert.Equal(25, weaponCopy.WeaponAttributes.SplinteringWeapon);
        Assert.Equal(35, weaponCopy.WeaponAttributes.HitCurse);
        Assert.Equal(40, weaponCopy.WeaponAttributes.HitFatigue);
        Assert.Equal(45, weaponCopy.WeaponAttributes.HitManaDrain);
        Assert.Equal(1, weaponCopy.WeaponAttributes.BloodDrinker);
        Assert.Equal(1, weaponCopy.WeaponAttributes.Bane);

        // Verify clearing bit 31 leaves other attributes intact.
        weaponCopy.WeaponAttributes.Bane = 0;
        Assert.Equal(0, weaponCopy.WeaponAttributes.Bane);
        Assert.Equal(1, weaponCopy.WeaponAttributes.BloodDrinker);
        Assert.Equal(25, weaponCopy.WeaponAttributes.SplinteringWeapon);
    }

    // =========================================================================
    // 2. Phase 2: SAItemCombatMechanics Behavior
    // =========================================================================

    [Fact]
    public void Phase2_EatersAndSoulCharge_RespectCapsAndRestoreHealthAndManaOverTime()
    {
        using var scope = new TimerWheelScope(startTick: 100_000);
        var defender = CreatePlayer(1410, 1410);
        var attacker = _world.NewCreature();
        attacker.MoveToWorld(new Point3D(1411, 1410, 0), Map.Felucca);

        var chest = _world.TrackItem(new PlateChest { Layer = Layer.InnerTorso });
        chest.Attributes.DamageEater = 25; // Exceeds 18% DamageEater cap
        chest.Attributes.FireEater = 25;   // Combined 18 + 25 = 43 -> capped to 30%
        chest.ArmorAttributes.SoulCharge = 40; // Exceeds 30% SoulCharge cap
        defender.Backpack.DropItem(chest);
        Assert.True(defender.EquipItem(chest));

        // Pure physical: uses DamageEater cap (18%) -> 18 heal on 100 damage.
        Assert.Equal(18, SAItemCombatMechanics.ComputeEaterHeal(defender, 100, 100, 0, 0, 0, 0));
        // Pure fire: min(30, 18 + 25) = 30% -> 30 heal on 100 damage.
        Assert.Equal(30, SAItemCombatMechanics.ComputeEaterHeal(defender, 100, 0, 100, 0, 0, 0));
        // 50% phys / 50% fire: weighted avg (18 + 30) / 2 = 24% -> 24 heal on 100 damage.
        Assert.Equal(24, SAItemCombatMechanics.ComputeEaterHeal(defender, 100, 50, 50, 0, 0, 0));
        // Soul Charge capped at 30% -> 30 mana on 100 damage.
        Assert.Equal(30, SAItemCombatMechanics.ComputeSoulChargeMana(defender, 100));

        defender.Hits = 50;
        defender.Mana = 20;
        SAItemCombatMechanics.OnDamageApplied(defender, attacker, 100, 0, 100, 0, 0, 0);

        TimerWheelScope.RunFor(4000);

        Assert.Equal(80, defender.Hits);
        Assert.Equal(50, defender.Mana);

        // Second damage event must also heal/restore (verifies timer resets cleanly after draining).
        SAItemCombatMechanics.OnDamageApplied(defender, attacker, 100, 0, 100, 0, 0, 0);
        TimerWheelScope.RunFor(4000);

        Assert.Equal(110, defender.Hits);
        Assert.Equal(80, defender.Mana);
    }

    [Fact]
    public void Phase2_CastingFocusAndResonance_ComputeCappedDisturbResist()
    {
        using var scope = new TimerWheelScope(startTick: 200_000);
        var caster = CreatePlayer(1420, 1420);

        var helm = _world.TrackItem(new PlateHelm { Layer = Layer.Helm });
        helm.Attributes.CastingFocus = 20; // Capped to 12 -> 24% disturb resist
        helm.ArmorAttributes.FireResonance = 30; // Capped to 20 -> +20% matching fire
        caster.Backpack.DropItem(helm);
        Assert.True(caster.EquipItem(helm));

        // Without incoming damage element recorded: 12 * 2 = 24%.
        Assert.Equal(24, SAItemCombatMechanics.GetSpellDisturbResistChance(caster));

        // Matching 100% fire damage: 24 + 20 = 44%.
        SAItemCombatMechanics.RecordIncomingElement(caster, 0, 100, 0, 0, 0);
        Assert.Equal(44, SAItemCombatMechanics.GetSpellDisturbResistChance(caster));

        // Non-matching 100% cold damage: 24 + 0 = 24%.
        SAItemCombatMechanics.RecordIncomingElement(caster, 0, 0, 100, 0, 0);
        Assert.Equal(24, SAItemCombatMechanics.GetSpellDisturbResistChance(caster));

        // Element memory expires after 1.5s.
        SAItemCombatMechanics.RecordIncomingElement(caster, 0, 100, 0, 0, 0);
        TimerWheelScope.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(24, SAItemCombatMechanics.GetSpellDisturbResistChance(caster));
    }

    [Fact]
    public void Phase2_ManaPhaseAndReactiveParalyze_TriggerAndEnforceCooldowns()
    {
        using var scope = new TimerWheelScope(startTick: 300_000);
        var defender = CreatePlayer(1430, 1430);
        var attacker = _world.NewCreature();
        attacker.MoveToWorld(new Point3D(1431, 1430, 0), Map.Felucca);

        var chest = _world.TrackItem(new LeatherChest { Layer = Layer.InnerTorso });
        chest.ArmorAttributes.ManaPhase = 1;
        defender.Backpack.DropItem(chest);
        Assert.True(defender.EquipItem(chest));

        var shield = _world.TrackItem(new HeaterShield { Layer = Layer.TwoHanded });
        shield.ArmorAttributes.ReactiveParalyze = 1;
        defender.Backpack.DropItem(shield);
        Assert.True(defender.EquipItem(shield));

        // Mana Phase negates mana to 0 and enters 20s cooldown.
        var manaCost = 40;
        Assert.True(SAItemCombatMechanics.TryTriggerManaPhase(defender, ref manaCost, forceRoll: true));
        Assert.Equal(0, manaCost);

        var secondCost = 40;
        Assert.False(SAItemCombatMechanics.TryTriggerManaPhase(defender, ref secondCost, forceRoll: true));
        Assert.Equal(40, secondCost);

        TimerWheelScope.Advance(TimeSpan.FromSeconds(21));
        Assert.True(SAItemCombatMechanics.TryTriggerManaPhase(defender, ref secondCost, forceRoll: true));
        Assert.Equal(0, secondCost);

        // Reactive Paralyze paralyzes melee attacker and enters 12s cooldown.
        Assert.False(attacker.Paralyzed);
        Assert.True(SAItemCombatMechanics.TryReactiveParalyze(defender, attacker, forceRoll: true));
        Assert.True(attacker.Paralyzed);

        attacker.Paralyzed = false;
        Assert.False(SAItemCombatMechanics.TryReactiveParalyze(defender, attacker, forceRoll: true));

        TimerWheelScope.Advance(TimeSpan.FromSeconds(13));
        Assert.True(SAItemCombatMechanics.TryReactiveParalyze(defender, attacker, forceRoll: true));
        Assert.True(attacker.Paralyzed);
    }

    [Fact]
    public void Phase2_BattleLustBloodDrinkerBaneSplinteringAndOnHitDebuffs_WorkAsSpecified()
    {
        using var scope = new TimerWheelScope(startTick: 400_000);
        var attacker = CreatePlayer(1440, 1440);
        var monster = _world.NewCreature();
        monster.RawStr = 100;
        monster.RawDex = 100;
        monster.RawInt = 100;
        monster.HitsMaxSeed = 500;
        monster.Hits = 500;
        monster.StamMaxSeed = 100;
        monster.Stam = 100;
        monster.ManaMaxSeed = 100;
        monster.Mana = 100;
        monster.Skills.MagicResist.Base = 0.0;
        monster.MoveToWorld(new Point3D(1441, 1440, 0), Map.Felucca);

        var rivalPlayer = CreatePlayer(1442, 1440);

        var weapon = _world.TrackItem(new Katana { Layer = Layer.OneHanded });
        weapon.WeaponAttributes.BattleLust = 1;
        weapon.WeaponAttributes.BloodDrinker = 1;
        weapon.WeaponAttributes.Bane = 1;
        weapon.WeaponAttributes.HitFatigue = 50;
        weapon.WeaponAttributes.HitManaDrain = 50;
        weapon.WeaponAttributes.HitCurse = 50;
        attacker.Backpack.DropItem(weapon);
        Assert.True(attacker.EquipItem(weapon));

        // Battle Lust ramps +15 per hit up to 45 PvM / 15 PvP and decays after 8s.
        SAItemCombatMechanics.BuildBattleLust(attacker, monster);
        Assert.Equal(15, SAItemCombatMechanics.GetBattleLustDamageBonus(attacker, monster));
        SAItemCombatMechanics.BuildBattleLust(attacker, monster);
        Assert.Equal(30, SAItemCombatMechanics.GetBattleLustDamageBonus(attacker, monster));
        SAItemCombatMechanics.BuildBattleLust(attacker, monster);
        SAItemCombatMechanics.BuildBattleLust(attacker, monster);
        Assert.Equal(SAItemCombatMechanics.MaxBattleLustPvM, SAItemCombatMechanics.GetBattleLustDamageBonus(attacker, monster));
        Assert.Equal(SAItemCombatMechanics.MaxBattleLustPvP, SAItemCombatMechanics.GetBattleLustDamageBonus(attacker, rivalPlayer));

        TimerWheelScope.Advance(TimeSpan.FromSeconds(9));
        Assert.Equal(0, SAItemCombatMechanics.GetBattleLustDamageBonus(attacker, monster));

        // Blood Drinker heals attacker 100% of bleed tick damage.
        attacker.Hits = 60;
        SAItemCombatMechanics.OnBleedTick(monster, attacker, 18);
        Assert.Equal(78, attacker.Hits);

        // Bane does not execute at >= 50% HP, executes below 50% HP, and respects 3s cooldown.
        monster.Hits = 300; // 60% HP
        Assert.Equal(0, SAItemCombatMechanics.TryTriggerBane(attacker, monster, 50, forceRoll: true));

        monster.Hits = 200; // 40% HP
        var hitsBeforeBane = monster.Hits;
        var baneDmg = SAItemCombatMechanics.TryTriggerBane(attacker, monster, 50, forceRoll: true);
        Assert.True(baneDmg >= 12);
        Assert.Equal(hitsBeforeBane - baneDmg, monster.Hits);
        Assert.Equal(0, SAItemCombatMechanics.TryTriggerBane(attacker, monster, 50, forceRoll: true));

        TimerWheelScope.Advance(TimeSpan.FromSeconds(4));
        Assert.True(SAItemCombatMechanics.TryTriggerBane(attacker, monster, 50, forceRoll: true) >= 12);

        // Splintering Weapon inflicts bleed + bonus damage and enforces 6s cooldown.
        var hitsBeforeSplinter = monster.Hits;
        Assert.True(SAItemCombatMechanics.TriggerSplinteringWeapon(attacker, monster, 40));
        Assert.True(BleedAttack.IsBleeding(monster));
        Assert.True(monster.Hits < hitsBeforeSplinter);
        Assert.False(SAItemCombatMechanics.TriggerSplinteringWeapon(attacker, monster, 40));

        // Hit Fatigue, Hit Mana Drain, and Hit Curse fire via OnWeaponHitEffects (propertyBonus = 2.0 -> 100% roll).
        // Verify Hit Curse works for a melee warrior with 0 EvalInt / 0 Magery (not dependent on caster skills).
        monster.Stam = 100;
        monster.Mana = 100;
        attacker.Skills.EvalInt.Base = 0.0;
        attacker.Skills.Magery.Base = 0.0;
        SAItemCombatMechanics.OnWeaponHitEffects(weapon, attacker, monster, 40, propertyBonus: 2.0);
        Assert.True(monster.Stam < 100, "Hit Fatigue should drain defender stamina.");
        Assert.True(monster.Mana < 100, "Hit Mana Drain should drain defender mana.");
        Assert.True(CurseSpell.UnderEffect(monster), "Hit Curse should apply Curse to defender.");
    }

    // =========================================================================
    // 3. Phase 1: Publish 81 ItemRarity, Scale Curve, Pools, Paragons & Chests
    // =========================================================================

    [Fact]
    public void Phase1_ItemRarity_MapsWeightTiersAndStaysSilentOnPlainAndFixedArtifacts()
    {
        var plain = _world.TrackItem(new Longsword());
        Assert.Equal(0, ItemRarity.ComputeItemWeight(plain));
        Assert.Equal(MagicItemRarity.None, ItemRarity.GetRarity(plain));
        var plainOpl = DecodeOpl(plain);
        Assert.DoesNotContain(plainOpl, e => e.Cliloc is >= 1151488 and <= 1151495);

        // Minor (<150) -> 1151488
        var minor = _world.TrackItem(new Longsword());
        minor.Attributes.WeaponDamage = 25; // weight ~50
        Assert.Equal(MagicItemRarity.MinorMagicItem, ItemRarity.GetRarity(minor));
        AssertHasCliloc(DecodeOpl(minor), 1151488);

        // Major (350..449) -> 1151491
        var major = _world.TrackItem(new Longsword());
        major.Attributes.WeaponDamage = 50;   // 100
        major.Attributes.WeaponSpeed = 30;    // 110
        major.Attributes.AttackChance = 15;   // 130
        major.WeaponAttributes.HitLeechHits = 25; // 55 -> total 395
        Assert.Equal(MagicItemRarity.MajorMagicItem, ItemRarity.GetRarity(major));
        AssertHasCliloc(DecodeOpl(major), 1151491);

        // Legendary (>=750) -> 1151495
        var legendary = _world.TrackItem(new Bow { Balanced = true, Velocity = 50 });
        legendary.Attributes.WeaponDamage = 60;
        legendary.Attributes.WeaponSpeed = 40;
        legendary.Attributes.AttackChance = 20;
        legendary.WeaponAttributes.SplinteringWeapon = 30;
        legendary.WeaponAttributes.Bane = 1;
        Assert.True(ItemRarity.ComputeItemWeight(legendary) >= 750);
        Assert.Equal(MagicItemRarity.LegendaryArtifact, ItemRarity.GetRarity(legendary));
        AssertHasCliloc(DecodeOpl(legendary), 1151495);

        // Fixed artifact (ArtifactRarity > 0) stays silent for dynamic Publish 81 clilocs.
        var artifact = _world.TrackItem(new BoneCrusher());
        Assert.True(artifact.ArtifactRarity > 0);
        var artifactOpl = DecodeOpl(artifact);
        AssertHasCliloc(artifactOpl, 1061078); // artifact rarity ~1_val~
        Assert.DoesNotContain(artifactOpl, e => e.Cliloc is >= 1151488 and <= 1151495);
    }

    [Fact]
    public void Phase1_BaseRunicToolScale_SpansRangeAboveFloorAndIncreasesWithLuck()
    {
        const int samples = 500;
        double sumZeroLuck = 0;
        double sumHighLuck = 0;

        for (var i = 0; i < samples; i++)
        {
            var zeroRoll = BaseRunicTool.Scale(25, 100, 1, 50, isRunicTool: false, luckChance: 0);
            var luckRoll = BaseRunicTool.Scale(25, 100, 1, 50, isRunicTool: false, luckChance: 7000);
            Assert.InRange(zeroRoll, 1, 50);
            Assert.InRange(luckRoll, 1, 50);
            sumZeroLuck += zeroRoll;
            sumHighLuck += luckRoll;
        }

        var avgZeroLuck = sumZeroLuck / samples;
        var avgHighLuck = sumHighLuck / samples;

        // Legacy 100 - sqrt(U) averaged ~20% (~10) on [25, 100]; new curve averages ~28+ and rises with Luck.
        Assert.True(avgZeroLuck > 22.0, $"Expected average intensity > 22.0 at 0 luck, got {avgZeroLuck:F2}.");
        Assert.True(
            avgHighLuck > avgZeroLuck + 4.0,
            $"Expected high-luck average ({avgHighLuck:F2}) to clearly exceed 0-luck average ({avgZeroLuck:F2})."
        );
    }

    [Fact]
    public void Phase1_LootPoolsParagonsChestsDoomAndToT_AreUpgradedForSolo()
    {
        // Earrings in Loot.JewelryTypes
        Assert.Contains(typeof(GoldEarrings), Loot.JewelryTypes);
        Assert.Contains(typeof(SilverEarrings), Loot.JewelryTypes);

        // Rich+ LootPack tables include Spellbook and RandomTalisman
        Assert.Contains(LootPack.AosMagicItemsRichType1, i => i.Type == typeof(Spellbook));
        Assert.Contains(LootPack.AosMagicItemsRichType1, i => i.Type == typeof(RandomTalisman));
        Assert.Contains(LootPack.AosMagicItemsFilthyRichType1, i => i.Type == typeof(Spellbook));
        Assert.Contains(LootPack.AosMagicItemsFilthyRichType1, i => i.Type == typeof(RandomTalisman));

        // Actual LootPack.FilthyRich generation produces magic Spellbooks and Talismans.
        var killer = CreatePlayer(1450, 1450);
        var dropBag = _world.TrackItem(new Backpack());
        var foundSpellbook = false;
        var foundTalisman = false;
        for (var i = 0; i < 120 && !(foundSpellbook && foundTalisman); i++)
        {
            LootPack.FilthyRich.Generate(killer, dropBag, spawning: false, luckChance: 2000);
            foreach (var item in dropBag.Items.ToArray())
            {
                if (item is Spellbook sb && sb.LootType == LootType.Regular && ItemRarity.ComputeItemWeight(sb) > 0)
                {
                    foundSpellbook = true;
                }
                else if (item is BaseTalisman)
                {
                    foundTalisman = true;
                }

                item.Delete();
            }
        }

        Assert.True(foundSpellbook, "Expected LootPack.FilthyRich to drop a non-blessed magic Spellbook.");
        Assert.True(foundTalisman, "Expected LootPack.FilthyRich to drop a RandomTalisman.");

        // Balanced and Velocity roll on ranged drops.
        var sawBalanced = false;
        var sawVelocity = false;
        for (var i = 0; i < 80 && !(sawBalanced && sawVelocity); i++)
        {
            var bow = _world.TrackItem(new Bow());
            BaseRunicTool.ApplyAttributesTo(bow, false, 1000, 5, 60, 100);
            sawBalanced |= bow.Balanced;
            sawVelocity |= bow.Velocity > 0;
        }

        Assert.True(sawBalanced, "Expected ranged loot drops to roll Balanced.");
        Assert.True(sawVelocity, "Expected ranged loot drops to roll Velocity.");

        // Paragon.Maps includes all 6 hunting facets, and solo artifact chance increases.
        Assert.Contains(Map.Ilshenar, Paragon.Maps);
        Assert.Contains(Map.Trammel, Paragon.Maps);
        Assert.Contains(Map.Felucca, Paragon.Maps);
        Assert.Contains(Map.Malas, Paragon.Maps);
        Assert.Contains(Map.Tokuno, Paragon.Maps);
        Assert.Contains(Map.TerMur, Paragon.Maps);

        var mob = _world.NewCreature();
        mob.Fame = 15000;
        SetSoloMode(false);
        var stockParagonChance = Paragon.GetArtifactChance(killer, mob);
        var stockDoomChance = DemonKnight.GetArtifactChance(mob);

        SetSoloMode(true);
        var soloParagonChance = Paragon.GetArtifactChance(killer, mob);
        var soloDoomChance = DemonKnight.GetArtifactChance(mob);

        Assert.True(soloParagonChance > stockParagonChance * 2.5);
        Assert.True(soloDoomChance > stockDoomChance);

        // ParagonChest and TreasureMapChest scale with chest level.
        double lvl1ParagonAvg = AverageChestMagicWeight(level => _world.TrackItem(new ParagonChest("orc", level)), 1, 25);
        double lvl5ParagonAvg = AverageChestMagicWeight(level => _world.TrackItem(new ParagonChest("balron", level)), 5, 25);
        Assert.True(
            lvl5ParagonAvg > lvl1ParagonAvg,
            $"Expected Level 5 ParagonChest avg weight ({lvl5ParagonAvg:F1}) > Level 1 ({lvl1ParagonAvg:F1})."
        );

        double lvl1TmapAvg = AverageChestMagicWeight(level => _world.TrackItem(new TreasureMapChest(killer, level)), 1, 25);
        double lvl5TmapAvg = AverageChestMagicWeight(level => _world.TrackItem(new TreasureMapChest(killer, level)), 5, 25);
        Assert.True(
            lvl5TmapAvg > lvl1TmapAvg,
            $"Expected Level 5 TreasureMapChest avg weight ({lvl5TmapAvg:F1}) > Level 1 ({lvl1TmapAvg:F1})."
        );

        // TreasuresOfTokuno defaults to ToTThree when solo enabled (unless tot.json overrides).
        var totPath = Path.Combine(Core.BaseDirectory, "Data/tot.json");
        if (!File.Exists(totPath))
        {
            SetSoloMode(true);
            TreasuresOfTokuno.Configure();
            Assert.Equal(TreasuresOfTokunoEra.ToTThree, TreasuresOfTokuno.DropEra);
            Assert.Equal(TreasuresOfTokunoEra.ToTThree, TreasuresOfTokuno.RewardEra);

            SetSoloMode(false);
            TreasuresOfTokuno.Configure();
            Assert.Equal(TreasuresOfTokunoEra.None, TreasuresOfTokuno.DropEra);
            SetSoloMode(true);
            TreasuresOfTokuno.Configure();
        }
    }

    private static double AverageChestMagicWeight(Func<int, Container> makeChest, int level, int trials)
    {
        double totalWeight = 0;
        var count = 0;

        for (var i = 0; i < trials; i++)
        {
            var chest = makeChest(level);
            foreach (var item in chest.Items)
            {
                var w = ItemRarity.ComputeItemWeight(item);
                if (w > 0)
                {
                    totalWeight += w;
                    count++;
                }
            }
        }

        Assert.True(count > 0, $"Expected magic items in level {level} chest.");
        return totalWeight / count;
    }

    // =========================================================================
    // 4. Crafting Superiority Invariants & Bug-Fix Verifications
    // =========================================================================

    [Fact]
    public void Crafting_RunicWeaponDIAndRunicArmorResistsAndMLShields_AreFixed()
    {
        EnsureCraftSystemsInitialized();
        Core.Expansion = Expansion.ML;

        var crafter = CreatePlayer(1460, 1460);
        crafter.Skills.ArmsLore.Base = 0.0;
        crafter.Skills.Blacksmith.Base = 0.0; // skillTier == 0 isolates BaseWeapon.OnCraft fix directly

        var katanaRecipe = DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(Katana));
        var plateRecipe = DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(PlateChest));
        var shieldRecipe = DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(HeaterShield));
        Assert.NotNull(katanaRecipe);
        Assert.NotNull(plateRecipe);
        Assert.NotNull(shieldRecipe);

        // 1) Valorite Runic Hammer DI > 35 is NOT overwritten down to 35 on Exceptional craft.
        var valoriteHammer = _world.TrackItem(new RunicHammer(CraftResource.Valorite, 200));
        var maxObservedDI = 0;
        for (var i = 0; i < 60; i++)
        {
            var katana = _world.TrackItem(new Katana());
            katana.OnCraft(
                (int)WeaponQuality.Exceptional,
                false,
                crafter,
                DefBlacksmithy.CraftSystem,
                typeof(IronIngot),
                valoriteHammer,
                katanaRecipe,
                0
            );

            if (katana.Attributes.WeaponDamage > maxObservedDI)
            {
                maxObservedDI = katana.Attributes.WeaponDamage;
            }
        }

        Assert.True(
            maxObservedDI > 35,
            $"Expected Valorite Runic Hammer exceptional weapon to preserve DI > 35, max observed was {maxObservedDI}."
        );

        // 2) Runic armor retains full 15 exceptional resist points (no 6-point runic penalty).
        crafter.Skills.Blacksmith.Base = 100.0;
        var dullCopperHammer = _world.TrackItem(new RunicHammer(CraftResource.DullCopper, 50));
        var plate = _world.TrackItem(new PlateChest());
        plate.OnCraft(
            (int)ArmorQuality.Exceptional,
            false,
            crafter,
            DefBlacksmithy.CraftSystem,
            typeof(IronIngot),
            dullCopperHammer,
            plateRecipe,
            0
        );
        var bonusResistSum = plate.PhysicalBonus + plate.FireBonus + plate.ColdBonus + plate.PoisonBonus + plate.EnergyBonus;
        Assert.True(
            bonusResistSum >= 15,
            $"Expected Runic exceptional armor to retain at least 15 exceptional resist points, got {bonusResistSum}."
        );

        // 3) Exceptional ML Shields get real resist & masterwork stats instead of staying blank.
        var shield = _world.TrackItem(new HeaterShield());
        shield.OnCraft(
            (int)ArmorQuality.Exceptional,
            true,
            crafter,
            DefBlacksmithy.CraftSystem,
            typeof(ValoriteIngot),
            null,
            shieldRecipe,
            0
        );
        var shieldResistSum = shield.PhysicalBonus + shield.FireBonus + shield.ColdBonus + shield.PoisonBonus + shield.EnergyBonus;
        Assert.True(shieldResistSum >= 6, $"Expected ML exceptional shield resist bonus >= 6, got {shieldResistSum}.");
        Assert.True(shield.Attributes.DefendChance >= 8);
        Assert.True(shield.Attributes.ReflectPhysical >= 8);
        Assert.True(
            shield.Attributes.SpellChanneling > 0 ||
            shield.ArmorAttributes.SoulCharge > 0 ||
            shield.ArmorAttributes.ReactiveParalyze > 0,
            "Expected GM+ Valorite shield to receive SpellChanneling, SoulCharge, or ReactiveParalyze."
        );
    }

    [Fact]
    public void Crafting_MasterworkGearOutscoresAverageRichFilthyRichLoot_WhileApexBossDropsCanBreakCaps()
    {
        EnsureCraftSystemsInitialized();

        var crafter = CreatePlayer(1470, 1470);
        crafter.Skills.Blacksmith.Cap = 120.0;
        crafter.Skills.Blacksmith.Base = 120.0;
        crafter.Skills.ArmsLore.Cap = 120.0;
        crafter.Skills.ArmsLore.Base = 100.0;
        crafter.Skills.Tailoring.Cap = 120.0;
        crafter.Skills.Tailoring.Base = 120.0;
        crafter.Skills.Fletching.Cap = 120.0;
        crafter.Skills.Fletching.Base = 120.0;
        crafter.Skills.Tinkering.Cap = 120.0;
        crafter.Skills.Tinkering.Base = 120.0;
        crafter.Skills.Inscribe.Cap = 120.0;
        crafter.Skills.Inscribe.Base = 120.0;
        crafter.Skills.Magery.Cap = 120.0;
        crafter.Skills.Magery.Base = 120.0;

        // Craft 120 Masterwork pieces across every major equipment category.
        var katana = _world.TrackItem(new Katana());
        katana.OnCraft(
            (int)WeaponQuality.Exceptional,
            true,
            crafter,
            DefBlacksmithy.CraftSystem,
            typeof(ValoriteIngot),
            null,
            DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(Katana)),
            0
        );

        var bow = _world.TrackItem(new Bow());
        bow.OnCraft(
            (int)WeaponQuality.Exceptional,
            true,
            crafter,
            DefBowFletching.CraftSystem,
            typeof(FrostwoodBoard),
            null,
            DefBowFletching.CraftSystem.CraftItems.SearchFor(typeof(Bow)),
            0
        );
        Assert.True(bow.Balanced);
        Assert.True(bow.Velocity > 0);

        var plate = _world.TrackItem(new PlateChest());
        plate.OnCraft(
            (int)ArmorQuality.Exceptional,
            true,
            crafter,
            DefBlacksmithy.CraftSystem,
            typeof(ValoriteIngot),
            null,
            DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(PlateChest)),
            0
        );

        var shield = _world.TrackItem(new HeaterShield());
        shield.OnCraft(
            (int)ArmorQuality.Exceptional,
            true,
            crafter,
            DefBlacksmithy.CraftSystem,
            typeof(ValoriteIngot),
            null,
            DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(HeaterShield)),
            0
        );

        var hat = _world.TrackItem(new WizardsHat());
        hat.OnCraft(
            (int)ClothingQuality.Exceptional,
            true,
            crafter,
            DefTailoring.CraftSystem,
            typeof(Cloth),
            null,
            DefTailoring.CraftSystem.CraftItems.SearchFor(typeof(WizardsHat)),
            0
        );

        var clothing = _world.TrackItem(new Doublet());
        clothing.OnCraft(
            (int)ClothingQuality.Exceptional,
            true,
            crafter,
            DefTailoring.CraftSystem,
            typeof(Cloth),
            null,
            DefTailoring.CraftSystem.CraftItems.SearchFor(typeof(Doublet)),
            0
        );

        var ring = _world.TrackItem(new GoldRing { PlayerConstructed = true, GemType = GemType.Diamond });
        var ringRecipe = new CraftItem(typeof(GoldRing), "ring", "gold ring");
        ring.OnCraft(2, true, crafter, DefTinkering.CraftSystem, typeof(IronIngot), null, ringRecipe, 0);

        var spellbook = _world.TrackItem(new Spellbook());
        spellbook.OnCraft(
            2,
            true,
            crafter,
            DefInscription.CraftSystem,
            typeof(BlankScroll),
            null,
            DefInscription.CraftSystem.CraftItems.SearchFor(typeof(Spellbook)),
            0
        );

        // Compute average Rich/FilthyRich loot weight per category (60 samples each).
        double avgLootWeapon = AverageLootWeight(() => _world.TrackItem(new Katana()), 3, 25, 85, 60);
        double avgLootArmor = AverageLootWeight(() => _world.TrackItem(new PlateChest()), 3, 25, 85, 60);
        double avgLootShield = AverageLootWeight(() => _world.TrackItem(new HeaterShield()), 3, 25, 85, 60);
        double avgLootHat = AverageLootWeight(() => _world.TrackItem(new WizardsHat()), 3, 25, 85, 60);
        double avgLootJewel = AverageLootWeight(() => _world.TrackItem(new GoldRing()), 3, 25, 85, 60);
        double avgLootBook = AverageLootWeight(() => _world.TrackItem(new Spellbook()), 3, 25, 85, 60);

        Assert.True(
            ItemRarity.ComputeItemWeight(katana) > avgLootWeapon,
            $"Masterwork weapon ({ItemRarity.ComputeItemWeight(katana)}) should outscore Rich/FilthyRich loot avg ({avgLootWeapon:F1})."
        );
        Assert.True(
            ItemRarity.ComputeItemWeight(bow) > avgLootWeapon,
            $"Masterwork bow ({ItemRarity.ComputeItemWeight(bow)}) should outscore Rich/FilthyRich loot avg ({avgLootWeapon:F1})."
        );
        Assert.True(
            ItemRarity.ComputeItemWeight(plate) > avgLootArmor,
            $"Masterwork armor ({ItemRarity.ComputeItemWeight(plate)}) should outscore Rich/FilthyRich loot avg ({avgLootArmor:F1})."
        );
        Assert.True(
            ItemRarity.ComputeItemWeight(shield) > avgLootShield,
            $"Masterwork shield ({ItemRarity.ComputeItemWeight(shield)}) should outscore Rich/FilthyRich loot avg ({avgLootShield:F1})."
        );
        Assert.True(
            ItemRarity.ComputeItemWeight(hat) > avgLootHat,
            $"Masterwork hat ({ItemRarity.ComputeItemWeight(hat)}) should outscore Rich/FilthyRich loot avg ({avgLootHat:F1})."
        );
        // Non-hat decorative clothing intentionally receives modest utility polish only (not full armor combat stats).
        Assert.True(clothing.Attributes.Luck > 0 || clothing.Attributes.LowerRegCost > 0);
        Assert.Equal(0, clothing.Attributes.LowerManaCost);
        Assert.Equal(0, clothing.Attributes.CastingFocus);
        Assert.False(ImbuingEngine.CanImbue(clothing, out _));
        Assert.True(ImbuingEngine.CanImbue(hat, out _));
        Assert.True(
            ItemRarity.ComputeItemWeight(ring) > avgLootJewel,
            $"Masterwork diamond ring ({ItemRarity.ComputeItemWeight(ring)}) should outscore Rich/FilthyRich jewel avg ({avgLootJewel:F1})."
        );
        Assert.True(
            ItemRarity.ComputeItemWeight(spellbook) > avgLootBook,
            $"Masterwork spellbook ({ItemRarity.ComputeItemWeight(spellbook)}) should outscore Rich/FilthyRich spellbook avg ({avgLootBook:F1})."
        );

        // Rare Apex SuperBoss drops (maxIntensity = 120) can break standard crafting caps.
        var brokeStandardCap = false;
        for (var i = 0; i < 60 && !brokeStandardCap; i++)
        {
            var apexWeapon = _world.TrackItem(new Katana());
            BaseRunicTool.ApplyAttributesTo(apexWeapon, false, 3000, 6, 80, 120);
            if (apexWeapon.Attributes.WeaponDamage > 50 ||
                apexWeapon.Attributes.WeaponSpeed > 30 ||
                apexWeapon.Attributes.AttackChance > 15 ||
                apexWeapon.Attributes.DefendChance > 15 ||
                apexWeapon.Attributes.Luck > 100)
            {
                brokeStandardCap = true;
            }
        }

        Assert.True(brokeStandardCap, "Expected Apex Boss overcap rolls (maxIntensity = 120) to break standard crafting caps.");
    }

    private static double AverageLootWeight(Func<Item> factory, int props, int min, int max, int samples)
    {
        double total = 0;
        for (var i = 0; i < samples; i++)
        {
            var item = factory();
            switch (item)
            {
                case BaseWeapon w:
                    {
                        BaseRunicTool.ApplyAttributesTo(w, false, 0, props, min, max);
                        break;
                    }
                case BaseArmor a:
                    {
                        BaseRunicTool.ApplyAttributesTo(a, false, 0, props, min, max);
                        break;
                    }
                case BaseHat h:
                    {
                        BaseRunicTool.ApplyAttributesTo(h, false, 0, props, min, max);
                        break;
                    }
                case BaseJewel j:
                    {
                        BaseRunicTool.ApplyAttributesTo(j, false, 0, props, min, max);
                        break;
                    }
                case Spellbook s:
                    {
                        BaseRunicTool.ApplyAttributesTo(s, false, 0, props, min, max);
                        break;
                    }
            }

            total += ItemRarity.ComputeItemWeight(item);
        }

        return total / samples;
    }

    // =========================================================================
    // 5. Phase 3: Soulforge, Unraveling, Imbuing Engine & Persistence
    // =========================================================================

    [Fact]
    public void Phase3_SoulForgeAndUnraveling_YieldByWeightAndProtectSafeItems()
    {
        EnsureCraftSystemsInitialized();
        Assert.NotNull(DefTinkering.CraftSystem.CraftItems.SearchFor(typeof(SoulForge)));

        var player = CreatePlayer(1480, 1480);
        player.Skills.Imbuing.Base = 100.0;

        Assert.False(ImbuingEngine.IsNearSoulForgeOrForge(player));
        var soulForge = _world.TrackItem(new SoulForge());
        soulForge.MoveToWorld(new Point3D(1481, 1480, 0), Map.Felucca);
        Assert.True(ImbuingEngine.IsNearSoulForge(player));
        Assert.True(ImbuingEngine.IsNearSoulForgeOrForge(player));

        // Tier yields: Residue (<200), Essence (200..449), Relic (450+)
        var lowMagic = _world.TrackItem(new Longsword());
        lowMagic.Attributes.WeaponDamage = 30; // weight ~60 -> MagicalResidue
        player.Backpack.DropItem(lowMagic);
        Assert.True(ImbuingEngine.TryUnravel(player, lowMagic, out var lowType, out var lowAmt));
        Assert.Equal(typeof(MagicalResidue), lowType);
        Assert.InRange(lowAmt, 1, 4);
        Assert.True(lowMagic.Deleted);

        var midMagic = _world.TrackItem(new Longsword());
        midMagic.Attributes.WeaponDamage = 50; // 100
        midMagic.Attributes.WeaponSpeed = 30;  // 110
        midMagic.Attributes.AttackChance = 15; // 130 -> total 340 -> EnchantedEssence
        player.Backpack.DropItem(midMagic);
        Assert.True(ImbuingEngine.TryUnravel(player, midMagic, out var midType, out var midAmt));
        Assert.Equal(typeof(EnchantedEssence), midType);
        Assert.InRange(midAmt, 1, 4);
        Assert.True(midMagic.Deleted);

        var highMagic = _world.TrackItem(new Longsword());
        highMagic.Attributes.WeaponDamage = 50; // 100
        highMagic.Attributes.WeaponSpeed = 30;  // 110
        highMagic.Attributes.AttackChance = 15; // 130
        highMagic.WeaponAttributes.HitLeechHits = 50; // 110
        highMagic.WeaponAttributes.Bane = 1;    // 140 -> total 590 -> RelicFragment
        player.Backpack.DropItem(highMagic);
        Assert.True(ImbuingEngine.TryUnravel(player, highMagic, out var highType, out var highAmt));
        Assert.Equal(typeof(RelicFragment), highType);
        Assert.InRange(highAmt, 1, 4);
        Assert.True(highMagic.Deleted);

        // Protected items must be blocked by CanUnravel AND left untouched by TryUnravelContainer.
        var salvageBag = _world.TrackItem(new Bag());
        player.Backpack.DropItem(salvageBag);

        var artifact = _world.TrackItem(new BoneCrusher());
        var blessed = _world.TrackItem(new Longsword { LootType = LootType.Blessed });
        blessed.Attributes.WeaponDamage = 40;
        var insured = _world.TrackItem(new Longsword { Insured = true });
        insured.Attributes.WeaponDamage = 40;
        var leveled = _world.TrackItem(new Longsword());
        leveled.Attributes.WeaponDamage = 40;
        player.Backpack.DropItem(leveled);
        Assert.True(leveled.TryEnableWeaponLeveling(player));
        var plain = _world.TrackItem(new Longsword());

        var eligible1 = _world.TrackItem(new Longsword());
        eligible1.Attributes.WeaponDamage = 40;
        var eligible2 = _world.TrackItem(new PlateChest());
        eligible2.Attributes.LowerManaCost = 8;
        eligible2.Attributes.LowerRegCost = 20;
        eligible2.Attributes.CastingFocus = 6;

        salvageBag.DropItem(artifact);
        salvageBag.DropItem(blessed);
        salvageBag.DropItem(insured);
        salvageBag.DropItem(leveled);
        salvageBag.DropItem(plain);
        salvageBag.DropItem(eligible1);
        salvageBag.DropItem(eligible2);

        Assert.False(ImbuingEngine.CanUnravel(artifact, out _));
        Assert.False(ImbuingEngine.CanUnravel(blessed, out _));
        Assert.False(ImbuingEngine.CanUnravel(insured, out _));
        Assert.False(ImbuingEngine.CanUnravel(leveled, out _));
        Assert.False(ImbuingEngine.CanUnravel(plain, out _));

        var batch = ImbuingEngine.TryUnravelContainer(player, salvageBag);
        Assert.Equal(2, batch.UnraveledCount);
        Assert.Equal(5, batch.SkippedCount);
        Assert.True(eligible1.Deleted);
        Assert.True(eligible2.Deleted);
        Assert.False(artifact.Deleted);
        Assert.False(blessed.Deleted);
        Assert.False(insured.Deleted);
        Assert.False(leveled.Deleted);
        Assert.False(plain.Deleted);
    }

    [Fact]
    public void Phase3_ImbuingEngine_EnforcesBudgetsFivePropCapUpgradesAndPersistenceRoundTrip()
    {
        var player = CreatePlayer(1490, 1490);
        player.Skills.Imbuing.Cap = 120.0;
        player.Skills.Imbuing.Base = 120.0; // Guarantees 100% success at SoulForge

        var soulForge = _world.TrackItem(new SoulForge());
        soulForge.MoveToWorld(new Point3D(1491, 1490, 0), Map.Felucca);

        // Stock ample ingredients & gems in backpack.
        player.Backpack.DropItem(_world.TrackItem(new MagicalResidue(200)));
        player.Backpack.DropItem(_world.TrackItem(new EnchantedEssence(200)));
        player.Backpack.DropItem(_world.TrackItem(new RelicFragment(200)));
        player.Backpack.DropItem(_world.TrackItem(new Ruby(100)));
        player.Backpack.DropItem(_world.TrackItem(new Diamond(100)));
        player.Backpack.DropItem(_world.TrackItem(new Tourmaline(100)));
        player.Backpack.DropItem(_world.TrackItem(new Sapphire(100)));
        player.Backpack.DropItem(_world.TrackItem(new Emerald(100)));
        player.Backpack.DropItem(_world.TrackItem(new Amethyst(100)));
        player.Backpack.DropItem(_world.TrackItem(new Citrine(100)));
        player.Backpack.DropItem(_world.TrackItem(new Amber(100)));
        player.Backpack.DropItem(_world.TrackItem(new StarSapphire(100)));

        // Verify exact weight budgets: 550 Exceptional Crafted / 500 Standard Crafted / 450 Looted.
        var exceptionalCrafted = _world.TrackItem(new Katana { PlayerConstructed = true, Quality = WeaponQuality.Exceptional });
        var standardCrafted = _world.TrackItem(new Katana { PlayerConstructed = true, Quality = WeaponQuality.Regular });
        var looted = _world.TrackItem(new Katana { PlayerConstructed = false, Quality = WeaponQuality.Regular });

        Assert.Equal(550, ImbuingCatalog.GetMaxWeight(exceptionalCrafted));
        Assert.Equal(500, ImbuingCatalog.GetMaxWeight(standardCrafted));
        Assert.Equal(450, ImbuingCatalog.GetMaxWeight(looted));

        player.Backpack.DropItem(exceptionalCrafted);
        player.Backpack.DropItem(looted);

        // Verify weight budget rejection on looted (450 cap):
        // DI 50 (100) + SSI 30 (110) + HCI 15 (130) + Bane (140) = 480 > 450 (blocked on looted, allowed on exceptional 550!).
        var diMod = ImbuingCatalog.FindMod(AosAttribute.WeaponDamage);
        var ssiMod = ImbuingCatalog.FindMod(AosAttribute.WeaponSpeed);
        var hciMod = ImbuingCatalog.FindMod(AosAttribute.AttackChance);
        var baneMod = ImbuingCatalog.FindMod(AosWeaponAttribute.Bane);
        var leechMod = ImbuingCatalog.FindMod(AosWeaponAttribute.HitLeechHits);
        var staminaLeechMod = ImbuingCatalog.FindMod(AosWeaponAttribute.HitLeechStam);
        Assert.NotNull(diMod);
        Assert.NotNull(ssiMod);
        Assert.NotNull(hciMod);
        Assert.NotNull(baneMod);
        Assert.NotNull(leechMod);
        Assert.NotNull(staminaLeechMod);

        Assert.True(ImbuingEngine.TryImbue(player, looted, diMod.Id, 50, out _));
        Assert.True(ImbuingEngine.TryImbue(player, looted, ssiMod.Id, 30, out _));
        Assert.True(ImbuingEngine.TryImbue(player, looted, hciMod.Id, 15, out _));
        Assert.False(ImbuingEngine.TryImbue(player, looted, baneMod.Id, 1, out var overBudgetMsg));
        Assert.Contains("budget", overBudgetMsg, StringComparison.OrdinalIgnoreCase);

        // On Exceptional crafted (550 budget): fill 5 properties cleanly, then test 6th prop rejection + in-place upgrade!
        Assert.True(ImbuingEngine.TryImbue(player, exceptionalCrafted, diMod.Id, 20, out _));        // prop 1 (weight 40)
        Assert.True(ImbuingEngine.TryImbue(player, exceptionalCrafted, ssiMod.Id, 25, out _));       // prop 2 (weight 92)
        Assert.True(ImbuingEngine.TryImbue(player, exceptionalCrafted, hciMod.Id, 10, out _));       // prop 3 (weight 87)
        Assert.True(ImbuingEngine.TryImbue(player, exceptionalCrafted, baneMod.Id, 1, out _));       // prop 4 (weight 140)
        Assert.True(ImbuingEngine.TryImbue(player, exceptionalCrafted, leechMod.Id, 20, out _));     // prop 5 (weight 44)
        Assert.Equal(5, ImbuingCatalog.CountProperties(exceptionalCrafted));

        // 6th new property must be blocked by 5-property cap.
        Assert.False(ImbuingEngine.TryImbue(player, exceptionalCrafted, staminaLeechMod.Id, 10, out var capMsg));
        Assert.Contains("5", capMsg);

        // Upgrading an EXISTING property (DI 20 -> 50) succeeds without consuming a new slot!
        Assert.True(ImbuingEngine.TryImbue(player, exceptionalCrafted, diMod.Id, 50, out _));
        Assert.Equal(50, exceptionalCrafted.Attributes.WeaponDamage);
        Assert.Equal(5, ImbuingCatalog.CountProperties(exceptionalCrafted));

        // ImbuingPersistence marks item imbued, emits cliloc 1080418, and survives Serialize/Deserialize.
        Assert.True(ImbuingPersistence.IsImbued(exceptionalCrafted));
        Assert.Equal(6, ImbuingPersistence.GetTimesImbued(exceptionalCrafted));
        AssertHasCliloc(DecodeOpl(exceptionalCrafted), ImbuingPersistence.ImbuedCliloc);

        var writer = new BufferWriter(true);
        ImbuingPersistence.SerializeState(writer);
        ImbuingPersistence.ResetForTesting();
        Assert.False(ImbuingPersistence.IsImbued(exceptionalCrafted));

        var reader = SnapshotReader(writer);
        ImbuingPersistence.DeserializeState(reader);
        Assert.Equal(writer.Position, reader.Position);
        Assert.True(ImbuingPersistence.IsImbued(exceptionalCrafted));
        Assert.Equal(6, ImbuingPersistence.GetTimesImbued(exceptionalCrafted));
        AssertHasCliloc(DecodeOpl(exceptionalCrafted), ImbuingPersistence.ImbuedCliloc);
    }

    // =========================================================================
    // 6. Hit-Chance Bug Fixes, Solo HCI Cap & PvM Anti-Streak Protection
    // =========================================================================

    [Fact]
    public void HitChance_SoloCapAndPvMMissStreakProtection_WorkAndResetProperly()
    {
        var attacker = CreatePlayer(1500, 1500);
        var boneKnight = _world.NewCreature();
        var secondMonster = _world.NewCreature();
        boneKnight.MoveToWorld(new Point3D(1501, 1500, 0), Map.Felucca);
        secondMonster.MoveToWorld(new Point3D(1500, 1501, 0), Map.Felucca);

        attacker.Skills.Swords.Base = 100.0;
        boneKnight.Skills.Wrestling.Base = 100.0;
        secondMonster.Skills.Wrestling.Base = 100.0;

        var sword = _world.TrackItem(new Katana { Layer = Layer.OneHanded });
        attacker.AddItem(sword);

        // Verify SoloProgression.GetAttackChanceCap: 60 for solo player, 45 for NPC or non-solo.
        SetSoloMode(true);
        Assert.Equal(60, SoloProgression.GetAttackChanceCap(attacker));
        Assert.Equal(45, SoloProgression.GetAttackChanceCap(boneKnight));
        SetSoloMode(false);
        Assert.Equal(45, SoloProgression.GetAttackChanceCap(attacker));
        SetSoloMode(true);

        // Verify Solo HCI cap = 60 in CheckHit (with 80% gear HCI -> clamped to 60%).
        var ring = _world.TrackItem(new GoldRing { Layer = Layer.Ring });
        ring.Attributes.AttackChance = 80;
        attacker.AddItem(ring);

        var originalDirect = Mobile.SkillCheckDirectLocationHandler;
        var recordedChance = 0.0;
        var forcedResult = true;
        Mobile.SkillCheckDirectLocationHandler = (_, _, chance) =>
        {
            recordedChance = chance;
            return forcedResult;
        };

        try
        {
            SoloProgression.ResetPvMMissStreak(attacker);
            Assert.True(sword.CheckHit(attacker, boneKnight));
            var expectedCappedChance = (100.0 + 20.0) * (100 + 60) / ((100.0 + 20.0) * 100 * 2.0); // 0.80
            Assert.Equal(expectedCappedChance, recordedChance, 8);

            // Lightning Strike (+50% accuracy) applies AFTER the 60% gear HCI cap!
            attacker.Skills.Bushido.Base = 100.0;
            attacker.Mana = attacker.ManaMax;
            Assert.True(SpecialMove.SetCurrentMove(attacker, new LightningStrike()));
            Assert.True(sword.CheckHit(attacker, boneKnight));
            var expectedLightningChance = (100.0 + 20.0) * (100 + 60 + 50) / ((100.0 + 20.0) * 100 * 2.0); // 1.05
            Assert.Equal(expectedLightningChance, recordedChance, 8);
            SpecialMove.ClearCurrentMove(attacker);

            // Remove HCI ring to test pure 50% baseline + PvM anti-streak ramp (+0, +0, +0.15, +0.30, +0.45) + resets.
            ring.Delete();
            SoloProgression.ResetPvMMissStreak(attacker);
            forcedResult = false;

            // Swing 1 (0 prior misses): pure 0.50
            Assert.False(sword.CheckHit(attacker, boneKnight));
            Assert.Equal(0.50, recordedChance, 8);

            // Swing 2 (1 prior miss): pure 0.50
            Assert.False(sword.CheckHit(attacker, boneKnight));
            Assert.Equal(0.50, recordedChance, 8);

            // Swing 3 (2 prior misses): 0.50 + 0.15 = 0.65
            Assert.False(sword.CheckHit(attacker, boneKnight));
            Assert.Equal(0.65, recordedChance, 8);

            // Swing 4 (3 prior misses): 0.50 + 0.30 = 0.80
            Assert.False(sword.CheckHit(attacker, boneKnight));
            Assert.Equal(0.80, recordedChance, 8);

            // Swing 5 (4 prior misses): 0.50 + 0.45 = 0.95
            Assert.False(sword.CheckHit(attacker, boneKnight));
            Assert.Equal(0.95, recordedChance, 8);

            // Swing 6 (5 prior misses): 0.50 + 0.60 -> clamped to 0.98
            forcedResult = true;
            Assert.True(sword.CheckHit(attacker, boneKnight));
            Assert.Equal(0.98, recordedChance, 8);

            // Swing 7 (after hit): reset back to pure 0.50
            forcedResult = false;
            Assert.False(sword.CheckHit(attacker, boneKnight));
            Assert.Equal(0.50, recordedChance, 8);

            // Target change resets streak immediately.
            Assert.False(sword.CheckHit(attacker, boneKnight)); // 2 misses on boneKnight
            Assert.False(sword.CheckHit(attacker, secondMonster)); // switched -> pure 0.50
            Assert.Equal(0.50, recordedChance, 8);
        }
        finally
        {
            SpecialMove.ClearCurrentMove(attacker);
            SoloProgression.ResetPvMMissStreak(attacker);
            Mobile.SkillCheckDirectLocationHandler = originalDirect;
        }
    }

    [Fact]
    public void HitChance_DiscordanceChecksDefenderAndDivineFuryUsesHSGetters()
    {
        using var scope = new TimerWheelScope(startTick: 500_000);
        Core.Expansion = Expansion.EJ;

        var attacker = CreatePlayer(1510, 1510);
        var defender = _world.NewCreature();
        var bard = CreatePlayer(1512, 1510);
        defender.MoveToWorld(new Point3D(1511, 1510, 0), Map.Felucca);

        attacker.Skills.Swords.Base = 100.0;
        attacker.Skills.Chivalry.Cap = 120.0;
        attacker.Skills.Chivalry.Base = 120.0;
        attacker.Karma = 15000;
        attacker.TithingPoints = 100;
        attacker.Mana = attacker.ManaMax;
        defender.Skills.Wrestling.Base = 100.0;

        var sword = _world.TrackItem(new Katana { Layer = Layer.OneHanded });
        attacker.AddItem(sword);

        var defenderRing = _world.TrackItem(new GoldRing { Layer = Layer.Ring });
        defenderRing.Attributes.DefendChance = 30;
        defender.AddItem(defenderRing);

        var originalDirect = Mobile.SkillCheckDirectLocationHandler;
        var originalTarget = Mobile.SkillCheckTargetHandler;
        var originalLocation = Mobile.SkillCheckLocationHandler;
        var recordedChance = 0.0;

        Mobile.SkillCheckDirectLocationHandler = (_, _, chance) =>
        {
            recordedChance = chance;
            return true;
        };
        Mobile.SkillCheckTargetHandler = (_, _, _, _, _) => true;
        Mobile.SkillCheckLocationHandler = (_, _, _, _) => true;

        var discordTable = typeof(Discordance)
            .GetField("m_Table", BindingFlags.Static | BindingFlags.NonPublic)
            ?.GetValue(null) as IDictionary;

        try
        {
            // 1) Divine Fury with 120 Chivalry & 10k+ Karma grants +15 HCI (not hardcoded +10).
            var fury = new DivineFurySpell(attacker);
            fury.Cast();
            TimerWheelScope.Advance(TimeSpan.FromSeconds(2));
            Assert.True(DivineFurySpell.UnderEffect(attacker));
            Assert.Equal(15, DivineFurySpell.GetAttackBonus(attacker));
            Assert.Equal(10, DivineFurySpell.GetDefendMalus(attacker));

            Assert.True(sword.CheckHit(attacker, defender));
            var expectedWithFury = (100.0 + 20.0) * (100 + 15) / ((100.0 + 20.0) * (100 + 30) * 2.0);
            Assert.Equal(expectedWithFury, recordedChance, 8);
            DivineFurySpell.StopDivineFury(attacker);

            // 2) Discordance on DEFENDER lowers defender's DCI (raising attacker hit chance).
            bard.Skills.Musicianship.Base = 100.0;
            bard.Skills.Discordance.Base = 100.0;
            var lute = _world.TrackItem(new Lute());
            bard.Backpack.DropItem(lute);

            Discordance.OnPickedInstrument(bard, lute);
            Assert.NotNull(bard.Target);
            bard.Target.Invoke(bard, defender);

            var discordEffect = 0;
            Assert.True(Discordance.GetEffect(defender, ref discordEffect));
            Assert.True(discordEffect > 0);

            var defSkillAfterDiscord = defender.Skills.Wrestling.Value;
            Assert.True(sword.CheckHit(attacker, defender));
            var expectedWithDefenderDiscord =
                (100.0 + 20.0) * 100.0 / ((defSkillAfterDiscord + 20.0) * (100 + 30 - discordEffect) * 2.0);
            Assert.Equal(expectedWithDefenderDiscord, recordedChance, 8);

            // Verify NinjaWeapon.CombatCheck also uses defender's Discordance.
            attacker.Skills.Ninjitsu.Base = 100.0;
            var ninjaCheck = typeof(NinjaWeapon).GetMethod("CombatCheck", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(ninjaCheck);
            recordedChance = 0.0;
            Assert.True((bool)ninjaCheck.Invoke(null, [attacker, defender]));
            Assert.Equal(expectedWithDefenderDiscord, recordedChance, 8);
        }
        finally
        {
            DivineFurySpell.StopDivineFury(attacker);
            discordTable?.Remove(defender);
            Mobile.SkillCheckDirectLocationHandler = originalDirect;
            Mobile.SkillCheckTargetHandler = originalTarget;
            Mobile.SkillCheckLocationHandler = originalLocation;
        }
    }

    // =========================================================================
    // 7. Wave 2 Adversarial Audit Regression Tests
    // =========================================================================

    [Fact]
    public void Wave2Audit_CraftingHeadroomWoodCapsThrownRangeSCFCAndMutualExclusion()
    {
        EnsureCraftSystemsInitialized();
        Core.Expansion = Expansion.EJ;

        var crafter = CreatePlayer(1520, 1520);
        crafter.Skills.Blacksmith.Cap = 120.0;
        crafter.Skills.Blacksmith.Base = 120.0;
        crafter.Skills.ArmsLore.Cap = 120.0;
        crafter.Skills.ArmsLore.Base = 120.0;
        crafter.Skills.Fletching.Cap = 120.0;
        crafter.Skills.Fletching.Base = 120.0;
        crafter.Skills.Tailoring.Cap = 120.0;
        crafter.Skills.Tailoring.Base = 120.0;
        crafter.Skills.Tinkering.Cap = 120.0;
        crafter.Skills.Tinkering.Base = 120.0;
        crafter.Skills.Inscribe.Cap = 120.0;
        crafter.Skills.Inscribe.Base = 120.0;
        crafter.Skills.Magery.Cap = 120.0;
        crafter.Skills.Magery.Base = 120.0;
        crafter.Skills.Imbuing.Cap = 120.0;
        crafter.Skills.Imbuing.Base = 120.0;

        // 1) Frostwood Bow does NOT double Cold damage (40% cold / 60% phys) and obeys caps & <= 5 props / <= 550 weight.
        var bow = _world.TrackItem(new Bow());
        bow.OnCraft(
            (int)WeaponQuality.Exceptional,
            true,
            crafter,
            DefBowFletching.CraftSystem,
            typeof(FrostwoodBoard),
            null,
            DefBowFletching.CraftSystem.CraftItems.SearchFor(typeof(Bow)),
            0
        );
        bow.GetDamageTypes(crafter, out var phys, out _, out var cold, out _, out _, out _, out _);
        Assert.Equal((60, 40), (phys, cold));
        Assert.True(bow.Attributes.WeaponDamage <= 50);
        Assert.True(bow.Attributes.WeaponSpeed <= 30);
        Assert.True(bow.Attributes.AttackChance <= 15);
        Assert.True(bow.WeaponAttributes.HitLeechHits <= 50);
        Assert.True(ImbuingCatalog.CountProperties(bow) <= ImbuingCatalog.MaxProperties);
        Assert.True(ImbuingCatalog.ComputeTotalWeight(bow) <= ImbuingCatalog.ExceptionalCraftedMaxWeight);

        // 2) Masterwork Katana, PlateChest, HeaterShield, WizardsHat, GoldRing, Spellbook stay <= 5 props and <= 550 weight.
        var katana = _world.TrackItem(new Katana());
        katana.OnCraft(
            (int)WeaponQuality.Exceptional,
            true,
            crafter,
            DefBlacksmithy.CraftSystem,
            typeof(ValoriteIngot),
            null,
            DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(Katana)),
            0
        );
        var plate = _world.TrackItem(new PlateChest());
        plate.OnCraft(
            (int)ArmorQuality.Exceptional,
            true,
            crafter,
            DefBlacksmithy.CraftSystem,
            typeof(ValoriteIngot),
            null,
            DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(PlateChest)),
            0
        );
        var shield = _world.TrackItem(new HeaterShield());
        shield.OnCraft(
            (int)ArmorQuality.Exceptional,
            true,
            crafter,
            DefBlacksmithy.CraftSystem,
            typeof(ValoriteIngot),
            null,
            DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(HeaterShield)),
            0
        );
        var ring = _world.TrackItem(new GoldRing { PlayerConstructed = true, GemType = GemType.Diamond });
        ring.OnCraft(
            2,
            true,
            crafter,
            DefTinkering.CraftSystem,
            typeof(IronIngot),
            null,
            new CraftItem(typeof(GoldRing), "ring", "gold ring"),
            0
        );
        var book = _world.TrackItem(new Spellbook());
        book.OnCraft(
            2,
            true,
            crafter,
            DefInscription.CraftSystem,
            typeof(BlankScroll),
            null,
            DefInscription.CraftSystem.CraftItems.SearchFor(typeof(Spellbook)),
            0
        );

        foreach (var crafted in new Item[] { katana, plate, shield, ring, book })
        {
            Assert.True(
                ImbuingCatalog.CountProperties(crafted) <= ImbuingCatalog.MaxProperties,
                $"{crafted.GetType().Name} exceeded 5 props ({ImbuingCatalog.CountProperties(crafted)})."
            );
            Assert.True(
                ImbuingCatalog.ComputeTotalWeight(crafted) <= ImbuingCatalog.ExceptionalCraftedMaxWeight,
                $"{crafted.GetType().Name} exceeded 550 weight ({ImbuingCatalog.ComputeTotalWeight(crafted)})."
            );
        }

        // Crafted PlayerConstructed BaseJewel gets full 550 budget.
        Assert.Equal(550, ImbuingCatalog.GetMaxWeight(ring));

        // 3) BaseThrown never receives Balanced from crafting or imbuing, and emits Min Throw Range (not ship-decay 1149791).
        var boomerang = _world.TrackItem(new Boomerang());
        boomerang.OnCraft(
            (int)WeaponQuality.Exceptional,
            true,
            crafter,
            DefBlacksmithy.CraftSystem,
            typeof(ValoriteIngot),
            null,
            DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(Boomerang))
                ?? new CraftItem(typeof(Boomerang), "boomerang", "boomerang"),
            0
        );
        Assert.False(boomerang.Balanced);
        Assert.False(ImbuingCatalog.FindRangedBalancedMod().IsEligibleFor(boomerang));
        var thrownOpl = DecodeOpl(boomerang);
        Assert.DoesNotContain(thrownOpl, e => e.Cliloc == 1149791);
        Assert.Contains(thrownOpl, e => e.Arg.Contains("Min Throw Range", StringComparison.Ordinal));

        // 4) SpellChanneling + FasterCasting exploit closed: SC sets CastSpeed = -1; FC only raises -1 -> 0 (not +1).
        var soulForge = _world.TrackItem(new SoulForge());
        Assert.Equal(0x2DB0, soulForge.ItemID);
        soulForge.MoveToWorld(new Point3D(1521, 1520, 0), Map.Felucca);

        crafter.Backpack.DropItem(_world.TrackItem(new MagicalResidue(50)));
        crafter.Backpack.DropItem(_world.TrackItem(new RelicFragment(50)));
        crafter.Backpack.DropItem(_world.TrackItem(new Diamond(50)));
        crafter.Backpack.DropItem(_world.TrackItem(new Sapphire(50)));

        var scSword = _world.TrackItem(new Longsword { PlayerConstructed = true, Quality = WeaponQuality.Exceptional });
        crafter.Backpack.DropItem(scSword);

        var scMod = ImbuingCatalog.FindMod(AosAttribute.SpellChanneling);
        var fcMod = ImbuingCatalog.FindMod(AosAttribute.CastSpeed);
        Assert.True(ImbuingEngine.TryImbue(crafter, scSword, scMod.Id, 1, out _));
        Assert.Equal(1, scSword.Attributes.SpellChanneling);
        Assert.Equal(-1, scSword.Attributes.CastSpeed);

        Assert.True(ImbuingEngine.TryImbue(crafter, scSword, fcMod.Id, 1, out _));
        Assert.Equal(0, scSword.Attributes.CastSpeed);

        // 5) Imbued weapon is rejected by TryEnableWeaponLeveling (mutual exclusion), and dupe preserves imbued state.
        Assert.False(scSword.TryEnableWeaponLeveling(crafter));

        var dupedSword = _world.TrackItem(new Longsword());
        scSword.OnAfterDuped(dupedSword);
        Assert.True(ImbuingPersistence.IsImbued(dupedSword));
        Assert.Equal(ImbuingPersistence.GetTimesImbued(scSword), ImbuingPersistence.GetTimesImbued(dupedSword));
    }

    [Fact]
    public void Wave2Audit_ImbuingExploitGuardsFixedArtifactsAndFilledSpellbookSafety()
    {
        var player = CreatePlayer(1530, 1530);
        player.Skills.Imbuing.Cap = 120.0;
        player.Skills.Imbuing.Base = 120.0;
        player.Backpack.DropItem(_world.TrackItem(new EnchantedEssence(50)));
        player.Backpack.DropItem(_world.TrackItem(new Ruby(50)));

        var diMod = ImbuingCatalog.FindMod(AosAttribute.WeaponDamage);
        var packSword = _world.TrackItem(new Longsword());
        packSword.Attributes.WeaponDamage = 30;
        player.Backpack.DropItem(packSword);

        // 1) Away from any SoulForge or Anvil+Forge: TryImbue and TryUnravel must reject.
        Assert.False(ImbuingEngine.TryImbue(player, packSword, diMod.Id, 40, out _));
        Assert.False(ImbuingEngine.TryUnravel(player, packSword, out _, out _));

        // Place world SoulForge adjacent to player.
        var soulForge = _world.TrackItem(new SoulForge());
        soulForge.MoveToWorld(new Point3D(1531, 1530, 0), Map.Felucca);

        // 2) Ground item (Parent == null) must be rejected by TryImbue and TryUnravel.
        var groundSword = _world.TrackItem(new Longsword());
        groundSword.Attributes.WeaponDamage = 30;
        groundSword.MoveToWorld(new Point3D(1532, 1530, 0), Map.Felucca);
        Assert.False(ImbuingEngine.TryImbue(player, groundSword, diMod.Id, 40, out _));
        Assert.False(ImbuingEngine.TryUnravel(player, groundSword, out _, out _));

        // 3) Equipped item must be rejected by TryImbue.
        var equippedSword = _world.TrackItem(new Longsword { Layer = Layer.OneHanded });
        player.Backpack.DropItem(equippedSword);
        Assert.True(player.EquipItem(equippedSword));
        Assert.False(ImbuingEngine.TryImbue(player, equippedSword, diMod.Id, 20, out _));

        // 4) Filled Spellbook (>16 spells) is protected from unraveling; 8-spell looted spellbook is unravelable.
        var filledBook = _world.TrackItem(new Spellbook(0xFFFF_FFFF_FFFF_FFFFUL) { LootType = LootType.Regular });
        filledBook.Attributes.SpellDamage = 10;
        Assert.False(ImbuingEngine.CanUnravel(filledBook, out _));

        var lootBook = _world.TrackItem(new Spellbook(0xFFUL) { LootType = LootType.Regular });
        lootBook.Attributes.SpellDamage = 10;
        Assert.True(ImbuingEngine.CanUnravel(lootBook, out _));

        // 5) Fixed named artifacts with ArtifactRarity == 0 are recognized, silent on Publish 81 rarity, and blocked from imbue/unravel.
        var minorArt = _world.TrackItem(new HeartOfTheLion());
        var artBook = _world.TrackItem(new OssianGrimoire());
        Assert.True(ItemRarity.IsFixedArtifact(minorArt));
        Assert.True(ItemRarity.IsFixedArtifact(artBook));
        Assert.DoesNotContain(DecodeOpl(minorArt), e => e.Cliloc is >= 1151488 and <= 1151495);
        Assert.DoesNotContain(DecodeOpl(artBook), e => e.Cliloc is >= 1151488 and <= 1151495);
        Assert.False(ImbuingEngine.CanUnravel(minorArt, out _));
        Assert.False(ImbuingEngine.CanImbue(minorArt, out _));
        Assert.False(ImbuingEngine.CanImbue(artBook, out _));
    }

    [Fact]
    public void Wave2Audit_CombatStatModLeakBlockExpirationDCIClampParagonAndSAEdgeCases()
    {
        using var scope = new TimerWheelScope(startTick: 600_000);
        Core.Expansion = Expansion.EJ;

        var player = CreatePlayer(1540, 1540);
        var opponent = CreatePlayer(1541, 1540);

        // 1) AosAttributes.AddStatBonuses / RemoveStatBonuses StatMod key match (zero leak on unequip).
        var baseStr = player.Str;
        var quiver = _world.TrackItem(new BaseQuiver { Layer = Layer.Cloak });
        quiver.Attributes.BonusStr = 7;
        player.Backpack.DropItem(quiver);
        Assert.True(player.EquipItem(quiver));
        Assert.Equal(baseStr + 7, player.Str);
        player.Backpack.DropItem(quiver);
        Assert.Equal(baseStr, player.Str);

        // 2) Block grants 10..15 DCI (not 50..60) and expires after 6s.
        Block.BeginBlock(player, 15);
        var blockBonus = 0;
        Assert.True(Block.GetBonus(player, ref blockBonus));
        Assert.Equal(15, blockBonus);
        TimerWheelScope.RunFor(7000);
        blockBonus = 0;
        Assert.False(Block.GetBonus(player, ref blockBonus));

        // 3) Negative DCI lower clamp (-45) prevents CheckHit from flipping to 2% under stacked debuffs.
        player.Skills.Swords.Base = 100.0;
        opponent.Skills.Wrestling.Base = 100.0;
        var sword = _world.TrackItem(new Katana { Layer = Layer.OneHanded });
        player.AddItem(sword);

        var penaltyRing = _world.TrackItem(new GoldRing { Layer = Layer.Ring });
        penaltyRing.Attributes.DefendChance = -150;
        opponent.AddItem(penaltyRing);

        var originalDirect = Mobile.SkillCheckDirectLocationHandler;
        var recordedChance = 0.0;
        Mobile.SkillCheckDirectLocationHandler = (_, _, chance) =>
        {
            recordedChance = chance;
            return true;
        };
        try
        {
            Assert.True(sword.CheckHit(player, opponent));
            var expectedClamped = (100.0 + 20.0) * 100.0 / ((100.0 + 20.0) * (100 - 45) * 2.0);
            Assert.Equal(expectedClamped, recordedChance, 8);
        }
        finally
        {
            Mobile.SkillCheckDirectLocationHandler = originalDirect;
        }

        // 4) SplinteringWeapon respects BleedImmune, and BuffInfo uses valid 0-arg clilocs.
        var lich = _world.Track(new Lich());
        lich.Hits = 300;
        lich.MoveToWorld(new Point3D(1542, 1540, 0), Map.Felucca);
        Assert.True(BleedAttack.IsBleedImmune(lich));
        Assert.True(SAItemCombatMechanics.TriggerSplinteringWeapon(player, lich, 30));
        Assert.False(BleedAttack.IsBleeding(lich));

        // SplinteringEffect & ManaPhase BuffInfo clilocs have no unformatted ~1_val~ placeholders.
        var chest = _world.TrackItem(new LeatherChest { Layer = Layer.InnerTorso });
        chest.ArmorAttributes.ManaPhase = 1;
        player.Backpack.DropItem(chest);
        Assert.True(player.EquipItem(chest));
        var manaCost = 20;
        Assert.True(SAItemCombatMechanics.TryTriggerManaPhase(player, ref manaCost, forceRoll: true));
        var manaPhaseBuff = player.GetBuff(BuffIcon.ManaPhase);
        Assert.NotNull(manaPhaseBuff);
        Assert.Equal(1116158, manaPhaseBuff.TitleCliloc);
        Assert.Equal(1153816, manaPhaseBuff.SecondaryCliloc);

        Assert.True(SAItemCombatMechanics.TriggerSplinteringWeapon(opponent, player, 20));
        var splinterBuff = player.GetBuff(BuffIcon.SplinteringEffect);
        Assert.NotNull(splinterBuff);
        Assert.Equal(1154670, splinterBuff.TitleCliloc);
        Assert.Equal(1153818, splinterBuff.SecondaryCliloc);

        // 5) Eaters exclude direct damage, skip self-damage, and respect MortalStrike.IsWounded.
        BleedAttack.EndBleed(player, false);
        chest.Attributes.DamageEater = 18;
        Assert.Equal(0, SAItemCombatMechanics.ComputeEaterHeal(player, 100, 0, 0, 0, 0, 0, direct: 100));
        player.Hits = 50;
        SAItemCombatMechanics.OnDamageApplied(player, player, 100, 100, 0, 0, 0, 0);
        TimerWheelScope.RunFor(4000);
        Assert.Equal(50, player.Hits);

        MortalStrike.BeginWound(player, TimeSpan.FromSeconds(10));
        SAItemCombatMechanics.OnDamageApplied(player, opponent, 100, 100, 0, 0, 0, 0);
        TimerWheelScope.RunFor(4000);
        Assert.Equal(50, player.Hits);

        // 6) Bane PvP damage is capped to 35 (50 HP is < 50% of 112 max HP while surviving 35 damage).
        opponent.Hits = 50;
        var pvpBane = SAItemCombatMechanics.TryTriggerBane(player, opponent, 500, forceRoll: true);
        Assert.Equal(35, pvpBane);
        Assert.Equal(15, opponent.Hits);

        // 7) Paragon.CheckConvert safely excludes mounts and controlled/blessed creatures on non-Ilshenar maps.
        var horse = _world.Track(new Horse());
        Assert.False(Paragon.CheckConvert(horse, new Point3D(1540, 1540, 0), Map.Trammel));
        var pet = _world.NewCreature();
        pet.Controlled = true;
        Assert.False(Paragon.CheckConvert(pet, new Point3D(1540, 1540, 0), Map.Felucca));

        // 8) ParagonChest handles level 0 and level 6 safely (RequiredSkill 100, no Level 7 TreasureMap).
        var lvl0 = _world.TrackItem(new ParagonChest("orc", 0));
        Assert.True(lvl0.RequiredSkill > 0);
        var lvl6 = _world.TrackItem(new ParagonChest("balron", 6));
        Assert.Equal(100, lvl6.RequiredSkill);
        foreach (var item in lvl6.Items)
        {
            if (item is TreasureMap tmap)
            {
                Assert.True(tmap.Level <= 6);
            }
        }
    }

    [Fact]
    public void PolishFixes_WoodenDefaultsRunicToolsQuiverTalismanMultiSchoolSpellbooksAndToTPigments()
    {
        Core.Expansion = Expansion.EJ;
        if (DefInscription.CraftSystem == null)
        {
            DefInscription.Initialize();
        }

        // 1) Wooden shields & Woodland armor default to RegularWood (not Iron).
        var woodenItems = new BaseArmor[]
        {
            _world.TrackItem(new WoodenShield()),
            _world.TrackItem(new GargishWoodenShield()),
            _world.TrackItem(new WoodlandChest()),
            _world.TrackItem(new FemaleElvenPlateChest()),
            _world.TrackItem(new WoodlandArms()),
            _world.TrackItem(new WoodlandGloves()),
            _world.TrackItem(new WoodlandGorget()),
            _world.TrackItem(new WoodlandLegs())
        };
        foreach (var item in woodenItems)
        {
            Assert.Equal(CraftResource.RegularWood, item.DefaultResource);
            Assert.Equal(CraftResource.RegularWood, item.Resource);
        }

        // 2) RunicTinkerTool & RunicCarpenterSaw inherit material hue; Fletcher & DovetailSaw display Excelsior wood name.
        var tinkerTool = _world.TrackItem(new RunicTinkerTool(CraftResource.Valorite));
        Assert.Equal(CraftResources.GetHue(CraftResource.Valorite), tinkerTool.Hue);

        var carpSaw = _world.TrackItem(new RunicCarpenterSaw(CraftResource.PetrifiedWood));
        Assert.Equal(CraftResources.GetHue(CraftResource.PetrifiedWood), carpSaw.Hue);

        var fletcherTool = _world.TrackItem(new RunicFletcherTool(CraftResource.PetrifiedWood));
        var dovetailSaw = _world.TrackItem(new RunicDovetailSaw(CraftResource.PetrifiedWood));
        var petrifiedName = CraftResources.GetName(CraftResource.PetrifiedWood);
        Assert.Contains(DecodeOpl(fletcherTool), e => e.Arg.Contains(petrifiedName, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(DecodeOpl(dovetailSaw), e => e.Arg.Contains(petrifiedName, StringComparison.OrdinalIgnoreCase));

        // 3) BaseQuiver emits SA attributes + Publish 81 rarity; BaseTalisman emits Publish 81 rarity.
        var quiver = _world.TrackItem(new ElvenQuiver());
        quiver.Attributes.CastingFocus = 6;
        quiver.Attributes.DamageEater = 10;
        var quiverOpl = DecodeOpl(quiver);
        Assert.Contains(quiverOpl, e => e.Cliloc == 1113696); // Casting Focus
        Assert.Contains(quiverOpl, e => e.Cliloc == 1113598); // Damage Eater
        Assert.Contains(quiverOpl, e => e.Cliloc is >= 1151488 and <= 1151495);

        var talisman = _world.TrackItem(new BaseTalisman());
        talisman.Attributes.Luck = 100;
        talisman.Attributes.LowerManaCost = 8;
        var talismanOpl = DecodeOpl(talisman);
        Assert.Contains(talismanOpl, e => e.Cliloc is >= 1151488 and <= 1151495);

        // 4) DefInscription crafts all school spellbooks with MysticSpellbook maxSkill 120, and OnCraft scales with school skill.
        var mysticRecipe = DefInscription.CraftSystem.CraftItems.SearchFor(typeof(MysticSpellbook));
        Assert.NotNull(mysticRecipe);
        Assert.Equal(120.0, mysticRecipe.Skills[0].MaxSkill);
        Assert.NotNull(DefInscription.CraftSystem.CraftItems.SearchFor(typeof(NecromancerSpellbook)));
        Assert.NotNull(DefInscription.CraftSystem.CraftItems.SearchFor(typeof(BookOfChivalry)));
        Assert.NotNull(DefInscription.CraftSystem.CraftItems.SearchFor(typeof(BookOfBushido)));
        Assert.NotNull(DefInscription.CraftSystem.CraftItems.SearchFor(typeof(BookOfNinjitsu)));
        Assert.NotNull(DefInscription.CraftSystem.CraftItems.SearchFor(typeof(SpellweavingBook)));

        var mystic = CreatePlayer(1560, 1560);
        mystic.Skills.Mysticism.Cap = 120.0;
        mystic.Skills.Mysticism.Base = 120.0;
        mystic.Skills.Magery.Base = 0.0;
        mystic.Skills.Inscribe.Base = 80.0;

        var mysticBook = _world.TrackItem(new MysticSpellbook());
        mysticBook.OnCraft(
            2,
            true,
            mystic,
            DefInscription.CraftSystem,
            typeof(BlankScroll),
            null,
            mysticRecipe,
            0
        );
        Assert.True(ItemRarity.ComputeItemWeight(mysticBook) > 0);
        Assert.True(mysticBook.Attributes.CastingFocus > 0 || mysticBook.Attributes.SpellDamage > 0);

        // 5) Multi-school looted spellbooks keep spell bits strictly within BookCount.
        var spellEntry = new LootPackEntry(false, [new LootPackItem(typeof(Spellbook), 1)], 100.0, 1, 4, 40, 80);
        var sawNonMageryBook = false;
        for (var i = 0; i < 40; i++)
        {
            var dropped = _world.TrackItem(spellEntry.Construct(mystic, 0, false));
            var sb = Assert.IsType<Spellbook>(dropped, exactMatch: false);
            if (sb.GetType() != typeof(Spellbook))
            {
                sawNonMageryBook = true;
            }

            if (sb.BookCount < 64)
            {
                var validMask = (1UL << sb.BookCount) - 1;
                Assert.Equal(0UL, sb.Content & ~validMask);
            }
        }

        Assert.True(sawNonMageryBook);

        // 6) ToTThree NormalRewards[2] exposes PigmentsOfTokuno at Ihara Soko.
        Assert.Contains(Server.Gumps.ToTRedeemGump.NormalRewards[2], r => r.Type == typeof(PigmentsOfTokuno));
    }
}
