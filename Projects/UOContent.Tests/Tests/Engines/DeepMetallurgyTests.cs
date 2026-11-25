using System;
using Server;
using Server.Engines.Harvest;
using Server.Engines.Metallurgy;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public sealed class DeepMetallurgyTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void SeamHonorsMiningOwnershipCapacityAndPersistedRefill()
    {
        var site = DeepOreSeam.FireSite;
        var miner = NewPlayer(new Point3D(site.X, site.Y, 0), Map.Trammel);
        var seam = _world.TrackItem(new DeepOreSeam());
        seam.MoveToWorld(miner.Location, miner.Map);
        var tool = Put(miner, new Pickaxe());
        var stranger = NewPlayer(miner.Location, miner.Map);
        var otherTool = Put(stranger, new Shovel(9));
        var now = new DateTime(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

        miner.Skills.Mining.Base = DeepOreSeam.GlassMining - 0.1;
        Assert.False(seam.TryMine(miner, tool, now));
        miner.Skills.Mining.Base = DeepOreSeam.GlassMining;
        Assert.False(seam.TryMine(miner, otherTool, now));
        miner.Backpack.MaxItems = miner.Backpack.TotalItems;
        Assert.False(seam.TryMine(miner, tool, now));
        Assert.Equal(50, tool.UsesRemaining);
        Assert.Equal(DeepOreSeam.Capacity, seam.Available(now));
        miner.Backpack.MaxItems = 125;

        Assert.True(miner.BeginAction(Mining.System));
        try
        {
            tool.OnDoubleClick(miner);
            Assert.NotNull(miner.Target);
            miner.Target.Invoke(miner, seam);
            Assert.Equal(50, tool.UsesRemaining);
            Assert.Equal(DeepOreSeam.Capacity, seam.Available(now));
            Assert.Null(miner.Backpack.FindItemByType<VolcanicGlass>());
        }
        finally
        {
            miner.EndAction(Mining.System);
        }

        tool.OnDoubleClick(miner);
        Assert.NotNull(miner.Target);
        miner.Target.Invoke(miner, seam);
        Assert.Equal(49, tool.UsesRemaining);
        Assert.Equal(1, miner.Backpack.FindItemByType<VolcanicGlass>().Amount);
        for (var i = 1; i < DeepOreSeam.Capacity; i++)
        {
            Assert.True(seam.TryMine(miner, tool, now));
        }

        Assert.False(seam.TryMine(miner, tool, now));
        Assert.Equal(DeepOreSeam.Capacity, miner.Backpack.FindItemByType<VolcanicGlass>().Amount);
        Assert.Equal(50 - DeepOreSeam.Capacity, tool.UsesRemaining);

        var restored = _world.TrackItem(new DeepOreSeam(World.NewItem));
        Reload(seam, restored);
        seam.Delete();
        Assert.Equal(0, restored.Available(now.AddMinutes(DeepOreSeam.RefillMinutes).AddSeconds(-1)));
        Assert.Equal(DeepOreSeam.Capacity, restored.Available(now.AddMinutes(DeepOreSeam.RefillMinutes)));
        Assert.True(restored.TryMine(miner, tool, now.AddMinutes(DeepOreSeam.RefillMinutes)));
        Assert.Equal(DeepOreSeam.Capacity - 1, restored.Available(now.AddMinutes(DeepOreSeam.RefillMinutes)));
    }

    [Fact]
    public void DeepRecipesMakeDistinctEquipmentAndSpendOnlyOrdinaryInputs()
    {
        var smith = NewPlayer(new Point3D(1800, 1800, 0), Map.Felucca);
        smith.Skills.Blacksmith.Base = 95;
        _world.TrackItem(new Anvil()).MoveToWorld(smith.Location, smith.Map);
        _world.TrackItem(new Forge()).MoveToWorld(smith.Location, smith.Map);
        Put(smith, new VolcanicGlass { Amount = 2 });
        Put(smith, new MeteoricIronOre { Amount = 3 });
        var iron = Put(smith, new IronIngot(28));
        var copper = Put(smith, new CopperIngot(8));
        Put(smith, new ShadowIronIngot(4));
        Put(smith, new DullCopperIngot(4));

        Assert.True(Metallurgy.TryCraft(smith, MetallurgyRecipe.VolcanicGlassDagger));
        var dagger = Assert.IsType<VolcanicGlassDagger>(smith.Backpack.FindItemByType<VolcanicGlassDagger>());
        dagger.GetDamageTypes(smith, out var physical, out var fire, out _, out _, out _, out _, out _);
        Assert.Equal((75, 25), (physical, fire));
        Assert.InRange(dagger.MaxHitPoints, 18, 25);
        Assert.Equal(0, dagger.ArtifactRarity);
        Assert.True(dagger.PlayerConstructed);
        Assert.Equal(20, iron.Amount);
        Assert.Equal(4, copper.Amount);

        Assert.True(Metallurgy.TryCraft(smith, MetallurgyRecipe.MeteoricIronShield));
        var shield = Assert.IsType<MeteoricIronShield>(smith.Backpack.FindItemByType<MeteoricIronShield>());
        Assert.InRange(shield.MaxHitPoints, 95, 110);
        Assert.Equal(14, shield.Weight);
        Assert.Equal(2, shield.PhysicalResistance);
        Assert.Equal(0, shield.FireResistance);

        var magicalRing = Put(smith, new SilverRing());
        magicalRing.Attributes.Luck = 25;
        Assert.False(Metallurgy.TryCraft(smith, MetallurgyRecipe.ConductiveSilver));
        Assert.Equal(1, smith.Backpack.FindItemByType<MeteoricIronOre>().Amount);
        var plainRing = Put(smith, new SilverRing());
        Assert.True(Metallurgy.TryCraft(smith, MetallurgyRecipe.ConductiveSilver));
        Assert.True(plainRing.Deleted);
        Assert.False(magicalRing.Deleted);
        Assert.True(Metallurgy.TryCraft(smith, MetallurgyRecipe.ConductiveSilverSleeves));
        Assert.False(Metallurgy.TryCraft(smith, MetallurgyRecipe.ConductiveSilverSleeves));
        Assert.True(iron.Deleted);
        Assert.True(copper.Deleted);

        var sleeves = Assert.IsType<ConductiveSilverSleeves>(smith.Backpack.FindItemByType<ConductiveSilverSleeves>());
        var copy = _world.TrackItem(new ConductiveSilverSleeves(World.NewItem));
        Reload(sleeves, copy);
        Assert.Equal(5, copy.Attributes.LowerManaCost);
        Assert.Equal(ArmorMeditationAllowance.All, copy.MeditationAllowance);
        Assert.Equal(2, copy.PhysicalResistance);
        Assert.InRange(copy.MaxHitPoints, 20, 28);
    }

    private PlayerMobile NewPlayer(Point3D position, Map map)
    {
        var player = _world.Track(new PlayerMobile { Player = true, Race = Race.Human });
        player.AddItem(_world.TrackItem(new Backpack()));
        player.MoveToWorld(position, map);
        return player;
    }

    private T Put<T>(PlayerMobile player, T item) where T : Item
    {
        player.Backpack.DropItem(_world.TrackItem(item));
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
