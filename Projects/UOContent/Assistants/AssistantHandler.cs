using System;
using System.Buffers;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Assistants;

public enum AssistantHandshakeState
{
    Disabled,
    Pending,
    Negotiated,
    NotNegotiated
}

public static class AssistantHandler
{
    public static bool Enabled { get; private set; }

    private static readonly Dictionary<Mobile, Timer> _handshakes = new();
    private static readonly HashSet<Mobile> _negotiated = new();

    public static unsafe void Configure()
    {
        Enabled = ServerConfiguration.GetOrUpdateSetting("assistants.enableNegotiation", false);

        IncomingPackets.Register(0xBE, 0, false, &AssistVersion);
        AssistantProtocol.Register(0xFF, false, &HandshakeResponse);
        EventSink.Disconnected += OnDisconnected;
    }

    public static AssistantHandshakeState GetHandshakeState(Mobile m)
    {
        if (!Enabled)
        {
            return AssistantHandshakeState.Disabled;
        }

        if (m == null)
        {
            return AssistantHandshakeState.NotNegotiated;
        }

        if (_negotiated.Contains(m))
        {
            return AssistantHandshakeState.Negotiated;
        }

        return _handshakes.ContainsKey(m)
            ? AssistantHandshakeState.Pending
            : AssistantHandshakeState.NotNegotiated;
    }

    public static void BroadcastDisallowedFeatures()
    {
        if (!Enabled)
        {
            return;
        }

        var disallowed = AssistantConfiguration.Settings?.DisallowedFeatures ?? AssistantFeatures.None;

        foreach (var ns in NetState.Instances)
        {
            if (ns.Running && ns.Mobile != null)
            {
                ns.SendAssistHandshake(disallowed);
            }
        }
    }

    internal static void SetEnabledForTesting(bool enabled)
    {
        Enabled = enabled;
    }

    internal static void ResetForTesting()
    {
        foreach (var timer in _handshakes.Values)
        {
            timer?.Stop();
        }

        _handshakes.Clear();
        _negotiated.Clear();
    }

    internal static bool HasPendingHandshake(Mobile m) => m != null && _handshakes.ContainsKey(m);

    internal static void FailedNegotiation(Mobile m)
    {
        if (m?.NetState?.Running != true)
        {
            return;
        }

        var settings = AssistantConfiguration.Settings ?? AssistantConfiguration.CreateDefaultSettings();
        var isPlayer = m.AccessLevel <= AccessLevel.Player;
        var willKick = settings.KickOnFailure;
        var delay = settings.DisconnectDelay;

        if (willKick && isPlayer && delay <= TimeSpan.Zero)
        {
            m.NetState.Disconnect("Failed to negotiate assistant features.");
            return;
        }

        if (settings.WarnOnFailure)
        {
            m.SendGump(new AssistantFailureWarningGump());
        }

        if (willKick && isPlayer)
        {
            _handshakes[m] = Timer.DelayCall(delay, OnForceDisconnect, m);
        }

        m.NetState.LogInfo("Failed to negotiate assistant features.");
    }

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm)
    {
        if (pm?.NetState?.Running != true || !Enabled)
        {
            return;
        }

        _negotiated.Remove(pm);
        pm.NetState.SendAssistVersionReq();
        pm.NetState.SendAssistHandshake();

        if (_handshakes.TryGetValue(pm, out var t))
        {
            t?.Stop();
        }

        _handshakes[pm] = Timer.DelayCall(TimeSpan.FromSeconds(30), OnTimeout, pm);
    }

    internal static void OnDisconnected(Mobile m)
    {
        if (m == null)
        {
            return;
        }

        if (_handshakes.Remove(m, out var t))
        {
            t?.Stop();
        }

        _negotiated.Remove(m);
    }

    [OnEvent(nameof(PlayerMobile.PlayerDeletedEvent))]
    public static void OnPlayerDeleted(Mobile m) => OnDisconnected(m);

    public static void AssistVersion(NetState state, SpanReader reader)
    {
        if (state == null || reader.Remaining <= 0)
        {
            return;
        }

        // Legacy UOAssist / CrossUO 0xBE starts with a 4-byte big-endian version followed by an ASCII client version;
        // RazorCE / Razor Enhanced / ClassicUO send a null-terminated ASCII string directly.
        var firstByte = reader.Buffer[reader.Position];
        if (firstByte < 0x20 && reader.Remaining >= 4)
        {
            var version = reader.ReadUInt32();
            var clientVersion = reader.ReadAscii().Trim();
            state.Assistant = string.IsNullOrEmpty(clientVersion)
                ? $"UOAssist {version}"
                : $"UOAssist {version} ({clientVersion})";
            return;
        }

        var assistVersion = reader.ReadAscii().Trim();
        if (string.IsNullOrEmpty(assistVersion))
        {
            return;
        }

        state.Assistant = assistVersion.ContainsOrdinal(' ') ? assistVersion : $"RazorCE {assistVersion}";
    }

    internal static void HandshakeResponse(NetState state, SpanReader reader)
    {
        var m = state?.Mobile;

        if (m == null)
        {
            return;
        }

        _negotiated.Add(m);

        if (_handshakes.Remove(m, out var t))
        {
            t?.Stop();
        }
    }

    private static void OnTimeout(Mobile m)
    {
        if (m == null || !_handshakes.Remove(m, out var t))
        {
            return;
        }

        t?.Stop();

        if (m.NetState?.Running != true || !Enabled)
        {
            return;
        }

        FailedNegotiation(m);
    }

    private static void OnForceDisconnect(Mobile m)
    {
        if (m == null)
        {
            return;
        }

        m.NetState?.Disconnect($"Player {m} kicked (Failed assistant handshake)");
        _handshakes.Remove(m);
        _negotiated.Remove(m);
    }

    public static void SendAssistVersionReq(this NetState ns)
    {
        if (ns.CannotSendPackets())
        {
            return;
        }

        ns.Send(stackalloc byte[] { 0xBE, 0x00, 0x03 });
    }

    public static void SendAssistHandshake(this NetState ns) =>
        ns.SendAssistHandshake(AssistantConfiguration.Settings?.DisallowedFeatures ?? AssistantFeatures.None);

    public static void SendAssistHandshake(this NetState ns, AssistantFeatures disallowedFeatures)
    {
        if (ns.CannotSendPackets())
        {
            return;
        }

        var writer = new SpanWriter(stackalloc byte[12]);
        writer.Write((byte)0xF0); // Packet ID
        writer.Write((ushort)12);
        writer.Write((byte)0xFE); // Command
        writer.Write((ulong)disallowedFeatures);

        ns.Send(writer.Span);
    }

    private class AssistantFailureWarningGump : StaticWarningGump<AssistantFailureWarningGump>
    {
        private static TextDefinition WarningMessage =>
            string.IsNullOrWhiteSpace(AssistantConfiguration.Settings?.WarningMessage)
                ? AssistantConfiguration.DefaultWarningMessage
                : AssistantConfiguration.Settings.WarningMessage;

        public override int StaticLocalizedContent => WarningMessage.Number;
        public override string Content => WarningMessage.String;
        public override int Width => 420;
        public override int Height => 250;
        public override bool CancelButton => false;
    }
}
