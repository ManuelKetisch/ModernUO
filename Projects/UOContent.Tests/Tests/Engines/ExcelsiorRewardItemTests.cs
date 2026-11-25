using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public sealed class ExcelsiorRewardItemTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void DippingAndFiringConserveArrowsAndTubCharges()
    {
        var owner = NewPlayer();
        var stranger = NewPlayer();
        var tub = Put(owner, new ArrowDipTub(ArrowDipKind.Lightning, 1));
        var regular = Put(owner, new Arrow(14));
        var foreign = Put(stranger, new Arrow(20));

        Assert.False(tub.TryDip(owner, foreign));
        Assert.False(tub.TryDip(stranger, regular));
        Assert.Equal(1, tub.UsesRemaining);

        owner.Backpack.MaxItems = owner.Backpack.TotalItems;
        Assert.False(tub.TryDip(owner, regular));
        Assert.Equal(14, regular.Amount);
        Assert.Equal(1, tub.UsesRemaining);
        owner.Backpack.MaxItems = -1;

        Assert.True(tub.TryDip(owner, regular));
        Assert.Equal(4, regular.Amount);
        Assert.True(tub.Deleted);

        var dipped = _world.TrackItem(owner.Backpack.FindItemByType<DippedArrow>());
        Assert.Equal(10, dipped.Amount);
        Assert.False(regular.CanStackWith(dipped));
        Assert.False(DippedArrow.TryFire(owner, out _));
        dipped.OnDoubleClick(owner);
        Assert.True(DippedArrow.TryFire(owner, out var fired));
        Assert.Equal(ArrowDipKind.Lightning, fired);
        Assert.Equal(9, dipped.Amount);
        Assert.Equal(4, regular.Amount);

        var freeze = Put(owner, new DippedArrow(ArrowDipKind.Freeze));
        freeze.OnDoubleClick(owner);
        Assert.True(DippedArrow.TryFire(owner, out fired));
        Assert.Equal(ArrowDipKind.Freeze, fired);
        Assert.True(freeze.Deleted);
        Assert.False(DippedArrow.TryFire(owner, out _));
        Assert.Equal(9, dipped.Amount);
    }

    [Fact]
    public void WithdrawingPartOfADippedStackPreservesItsEffectHueAndReadiedState()
    {
        var owner = NewPlayer();
        var keys = Put(owner, new TradeResourceStorage(TradeSupplyKind.Woodworking));
        var arrows = Put(owner, new DippedArrow(ArrowDipKind.Lightning, 10) { Hue = 0x489 });
        arrows.OnDoubleClick(owner);
        Assert.True(keys.TryStore(owner, arrows));
        Assert.True(keys.TryWithdraw(owner, arrows, 3));

        var withdrawn = _world.TrackItem(owner.Backpack.FindItemByType<DippedArrow>());
        Assert.NotSame(arrows, withdrawn);
        Assert.Equal((7, 3), (arrows.Amount, withdrawn.Amount));
        Assert.Equal(ArrowDipKind.Lightning, withdrawn.Kind);
        Assert.Equal(0x489, withdrawn.Hue);
        Assert.True(withdrawn.Readied);
        Assert.True(DippedArrow.TryFire(owner, out var effect));
        Assert.Equal(ArrowDipKind.Lightning, effect);
        Assert.Equal((7, 2), (arrows.Amount, withdrawn.Amount));
    }

    [Fact]
    public void WorkshopDeedsRetainHueAndResourcesBagOnlyCarriesRawGoods()
    {
        var forge = _world.TrackItem(new ColoredForgeDeed(0x489));
        var loom = _world.TrackItem(new ColoredLoomDeed(0x4A7));
        var forgeAddon = _world.TrackItem(forge.Addon);
        var loomAddon = _world.TrackItem(loom.Addon);
        Assert.Equal(forge.Hue, forgeAddon.Hue);
        Assert.Equal(loom.Hue, loomAddon.Hue);
        Assert.Equal(forge.Hue, _world.TrackItem(forgeAddon.Deed).Hue);
        Assert.Equal(loom.Hue, _world.TrackItem(loomAddon.Deed).Hue);

        var owner = NewPlayer();
        var bag = Put(owner, new BagOfResources());
        var ore = Put(owner, new IronOre(20));
        var boards = Put(owner, new Board(2));
        Assert.False(bag.TryDropItem(owner, boards, false));
        Assert.True(bag.TryDropItem(owner, ore, false));
        Assert.True(bag.TryDropItem(owner, Put(owner, new PineLog(2)), false));
        Assert.True(bag.TryDropItem(owner, Put(owner, new GradeALog(2)), false));
        Assert.Equal(0, bag.TotalWeight);
        Assert.Equal(100.0, bag.Weight);
        Assert.True(ore.IsChildOf(bag));
    }

    [Fact]
    public void FinishesRespectOwnershipAndDamageAndDurabilityLimits()
    {
        var owner = NewPlayer();
        owner.Skills.Blacksmith.Base = 100;
        var stranger = NewPlayer();
        var blade = Put(owner, new SharpeningBlade());
        var sword = Put(owner, new Broadsword());
        sword.Attributes.WeaponDamage = 49;
        var foreign = Put(stranger, new Broadsword());
        Assert.False(blade.Apply(owner, foreign));
        Assert.True(blade.Apply(owner, sword));
        Assert.Equal(50, sword.Attributes.WeaponDamage);
        Assert.True(blade.Deleted);

        var stain = Put(owner, new StainOfDurability());
        var bow = Put(owner, new Bow { Resource = CraftResource.OakWood, MaxHitPoints = 251, HitPoints = 245 });
        Assert.False(stain.Apply(owner, sword));
        Assert.True(stain.Apply(owner, bow));
        Assert.Equal(255, bow.MaxHitPoints);
        Assert.Equal(249, bow.HitPoints);
        Assert.True(stain.Deleted);
    }

    private PlayerMobile NewPlayer()
    {
        var player = _world.Track(new PlayerMobile { Player = true, Race = Race.Human });
        player.AddItem(_world.TrackItem(new Backpack()));
        return player;
    }

    private T Put<T>(PlayerMobile owner, T item) where T : Item
    {
        owner.Backpack.DropItem(_world.TrackItem(item));
        return item;
    }
}
