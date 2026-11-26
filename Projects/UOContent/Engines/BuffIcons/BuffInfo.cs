using System;
using Server.Mobiles;

namespace Server.Engines.BuffIcons;

public class BuffInfo
{
    private TimerExecutionToken _timerToken;
    private TimeSpan _remaining;

    public BuffInfo(
        BuffIcon iconID, int titleCliloc, TimeSpan duration = default, TextDefinition args = null,
        bool retainThroughDeath = false, bool pauseWhileOffline = false, bool persistAcrossRestart = false
    ) : this(
        iconID,
        titleCliloc,
        titleCliloc + 1,
        duration,
        args,
        retainThroughDeath,
        pauseWhileOffline,
        persistAcrossRestart
    )
    {
    }

    public BuffInfo(
        BuffIcon iconID, int titleCliloc, int secondaryCliloc, TimeSpan duration = default, TextDefinition args = null,
        bool retainThroughDeath = false, bool pauseWhileOffline = false, bool persistAcrossRestart = false
    )
    {
        ID = iconID;
        TitleCliloc = titleCliloc;
        SecondaryCliloc = secondaryCliloc;
        Duration = duration;
        _remaining = duration;
        Args = args;
        RetainThroughDeath = retainThroughDeath;
        PauseWhileOffline = pauseWhileOffline || persistAcrossRestart;
        PersistAcrossRestart = persistAcrossRestart;
    }

    public static bool Enabled { get; private set; }

    public BuffIcon ID { get; }

    public int TitleCliloc { get; }

    public int SecondaryCliloc { get; }

    public DateTime StartTime { get; private set; }

    public TimeSpan Duration { get; }

    public bool RetainThroughDeath { get; }

    public bool PauseWhileOffline { get; }

    public bool PersistAcrossRestart { get; }

    public bool IsPaused { get; private set; }

    public TextDefinition Args { get; }

    public TimeSpan RemainingDuration
    {
        get
        {
            if (Duration <= TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            if (IsPaused)
            {
                return _remaining;
            }

            return Utility.Max(_remaining - (Core.Now - StartTime), TimeSpan.Zero);
        }
    }

    public static void Configure()
    {
        Enabled = ServerConfiguration.GetOrUpdateSetting("buffIcons.enable", Core.ML);
    }

    public void StartTimer(PlayerMobile m) => StartTimer(m, Duration);

    internal void StartTimer(PlayerMobile m, TimeSpan remaining)
    {
        _timerToken.Cancel();

        if (Duration <= TimeSpan.Zero)
        {
            IsPaused = false;
            return;
        }

        _remaining = Utility.Max(remaining, TimeSpan.Zero);
        StartTime = Core.Now;

        if (PauseWhileOffline && m.NetState == null)
        {
            IsPaused = true;
            return;
        }

        IsPaused = false;
        var id = ID;
        Timer.StartTimer(_remaining, () => m.RemoveBuff(id), out _timerToken);
    }

    public void PauseTimer()
    {
        if (Duration <= TimeSpan.Zero || !PauseWhileOffline || IsPaused)
        {
            return;
        }

        _remaining = Utility.Max(_remaining - (Core.Now - StartTime), TimeSpan.Zero);
        _timerToken.Cancel();
        IsPaused = true;
    }

    public bool ResumeTimer(PlayerMobile m)
    {
        if (Duration <= TimeSpan.Zero || !PauseWhileOffline || !IsPaused)
        {
            return true;
        }

        if (_remaining <= TimeSpan.Zero)
        {
            IsPaused = false;
            return false;
        }

        IsPaused = false;
        StartTime = Core.Now;
        var id = ID;
        Timer.StartTimer(_remaining, () => m.RemoveBuff(id), out _timerToken);
        return true;
    }

    public void StopTimer()
    {
        _timerToken.Cancel();
        IsPaused = false;
    }
}
