using System;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.BuffIcons;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests.Engines.BuffIcons;

[Collection("Sequential UOContent Tests")]
public class PlayerBuffPersistenceTests : IDisposable
{
    private readonly MobileTestHarness _harness = new();
    private readonly Account _account;

    public PlayerBuffPersistenceTests()
    {
        AccountSecurity.Configure();
        BuffInfo.Configure();
        PlayerBuffPersistence.ClearRuntimeState();
        _account = new Account($"buffs-{Guid.NewGuid():N}", "test-pass");
    }

    public void Dispose()
    {
        PlayerBuffPersistence.ClearRuntimeState();
        _harness.Dispose();
        _account.Delete();
    }

    private NetState CreateConnectedNetState()
    {
        var ns = PacketTestUtilities.CreateTestNetState();
        ns.Account = _account;
        return ns;
    }

    private PlayerMobile CreatePlayer(bool online = true, NetState netState = null)
    {
        var player = _harness.NewPlayer();
        player.Race = Race.Elf;
        player.RawStr = 100;
        player.RawDex = 100;
        player.RawInt = 100;
        player.Skills.Alchemy.Base = 100.0;
        player.Skills.TasteID.Base = 0.0;
        player.MoveToWorld(new Point3D(1100, 1100, 0), Map.Felucca);

        if (online && netState != null)
        {
            player.NetState = netState;
        }

        return player;
    }

    [Fact]
    public void PotionStatModsAndBuffIconsPauseWhileOfflineAndExpireOnOnlineTime()
    {
        using var scope = new TimerWheelScope();
        using var ns = CreateConnectedNetState();
        var player = CreatePlayer(online: true, netState: ns);

        // 100 Alchemy (+30%) + 200 raw STR+INT (+20%) = +50% -> 45m Greater Agility, 30m Night Sight.
        var agilPot = _harness.TrackItem(new GreaterAgilityPotion());
        agilPot.MoveToWorld(player.Location, player.Map);
        Assert.True(agilPot.CanDrink(player));
        agilPot.Drink(player);

        var nsPot = _harness.TrackItem(new NightSightPotion());
        nsPot.MoveToWorld(player.Location, player.Map);
        Assert.True(nsPot.CanDrink(player));
        nsPot.Drink(player);

        Assert.NotNull(player.GetStatMod("[Magic] Dex Buff"));
        Assert.False(PlayerBuffPersistence.IsStatModPaused(player, "[Magic] Dex Buff"));
        Assert.False(PlayerBuffPersistence.IsNightSightPaused(player));
        Assert.Equal(TimeSpan.FromMinutes(45), PlayerBuffPersistence.GetStatModRemaining(player, "[Magic] Dex Buff"));
        Assert.Equal(TimeSpan.FromMinutes(30), PlayerBuffPersistence.GetNightSightRemaining(player));

        var agilBuff = player.GetBuff(BuffIcon.Agility);
        var nsBuff = player.GetBuff(BuffIcon.NightSight);
        Assert.NotNull(agilBuff);
        Assert.NotNull(nsBuff);
        Assert.False(agilBuff.IsPaused);
        Assert.False(nsBuff.IsPaused);

        // Play online for 10 minutes: remaining drops to 35m (Agility) and 20m (Night Sight).
        TimerWheelScope.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(TimeSpan.FromMinutes(35), PlayerBuffPersistence.GetStatModRemaining(player, "[Magic] Dex Buff"));
        Assert.Equal(TimeSpan.FromMinutes(35), agilBuff.RemainingDuration);
        Assert.Equal(TimeSpan.FromMinutes(20), PlayerBuffPersistence.GetNightSightRemaining(player));
        Assert.Equal(TimeSpan.FromMinutes(20), nsBuff.RemainingDuration);

        // Disconnect and stay offline for 3 hours: nothing decays or expires.
        player.NetState = null;
        Assert.True(PlayerBuffPersistence.IsStatModPaused(player, "[Magic] Dex Buff"));
        Assert.True(PlayerBuffPersistence.IsNightSightPaused(player));
        Assert.True(agilBuff.IsPaused);
        Assert.True(nsBuff.IsPaused);

        TimerWheelScope.Advance(TimeSpan.FromHours(3));
        Assert.NotNull(player.GetStatMod("[Magic] Dex Buff"));
        Assert.Equal(TimeSpan.FromMinutes(35), PlayerBuffPersistence.GetStatModRemaining(player, "[Magic] Dex Buff"));
        Assert.Equal(TimeSpan.FromMinutes(35), agilBuff.RemainingDuration);
        Assert.Equal(TimeSpan.FromMinutes(20), PlayerBuffPersistence.GetNightSightRemaining(player));
        Assert.Equal(TimeSpan.FromMinutes(20), nsBuff.RemainingDuration);
        Assert.Equal(LightCycle.DungeonLevel / 2, player.LightLevel);

        // Reconnect: timers resume from exact frozen remaining durations.
        using var ns2 = CreateConnectedNetState();
        player.NetState = ns2;
        Assert.False(PlayerBuffPersistence.IsStatModPaused(player, "[Magic] Dex Buff"));
        Assert.False(PlayerBuffPersistence.IsNightSightPaused(player));
        Assert.False(agilBuff.IsPaused);
        Assert.False(nsBuff.IsPaused);
        Assert.Equal(TimeSpan.FromMinutes(35), PlayerBuffPersistence.GetStatModRemaining(player, "[Magic] Dex Buff"));
        Assert.Equal(TimeSpan.FromMinutes(20), PlayerBuffPersistence.GetNightSightRemaining(player));

        // Advance 20m online: Night Sight expires, Agility still has 15m left.
        TimerWheelScope.Advance(TimeSpan.FromMinutes(20));
        Assert.Equal(0, player.LightLevel);
        Assert.True(player.CanBeginAction<LightCycle>());
        Assert.Null(player.GetBuff(BuffIcon.NightSight));
        Assert.NotNull(player.GetStatMod("[Magic] Dex Buff"));
        Assert.Equal(TimeSpan.FromMinutes(15), PlayerBuffPersistence.GetStatModRemaining(player, "[Magic] Dex Buff"));
        Assert.Equal(TimeSpan.FromMinutes(15), agilBuff.RemainingDuration);

        // Advance remaining 15m online: Agility stat mod and buff icon expire together.
        TimerWheelScope.Advance(TimeSpan.FromMinutes(15));
        Assert.Null(player.GetStatMod("[Magic] Dex Buff"));
        Assert.Equal(TimeSpan.Zero, PlayerBuffPersistence.GetStatModRemaining(player, "[Magic] Dex Buff"));
        Assert.Null(player.GetBuff(BuffIcon.Agility));

        player.NetState = null;
    }

    [Fact]
    public void OfflinePausableTimerFreezesWhileDisconnectedAndFiresOnOnlineTime()
    {
        using var scope = new TimerWheelScope();
        using var ns = CreateConnectedNetState();
        var player = CreatePlayer(online: true, netState: ns);

        var expired = false;
        var timer = OfflinePausableTimer.Start(player, TimeSpan.FromMinutes(5), () => expired = true);

        TimerWheelScope.Advance(TimeSpan.FromMinutes(2));
        Assert.False(expired);
        Assert.Equal(TimeSpan.FromMinutes(3), timer.Remaining);

        player.NetState = null;
        Assert.True(timer.IsPaused);

        TimerWheelScope.Advance(TimeSpan.FromHours(1));
        Assert.False(expired);
        Assert.Equal(TimeSpan.FromMinutes(3), timer.Remaining);

        using var ns2 = CreateConnectedNetState();
        player.NetState = ns2;
        Assert.False(timer.IsPaused);

        TimerWheelScope.Advance(TimeSpan.FromMinutes(2));
        Assert.False(expired);
        Assert.Equal(TimeSpan.FromMinutes(1), timer.Remaining);

        TimerWheelScope.Advance(TimeSpan.FromMinutes(1));
        Assert.True(expired);
        Assert.False(timer.Running);

        player.NetState = null;
    }

    [Fact]
    public void ManualRemoveStatModCancelsTrackedTimerCleanly()
    {
        using var scope = new TimerWheelScope();
        using var ns = CreateConnectedNetState();
        var player = CreatePlayer(online: true, netState: ns);

        PlayerBuffPersistence.AddTimedStatMod(player, StatType.Str, "[Magic] Str Buff", 20, TimeSpan.FromMinutes(20));
        var mod = player.GetStatMod("[Magic] Str Buff");
        Assert.NotNull(mod);
        Assert.Equal(TimeSpan.FromMinutes(20), PlayerBuffPersistence.GetStatModRemaining(player, "[Magic] Str Buff"));

        player.RemoveStatMod(mod);
        Assert.Null(player.GetStatMod("[Magic] Str Buff"));
        Assert.Equal(TimeSpan.Zero, PlayerBuffPersistence.GetStatModRemaining(player, "[Magic] Str Buff"));

        player.NetState = null;
    }

    [Fact]
    public void SaveAndRestartRoundTripRestoresPersistedBuffsPausedWithoutGhostTransientIcons()
    {
        using var scope = new TimerWheelScope();
        using var ns = CreateConnectedNetState();
        var player = CreatePlayer(online: true, netState: ns);

        var strPot = _harness.TrackItem(new StrengthPotion());
        strPot.MoveToWorld(player.Location, player.Map);
        Assert.True(strPot.CanDrink(player));
        strPot.Drink(player);

        var nsPot = _harness.TrackItem(new NightSightPotion());
        nsPot.MoveToWorld(player.Location, player.Map);
        Assert.True(nsPot.CanDrink(player));
        nsPot.Drink(player);

        // Add a transient offline-pausing buff (not persisted across restarts) to ensure no ghost icon after reboot.
        player.AddBuff(new BuffInfo(BuffIcon.EnemyOfOne, 1075653, 1044111, TimeSpan.FromMinutes(3), pauseWhileOffline: true));

        // Play 5 minutes online -> 25m remaining on Strength & Night Sight.
        TimerWheelScope.Advance(TimeSpan.FromMinutes(5));

        // Disconnect and serialize state.
        player.NetState = null;

        var writer = new BufferWriter(true);
        PlayerBuffPersistence.SerializeState(writer);
        var bytes = writer.Buffer.AsSpan(0, (int)writer.Position).ToArray();

        // Simulate server restart: clear runtime buff/night-sight state (StatMods persist on Mobile).
        PlayerBuffPersistence.ClearRuntimeState();
        player.RemoveBuff(BuffIcon.Strength);
        player.RemoveBuff(BuffIcon.NightSight);
        player.RemoveBuff(BuffIcon.EnemyOfOne);
        player.EndAction<LightCycle>();
        player.LightLevel = 0;

        // Simulate 6 hours of server downtime before boot.
        TimerWheelScope.Advance(TimeSpan.FromHours(6));

        var reader = new BufferReader(bytes);
        PlayerBuffPersistence.DeserializeState(reader);
        Assert.Equal(bytes.Length, reader.Position);

        // Restored paused while player is still offline.
        Assert.True(PlayerBuffPersistence.IsStatModPaused(player, "[Magic] Str Buff"));
        Assert.Equal(TimeSpan.FromMinutes(25), PlayerBuffPersistence.GetStatModRemaining(player, "[Magic] Str Buff"));
        Assert.True(PlayerBuffPersistence.IsNightSightPaused(player));
        Assert.Equal(TimeSpan.FromMinutes(25), PlayerBuffPersistence.GetNightSightRemaining(player));
        Assert.Equal(LightCycle.DungeonLevel / 2, player.LightLevel);
        Assert.False(player.CanBeginAction<LightCycle>());

        var restoredStr = player.GetBuff(BuffIcon.Strength);
        var restoredNs = player.GetBuff(BuffIcon.NightSight);
        Assert.NotNull(restoredStr);
        Assert.NotNull(restoredNs);
        Assert.True(restoredStr.IsPaused);
        Assert.True(restoredNs.IsPaused);
        Assert.Equal(TimeSpan.FromMinutes(25), restoredStr.RemainingDuration);
        Assert.Equal(TimeSpan.FromMinutes(25), restoredNs.RemainingDuration);
        Assert.Null(player.GetBuff(BuffIcon.EnemyOfOne));

        // Player logs in after restart: buffs resume and expire after 25 minutes online.
        using var ns2 = CreateConnectedNetState();
        player.NetState = ns2;
        Assert.False(restoredStr.IsPaused);
        Assert.False(PlayerBuffPersistence.IsStatModPaused(player, "[Magic] Str Buff"));

        TimerWheelScope.Advance(TimeSpan.FromMinutes(25));
        Assert.Null(player.GetStatMod("[Magic] Str Buff"));
        Assert.Null(player.GetBuff(BuffIcon.Strength));
        Assert.Null(player.GetBuff(BuffIcon.NightSight));
        Assert.Equal(0, player.LightLevel);

        player.NetState = null;
    }

    [Fact]
    public void NonPlayerMobilesKeepNormalWallClockStatModExpiration()
    {
        using var scope = new TimerWheelScope();
        var creature = _harness.NewCreature();
        creature.MoveToWorld(new Point3D(1105, 1105, 0), Map.Felucca);

        PlayerBuffPersistence.AddTimedStatMod(creature, StatType.Str, "[Magic] Str Buff", 15, TimeSpan.FromMinutes(2));
        Assert.NotNull(creature.GetStatMod("[Magic] Str Buff"));
        Assert.Equal(TimeSpan.Zero, PlayerBuffPersistence.GetStatModRemaining(creature, "[Magic] Str Buff"));

        TimerWheelScope.Advance(TimeSpan.FromMinutes(2));
        Assert.Null(creature.GetStatMod("[Magic] Str Buff"));
    }
}
