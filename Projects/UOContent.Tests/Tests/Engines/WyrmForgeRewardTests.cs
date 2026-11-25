using System;
using System.Collections.Generic;
using Server;
using Server.Engines.HybridForge;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public sealed class WyrmForgeRewardTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void DefeatReceiptKeepsPendingItemsAndSpentCreditsThroughReload()
    {
        var ledger = new WyrmRewardLedger();
        var defeat = Guid.NewGuid();
        var owner = _world.NewPlayer().Serial;
        var other = _world.NewPlayer().Serial;

        Assert.True(ledger.Record(defeat, owner, WyrmReward.PlateBlueprint));
        Assert.False(ledger.Record(defeat, other, WyrmReward.Trophy));
        ledger.Deliver(defeat, _ => false);

        for (var i = 0; i < 5; i++)
        {
            var next = Guid.NewGuid();
            Assert.True(ledger.Record(next, owner, WyrmReward.None));
            ledger.Deliver(next, _ => true);
        }

        Assert.True(ledger.Record(Guid.NewGuid(), other, WyrmReward.None));
        ledger = Reload(ledger);
        Assert.Equal(new WyrmRewardStatus(6, 2), ledger.Status(owner));
        Assert.Equal(new WyrmRewardStatus(1, 1), ledger.Status(other));
        Assert.False(ledger.Record(defeat, owner, WyrmReward.Trophy));
        Assert.False(ledger.Spend(owner, 6, () => false));
        Assert.Equal(new WyrmRewardStatus(6, 2), ledger.Status(owner));

        List<WyrmReward> claimed = [];
        ledger.Claim(owner, reward =>
        {
            claimed.Add(reward);
            return true;
        });
        Assert.Equal([WyrmReward.Scale, WyrmReward.PlateBlueprint], claimed);
        Assert.True(ledger.Spend(owner, 6, () => true));

        ledger = Reload(ledger);
        Assert.Equal(new WyrmRewardStatus(0, 0), ledger.Status(owner));
        Assert.Equal(new WyrmRewardStatus(1, 1), ledger.Status(other));
        Assert.False(ledger.Record(defeat, owner, WyrmReward.None));
        Assert.False(ledger.Spend(owner, 6, () => throw new InvalidOperationException()));
        ledger.Claim(owner, _ => throw new InvalidOperationException());
    }

    [Fact]
    public void StockRightsSelectOneEligiblePlayerWithAStableTieBreak()
    {
        var first = _world.NewPlayer();
        var second = _world.NewPlayer();
        var uncredited = _world.NewPlayer();
        var staff = _world.NewPlayer();
        staff.AccessLevel = AccessLevel.GameMaster;

        var winner = WyrmForgeRewards.ChooseRecipient(
            [
                new DamageStore(second, 100) { m_HasRight = true },
                new DamageStore(uncredited, 200),
                new DamageStore(staff, 200) { m_HasRight = true },
                new DamageStore(first, 100) { m_HasRight = true }
            ]
        );

        Assert.Same(first, winner);
    }

    private static WyrmRewardLedger Reload(WyrmRewardLedger original)
    {
        var writer = new BufferWriter(true);
        original.Serialize(writer);
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        var copy = new WyrmRewardLedger();
        copy.Deserialize(reader);
        Assert.Equal(writer.Position, reader.Position);
        return copy;
    }
}
