using System;
using Server;
using Server.Engines.WeaponLevelingServices;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public sealed class WeaponLevelingServiceTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void ChangeDeedWaivesArtifactTagBeforeActivationAndAcrossReload()
    {
        var player = CreatePlayer();
        var artifact = Put(player, new BladeOfInsanity());
        var deed = Put(player, new WeaponChangeDeed());
        Assert.True(artifact.HasWeaponLevelingArtifactTag);

        WithConnection(player, state =>
        {
            deed.OnDoubleClick(player);
            player.Target.Invoke(player, artifact);
            var preview = player.FindGump<WeaponServiceGump>();
            Assert.NotNull(preview);
            Assert.False(deed.Deleted);
            preview.OnResponse(state, new RelayInfo(1, [], [], [], []));
        });

        Assert.True(deed.Deleted);
        Assert.False(artifact.Deleted);
        Assert.False(artifact.HasWeaponLevelingArtifactTag);
        Assert.False(artifact.IsWeaponLevelingEnabled);
        Assert.Equal(50, artifact.Attributes.WeaponDamage);

        var copy = _world.TrackItem(new BladeOfInsanity(World.NewItem));
        Reload(artifact, copy);
        player.Backpack.DropItem(copy);
        Assert.False(copy.HasWeaponLevelingArtifactTag);
        Assert.True(copy.TryEnableWeaponLeveling(player));
        Assert.Equal(1, copy.WeaponLevelingCostMultiplier);
    }

    [Fact]
    public void ChangingTypeKeepsProgressAndPropertiesUsesNewTypeAndConsumesTemplates()
    {
        var player = CreatePlayer();
        var source = Put(player, new BladeOfInsanity { Layer = Layer.TwoHanded });
        var deed = Put(player, new WeaponChangeDeed());
        Assert.True(source.TryEnableWeaponLeveling(player));
        source.AwardWeaponExperience(9900, player);
        source.Attributes.BonusStr = 6;
        source.Slayer = SlayerName.Silver;
        source.MaxHitPoints = 155;
        source.HitPoints = 91;

        const int longswordID = 0xF61;
        var originalTile = TileData.ItemTable[longswordID];
        TileData.ItemTable[longswordID] = new ItemData("longsword", TileFlag.Weapon, 7, (int)Layer.OneHanded, 0, 0, 0, 0);
        try
        {
            var template = Put(player, new Longsword());

            WithConnection(player, state =>
            {
                deed.OnDoubleClick(player);
                player.Target.Invoke(player, source);
                var choice = player.FindGump<WeaponServiceGump>();
                Assert.NotNull(choice);
                choice.OnResponse(state, new RelayInfo(2, [], [], [], []));
                player.Target.Invoke(player, template);
                var preview = player.FindGump<WeaponServiceGump>();
                Assert.NotNull(preview);
                Assert.NotSame(choice, preview);
                Assert.False(template.Deleted);

                player.Backpack.MaxItems = player.Backpack.TotalItems;
                preview.OnResponse(state, new RelayInfo(1, [], [], [], []));
                Assert.False(template.Deleted);
                Assert.False(source.Deleted);
                Assert.False(deed.Deleted);

                player.Backpack.MaxItems = 125;
                preview.OnResponse(state, new RelayInfo(1, [], [], [], []));
            });

            Assert.True(deed.Deleted);
            Assert.True(source.Deleted);
            Assert.True(template.Deleted);
            var replacement = _world.TrackItem(Assert.IsType<Longsword>(Assert.Single(player.Backpack.Items)));
            Assert.Equal(Layer.OneHanded, replacement.Layer);
            Assert.Null(replacement.Name);
            Assert.Equal(template.LabelNumber, replacement.LabelNumber);
            Assert.Equal(10, replacement.WeaponLevel);
            Assert.Equal(9900, replacement.WeaponExperience);
            Assert.Equal(1, replacement.WeaponLevelingCostMultiplier);
            Assert.Equal(6, replacement.Attributes.BonusStr);
            Assert.Equal(50, replacement.Attributes.WeaponDamage);
            Assert.Equal(100, replacement.WeaponAttributes.HitLeechStam);
            Assert.Equal(SlayerName.Silver, replacement.Slayer);
            Assert.Equal(91, replacement.HitPoints);
            Assert.Equal(155, replacement.MaxHitPoints);
        }
        finally
        {
            TileData.ItemTable[longswordID] = originalTile;
        }
    }

    [Fact]
    public void ChangingRangedTypePreservesBalancedAndVelocityAndRejectsAnOldQuote()
    {
        var player = CreatePlayer();
        var source = Put(player, new Bow { Balanced = true, Velocity = 25 });
        var template = Put(player, new Crossbow());
        var deed = Put(player, new WeaponChangeDeed());
        Assert.True(source.TryEnableWeaponLeveling(player));

        WithConnection(player, state =>
        {
            deed.OnDoubleClick(player);
            Assert.NotNull(player.Target);
            player.Target.Invoke(player, source);
            Assert.NotNull(player.Target);
            player.Target.Invoke(player, template);
            var preview = player.FindGump<WeaponServiceGump>();
            Assert.NotNull(preview);

            source.Velocity = 30;
            preview.OnResponse(state, new RelayInfo(1, [], [], [], []));
            Assert.False(source.Deleted);
            Assert.False(template.Deleted);
            Assert.False(deed.Deleted);

            deed.OnDoubleClick(player);
            player.Target.Invoke(player, source);
            player.Target.Invoke(player, template);
            var refreshed = player.FindGump<WeaponServiceGump>();
            Assert.NotNull(refreshed);
            Assert.NotSame(preview, refreshed);
            refreshed.OnResponse(state, new RelayInfo(1, [], [], [], []));
        });

        Assert.True(source.Deleted);
        Assert.True(template.Deleted);
        Assert.True(deed.Deleted);
        var result = _world.TrackItem(Assert.IsType<Crossbow>(Assert.Single(player.Backpack.Items)));
        Assert.True(result.Balanced);
        Assert.Equal(30, result.Velocity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangingTypePreservesNativeCombatBonusesOrRejectsAnIncompatibleIncrease(bool higherDestinationDamage)
    {
        const int longswordID = 0xF61;
        const int tribalSpearID = 0xF62;
        var originalSwordTile = TileData.ItemTable[longswordID];
        var originalSpearTile = TileData.ItemTable[tribalSpearID];
        TileData.ItemTable[longswordID] = new ItemData("longsword", TileFlag.Weapon, 7, (int)Layer.OneHanded, 0, 0, 0, 0);
        TileData.ItemTable[tribalSpearID] = new ItemData("tribal spear", TileFlag.Weapon, 7,
            (int)Layer.TwoHanded, 0, 0, 0, 0);
        try
        {
            var player = CreatePlayer();
            BaseWeapon source = higherDestinationDamage ? new Longsword() : new TribalSpear();
            BaseWeapon template = higherDestinationDamage ? new TribalSpear() : new Longsword();
            Put(player, source);
            Put(player, template);
            var deed = Put(player, new WeaponChangeDeed());

            Assert.True(source.TryEnableWeaponLeveling(player));
            source.AccuracyLevel = WeaponAccuracyLevel.Supremely;
            source.Attributes.AttackChance = 12;
            source.Attributes.WeaponDamage = 17;

            WithConnection(player, state =>
            {
                deed.OnDoubleClick(player);
                Assert.NotNull(player.Target);
                player.Target.Invoke(player, source);
                Assert.NotNull(player.Target);
                player.Target.Invoke(player, template);
                var preview = player.FindGump<WeaponServiceGump>();
                Assert.NotNull(preview);
                preview.OnResponse(state, new RelayInfo(1, [], [], [], []));
            });

            if (higherDestinationDamage)
            {
                Assert.False(source.Deleted);
                Assert.False(template.Deleted);
                Assert.False(deed.Deleted);
                return;
            }

            Assert.True(source.Deleted);
            Assert.True(template.Deleted);
            Assert.True(deed.Deleted);
            var result = _world.TrackItem(Assert.IsType<Longsword>(Assert.Single(player.Backpack.Items)));
            Assert.Equal(12, result.Attributes.AttackChance);
            Assert.Equal(10, result.GetHitChanceBonus());
            Assert.Equal(42, result.Attributes.WeaponDamage);
            Assert.Equal(0, result.GetDamageBonus());
        }
        finally
        {
            TileData.ItemTable[longswordID] = originalSwordTile;
            TileData.ItemTable[tribalSpearID] = originalSpearTile;
        }
    }

    [Fact]
    public void RelayerChecksTheDisplayedWeaponStateAndCarriesOnlyWearableProperties()
    {
        var player = CreatePlayer();
        var source = Put(player, new TribalSpear());
        var template = Put(player, new Shirt());
        var deed = Put(player, new WeaponRelayerDeed());
        Assert.True(source.TryEnableWeaponLeveling(player));
        source.Attributes.BonusStr = 4;
        source.AccuracyLevel = WeaponAccuracyLevel.Supremely;
        source.Attributes.AttackChance = 12;
        source.Attributes.WeaponDamage = 17;
        source.WeaponAttributes.ResistColdBonus = 8;
        source.WeaponAttributes.HitLightning = 32;
        source.SkillBonuses.SetValues(0, SkillName.Alchemy, 3.0);

        WithConnection(player, state =>
        {
            var preview = SelectRelayerTemplate(player, deed, source, template);
            source.Attributes.BonusStr = 9;
            preview.OnResponse(state, new RelayInfo(1, [], [], [], []));
            Assert.False(source.Deleted);
            Assert.False(template.Deleted);
            Assert.False(deed.Deleted);

            preview = SelectRelayerTemplate(player, deed, source, template);
            preview.OnResponse(state, new RelayInfo(1, [], [], [], []));
        });

        Assert.True(source.Deleted);
        Assert.True(template.Deleted);
        Assert.True(deed.Deleted);
        var shirt = _world.TrackItem(Assert.IsType<Shirt>(Assert.Single(player.Backpack.Items)));
        Assert.Equal(Layer.Shirt, shirt.Layer);
        Assert.Equal(9, shirt.Attributes.BonusStr);
        Assert.Equal(22, shirt.Attributes.AttackChance);
        Assert.Equal(42, shirt.Attributes.WeaponDamage);
        Assert.Equal(8, shirt.Resistances.Cold);
        Assert.True(shirt.SkillBonuses.GetValues(0, out var skill, out var bonus));
        Assert.Equal(SkillName.Alchemy, skill);
        Assert.Equal(3.0, bonus);
    }

    [Theory]
    [InlineData(CraftResource.Gold, true, 15, 51, 65)]
    [InlineData(CraftResource.DullCopper, false, 80, 11, 100)]
    public void RelayerPreservesEffectiveLuckAndRequirementsAfterAMaterialChange(
        CraftResource material, bool toArmor, int addedRequirements, int expectedLuck, int expectedRequirements
    )
    {
        var player = CreatePlayer();
        var source = Put(player, new Longsword());
        var deed = Put(player, new WeaponRelayerDeed());
        Assert.True(source.TryEnableWeaponLeveling(player));
        source.Attributes.Luck = 11;
        source.WeaponAttributes.LowerStatReq = addedRequirements;

        const int plateChestID = 0x1415;
        var originalTile = TileData.ItemTable[plateChestID];
        TileData.ItemTable[plateChestID] = new ItemData("platemail chest", TileFlag.Armor, 10,
            (int)Layer.InnerTorso, 0, 0, 0, 0);
        try
        {
            Item template = toArmor ? new PlateChest() : new Shirt();
            Put(player, template);

            WithConnection(player, state =>
            {
                var preview = SelectRelayerTemplate(player, deed, source, template);
                var oldHue = source.Hue;
                var oldMaximum = source.MaxHitPoints;
                var oldHits = source.HitPoints;
                source.Resource = material;
                source.Hue = oldHue;
                source.MaxHitPoints = oldMaximum;
                source.HitPoints = oldHits;

                preview.OnResponse(state, new RelayInfo(1, [], [], [], []));
                Assert.False(source.Deleted);
                Assert.False(template.Deleted);
                Assert.False(deed.Deleted);

                preview = SelectRelayerTemplate(player, deed, source, template);
                preview.OnResponse(state, new RelayInfo(1, [], [], [], []));
            });

            Assert.True(source.Deleted);
            Assert.True(template.Deleted);
            Assert.True(deed.Deleted);
            var replacement = _world.TrackItem(Assert.Single(player.Backpack.Items));
            if (toArmor)
            {
                var armor = Assert.IsType<PlateChest>(replacement);
                Assert.Equal(expectedLuck, armor.Attributes.Luck + armor.GetLuckBonus());
                Assert.Equal(expectedRequirements, armor.GetLowerStatReq());
            }
            else
            {
                var shirt = Assert.IsType<Shirt>(replacement);
                Assert.Equal(expectedLuck, shirt.Attributes.Luck);
                Assert.Equal(expectedRequirements, shirt.GetLowerStatReq());
            }
        }
        finally
        {
            TileData.ItemTable[plateChestID] = originalTile;
        }
    }

    [Fact]
    public void OneHandedRelayerPreservesTheMeleeWeaponAndPersistsItsLayer()
    {
        var player = CreatePlayer();
        var source = Put(player, new Halberd { Layer = Layer.TwoHanded });
        var template = Put(player, new Longsword { Layer = Layer.OneHanded });
        var deed = Put(player, new WeaponRelayerDeed());
        Assert.True(source.TryEnableWeaponLeveling(player));
        source.AwardWeaponExperience(2400, player);
        var progress = source.CaptureWeaponLeveling();

        WithConnection(player, state =>
        {
            var preview = SelectRelayerTemplate(player, deed, source, template);
            preview.OnResponse(state, new RelayInfo(1, [], [], [], []));
        });

        Assert.True(template.Deleted);
        Assert.True(deed.Deleted);
        Assert.False(source.Deleted);
        Assert.Same(source, Assert.Single(player.Backpack.Items));
        Assert.Equal(Layer.OneHanded, source.Layer);
        Assert.Equal(progress, source.CaptureWeaponLeveling());

        var copy = _world.TrackItem(new Halberd(World.NewItem));
        Reload(source, copy);
        Assert.Equal(Layer.OneHanded, copy.Layer);
        Assert.Equal(progress, copy.CaptureWeaponLeveling());
    }

    [Fact]
    public void RelayerRejectsProtectedOrEnchantedEquipment()
    {
        var player = CreatePlayer();
        var source = Put(player, new Longsword());
        var template = Put(player, new Shirt());
        Assert.True(source.TryEnableWeaponLeveling(player));

        source.Insured = true;
        Assert.False(WeaponServiceTransfer.CanPerform(player, source, template, WeaponServiceOperation.Relayer));
        source.Insured = false;
        template.Attributes.Luck = 10;
        Assert.False(WeaponServiceTransfer.CanPerform(player, source, template, WeaponServiceOperation.Relayer));
        Assert.False(source.Deleted);
        Assert.False(template.Deleted);
    }

    private static WeaponServiceGump SelectRelayerTemplate(
        PlayerMobile player, WeaponRelayerDeed deed, BaseWeapon source, Item template
    )
    {
        deed.OnDoubleClick(player);
        Assert.NotNull(player.Target);
        player.Target.Invoke(player, source);
        Assert.NotNull(player.Target);
        player.Target.Invoke(player, template);
        var preview = player.FindGump<WeaponServiceGump>();
        Assert.NotNull(preview);
        return preview;
    }

    private static void Reload(Item source, Item copy)
    {
        var writer = new BufferWriter(true);
        source.Serialize(writer);
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        copy.Deserialize(reader);
        Assert.Equal(writer.Position, reader.Position);
    }

    private PlayerMobile CreatePlayer()
    {
        var player = _world.NewPlayer();
        player.Race = Race.Human;
        player.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);
        player.AddItem(_world.TrackItem(new Backpack()));
        return player;
    }

    private T Put<T>(PlayerMobile owner, T item) where T : Item
    {
        owner.Backpack.DropItem(_world.TrackItem(item));
        return item;
    }

    private static void WithConnection(PlayerMobile player, Action<NetState> action)
    {
        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = player;
        player.NetState = state;
        try
        {
            action(state);
        }
        finally
        {
            player.CloseGump<WeaponServiceGump>();
            player.NetState = null;
            state.Mobile = null;
        }
    }
}
