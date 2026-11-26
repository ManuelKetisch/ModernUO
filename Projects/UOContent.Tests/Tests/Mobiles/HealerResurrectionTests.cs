using System;
using System.Linq;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Mobiles;

[Collection("Sequential UOContent Tests")]
public class HealerResurrectionTests : IDisposable
{
    private readonly MobileTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    private PlayerMobile CreateKillablePlayer()
    {
        var player = _harness.Track(new PlayerMobile());
        player.Player = true;
        return player;
    }

    private sealed class RecordingHealer : Healer
    {
        public int OfferCount { get; private set; }

        public override void OfferResurrection(Mobile m)
        {
            OfferCount++;
        }
    }

    private sealed class RecordingHealerGuildmaster : HealerGuildmaster
    {
        public int OfferCount { get; private set; }

        public override void OfferResurrection(Mobile m)
        {
            OfferCount++;
        }
    }

    [Fact]
    public void HealerResurrectsWhenEnteringRangeOrLeavingBlockedTileOrDoubleClickedDead()
    {
        var map = Map.Felucca;
        var player = CreateKillablePlayer();
        var healer = _harness.Track(new RecordingHealer());

        // Clear open tile
        healer.MoveToWorld(new Point3D(1100, 1100, 0), map);
        player.MoveToWorld(new Point3D(1103, 1100, 0), map);
        player.Kill();
        Assert.False(player.Alive);

        // Entering range 4 from outside range 4 triggers resurrection.
        healer.OnMovement(player, new Point3D(1105, 1100, 0));
        Assert.Equal(1, healer.OfferCount);

        // Moving inside open range 4 without crossing range/LOS/blocked-tile boundary does not spam.
        healer.OnMovement(player, new Point3D(1102, 1100, 0));
        Assert.Equal(1, healer.OfferCount);

        // Double-clicking healer while dead within range 4 offers resurrection explicitly.
        healer.OnDoubleClickDead(player);
        Assert.Equal(2, healer.OfferCount);

        // Out of range double-click does not offer resurrection.
        player.MoveToWorld(new Point3D(1108, 1100, 0), map);
        healer.OnDoubleClickDead(player);
        Assert.Equal(2, healer.OfferCount);

        // Stepping through an impassable/blocking obstacle (like a closed door) inside range 4
        // triggers resurrection as soon as the ghost steps onto a clear tile in LOS.
        var door = _harness.TrackItem(new MetalDoor(DoorFacing.WestCW));
        door.MoveToWorld(new Point3D(1102, 1100, 0), map);
        var originalDoorData = TileData.ItemTable[door.ItemID];
        TileData.ItemTable[door.ItemID] = new ItemData(
            originalDoorData.Name,
            originalDoorData.Flags | TileFlag.Impassable | TileFlag.NoShoot | TileFlag.Door,
            originalDoorData.Weight,
            originalDoorData.Quality,
            originalDoorData.Animation,
            originalDoorData.Quantity,
            originalDoorData.Value,
            20
        );
        try
        {
            var healer2 = _harness.Track(new RecordingHealer());
            healer2.MoveToWorld(new Point3D(1100, 1100, 0), map);
            player.MoveToWorld(new Point3D(1101, 1100, 0), map);

            // Ghost walked from behind the closed door (1103, 1100) into the room (1101, 1100) —
            // both within range 4, but oldLocation was blocked by the closed door.
            healer2.OnMovement(player, new Point3D(1103, 1100, 0));
            Assert.Equal(1, healer2.OfferCount);

            // Stepping off the closed door tile itself (1102, 1100) onto clear floor (1101, 1100)
            // also triggers once cooldown is not blocking (fresh healer).
            var healer3 = _harness.Track(new RecordingHealer());
            healer3.MoveToWorld(new Point3D(1100, 1100, 0), map);
            healer3.OnMovement(player, new Point3D(1102, 1100, 0));
            Assert.Equal(1, healer3.OfferCount);
        }
        finally
        {
            TileData.ItemTable[door.ItemID] = originalDoorData;
        }
    }

    [Fact]
    public void HealerGuildmasterResurrectsDeadPlayersAndBlocksCriminals()
    {
        var map = Map.Felucca;
        var player = CreateKillablePlayer();
        var gm = _harness.Track(new RecordingHealerGuildmaster());

        gm.MoveToWorld(new Point3D(1110, 1110, 0), map);
        player.MoveToWorld(new Point3D(1112, 1110, 0), map);
        player.Kill();
        Assert.False(player.Alive);

        gm.OnMovement(player, new Point3D(1116, 1110, 0));
        Assert.Equal(1, gm.OfferCount);

        gm.OnDoubleClickDead(player);
        Assert.Equal(2, gm.OfferCount);

        player.Criminal = true;
        gm.OnDoubleClickDead(player);
        Assert.Equal(2, gm.OfferCount);

        var entries = PooledRefList<ContextMenuEntry>.Create();
        try
        {
            gm.GetContextMenuEntries(player, ref entries);
            Assert.Contains(entries.ToArray(), e => e.Number == 3006195 && e.Enabled);
        }
        finally
        {
            entries.Dispose();
        }
    }

    [Fact]
    public void AnkhContextMenuEnablesResurrectOnlyWhenDead()
    {
        var map = Map.Felucca;
        var player = CreateKillablePlayer();
        var ankh = _harness.TrackItem(new AnkhWest());
        ankh.MoveToWorld(new Point3D(1120, 1120, 0), map);
        player.MoveToWorld(new Point3D(1121, 1120, 0), map);

        Assert.True(player.Alive);
        var aliveEntries = PooledRefList<ContextMenuEntry>.Create();
        try
        {
            Ankhs.GetContextMenuEntries(player, ankh, ref aliveEntries);
            var resEntry = aliveEntries.ToArray().Single(e => e.Number == 3006195);
            Assert.False(resEntry.Enabled);
        }
        finally
        {
            aliveEntries.Dispose();
        }

        player.Kill();
        Assert.False(player.Alive);
        var deadEntries = PooledRefList<ContextMenuEntry>.Create();
        try
        {
            Ankhs.GetContextMenuEntries(player, ankh, ref deadEntries);
            var resEntry = deadEntries.ToArray().Single(e => e.Number == 3006195);
            Assert.True(resEntry.Enabled);
        }
        finally
        {
            deadEntries.Dispose();
        }
    }
}
