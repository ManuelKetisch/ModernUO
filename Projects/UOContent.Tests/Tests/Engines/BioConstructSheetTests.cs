using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using Server;
using Server.Engines.BioEngineering;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public sealed class BioConstructSheetTests : IDisposable
{
    private readonly MobileTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void InfusionRecordsTheFinalFourNamedDonorsAcrossSerialization()
    {
        var owner = CreateEngineer();
        var set = Pack(owner, new BioVialSet());
        Assert.True(set.TryAdd(owner, Sample(owner, BioSampleKind.Prowess, "first donor")));
        Assert.True(set.TryAdd(owner, Sample(owner, BioSampleKind.Environment, "fen wolf")));
        Assert.True(set.TryAdd(owner, Sample(owner, BioSampleKind.Mental, "ash mage")));
        Assert.True(set.TryAdd(owner, Sample(owner, BioSampleKind.Mimic, "wandering fighter")));
        Assert.True(set.TrySwap(owner, Sample(owner, BioSampleKind.Prowess, "mountain brute")));

        var construct = NewConstruct(owner);
        Assert.True(set.TryInfuse(owner, construct));
        Assert.True(set.Deleted);

        var writer = new BufferWriter(true);
        construct.Serialize(writer);
        var loaded = _harness.Track(new BioConstruct(World.NewMobile));
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        loaded.Deserialize(reader);

        Assert.Equal(writer.Position, reader.Position);
        Assert.Equal("mountain brute", loaded.DonorName(BioSampleKind.Prowess));
        Assert.Equal("fen wolf", loaded.DonorName(BioSampleKind.Environment));
        Assert.Equal("ash mage", loaded.DonorName(BioSampleKind.Mental));
        Assert.Equal("wandering fighter", loaded.DonorName(BioSampleKind.Mimic));
        Assert.Equal("Donor unrecorded", loaded.DonorName(BioSampleKind.None));
    }

    [Fact]
    public void NearbyOwnerCanViewEquipmentAndActiveEffectsThroughTheSheetCommand()
    {
        var owner = CreateEngineer();
        var stranger = CreateEngineer();
        var bio = NewConstruct(owner);
        Assert.True(bio.ApplySamples(
            [120, 250, 10, 14, 15, 20, 500, 500, 500],
            [100, 200, 0, 0, 0],
            [65, 100, 0, 0, 0, 0, 0, 0],
            [400, 0, 0, (int)BioTrait.SearingStrike, 0, 0, 0, 0, 0]
        ));
        var sword = Pack(owner, new Longsword { Name = "Old patrol sword", Layer = Layer.OneHanded });
        Assert.True(sword.TryEnableWeaponLeveling(owner));
        sword.AwardWeaponExperience(320, owner);
        Assert.True(bio.EquipItem(sword));
        Assert.True(bio.EquipItem(Pack(owner, new GoldRing { Name = "Copper signet", Layer = Layer.Ring })));

        _harness.TrackItem(new Anvil()).MoveToWorld(owner.Location, owner.Map);
        _harness.TrackItem(new Forge()).MoveToWorld(owner.Location, owner.Map);
        Pack(owner, new BronzeIngot(15));
        Pack(owner, new AgapiteIngot(20));
        Pack(owner, new ShadowIronIngot(15));
        Refit(bio, owner, BioRefitChange.AgapiteFrame);
        Refit(bio, owner, BioRefitChange.InstallCircuit);
        Refit(bio, owner, BioRefitChange.InstallRegulator);

        BioConstructSheet.Initialize();
        using var ownerState = Connect(owner);
        using var strangerState = Connect(stranger);
        try
        {
            Assert.True(CommandSystem.Handle(stranger, "[BioSheet"));
            stranger.Target.Invoke(stranger, bio);
            Assert.Null(stranger.FindGump<BioConstruct.BioSheetGump>());

            var position = owner.Location;
            owner.MoveToWorld(new Point3D(position.X + 4, position.Y, position.Z), owner.Map);
            Assert.False(bio.TryOpenSheet(owner));
            owner.MoveToWorld(position, owner.Map);

            Assert.True(CommandSystem.Handle(owner, "[BioSheet"));
            owner.Target.Invoke(owner, bio);
            var sheet = Assert.IsType<BioConstruct.BioSheetGump>(owner.FindGump<BioConstruct.BioSheetGump>());
            var buffer = new byte[8192];
            var writer = new SpanWriter(buffer);
            sheet.Compile(ref writer);
            var strings = ReadGumpStrings(writer.Span);
            Assert.Contains("Donor unrecorded", strings);
            Assert.Contains("Old patrol sword", strings);
            Assert.Contains("Copper signet", strings);
            Assert.Contains("Weapon level 2   320 XP   Unspent points 5", strings);
            Assert.Contains("Recover 4 mana", strings);
            Assert.Contains("9 stamina, 14-second cooldown", strings);
            Assert.Contains("Recover 5 stamina", strings);
            Assert.Contains("Once every 18 seconds", strings);

            owner.CloseGump<BioConstruct.BioSheetGump>();
            var speech = new SpeechEventArgs(owner, $"{bio.Name} sheet", MessageType.Regular, 0, []);
            bio.OnSpeech(speech);
            Assert.True(speech.Handled);
            Assert.NotNull(owner.FindGump<BioConstruct.BioSheetGump>());
        }
        finally
        {
            owner.CloseGump<BioConstruct.BioSheetGump>();
            owner.NetState = null;
            ownerState.Mobile = null;
            stranger.NetState = null;
            strangerState.Mobile = null;
        }
    }

    private static string ReadGumpStrings(ReadOnlySpan<byte> packet)
    {
        // Skip the packet header, packed layout (whose length includes its uncompressed-size field),
        // string count, and string-block sizes.
        var layoutLength = BinaryPrimitives.ReadInt32BigEndian(packet[19..]);
        using var input = new MemoryStream(packet[(19 + 4 + layoutLength + 12)..].ToArray());
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var plain = new MemoryStream();
        zlib.CopyTo(plain);
        return Encoding.BigEndianUnicode.GetString(plain.ToArray());
    }

    private static NetState Connect(PlayerMobile player)
    {
        var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = player;
        player.NetState = state;
        return state;
    }

    private static void Refit(BioConstruct bio, PlayerMobile owner, BioRefitChange change)
    {
        var quote = bio.QuoteRefit(owner, change);
        Assert.True(quote.HasValue);
        Assert.True(bio.TryRefit(owner, quote.Value));
    }

    private PlayerMobile CreateEngineer()
    {
        var player = _harness.Track(new PlayerMobile());
        player.Race = Race.Human;
        player.Player = true;
        player.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);
        player.AddItem(_harness.TrackItem(new Backpack()));
        player.Skills.AnimalTaming.Base = 100;
        player.Skills.Tinkering.Base = 100;
        Pack(player, new BioEngineeringBook()).OnDoubleClick(player);
        return player;
    }

    private BioConstruct NewConstruct(PlayerMobile owner) =>
        _harness.Track(Assert.IsType<BioConstruct>(Pack(owner, new BioCreationDeed()).TryCreate(owner)));

    private BioVial Sample(PlayerMobile owner, BioSampleKind kind, string name)
    {
        var creature = _harness.NewCreature();
        creature.Name = name;
        creature.MoveToWorld(new Point3D(owner.X + 1, owner.Y, owner.Z), owner.Map);
        var vial = Pack(owner, new BioVial());
        Assert.True(vial.TrySample(owner, kind, creature, 0.0));
        return vial;
    }

    private T Pack<T>(PlayerMobile owner, T item) where T : Item
    {
        owner.Backpack.DropItem(_harness.TrackItem(item));
        return item;
    }
}
