using System;
using Server;
using Server.Engines.BulkOrders;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public sealed class WoodworkBulkOrdersTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void CarpenterAndBowyerOfferSeparateOrdersWithIndependentCooldowns()
    {
        if (DefCarpentry.CraftSystem == null)
        {
            DefCarpentry.Initialize();
        }

        if (DefBowFletching.CraftSystem == null)
        {
            DefBowFletching.Initialize();
        }

        var player = NewPlayer();
        var carpenter = _world.Track(new Carpenter());
        var bowyer = _world.Track(new Bowyer());
        try
        {
            player.Skills.Carpentry.Base = 30;
            player.Skills.Fletching.Base = 60;

            var woodwork = _world.TrackItem(Assert.IsType<WoodworkSmallBOD>(carpenter.CreateBulkOrder(player, true)));
            Assert.Equal(BODType.Carpentry, woodwork.Profession);
            Assert.True(WoodworkBulkOrders.IsFurniture(woodwork.Type));
            Assert.True(carpenter.IsValidBulkOrder(woodwork));
            Assert.False(bowyer.IsValidBulkOrder(woodwork));
            Assert.Null(carpenter.CreateBulkOrder(player, true));
            Assert.InRange(carpenter.GetNextBulkOrder(player), TimeSpan.FromMinutes(119), TimeSpan.FromHours(2));
            Assert.Equal(TimeSpan.Zero, bowyer.GetNextBulkOrder(player));

            var ranged = _world.TrackItem(Assert.IsType<WoodworkSmallBOD>(bowyer.CreateBulkOrder(player, true)));
            Assert.Equal(BODType.Fletching, ranged.Profession);
            Assert.True(bowyer.IsValidBulkOrder(ranged));
            Assert.False(carpenter.IsValidBulkOrder(ranged));
            carpenter.OnSuccessfulBulkOrderReceive(player);
            Assert.Equal(TimeSpan.Zero, carpenter.GetNextBulkOrder(player));
            Assert.True(bowyer.GetNextBulkOrder(player) > TimeSpan.Zero);
        }
        finally
        {
            WoodworkBulkOrders.OnPlayerDeleted(player);
        }
    }

    [Fact]
    public void CombiningAndReloadingPreserveWoodQualityAndProfession()
    {
        var player = NewPlayer();
        var small = Put(player, new WoodworkSmallBOD(BODType.Fletching, 0, 1, typeof(Bow), 1025042, 0x13B2,
            false, BulkMaterialType.Oak));
        var purchased = Put(player, new Bow { Resource = CraftResource.OakWood });
        small.BeginCombine(player);
        player.Target.Invoke(player, purchased);
        Assert.False(purchased.Deleted);

        var wrongWood = Put(player, new Bow { Resource = CraftResource.RegularWood, PlayerConstructed = true });
        small.BeginCombine(player);
        player.Target.Invoke(player, wrongWood);
        Assert.False(wrongWood.Deleted);

        var oak = Put(player, new Bow
        {
            Resource = CraftResource.OakWood, PlayerConstructed = true, Quality = WeaponQuality.Exceptional
        });
        small.BeginCombine(player);
        player.Target.Invoke(player, oak);
        Assert.True(oak.Deleted);
        Assert.True(small.Complete);

        var large = Put(player, new WoodworkLargeBOD(BODType.Fletching, 1, false, BulkMaterialType.Oak,
            [new LargeBulkEntry(null, new SmallBulkEntry(typeof(Bow), 1025042, 0x13B2))]));
        large.BeginCombine(player);
        player.Target.Invoke(player, small);
        Assert.True(large.Complete);
        Assert.True(small.Deleted);
        Assert.True(large.ComputeGold() > 0);
        Assert.NotNull(large.GetRewardGroup().AcquireItem());

        var saved = Put(player, new WoodworkLargeBOD(World.NewItem));
        var writer = new BufferWriter(true);
        large.Serialize(writer);
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        saved.Deserialize(reader);
        Assert.Equal(writer.Position, reader.Position);
        Assert.Equal(BODType.Fletching, saved.Profession);
        Assert.Equal(BulkMaterialType.Oak, saved.Material);
        Assert.True(saved.Complete);
        Assert.Same(saved, saved.Entries[0].Owner);
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
