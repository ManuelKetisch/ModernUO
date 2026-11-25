using System;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public class WeaponLevelExchangeTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void RecycleConfirmationIssuesTokensAndRechecksInventory()
    {
        var owner = CreatePlayer();
        var stranger = CreatePlayer();
        var exchange = CreateExchange(owner);
        var gear = new Longsword();
        gear.Attributes.WeaponDamage = 10;
        owner.Backpack.DropItem(gear);

        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = owner;
        owner.NetState = state;
        try
        {
            var confirm = OpenRecycling(owner, state, exchange, gear);
            stranger.Backpack.DropItem(gear);
            confirm.OnResponse(state, new RelayInfo(1, [], [], [], []));

            Assert.False(gear.Deleted);
            Assert.Equal(0, owner.Backpack.GetAmount(typeof(WeaponRecyclingToken)));
            owner.Backpack.DropItem(gear);
            owner.CloseGump<BaseGump>();

            confirm = OpenRecycling(owner, state, exchange, gear);
            confirm.OnResponse(state, new RelayInfo(1, [], [], [], []));

            Assert.True(gear.Deleted);
            Assert.Equal(2000, owner.Backpack.GetAmount(typeof(WeaponRecyclingToken)));
        }
        finally
        {
            while (owner.CloseGump<BaseGump>())
            {
            }

            owner.NetState = null;
            state.Mobile = null;
        }
    }

    [Fact]
    public void OrdinaryAndProtectedGearCannotBeRecycled()
    {
        var owner = CreatePlayer();
        var exchange = CreateExchange(owner);
        var gear = new Longsword();
        owner.Backpack.DropItem(gear);

        Assert.False(exchange.TryRecycle(owner, gear));
        gear.Attributes.WeaponDamage = 10;
        gear.PlayerConstructed = true;
        Assert.False(exchange.TryRecycle(owner, gear));
        gear.PlayerConstructed = false;
        gear.Insured = true;
        Assert.False(exchange.TryRecycle(owner, gear));

        Assert.False(gear.Deleted);
        Assert.Equal(0, owner.Backpack.GetAmount(typeof(WeaponRecyclingToken)));
    }

    [Fact]
    public void BuyingTheLevelDeedConsumesExactlyOneHundredThousandTokens()
    {
        var owner = CreatePlayer();
        var exchange = CreateExchange(owner);
        owner.Backpack.DropItem(new WeaponRecyclingToken(60000));
        var secondStack = new WeaponRecyclingToken(39999);
        owner.Backpack.DropItem(secondStack);

        Assert.False(exchange.TryPurchase(owner, WeaponExchangePurchase.Level));
        Assert.Equal(99999, owner.Backpack.GetAmount(typeof(WeaponRecyclingToken)));
        Assert.Null(owner.Backpack.FindItemByType<WeaponLevelDeed>());
        secondStack.Amount = 40000;

        Assert.True(exchange.TryPurchase(owner, WeaponExchangePurchase.Level));
        Assert.IsType<WeaponLevelDeed>(Assert.Single(owner.Backpack.Items));
    }

    [Fact]
    public void HostileMonsterDeathEarnsDifficultyScaledTokensAndPeacetimeAnimalsDoNot()
    {
        var owner = CreatePlayer();
        var creature = _world.NewCreature();
        creature.Karma = -1000;
        creature.Fame = 1000;
        creature.SetHits(160);
        creature.MoveToWorld(new Point3D(1102, 1100, 0), owner.Map);
        creature.LastKiller = owner;

        creature.Kill();

        Assert.Equal(100, owner.Backpack.GetAmount(typeof(WeaponRecyclingToken)));

        var passive = _world.NewCreature();
        passive.Karma = 1000;
        passive.SetHits(160);
        passive.MoveToWorld(new Point3D(1102, 1100, 0), owner.Map);
        passive.LastKiller = owner;

        passive.Kill();

        Assert.Equal(100, owner.Backpack.GetAmount(typeof(WeaponRecyclingToken)));
    }

    private static BaseGump OpenRecycling(PlayerMobile owner, Server.Network.NetState state, WeaponLevelExchange exchange,
        Item gear)
    {
        exchange.OnDoubleClick(owner);
        var menu = owner.FindGump<BaseGump>();
        Assert.NotNull(menu);
        owner.CloseGump<BaseGump>();
        menu.OnResponse(state, new RelayInfo(5, [], [], [], []));
        Assert.NotNull(owner.Target);
        owner.Target.Invoke(owner, gear);
        var confirmation = owner.FindGump<BaseGump>();
        Assert.NotNull(confirmation);
        return confirmation;
    }

    private PlayerMobile CreatePlayer()
    {
        var player = _world.NewPlayer();
        player.AddItem(new Backpack());
        player.MoveToWorld(new Point3D(1100, 1100, 0), Map.Felucca);
        return player;
    }

    private WeaponLevelExchange CreateExchange(PlayerMobile player)
    {
        var exchange = _world.TrackItem(new WeaponLevelExchange());
        exchange.MoveToWorld(new Point3D(1101, 1100, 0), player.Map);
        return exchange;
    }
}
