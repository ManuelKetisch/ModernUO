using System;
using System.Reflection;
using Server;
using Server.Commands;
using Server.Engines.HybridForge;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public sealed class HybridForgeTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void PolehammerFromNormalTargetsRetainsOnlyBaseWeaponProgress()
    {
        const int halberdId = 0x143E;
        var oldTile = TileData.ItemTable[halberdId];
        TileData.ItemTable[halberdId] = new ItemData("halberd", TileFlag.Weapon, 16, (int)Layer.TwoHanded, 0, 0, 0, 0);
        try
        {
            var smith = CreateSmith();
            Put(smith, new WyrmForgeBlueprint()).OnDoubleClick(smith);
            var source = Put(smith, new Halberd { PlayerConstructed = true });
            var donor = Put(smith, new WarHammer());
            var scale = Put(smith, new AncientWyrmScale(2));
            Assert.True(source.TryEnableWeaponLeveling(smith));
            Assert.True(donor.TryEnableWeaponLeveling(smith));
            source.AwardWeaponExperience(9900, smith);
            donor.AwardWeaponExperience(2600, smith);
            source.Attributes.BonusStr = 6;
            source.MaxHitPoints = 125;
            source.HitPoints = 88;
            source.UsesRemaining = 105;
            source.ShowUsesRemaining = true;

            using var net = PacketTestUtilities.CreateTestNetState();
            net.Account = new MockAccount();
            net.Mobile = smith;
            smith.NetState = net;
            try
            {
                HybridForge.Initialize();
                Assert.True(CommandSystem.Handle(smith, "[HybridForge"));
                var open = Assert.IsType<HybridForgeMenu>(smith.FindGump<HybridForgeMenu>());
                open.OnResponse(net, new RelayInfo(1, [], [], [], []));
                smith.Target.Invoke(smith, source);
                smith.Target.Invoke(smith, donor);
                smith.Target.Invoke(smith, scale);
                var original = Assert.IsType<HybridForgePreview>(smith.FindGump<HybridForgePreview>());

                source.Attributes.BonusStr = 9;
                original.OnResponse(net, new RelayInfo(1, [], [], [], []));
                Assert.False(source.Deleted);
                var refreshed = Assert.IsType<HybridForgePreview>(smith.FindGump<HybridForgePreview>());
                Assert.NotSame(original, refreshed);

                smith.Backpack.MaxItems = smith.Backpack.TotalItems;
                refreshed.OnResponse(net, new RelayInfo(1, [], [], [], []));
                Assert.False(source.Deleted);
                Assert.False(donor.Deleted);
                Assert.Equal(2, scale.Amount);

                smith.Backpack.MaxItems = 125;
                refreshed.OnResponse(net, new RelayInfo(1, [], [], [], []));
                refreshed.OnResponse(net, new RelayInfo(1, [], [], [], []));
            }
            finally
            {
                smith.CloseGump<HybridForgePreview>();
                smith.CloseGump<HybridForgeMenu>();
                smith.NetState = null;
                net.Mobile = null;
            }

            Assert.True(source.Deleted);
            Assert.True(donor.Deleted);
            Assert.Equal(1, scale.Amount);
            var result = Assert.IsType<Polehammer>(smith.Backpack.FindItemByType<Polehammer>());
            Assert.Equal(source.CaptureWeaponLeveling(), result.CaptureWeaponLeveling());
            Assert.Equal(Layer.TwoHanded, result.Layer);
            Assert.Equal(source.Skill, result.Skill);
            Assert.Equal(source.MinDamage, result.MinDamage);
            Assert.Equal(source.MaxDamage, result.MaxDamage);
            Assert.Equal(source.Speed, result.Speed);
            Assert.Equal(source.Weight, result.Weight);
            Assert.Equal(source.StrRequirement, result.StrRequirement);
            Assert.Equal(105, result.UsesRemaining);
            Assert.True(result.ShowUsesRemaining);

            var flip = Assert.IsType<FlippableAttribute>(typeof(Polehammer).GetCustomAttribute<FlippableAttribute>(false));
            Assert.Equal(0x1439, result.ItemID);
            Assert.Same(WeaponAbility.WhirlwindAttack, result.PrimaryAbility);
            Assert.Same(WeaponAbility.CrushingBlow, result.SecondaryAbility);
            flip.Flip(result);
            Assert.Equal(0x1438, result.ItemID);
            Assert.Same(WeaponAbility.WhirlwindAttack, result.PrimaryAbility);
            Assert.Same(WeaponAbility.CrushingBlow, result.SecondaryAbility);
            Assert.Equal(9, result.Attributes.BonusStr);
            Assert.Equal(88, result.HitPoints);
            Assert.Equal(125, result.MaxHitPoints);

            var copy = _world.TrackItem(new Polehammer(World.NewItem));
            Reload(result, copy);
            Assert.Equal(result.CaptureWeaponLeveling(), copy.CaptureWeaponLeveling());
            Assert.Equal(0x1438, copy.ItemID);
            Assert.Same(WeaponAbility.CrushingBlow, copy.SecondaryAbility);
        }
        finally
        {
            TileData.ItemTable[halberdId] = oldTile;
        }
    }

    [Fact]
    public void ScaleLinedPlateMovesOnlyThreePointsAndRejectsSecondConversion()
    {
        const int plateId = 0x1415;
        var oldTile = TileData.ItemTable[plateId];
        TileData.ItemTable[plateId] = new ItemData("plate chest", TileFlag.Armor, 10, (int)Layer.InnerTorso, 0, 0, 0, 0);
        try
        {
            var smith = CreateSmith();
            Put(smith, new WyrmForgeBlueprint(true)).OnDoubleClick(smith);
            var source = Put(smith, new PlateChest { Resource = CraftResource.Bronze, ColdBonus = 7, Crafter = "Smith" });
            source.ArmorAttributes.SelfRepair = 2;
            source.MaxHitPoints = 90;
            source.HitPoints = 39;
            var donor = Put(smith, new RedScales(12));
            var scale = Put(smith, new AncientWyrmScale(2));
            var quote = HybridForge.TryQuote(smith, HybridForgeRecipe.ScaleLinedPlate, source, donor, scale);
            Assert.NotNull(quote);

            var plate = Assert.IsType<ScaleLinedPlateChest>(HybridForge.Complete(smith, quote));
            Assert.Equal(2, donor.Amount);
            Assert.Equal(1, scale.Amount);
            Assert.True(source.Deleted);
            Assert.Equal(source.PhysicalResistance - 3, plate.PhysicalResistance);
            Assert.Equal(source.FireResistance + 3, plate.FireResistance);
            Assert.Equal(source.ColdResistance, plate.ColdResistance);
            Assert.Equal(source.PoisonResistance, plate.PoisonResistance);
            Assert.Equal(source.EnergyResistance, plate.EnergyResistance);
            Assert.Equal(source.Resource, plate.Resource);
            Assert.Equal("Smith", plate.Crafter);
            Assert.Equal(2, plate.ArmorAttributes.SelfRepair);
            Assert.Equal(39, plate.HitPoints);
            Assert.Equal(90, plate.MaxHitPoints);
            donor.Amount = 12;
            Assert.Null(HybridForge.TryQuote(smith, HybridForgeRecipe.ScaleLinedPlate, plate, donor, scale));

            var copy = _world.TrackItem(new ScaleLinedPlateChest(World.NewItem));
            Reload(plate, copy);
            Assert.Equal(plate.FireResistance, copy.FireResistance);
            Assert.Equal(plate.PhysicalResistance, copy.PhysicalResistance);
            Assert.Equal(plate.ArmorAttributes.SelfRepair, copy.ArmorAttributes.SelfRepair);
        }
        finally
        {
            TileData.ItemTable[plateId] = oldTile;
        }
    }

    [Fact]
    public void ScaleLinedPlateLoadsExistingOrientationsWithOrWithoutTheArtProfile()
    {
        const int stock = ScaleLinedPlateChest.StockGraphic;
        const int stockAlternate = ScaleLinedPlateChest.StockAlternateGraphic;
        const int custom = ScaleLinedPlateChest.CustomGraphic;
        const int customAlternate = ScaleLinedPlateChest.CustomAlternateGraphic;
        var originalStock = TileData.ItemTable[stock];
        var originalCustom = TileData.ItemTable[custom];
        var originalCustomAlternate = TileData.ItemTable[customAlternate];
        var stockTile = new ItemData("plate chest", TileFlag.Wearable, 10, (int)Layer.InnerTorso, 131, 0, 0, 0);
        try
        {
            TileData.ItemTable[stock] = stockTile;
            TileData.ItemTable[custom] = default;
            TileData.ItemTable[customAlternate] = default;

            var plate = _world.TrackItem(new ScaleLinedPlateChest());
            Assert.Equal(stock, plate.ItemID);
            var flip = Assert.IsType<FlippableAttribute>(plate.GetType().GetCustomAttribute<FlippableAttribute>(false));
            flip.Flip(plate);
            Assert.Equal(stockAlternate, plate.ItemID);

            var artTile = new ItemData(
                ScaleLinedPlateChest.CustomTileName, TileFlag.Wearable | TileFlag.PartialHue,
                10, (int)Layer.InnerTorso, 131, 0, 0, 0
            );
            TileData.ItemTable[custom] = artTile;
            Assert.Equal(stock, _world.TrackItem(new ScaleLinedPlateChest()).ItemID);
            TileData.ItemTable[customAlternate] = artTile;
            Assert.Equal(custom, _world.TrackItem(new ScaleLinedPlateChest()).ItemID);
            Assert.Equal(stock, _world.TrackItem(new PlateChest()).ItemID);

            var upgraded = _world.TrackItem(new ScaleLinedPlateChest(World.NewItem));
            Reload(plate, upgraded);
            Assert.Equal(customAlternate, upgraded.ItemID);
            Assert.Equal(Layer.InnerTorso, upgraded.Layer);
            flip.Flip(upgraded);
            Assert.Equal(custom, upgraded.ItemID);

            TileData.ItemTable[customAlternate] = default;
            var fallback = _world.TrackItem(new ScaleLinedPlateChest(World.NewItem));
            Reload(upgraded, fallback);
            Assert.Equal(stock, fallback.ItemID);
            Assert.Equal(upgraded.Resource, fallback.Resource);
            Assert.Equal(upgraded.HitPoints, fallback.HitPoints);
        }
        finally
        {
            TileData.ItemTable[stock] = originalStock;
            TileData.ItemTable[custom] = originalCustom;
            TileData.ItemTable[customAlternate] = originalCustomAlternate;
        }
    }

    private PlayerMobile CreateSmith()
    {
        var smith = _world.NewPlayer();
        var spot = new Point3D(1000, 1000, 0);
        smith.MoveToWorld(spot, Map.Felucca);
        smith.AddItem(_world.TrackItem(new Backpack()));
        smith.Skills.Blacksmith.Base = 100;
        _world.TrackItem(new Item(4015)).MoveToWorld(spot, Map.Felucca);
        _world.TrackItem(new Item(4017)).MoveToWorld(spot, Map.Felucca);
        return smith;
    }

    private T Put<T>(PlayerMobile owner, T item) where T : Item
    {
        owner.Backpack.DropItem(_world.TrackItem(item));
        return item;
    }

    private static void Reload(Item source, Item copy)
    {
        var writer = new BufferWriter(true);
        source.Serialize(writer);
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        copy.Deserialize(reader);
        Assert.Equal(writer.Position, reader.Position);
    }
}
