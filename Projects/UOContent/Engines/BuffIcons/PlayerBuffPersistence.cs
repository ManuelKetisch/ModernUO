using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Collections;
using Server.Mobiles;

namespace Server.Engines.BuffIcons;

/// <summary>
/// Tracks offline-pausable player buff timers (timed StatMods, Night Sight, persisted BuffInfos,
/// and in-memory timed spell buffs) so active buff durations freeze while a player is disconnected
/// and resume with exact remaining time on reconnect or server restart.
/// </summary>
public sealed class PlayerBuffPersistence : GenericPersistence
{
    private const int SaveVersion = 0;

    private static PlayerBuffPersistence _instance;

    private static readonly Dictionary<PlayerMobile, Dictionary<string, TimedStatModEntry>> _statMods = new();
    private static readonly Dictionary<PlayerMobile, NightSightEntry> _nightSights = new();
    private static readonly Dictionary<PlayerMobile, HashSet<OfflinePausableTimer>> _pausableTimers = new();
    private static readonly HashSet<PlayerMobile> _persistedBuffOwners = new();

    public static void Configure()
    {
        _instance = new PlayerBuffPersistence();
    }

    public PlayerBuffPersistence() : base("PlayerBuffs", 10)
    {
    }

    public static void AddTimedStatMod(Mobile target, StatType type, string name, int offset, TimeSpan duration)
    {
        if (target == null || name == null)
        {
            return;
        }

        if (duration <= TimeSpan.Zero || target is not PlayerMobile pm)
        {
            target.AddStatMod(new StatMod(type, name, offset, duration));
            return;
        }

        pm.RemoveStatMod(name);

        // Use an indefinite StatMod so Mobile.StatMods never expires on wall-clock time while offline;
        // TimedStatModEntry drives expiration strictly from online time.
        pm.AddStatMod(new StatMod(type, name, offset, TimeSpan.Zero));

        if (!_statMods.TryGetValue(pm, out var playerMods))
        {
            playerMods = new Dictionary<string, TimedStatModEntry>();
            _statMods[pm] = playerMods;
        }

        var entry = new TimedStatModEntry(pm, name, duration, duration);
        playerMods[name] = entry;
        entry.StartOrPause();
    }

    public static void CancelStatModTimer(PlayerMobile pm, string name)
    {
        if (pm == null || name == null || !_statMods.TryGetValue(pm, out var playerMods))
        {
            return;
        }

        if (playerMods.Remove(name, out var entry))
        {
            entry.Cancel();
        }

        if (playerMods.Count == 0)
        {
            _statMods.Remove(pm);
        }
    }

    public static TimeSpan GetStatModRemaining(Mobile target, string name)
    {
        if (target is PlayerMobile pm && name != null &&
            _statMods.TryGetValue(pm, out var playerMods) &&
            playerMods.TryGetValue(name, out var entry))
        {
            return entry.GetRemaining();
        }

        return TimeSpan.Zero;
    }

    public static bool IsStatModPaused(Mobile target, string name) =>
        target is PlayerMobile pm &&
        name != null &&
        _statMods.TryGetValue(pm, out var playerMods) &&
        playerMods.TryGetValue(name, out var entry) &&
        entry.IsPaused;

    public static void StartNightSight(Mobile m, TimeSpan duration, int lightLevel)
    {
        if (m == null)
        {
            return;
        }

        m.LightLevel = lightLevel;

        if (m is not PlayerMobile pm)
        {
            new LightCycle.NightSightTimer(m, duration).Start();
            return;
        }

        if (_nightSights.Remove(pm, out var existing))
        {
            existing.Cancel();
        }

        var entry = new NightSightEntry(pm, duration, lightLevel);
        _nightSights[pm] = entry;
        entry.StartOrPause();
    }

    public static void CancelNightSight(PlayerMobile pm)
    {
        if (pm != null && _nightSights.Remove(pm, out var entry))
        {
            entry.Cancel();
        }
    }

    public static TimeSpan GetNightSightRemaining(Mobile target) =>
        target is PlayerMobile pm && _nightSights.TryGetValue(pm, out var entry)
            ? entry.GetRemaining()
            : TimeSpan.Zero;

    public static bool IsNightSightPaused(Mobile target) =>
        target is PlayerMobile pm && _nightSights.TryGetValue(pm, out var entry) && entry.IsPaused;

    internal static void TrackPersistedBuffOwner(PlayerMobile pm)
    {
        if (pm != null && !pm.Deleted)
        {
            _persistedBuffOwners.Add(pm);
        }
    }

    internal static void UntrackPersistedBuffOwner(PlayerMobile pm)
    {
        if (pm != null)
        {
            _persistedBuffOwners.Remove(pm);
        }
    }

    internal static void RegisterPausableTimer(PlayerMobile pm, OfflinePausableTimer timer)
    {
        if (!_pausableTimers.TryGetValue(pm, out var set))
        {
            set = new HashSet<OfflinePausableTimer>();
            _pausableTimers[pm] = set;
        }

        set.Add(timer);
    }

    internal static void UnregisterPausableTimer(PlayerMobile pm, OfflinePausableTimer timer)
    {
        if (!_pausableTimers.TryGetValue(pm, out var set))
        {
            return;
        }

        set.Remove(timer);

        if (set.Count == 0)
        {
            _pausableTimers.Remove(pm);
        }
    }

    public static void Pause(PlayerMobile pm)
    {
        if (pm == null)
        {
            return;
        }

        if (_statMods.TryGetValue(pm, out var playerMods))
        {
            foreach (var entry in playerMods.Values)
            {
                entry.Pause();
            }
        }

        if (_nightSights.TryGetValue(pm, out var nightSight))
        {
            nightSight.Pause();
        }

        if (_pausableTimers.TryGetValue(pm, out var timers))
        {
            foreach (var timer in timers)
            {
                timer.Pause();
            }
        }
    }

    public static void Resume(PlayerMobile pm)
    {
        if (pm == null || pm.Deleted)
        {
            return;
        }

        if (_statMods.TryGetValue(pm, out var playerMods))
        {
            using var expired = PooledRefList<string>.Create();

            foreach (var (name, entry) in playerMods)
            {
                if (!entry.Resume())
                {
                    expired.Add(name);
                }
            }

            for (var i = 0; i < expired.Count; i++)
            {
                ExpireStatMod(pm, expired[i]);
            }
        }

        if (_nightSights.TryGetValue(pm, out var nightSight) && !nightSight.Resume())
        {
            ExpireNightSight(pm);
        }

        if (_pausableTimers.TryGetValue(pm, out var timers))
        {
            using var snapshot = PooledRefList<OfflinePausableTimer>.Create();

            foreach (var timer in timers)
            {
                snapshot.Add(timer);
            }

            for (var i = 0; i < snapshot.Count; i++)
            {
                snapshot[i].Resume();
            }
        }
    }

    [OnEvent(nameof(PlayerMobile.PlayerDeletedEvent))]
    public static void OnPlayerDeleted(PlayerMobile pm)
    {
        if (pm == null)
        {
            return;
        }

        if (_statMods.Remove(pm, out var playerMods))
        {
            foreach (var entry in playerMods.Values)
            {
                entry.Cancel();
            }
        }

        if (_nightSights.Remove(pm, out var nightSight))
        {
            nightSight.Cancel();
        }

        if (_pausableTimers.Remove(pm, out var timers))
        {
            foreach (var timer in timers)
            {
                timer.CancelWithoutUnregister();
            }
        }

        _persistedBuffOwners.Remove(pm);
    }

    private static void ExpireStatMod(PlayerMobile pm, string name)
    {
        if (_statMods.TryGetValue(pm, out var playerMods))
        {
            if (playerMods.Remove(name, out var entry))
            {
                entry.Cancel();
            }

            if (playerMods.Count == 0)
            {
                _statMods.Remove(pm);
            }
        }

        if (!pm.Deleted)
        {
            pm.RemoveStatMod(name);
        }
    }

    private static void ExpireNightSight(PlayerMobile pm)
    {
        if (_nightSights.Remove(pm, out var entry))
        {
            entry.Cancel();
        }

        if (!pm.Deleted)
        {
            pm.EndAction<LightCycle>();
            pm.LightLevel = 0;
            pm.RemoveBuff(BuffIcon.NightSight);
        }
    }

    public override void Serialize(IGenericWriter writer) => SerializeState(writer);

    public override void Deserialize(IGenericReader reader) => DeserializeState(reader);

    internal static void SerializeState(IGenericWriter writer)
    {
        writer.WriteEncodedInt(SaveVersion);

        // 1. Timed StatMods
        using var statModRecords = PooledRefList<(PlayerMobile Mobile, string Name, TimeSpan Duration, TimeSpan Remaining)>.Create();
        foreach (var (pm, playerMods) in _statMods)
        {
            if (pm == null || pm.Deleted)
            {
                continue;
            }

            foreach (var (name, entry) in playerMods)
            {
                if (pm.GetStatMod(name, true) == null)
                {
                    continue;
                }

                var remaining = entry.GetRemaining();
                if (remaining > TimeSpan.Zero)
                {
                    statModRecords.Add((pm, name, entry.Duration, remaining));
                }
            }
        }

        writer.WriteEncodedInt(statModRecords.Count);
        for (var i = 0; i < statModRecords.Count; i++)
        {
            var record = statModRecords[i];
            writer.Write(record.Mobile);
            writer.Write(record.Name);
            writer.Write(record.Duration);
            writer.Write(record.Remaining);
        }

        // 2. Night Sight timers
        using var nightSightRecords = PooledRefList<(PlayerMobile Mobile, TimeSpan Remaining, int LightLevel)>.Create();
        foreach (var (pm, entry) in _nightSights)
        {
            if (pm == null || pm.Deleted)
            {
                continue;
            }

            var remaining = entry.GetRemaining();
            if (remaining > TimeSpan.Zero)
            {
                nightSightRecords.Add((pm, remaining, entry.LightLevel));
            }
        }

        writer.WriteEncodedInt(nightSightRecords.Count);
        for (var i = 0; i < nightSightRecords.Count; i++)
        {
            var record = nightSightRecords[i];
            writer.Write(record.Mobile);
            writer.Write(record.Remaining);
            writer.WriteEncodedInt(record.LightLevel);
        }

        // 3. Persisted BuffInfos
        using var buffRecords = PooledRefList<(PlayerMobile Mobile, BuffInfo Buff, TimeSpan Remaining)>.Create();
        var scratchBuffs = PooledRefList<BuffInfo>.Create();

        try
        {
            foreach (var pm in _persistedBuffOwners)
            {
                if (pm == null || pm.Deleted)
                {
                    continue;
                }

                scratchBuffs.Clear();
                pm.CollectPersistedBuffs(ref scratchBuffs);

                for (var i = 0; i < scratchBuffs.Count; i++)
                {
                    var buff = scratchBuffs[i];
                    var remaining = buff.RemainingDuration;
                    if (remaining > TimeSpan.Zero)
                    {
                        buffRecords.Add((pm, buff, remaining));
                    }
                }
            }
        }
        finally
        {
            scratchBuffs.Dispose();
        }

        writer.WriteEncodedInt(buffRecords.Count);
        for (var i = 0; i < buffRecords.Count; i++)
        {
            var record = buffRecords[i];
            writer.Write(record.Mobile);
            writer.WriteEncodedInt((int)record.Buff.ID);
            writer.WriteEncodedInt(record.Buff.TitleCliloc);
            writer.WriteEncodedInt(record.Buff.SecondaryCliloc);
            writer.Write(record.Buff.Args);
            writer.Write(record.Buff.RetainThroughDeath);
            writer.Write(record.Buff.Duration);
            writer.Write(record.Remaining);
        }
    }

    internal static void DeserializeState(IGenericReader reader)
    {
        ClearRuntimeState();

        var version = reader.ReadEncodedInt();
        if (version != SaveVersion)
        {
            throw new Exception($"Unsupported PlayerBuffPersistence version {version}.");
        }

        // 1. Timed StatMods
        var statModCount = reader.ReadEncodedInt();
        for (var i = 0; i < statModCount; i++)
        {
            var pm = reader.ReadEntity<PlayerMobile>();
            var name = reader.ReadString();
            var duration = reader.ReadTimeSpan();
            var remaining = reader.ReadTimeSpan();

            if (pm == null || pm.Deleted || name == null || pm.GetStatMod(name, true) == null)
            {
                continue;
            }

            if (remaining <= TimeSpan.Zero)
            {
                pm.RemoveStatMod(name);
                continue;
            }

            if (!_statMods.TryGetValue(pm, out var playerMods))
            {
                playerMods = new Dictionary<string, TimedStatModEntry>();
                _statMods[pm] = playerMods;
            }

            var entry = new TimedStatModEntry(pm, name, duration, remaining);
            playerMods[name] = entry;
            entry.StartOrPause();
        }

        // 2. Night Sight timers
        var nightSightCount = reader.ReadEncodedInt();
        for (var i = 0; i < nightSightCount; i++)
        {
            var pm = reader.ReadEntity<PlayerMobile>();
            var remaining = reader.ReadTimeSpan();
            var lightLevel = reader.ReadEncodedInt();

            if (pm == null || pm.Deleted || remaining <= TimeSpan.Zero)
            {
                continue;
            }

            pm.BeginAction<LightCycle>();
            pm.LightLevel = lightLevel;

            var entry = new NightSightEntry(pm, remaining, lightLevel);
            _nightSights[pm] = entry;
            entry.StartOrPause();
        }

        // 3. Persisted BuffInfos
        var buffCount = reader.ReadEncodedInt();
        for (var i = 0; i < buffCount; i++)
        {
            var pm = reader.ReadEntity<PlayerMobile>();
            var icon = (BuffIcon)reader.ReadEncodedInt();
            var titleCliloc = reader.ReadEncodedInt();
            var secondaryCliloc = reader.ReadEncodedInt();
            var args = reader.ReadTextDefinition();
            var retainThroughDeath = reader.ReadBool();
            var duration = reader.ReadTimeSpan();
            var remaining = reader.ReadTimeSpan();

            if (pm == null || pm.Deleted || remaining <= TimeSpan.Zero)
            {
                continue;
            }

            var buff = new BuffInfo(
                icon,
                titleCliloc,
                secondaryCliloc,
                duration,
                args,
                retainThroughDeath,
                pauseWhileOffline: true,
                persistAcrossRestart: true
            );
            pm.RestorePersistedBuff(buff, remaining);
        }
    }

    internal static void ClearRuntimeState()
    {
        foreach (var playerMods in _statMods.Values)
        {
            foreach (var entry in playerMods.Values)
            {
                entry.Cancel();
            }
        }
        _statMods.Clear();

        foreach (var nightSight in _nightSights.Values)
        {
            nightSight.Cancel();
        }
        _nightSights.Clear();

        foreach (var timers in _pausableTimers.Values)
        {
            foreach (var timer in timers)
            {
                timer.CancelWithoutUnregister();
            }
        }
        _pausableTimers.Clear();

        _persistedBuffOwners.Clear();
    }

    private sealed class TimedStatModEntry
    {
        private readonly PlayerMobile _mobile;
        private readonly string _modName;
        private DateTime _startTime;
        private TimeSpan _remaining;
        private TimerExecutionToken _timerToken;

        public TimedStatModEntry(PlayerMobile mobile, string modName, TimeSpan duration, TimeSpan remaining)
        {
            _mobile = mobile;
            _modName = modName;
            Duration = duration;
            _remaining = Utility.Max(remaining, TimeSpan.Zero);
        }

        public TimeSpan Duration { get; }

        public bool IsPaused { get; private set; }

        public TimeSpan GetRemaining() =>
            IsPaused ? _remaining : Utility.Max(_remaining - (Core.Now - _startTime), TimeSpan.Zero);

        public void StartOrPause()
        {
            _timerToken.Cancel();
            _startTime = Core.Now;

            if (_mobile.NetState == null)
            {
                IsPaused = true;
                return;
            }

            IsPaused = false;
            Timer.StartTimer(_remaining, () => ExpireStatMod(_mobile, _modName), out _timerToken);
        }

        public void Pause()
        {
            if (IsPaused)
            {
                return;
            }

            _remaining = Utility.Max(_remaining - (Core.Now - _startTime), TimeSpan.Zero);
            _timerToken.Cancel();
            IsPaused = true;
        }

        public bool Resume()
        {
            if (!IsPaused)
            {
                return true;
            }

            if (_remaining <= TimeSpan.Zero)
            {
                IsPaused = false;
                return false;
            }

            IsPaused = false;
            _startTime = Core.Now;
            Timer.StartTimer(_remaining, () => ExpireStatMod(_mobile, _modName), out _timerToken);
            return true;
        }

        public void Cancel()
        {
            _timerToken.Cancel();
            IsPaused = false;
        }
    }

    private sealed class NightSightEntry
    {
        private readonly PlayerMobile _mobile;
        private DateTime _startTime;
        private TimeSpan _remaining;
        private TimerExecutionToken _timerToken;

        public NightSightEntry(PlayerMobile mobile, TimeSpan remaining, int lightLevel)
        {
            _mobile = mobile;
            _remaining = Utility.Max(remaining, TimeSpan.Zero);
            LightLevel = lightLevel;
        }

        public int LightLevel { get; }

        public bool IsPaused { get; private set; }

        public TimeSpan GetRemaining() =>
            IsPaused ? _remaining : Utility.Max(_remaining - (Core.Now - _startTime), TimeSpan.Zero);

        public void StartOrPause()
        {
            _timerToken.Cancel();
            _startTime = Core.Now;

            if (_mobile.NetState == null)
            {
                IsPaused = true;
                return;
            }

            IsPaused = false;
            Timer.StartTimer(_remaining, () => ExpireNightSight(_mobile), out _timerToken);
        }

        public void Pause()
        {
            if (IsPaused)
            {
                return;
            }

            _remaining = Utility.Max(_remaining - (Core.Now - _startTime), TimeSpan.Zero);
            _timerToken.Cancel();
            IsPaused = true;
        }

        public bool Resume()
        {
            if (!IsPaused)
            {
                return true;
            }

            if (_remaining <= TimeSpan.Zero)
            {
                IsPaused = false;
                return false;
            }

            IsPaused = false;
            _startTime = Core.Now;
            Timer.StartTimer(_remaining, () => ExpireNightSight(_mobile), out _timerToken);
            return true;
        }

        public void Cancel()
        {
            _timerToken.Cancel();
            IsPaused = false;
        }
    }
}

/// <summary>
/// One-shot timer tied to a mobile that pauses while a PlayerMobile is disconnected and resumes on reconnect.
/// For non-player mobiles, behaves as a normal real-time one-shot timer.
/// </summary>
public sealed class OfflinePausableTimer
{
    private readonly PlayerMobile _player;
    private readonly Action _onExpire;
    private DateTime _startTime;
    private TimeSpan _remaining;
    private TimerExecutionToken _timerToken;
    private bool _active;

    private OfflinePausableTimer(Mobile owner, TimeSpan duration, Action onExpire)
    {
        _player = owner as PlayerMobile;
        _onExpire = onExpire;
        _remaining = Utility.Max(duration, TimeSpan.Zero);
        _active = true;
        _startTime = Core.Now;

        if (_player != null)
        {
            PlayerBuffPersistence.RegisterPausableTimer(_player, this);

            if (_player.NetState == null)
            {
                IsPaused = true;
                return;
            }
        }

        IsPaused = false;
        Timer.StartTimer(_remaining, OnTimerTick, out _timerToken);
    }

    public bool IsPaused { get; private set; }

    public bool Running => _active;

    public TimeSpan Remaining
    {
        get
        {
            if (!_active)
            {
                return TimeSpan.Zero;
            }

            return IsPaused ? _remaining : Utility.Max(_remaining - (Core.Now - _startTime), TimeSpan.Zero);
        }
    }

    public static OfflinePausableTimer Start(Mobile owner, TimeSpan duration, Action onExpire) =>
        new(owner, duration, onExpire);

    public void Stop()
    {
        if (!_active)
        {
            return;
        }

        _active = false;
        IsPaused = false;
        _timerToken.Cancel();

        if (_player != null)
        {
            PlayerBuffPersistence.UnregisterPausableTimer(_player, this);
        }
    }

    internal void CancelWithoutUnregister()
    {
        _active = false;
        IsPaused = false;
        _timerToken.Cancel();
    }

    internal void Pause()
    {
        if (!_active || IsPaused)
        {
            return;
        }

        _remaining = Utility.Max(_remaining - (Core.Now - _startTime), TimeSpan.Zero);
        _timerToken.Cancel();
        IsPaused = true;
    }

    internal void Resume()
    {
        if (!_active || !IsPaused)
        {
            return;
        }

        if (_remaining <= TimeSpan.Zero)
        {
            OnTimerTick();
            return;
        }

        IsPaused = false;
        _startTime = Core.Now;
        Timer.StartTimer(_remaining, OnTimerTick, out _timerToken);
    }

    private void OnTimerTick()
    {
        if (!_active)
        {
            return;
        }

        Stop();
        _onExpire?.Invoke();
    }
}
