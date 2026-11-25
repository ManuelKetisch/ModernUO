using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Server.Assistants;
using Server.Engines.PartySystem;
using Server.Guilds;
using Server.Json;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;

namespace Server.Tests.Assistants;

[Collection("Sequential UOContent Tests")]
public class AssistantProtocolTests : IDisposable
{
    private readonly AssistantSettings _savedSettings = AssistantConfiguration.Settings;
    private readonly bool _savedEnabled = AssistantHandler.Enabled;
    private readonly List<IEntity> _created = [];
    private readonly List<NetState> _states = [];

    public void Dispose()
    {
        AssistantHandler.ResetForTesting();
        AssistantHandler.SetEnabledForTesting(_savedEnabled);
        AssistantConfiguration.SetSettingsForTesting(_savedSettings);

        for (var i = _created.Count - 1; i >= 0; i--)
        {
            if (_created[i] is Mobile m)
            {
                m.NetState = null;
            }

            _created[i].Delete();
        }

        for (var i = 0; i < _states.Count; i++)
        {
            _states[i].Dispose();
        }
    }

    private PlayerMobile CreateConnectedPlayer(AccessLevel accessLevel = AccessLevel.Player, Point3D? location = null)
    {
        var pm = new PlayerMobile { Player = true, Body = 400, AccessLevel = accessLevel };
        _created.Add(pm);
        pm.MoveToWorld(location ?? new Point3D(1000, 1000, 0), Map.Felucca);

        var ns = PacketTestUtilities.CreateTestNetState();
        _states.Add(ns);
        ns.Mobile = pm;
        pm.NetState = ns;

        return pm;
    }

    [Fact]
    public void AssistantFeatures_BitPositionsMatchCrossUOAndRazorSpec()
    {
        Assert.Equal(0ul, (ulong)AssistantFeatures.None);
        Assert.Equal(1ul << 0, (ulong)AssistantFeatures.FilterWeather);
        Assert.Equal(1ul << 1, (ulong)AssistantFeatures.FilterLight);
        Assert.Equal(1ul << 2, (ulong)AssistantFeatures.SmartTarget);
        Assert.Equal(1ul << 3, (ulong)AssistantFeatures.RangedTarget);
        Assert.Equal(1ul << 4, (ulong)AssistantFeatures.AutoOpenDoors);
        Assert.Equal(1ul << 5, (ulong)AssistantFeatures.DequipOnCast);
        Assert.Equal(1ul << 6, (ulong)AssistantFeatures.AutoPotionEquip);
        Assert.Equal(1ul << 7, (ulong)AssistantFeatures.PoisonedChecks);
        Assert.Equal(1ul << 8, (ulong)AssistantFeatures.LoopedMacros);
        Assert.Equal(1ul << 9, (ulong)AssistantFeatures.UseOnceAgent);
        Assert.Equal(1ul << 10, (ulong)AssistantFeatures.RestockAgent);
        Assert.Equal(1ul << 11, (ulong)AssistantFeatures.SellAgent);
        Assert.Equal(1ul << 12, (ulong)AssistantFeatures.BuyAgent);
        Assert.Equal(1ul << 13, (ulong)AssistantFeatures.PotionHotkeys);
        Assert.Equal(1ul << 14, (ulong)AssistantFeatures.RandomTargets);
        Assert.Equal(1ul << 15, (ulong)AssistantFeatures.ClosestTargets);
        Assert.Equal(1ul << 16, (ulong)AssistantFeatures.OverheadHealth);
        Assert.Equal(1ul << 17, (ulong)AssistantFeatures.AutolootAgent);
        Assert.Equal(1ul << 18, (ulong)AssistantFeatures.BoneCutterAgent);
        Assert.Equal(1ul << 19, (ulong)AssistantFeatures.JScriptMacros);
        Assert.Equal(1ul << 20, (ulong)AssistantFeatures.AutoRemount);
        Assert.Equal(1ul << 21, (ulong)AssistantFeatures.AutoBandage);
        Assert.Equal(1ul << 22, (ulong)AssistantFeatures.EnemyTargetShare);
        Assert.Equal(1ul << 23, (ulong)AssistantFeatures.FilterSeason);
        Assert.Equal(1ul << 24, (ulong)AssistantFeatures.SpellTargetShare);
        Assert.Equal(ulong.MaxValue, (ulong)AssistantFeatures.All);
    }

    [Fact]
    public void AssistantConfiguration_DefaultSettingsAndJsonRoundTrip()
    {
        var defaults = AssistantConfiguration.CreateDefaultSettings();

        Assert.True(defaults.WarnOnFailure);
        Assert.True(defaults.KickOnFailure);
        Assert.Equal(AssistantFeatures.None, defaults.DisallowedFeatures);
        Assert.Equal(TimeSpan.FromSeconds(15), defaults.DisconnectDelay);
        Assert.False(string.IsNullOrWhiteSpace(defaults.WarningMessage));

        var custom = defaults with
        {
            KickOnFailure = false,
            DisallowedFeatures = AssistantFeatures.AutolootAgent | AssistantFeatures.AutoPotionEquip | AssistantFeatures.AutoBandage
        };

        var tempPath = Path.Join(Path.GetTempPath(), $"modernuo-assistants-{Guid.NewGuid():N}.json");
        try
        {
            JsonConfig.Serialize(tempPath, custom);
            var roundTripped = JsonConfig.Deserialize<AssistantSettings>(tempPath);

            Assert.NotNull(roundTripped);
            Assert.False(roundTripped.KickOnFailure);
            Assert.Equal(custom.DisallowedFeatures, roundTripped.DisallowedFeatures);
            Assert.Equal(custom.DisconnectDelay, roundTripped.DisconnectDelay);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void SendAssistVersionReq_WritesExactPacketBytes()
    {
        using var ns = PacketTestUtilities.CreateTestNetState();
        ns.SendAssistVersionReq();

        var sent = ns.SendBuffer.GetReadSpan();
        AssertThat.Equal(sent, [0xBE, 0x00, 0x03]);
    }

    [Theory]
    [InlineData(AssistantFeatures.None)]
    [InlineData(AssistantFeatures.AutolootAgent)]
    [InlineData(AssistantFeatures.AutoPotionEquip | AssistantFeatures.AutoBandage | AssistantFeatures.LoopedMacros)]
    [InlineData(AssistantFeatures.All)]
    public void SendAssistHandshake_WritesBigEndianDisallowedFeaturesMask(AssistantFeatures disallowed)
    {
        using var ns = PacketTestUtilities.CreateTestNetState();
        ns.SendAssistHandshake(disallowed);

        var sent = ns.SendBuffer.GetReadSpan();
        Assert.Equal(12, sent.Length);
        Assert.Equal(0xF0, sent[0]);
        Assert.Equal((ushort)12, BinaryPrimitives.ReadUInt16BigEndian(sent.Slice(1, 2)));
        Assert.Equal(0xFE, sent[3]);
        Assert.Equal((ulong)disallowed, BinaryPrimitives.ReadUInt64BigEndian(sent.Slice(4, 8)));
    }

    [Theory]
    [InlineData("1.6.5.0", "RazorCE 1.6.5.0")]
    [InlineData("RazorEnhanced 0.8.2.210", "RazorEnhanced 0.8.2.210")]
    [InlineData("  CrossUO 1.0  ", "CrossUO 1.0")]
    public void AssistVersion_ParsesAsciiRazorFormat(string payloadText, string expected)
    {
        using var ns = PacketTestUtilities.CreateTestNetState();
        var bytes = Encoding.ASCII.GetBytes($"{payloadText}\0");
        var reader = new SpanReader(bytes);

        AssistantHandler.AssistVersion(ns, reader);

        Assert.Equal(expected, ns.Assistant);
    }

    [Fact]
    public void AssistVersion_ParsesLegacyUOAssistBinaryPrefixFormat()
    {
        using var ns = PacketTestUtilities.CreateTestNetState();

        Span<byte> payload = stackalloc byte[4 + 9];
        BinaryPrimitives.WriteUInt32BigEndian(payload.Slice(0, 4), 1u);
        Encoding.ASCII.GetBytes("7.0.95.0\0", payload.Slice(4));

        var reader = new SpanReader(payload);
        AssistantHandler.AssistVersion(ns, reader);

        Assert.Equal("UOAssist 1 (7.0.95.0)", ns.Assistant);
    }

    [Fact]
    public void AssistVersion_IgnoresEmptyOrWhitespacePayload()
    {
        using var ns = PacketTestUtilities.CreateTestNetState();
        ns.Assistant = "Existing";

        AssistantHandler.AssistVersion(ns, new SpanReader(ReadOnlySpan<byte>.Empty));
        Assert.Equal("Existing", ns.Assistant);

        AssistantHandler.AssistVersion(ns, new SpanReader("   \0"u8));
        Assert.Equal("Existing", ns.Assistant);
    }

    [Fact]
    public void HandshakeLifecycle_DisabledDoesNotSendPackets()
    {
        AssistantHandler.SetEnabledForTesting(false);
        var pm = CreateConnectedPlayer();

        AssistantHandler.OnLogin(pm);

        Assert.Equal(0, pm.NetState.SendBuffer.GetReadSpan().Length);
        Assert.Equal(AssistantHandshakeState.Disabled, AssistantHandler.GetHandshakeState(pm));
    }

    [Fact]
    public void HandshakeLifecycle_LoginPendingThenAckTransitionsToNegotiated()
    {
        AssistantHandler.SetEnabledForTesting(true);
        AssistantConfiguration.SetSettingsForTesting(AssistantConfiguration.CreateDefaultSettings());
        var pm = CreateConnectedPlayer();

        AssistantHandler.OnLogin(pm);

        // 0xBE version req (3 bytes) + 0xF0 0xFE handshake (12 bytes) = 15 bytes
        Assert.Equal(15, pm.NetState.SendBuffer.GetReadSpan().Length);
        Assert.Equal(AssistantHandshakeState.Pending, AssistantHandler.GetHandshakeState(pm));
        Assert.True(AssistantHandler.HasPendingHandshake(pm));

        AssistantHandler.HandshakeResponse(pm.NetState, new SpanReader(ReadOnlySpan<byte>.Empty));

        Assert.Equal(AssistantHandshakeState.Negotiated, AssistantHandler.GetHandshakeState(pm));
        Assert.False(AssistantHandler.HasPendingHandshake(pm));

        AssistantHandler.OnDisconnected(pm);
        Assert.Equal(AssistantHandshakeState.NotNegotiated, AssistantHandler.GetHandshakeState(pm));
    }

    [Fact]
    public void FailedNegotiation_RespectsKickOnFailureFalse()
    {
        AssistantHandler.SetEnabledForTesting(true);
        AssistantConfiguration.SetSettingsForTesting(
            AssistantConfiguration.CreateDefaultSettings() with
            {
                WarnOnFailure = false,
                KickOnFailure = false,
                DisconnectDelay = TimeSpan.FromSeconds(5)
            }
        );

        var pm = CreateConnectedPlayer(AccessLevel.Player);

        AssistantHandler.FailedNegotiation(pm);

        Assert.True(pm.NetState.Running);
        Assert.False(AssistantHandler.HasPendingHandshake(pm));
    }

    [Fact]
    public void FailedNegotiation_ImmediateKickSkipsWarningGumpAndDelayedTimer()
    {
        AssistantHandler.SetEnabledForTesting(true);
        AssistantConfiguration.SetSettingsForTesting(
            AssistantConfiguration.CreateDefaultSettings() with
            {
                WarnOnFailure = true,
                KickOnFailure = true,
                DisconnectDelay = TimeSpan.Zero
            }
        );

        var pm = CreateConnectedPlayer(AccessLevel.Player);
        pm.NetState.SendBuffer.CommitRead(pm.NetState.SendBuffer.ReadableBytes);

        AssistantHandler.FailedNegotiation(pm);

        // Zero-delay kick returns before sending the warning gump or scheduling a delayed disconnect timer.
        Assert.Equal(0, pm.NetState.SendBuffer.ReadableBytes);
        Assert.False(AssistantHandler.HasPendingHandshake(pm));
    }

    [Fact]
    public void FailedNegotiation_SchedulesDelayedDisconnectWhenKickEnabledWithPositiveDelay()
    {
        AssistantHandler.SetEnabledForTesting(true);
        AssistantConfiguration.SetSettingsForTesting(
            AssistantConfiguration.CreateDefaultSettings() with
            {
                WarnOnFailure = false,
                KickOnFailure = true,
                DisconnectDelay = TimeSpan.FromSeconds(5)
            }
        );

        var pm = CreateConnectedPlayer(AccessLevel.Player);

        AssistantHandler.FailedNegotiation(pm);

        Assert.True(AssistantHandler.HasPendingHandshake(pm));
    }

    [Fact]
    public void FailedNegotiation_DoesNotKickStaff()
    {
        AssistantHandler.SetEnabledForTesting(true);
        AssistantConfiguration.SetSettingsForTesting(
            AssistantConfiguration.CreateDefaultSettings() with
            {
                WarnOnFailure = false,
                KickOnFailure = true,
                DisconnectDelay = TimeSpan.Zero
            }
        );

        var gm = CreateConnectedPlayer(AccessLevel.GameMaster);

        AssistantHandler.FailedNegotiation(gm);

        Assert.True(gm.NetState.Running);
        Assert.False(AssistantHandler.HasPendingHandshake(gm));
    }

    [Fact]
    public void MapUO_SendPartyMemberLocations_WritesStandardWireFormat()
    {
        var leader = CreateConnectedPlayer(location: new Point3D(1000, 1000, 0));
        var member = CreateConnectedPlayer(location: new Point3D(1200, 1300, 0));

        var party = new Party(leader);
        party.Add(member);

        leader.NetState.SendBuffer.CommitRead(leader.NetState.SendBuffer.ReadableBytes);
        leader.NetState.SendPartyMemberLocations(leader, party);

        var sent = leader.NetState.SendBuffer.GetReadSpan();
        // Header (4) + 1 member (9) + terminator (4) = 17 bytes
        Assert.Equal(17, sent.Length);
        Assert.Equal(0xF0, sent[0]);
        Assert.Equal((ushort)17, BinaryPrimitives.ReadUInt16BigEndian(sent.Slice(1, 2)));
        Assert.Equal(0x01, sent[3]);
        Assert.Equal(member.Serial.Value, BinaryPrimitives.ReadUInt32BigEndian(sent.Slice(4, 4)));
        Assert.Equal((short)1200, BinaryPrimitives.ReadInt16BigEndian(sent.Slice(8, 2)));
        Assert.Equal((short)1300, BinaryPrimitives.ReadInt16BigEndian(sent.Slice(10, 2)));
        Assert.Equal((byte)Map.Felucca.MapID, sent[12]);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32BigEndian(sent.Slice(13, 4)));
    }

    [Fact]
    public void MapUO_SendGuildMemberLocations_WritesStandardWireFormat()
    {
        var viewer = CreateConnectedPlayer(location: new Point3D(1000, 1000, 0));
        var member = CreateConnectedPlayer(location: new Point3D(1400, 1500, 0));
        member.Hits = 50;

        var guild = new Guild(viewer, "TestGuild", "TST");
        guild.AddMember(member);
        try
        {
            viewer.NetState.SendBuffer.CommitRead(viewer.NetState.SendBuffer.ReadableBytes);
            viewer.NetState.SendGuildMemberLocations(viewer, guild, true);

            var sent = viewer.NetState.SendBuffer.GetReadSpan();
            // Header (4) + locations bool (1) + 1 member (10) + terminator (4) = 19 bytes
            Assert.Equal(19, sent.Length);
            Assert.Equal(0xF0, sent[0]);
            Assert.Equal((ushort)19, BinaryPrimitives.ReadUInt16BigEndian(sent.Slice(1, 2)));
            Assert.Equal(0x02, sent[3]);
            Assert.Equal(1, sent[4]);
            Assert.Equal(member.Serial.Value, BinaryPrimitives.ReadUInt32BigEndian(sent.Slice(5, 4)));
            Assert.Equal((short)1400, BinaryPrimitives.ReadInt16BigEndian(sent.Slice(9, 2)));
            Assert.Equal((short)1500, BinaryPrimitives.ReadInt16BigEndian(sent.Slice(11, 2)));
            Assert.Equal((byte)Map.Felucca.MapID, sent[13]);
            Assert.Equal(0u, BinaryPrimitives.ReadUInt32BigEndian(sent.Slice(15, 4)));
        }
        finally
        {
            guild.Disband();
        }
    }
}
