using System;
using Server;
using Server.Engines.Harvest;
using Server.Engines.Metallurgy;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;
using Server.Tests;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public sealed class MetallurgyTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void NormalMenuMakesOneAlloyAndOneFiniteMiningPickaxe()
    {
        var smith = CreateSmith();
        var fragment = Put(smith, new MeteoriteFragment { Amount = 2 });
        var iron = Put(smith, new IronIngot(11));
        var bronze = Put(smith, new BronzeIngot(8));

        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = smith;
        smith.NetState = state;
        try
        {
            Metallurgy.Initialize();
            Assert.True(CommandSystem.Handle(smith, "[Metallurgy"));
            Assert.IsType<MetallurgyGump>(smith.FindGump<MetallurgyGump>())
                .OnResponse(state, new RelayInfo((int)MetallurgyRecipe.MeteoricBronze, [], [], [], []));

            Assert.Equal(1, fragment.Amount);
            Assert.Equal(3, iron.Amount);
            Assert.Equal(2, bronze.Amount);
            var alloy = Assert.IsType<MeteoricBronzeIngot>(smith.Backpack.FindItemByType<MeteoricBronzeIngot>());

            var menu = Assert.IsType<MetallurgyGump>(smith.FindGump<MetallurgyGump>());
            var craftPick = new RelayInfo((int)MetallurgyRecipe.MeteoricPickaxe, [], [], [], []);
            menu.OnResponse(state, craftPick);
            menu.OnResponse(state, craftPick);
            Assert.True(alloy.Deleted);
        }
        finally
        {
            smith.CloseGump<MetallurgyGump>();
            smith.NetState = null;
            state.Mobile = null;
        }

        var pickaxe = Assert.IsType<MeteoricPickaxe>(smith.Backpack.FindItemByType<MeteoricPickaxe>());
        Assert.Single(smith.Backpack.Items, item => item is MeteoricPickaxe);
        Assert.Equal(240, pickaxe.UsesRemaining);
        Assert.Equal(5.0, pickaxe.Weight);
        Assert.Same(Mining.System, pickaxe.HarvestSystem);
        Assert.True(pickaxe.ShowUsesRemaining);

        pickaxe.UsesRemaining -= 37;
        var copy = _world.TrackItem(new MeteoricPickaxe(World.NewItem));
        Reload(pickaxe, copy);
        Assert.Equal(203, copy.UsesRemaining);
        Assert.Equal(5.0, copy.Weight);
    }

    [Fact]
    public void RejectedRefinesKeepAllInputs()
    {
        var smith = CreateSmith();
        var fragment = Put(smith, new MeteoriteFragment());
        var iron = Put(smith, new IronIngot(8));
        var bronze = Put(smith, new BronzeIngot(5));

        Assert.False(Metallurgy.TryCraft(smith, MetallurgyRecipe.MeteoricBronze));
        bronze.Amount = 6;
        smith.Skills.Blacksmith.Base = 79.9;
        Assert.False(Metallurgy.TryCraft(smith, MetallurgyRecipe.MeteoricBronze));
        smith.Skills.Blacksmith.Base = 80;

        var bench = smith.Location;
        smith.MoveToWorld(new Point3D(bench.X + 20, bench.Y, bench.Z), smith.Map);
        Assert.False(Metallurgy.TryCraft(smith, MetallurgyRecipe.MeteoricBronze));
        smith.MoveToWorld(bench, smith.Map);

        smith.Backpack.MaxItems = smith.Backpack.TotalItems;
        Assert.False(Metallurgy.TryCraft(smith, MetallurgyRecipe.MeteoricBronze));
        Assert.False(fragment.Deleted);
        Assert.Equal(8, iron.Amount);
        Assert.Equal(6, bronze.Amount);
        Assert.Null(smith.Backpack.FindItemByType<MeteoricBronzeIngot>());

        var stockpile = Put(smith, new MeteoricBronzeIngot());
        smith.Backpack.MaxItems = smith.Backpack.TotalItems;
        Assert.False(Metallurgy.TryCraft(smith, MetallurgyRecipe.MeteoricBronze));
        Assert.Equal(1, stockpile.Amount);
        Assert.False(fragment.Deleted);
        Assert.Equal(8, iron.Amount);
        Assert.Equal(6, bronze.Amount);

        smith.Backpack.MaxItems++;
        Assert.True(Metallurgy.TryCraft(smith, MetallurgyRecipe.MeteoricBronze));
        Assert.Equal(2, stockpile.Amount);
        Assert.True(fragment.Deleted);
        Assert.True(iron.Deleted);
        Assert.True(bronze.Deleted);
    }

    [Fact]
    public void MerchantMapKeepsItsPinAndRejectsWrongFacetAndOtherOwners()
    {
        var smith = CreateSmith();
        var stranger = CreateSmith();
        var miner = Assert.Single(new SBMiner().BuyInfo, item => item.Type == typeof(MinocProspectingMap));
        var blacksmith = Assert.Single(new SBBlacksmith().BuyInfo, item => item.Type == typeof(MinocProspectingMap));
        Assert.Equal("Minoc prospecting map", miner.Name);
        Assert.Equal(200, blacksmith.Price);
        var map = Put(smith, new MinocProspectingMap());

        var copy = _world.TrackItem(new MinocProspectingMap(World.NewItem));
        Reload(map, copy);
        Assert.Same(Map.Trammel, copy.Facet);
        Assert.True(copy.Protected);
        var pin = Assert.Single(copy.Pins);
        copy.ConvertToWorld(pin.X, pin.Y, out var x, out var y);
        Assert.Equal(2558, x);
        Assert.Equal(499, y);

        var shovel = Put(smith, new Shovel(9));
        smith.Skills.Mining.Base = 65;
        smith.MoveToWorld(MinocProspectingMap.SurveySite, Map.Felucca);
        var target = new StaticTarget(MinocProspectingMap.SurveySite, 0x53B);
        Assert.False(map.TryProspect(smith, target));
        stranger.MoveToWorld(MinocProspectingMap.SurveySite, Map.Trammel);
        Assert.False(map.TryProspect(stranger, target));
        Assert.Equal(9, shovel.UsesRemaining);
        Assert.Null(smith.Backpack.FindItemByType<MeteoriteFragment>());
        Assert.False(map.Deleted);
    }

    private PlayerMobile CreateSmith()
    {
        var smith = _world.Track(new PlayerMobile { Player = true, Race = Race.Human });
        smith.AddItem(_world.TrackItem(new Backpack()));
        smith.MoveToWorld(new Point3D(1800, 1800, 0), Map.Felucca);
        smith.Skills.Blacksmith.Base = 80;
        _world.TrackItem(new Anvil()).MoveToWorld(smith.Location, smith.Map);
        _world.TrackItem(new Forge()).MoveToWorld(smith.Location, smith.Map);
        return smith;
    }

    private T Put<T>(PlayerMobile smith, T item) where T : Item
    {
        smith.Backpack.DropItem(_world.TrackItem(item));
        return item;
    }

    private static void Reload(Item original, Item copy)
    {
        var writer = new BufferWriter(true);
        original.Serialize(writer);
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        copy.Deserialize(reader);
        Assert.Equal(writer.Position, reader.Position);
    }
}
