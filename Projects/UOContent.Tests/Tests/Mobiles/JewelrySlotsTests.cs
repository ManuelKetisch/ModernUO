using System;
using System.Buffers;
using System.Collections.Generic;
using Server;
using Server.Collections;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Mobiles;

[Collection("Sequential UOContent Tests")]
public class JewelrySlotsTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void PlayerCanEquipSixRingsAndAllBonusesStack()
    {
        var player = NewPlayer();
        player.Skills.Swords.Base = 50.0;
        var baseStr = player.Str;
        var baseFire = player.FireResistance;
        var baseSwords = player.Skills.Swords.Value;

        var rings = new GoldRing[6];
        for (var i = 0; i < 6; i++)
        {
            var ring = Carry(player, new GoldRing());
            ring.Attributes.BonusStr = i + 1;       // 1+2+3+4+5+6 = 21
            ring.Attributes.SpellDamage = 2;        // 6 * 2 = 12
            ring.Attributes.Luck = 50;              // 6 * 50 = 300
            ring.Resistances.Fire = 3;              // 6 * 3 = 18
            ring.SkillBonuses.SetValues(0, SkillName.Swords, 2.0); // 6 * 2.0 = 12.0
            rings[i] = ring;

            Assert.True(player.EquipItem(ring));
        }

        Assert.Same(rings[0], player.FindItemOnLayer(JewelrySlots.Ring1));
        Assert.Same(rings[1], player.FindItemOnLayer(JewelrySlots.Ring2));
        Assert.Same(rings[2], player.FindItemOnLayer(JewelrySlots.Ring3));
        Assert.Same(rings[3], player.FindItemOnLayer(JewelrySlots.Ring4));
        Assert.Same(rings[4], player.FindItemOnLayer(JewelrySlots.Ring5));
        Assert.Same(rings[5], player.FindItemOnLayer(JewelrySlots.Ring6));

        // Distinct layers ensure SendMobileIncoming (0x78) and ProcessDelta never drop or dedupe them.
        var uniqueLayers = new HashSet<byte>();
        for (var i = 0; i < 6; i++)
        {
            Assert.False(rings[i].IsDupedEquipLayer());
            Assert.True(uniqueLayers.Add((byte)rings[i].Layer));
        }

        Assert.Equal(baseStr + 21, player.Str);
        Assert.Equal(12, AosAttributes.GetValue(player, AosAttribute.SpellDamage));
        Assert.Equal(300, AosAttributes.GetValue(player, AosAttribute.Luck));
        Assert.Equal(baseFire + 18, player.FireResistance);
        Assert.Equal(baseSwords + 12.0, player.Skills.Swords.Value, 1);

        // 7th ring must be rejected and retain canonical Layer.Ring.
        var seventh = Carry(player, new SilverRing());
        seventh.Attributes.BonusStr = 5;
        Assert.False(player.EquipItem(seventh));
        Assert.Same(player.Backpack, seventh.Parent);
        Assert.Equal(Layer.Ring, seventh.Layer);

        // Removing Ring 3 resets its layer and removes only its bonuses.
        player.Backpack.DropItem(rings[2]);
        Assert.Equal(Layer.Ring, rings[2].Layer);
        Assert.Null(player.FindItemOnLayer(JewelrySlots.Ring3));
        Assert.Equal(baseStr + 18, player.Str);
        Assert.Equal(10, AosAttributes.GetValue(player, AosAttribute.SpellDamage));
        Assert.Equal(250, AosAttributes.GetValue(player, AosAttribute.Luck));
        Assert.Equal(baseFire + 15, player.FireResistance);
        Assert.Equal(baseSwords + 10.0, player.Skills.Swords.Value, 1);

        // Equipping the 7th ring now fills the vacated Ring 3 slot.
        Assert.True(player.EquipItem(seventh));
        Assert.Same(seventh, player.FindItemOnLayer(JewelrySlots.Ring3));
        Assert.Equal(JewelrySlots.Ring3, seventh.Layer);
        Assert.Equal(baseStr + 23, player.Str);
    }

    [Fact]
    public void PlayerCanEquipTwoEarringsAndThirdIsRejected()
    {
        var player = NewPlayer();
        var baseCold = player.ColdResistance;

        var left = Carry(player, new GoldEarrings());
        left.Attributes.Luck = 40;
        left.Resistances.Cold = 4;

        // EarringsOfProtection derives directly from BaseJewel with Layer.Earrings (not BaseEarrings).
        var right = Carry(player, new EarringsOfProtection(AosElementAttribute.Cold));
        right.Attributes.Luck = 35;

        var third = Carry(player, new SilverEarrings());

        Assert.True(player.EquipItem(left));
        Assert.True(player.EquipItem(right));
        Assert.Same(left, player.FindItemOnLayer(JewelrySlots.Earrings1));
        Assert.Same(right, player.FindItemOnLayer(JewelrySlots.Earrings2));
        Assert.False(left.IsDupedEquipLayer());
        Assert.False(right.IsDupedEquipLayer());
        Assert.Equal(75, AosAttributes.GetValue(player, AosAttribute.Luck));
        Assert.Equal(baseCold + 4 + right.ColdResistance, player.ColdResistance);

        Assert.False(player.EquipItem(third));
        Assert.Same(player.Backpack, third.Parent);
        Assert.Equal(Layer.Earrings, third.Layer);

        // Unequipping right earring resets its layer and opens Earrings2.
        player.Backpack.DropItem(right);
        Assert.Equal(Layer.Earrings, right.Layer);
        Assert.Null(player.FindItemOnLayer(JewelrySlots.Earrings2));
        Assert.True(player.EquipItem(third));
        Assert.Same(third, player.FindItemOnLayer(JewelrySlots.Earrings2));
    }

    [Fact]
    public void NecklaceAndBraceletRemainSingleSlot()
    {
        var player = NewPlayer();
        var neck1 = Carry(player, new GoldNecklace());
        var neck2 = Carry(player, new SilverNecklace());
        var brace1 = Carry(player, new GoldBracelet());
        var brace2 = Carry(player, new SilverBracelet());

        Assert.True(player.EquipItem(neck1));
        Assert.False(player.EquipItem(neck2));
        Assert.Same(neck1, player.FindItemOnLayer(Layer.Neck));
        Assert.Same(player.Backpack, neck2.Parent);

        Assert.True(player.EquipItem(brace1));
        Assert.False(player.EquipItem(brace2));
        Assert.Same(brace1, player.FindItemOnLayer(Layer.Bracelet));
        Assert.Same(player.Backpack, brace2.Parent);
    }

    [Fact]
    public void TargetedJewelrySlotEquipAndCrossFamilyIsolationWork()
    {
        var player = NewPlayer();
        var ringA = Carry(player, new GoldRing());
        var ringB = Carry(player, new SilverRing());
        var ringC = Carry(player, new WeddingRing());
        var earring = Carry(player, new GoldEarrings());
        var sword = Carry(player, new Katana { Layer = Layer.OneHanded });

        // Explicitly targeting Ring5 and Earrings2 places each item in that exact slot.
        Assert.True(player.EquipItem(ringA, JewelrySlots.Ring5));
        Assert.Same(ringA, player.FindItemOnLayer(JewelrySlots.Ring5));
        Assert.Null(player.FindItemOnLayer(JewelrySlots.Ring1));

        Assert.True(player.EquipItem(earring, JewelrySlots.Earrings2));
        Assert.Same(earring, player.FindItemOnLayer(JewelrySlots.Earrings2));
        Assert.Null(player.FindItemOnLayer(JewelrySlots.Earrings1));

        // Cross-family requested layer (earring slot or helm for a ring) is ignored; falls back to Ring1.
        Assert.True(player.EquipItem(ringB, JewelrySlots.Earrings1));
        Assert.Same(ringB, player.FindItemOnLayer(JewelrySlots.Ring1));
        Assert.Null(player.FindItemOnLayer(JewelrySlots.Earrings1));

        // Occupied requested ring slot (Ring5) falls back to first free ring slot (Ring2).
        Assert.True(player.EquipItem(ringC, JewelrySlots.Ring5));
        Assert.Same(ringC, player.FindItemOnLayer(JewelrySlots.Ring2));

        // Non-jewelry item targeting Ring3 still equips to its own Layer.OneHanded.
        Assert.True(player.EquipItem(sword, JewelrySlots.Ring3));
        Assert.Same(sword, player.FindItemOnLayer(Layer.OneHanded));
        Assert.Null(player.FindItemOnLayer(JewelrySlots.Ring3));
    }

    [Fact]
    public void DoubleClickFromBackpackFillsSixRingsAndTwoEarrings()
    {
        var player = NewPlayer();
        var rings = new GoldRing[7];
        for (var i = 0; i < 7; i++)
        {
            rings[i] = Carry(player, new GoldRing());
            player.Use(rings[i]);
        }

        for (var i = 0; i < 6; i++)
        {
            Assert.Same(player, rings[i].Parent);
        }

        Assert.Same(player.Backpack, rings[6].Parent);
        Assert.Equal(Layer.Ring, rings[6].Layer);

        var ear1 = Carry(player, new GoldEarrings());
        var ear2 = Carry(player, new SilverEarrings());
        var ear3 = Carry(player, new GoldEarrings());
        player.Use(ear1);
        player.Use(ear2);
        player.Use(ear3);

        Assert.Same(player, ear1.Parent);
        Assert.Same(player, ear2.Parent);
        Assert.Same(player.Backpack, ear3.Parent);
        Assert.Equal(Layer.Earrings, ear3.Layer);
    }

    [Fact]
    public void EquipAndUnequipMacrosHandleMultiSlotJewelry()
    {
        var player = NewPlayer();
        var rings = new GoldRing[6];
        var serials = PooledRefList<Serial>.Create(8);
        try
        {
            for (var i = 0; i < 6; i++)
            {
                rings[i] = Carry(player, new GoldRing());
                serials.Add(rings[i].Serial);
            }

            var ear1 = Carry(player, new GoldEarrings());
            var ear2 = Carry(player, new SilverEarrings());
            serials.Add(ear1.Serial);
            serials.Add(ear2.Serial);

            PlayerMobile.EquipMacro(player, ref serials);

            for (var i = 0; i < 6; i++)
            {
                Assert.Same(player, rings[i].Parent);
            }

            Assert.Same(player, ear1.Parent);
            Assert.Same(player, ear2.Parent);

            var layers = PooledRefList<Layer>.Create(2);
            try
            {
                layers.Add(Layer.Ring);
                layers.Add(Layer.Earrings);
                PlayerMobile.UnequipMacro(player, ref layers);
            }
            finally
            {
                layers.Dispose();
            }

            for (var i = 0; i < 6; i++)
            {
                Assert.Same(player.Backpack, rings[i].Parent);
                Assert.Equal(Layer.Ring, rings[i].Layer);
            }

            Assert.Same(player.Backpack, ear1.Parent);
            Assert.Same(player.Backpack, ear2.Parent);
            Assert.Equal(Layer.Earrings, ear1.Layer);
            Assert.Equal(Layer.Earrings, ear2.Layer);
        }
        finally
        {
            serials.Dispose();
        }
    }

    [Fact]
    public void RejectedEquipAndLiftBouncePreserveValidLayerState()
    {
        var player = NewPlayer();

        // Rejected equip must restore canonical Layer.Ring.
        var rejected = Carry(player, new UnwearableRing());
        Assert.False(player.EquipItem(rejected, JewelrySlots.Ring4));
        Assert.Same(player.Backpack, rejected.Parent);
        Assert.Equal(Layer.Ring, rejected.Layer);

        // Lifting an extended-layer ring and bouncing re-equips it safely onto the player.
        var ring1 = Carry(player, new GoldRing());
        var ring2 = Carry(player, new SilverRing());
        Assert.True(player.EquipItem(ring1));
        Assert.True(player.EquipItem(ring2));
        Assert.Equal(JewelrySlots.Ring2, ring2.Layer);

        ring2.RecordBounce();
        ring2.Internalize();
        Assert.Equal(Layer.Ring, ring2.Layer);

        ring2.Bounce(player);
        Assert.Same(player, ring2.Parent);
        Assert.Same(ring2, player.FindItemOnLayer(JewelrySlots.Ring2));
        Assert.Equal(JewelrySlots.Ring2, ring2.Layer);
    }

    [Fact]
    public void ExtendedJewelryLayersRoundTripThroughItemSerialization()
    {
        var player = NewPlayer();
        var ring = Carry(player, new GoldRing());
        var earring = Carry(player, new GoldEarrings());

        Assert.True(player.EquipItem(ring, JewelrySlots.Ring4));
        Assert.True(player.EquipItem(earring, JewelrySlots.Earrings2));

        // Equipped on PlayerMobile: extended layers persist through save/load.
        Assert.Equal(JewelrySlots.Ring4, RoundTripItemLayer(ring));
        Assert.Equal(JewelrySlots.Earrings2, RoundTripItemLayer(earring));

        // Unequipped item erroneously saved with an extended layer resets to canonical on load.
        var looseRing = _world.TrackItem(new GoldRing());
        looseRing.Layer = JewelrySlots.Ring5;
        Assert.Equal(Layer.Ring, RoundTripItemLayer(looseRing));
    }

    [Fact]
    public void NonPlayerMobilesRemainSingleRingAndEarring()
    {
        var creature = _world.NewCreature();
        creature.MoveToWorld(new Point3D(1100, 1100, 0), Map.Felucca);

        var r1 = _world.TrackItem(new GoldRing());
        var r2 = _world.TrackItem(new SilverRing());
        var e1 = _world.TrackItem(new GoldEarrings());
        var e2 = _world.TrackItem(new SilverEarrings());

        Assert.True(creature.EquipItem(r1));
        Assert.False(creature.EquipItem(r2));
        Assert.True(creature.EquipItem(e1));
        Assert.False(creature.EquipItem(e2));
    }

    [Fact]
    public void DeathAndCorpseSelfLootReequipAllSixRingsAndBothEarrings()
    {
        var player = NewPlayer();
        var rings = new GoldRing[6];
        for (var i = 0; i < 6; i++)
        {
            rings[i] = Carry(player, new GoldRing());
            Assert.True(player.EquipItem(rings[i]));
        }

        var ear1 = Carry(player, new GoldEarrings());
        var ear2 = Carry(player, new SilverEarrings());
        Assert.True(player.EquipItem(ear1));
        Assert.True(player.EquipItem(ear2));

        var equipList = new List<Item>(8);
        equipList.AddRange(rings);
        equipList.Add(ear1);
        equipList.Add(ear2);

        var corpse = _world.TrackItem(new Corpse(player, equipList));
        corpse.RestoreEquip = new List<Item>(equipList);
        corpse.MoveToWorld(player.Location, player.Map);
        foreach (var item in equipList)
        {
            corpse.DropItem(item);
            corpse.SetRestoreInfo(item, new Point3D(10, 10, 0));
        }

        // Items moved to corpse must have reset back to canonical layers for CorpsePackets / client safety.
        for (var i = 0; i < 6; i++)
        {
            Assert.Same(corpse, rings[i].Parent);
            Assert.Equal(Layer.Ring, rings[i].Layer);
        }

        Assert.Same(corpse, ear1.Parent);
        Assert.Same(corpse, ear2.Parent);
        Assert.Equal(Layer.Earrings, ear1.Layer);
        Assert.Equal(Layer.Earrings, ear2.Layer);

        // Self-looting the corpse re-equips all 6 rings and both earrings cleanly.
        corpse.Open(player, true);

        for (var i = 0; i < 6; i++)
        {
            Assert.Same(player, rings[i].Parent);
            Assert.False(rings[i].IsDupedEquipLayer());
        }

        Assert.Same(player, ear1.Parent);
        Assert.Same(player, ear2.Parent);
        Assert.False(ear1.IsDupedEquipLayer());
        Assert.False(ear2.IsDupedEquipLayer());
        Assert.NotNull(player.FindItemOnLayer(JewelrySlots.Ring6));
        Assert.NotNull(player.FindItemOnLayer(JewelrySlots.Earrings2));
    }

    private Layer RoundTripItemLayer(Item item)
    {
        var writer = new BufferWriter(true);
        item.Serialize(writer);
        var bytes = writer.Buffer.AsSpan(0, (int)writer.Position).ToArray();

        var reader = new BufferReader(bytes);
        var clone = item is BaseRing
            ? _world.TrackItem((Item)new GoldRing(World.NewItem))
            : _world.TrackItem((Item)new GoldEarrings(World.NewItem));
        clone.Deserialize(reader);
        Assert.Equal(writer.Position, reader.Position);
        return clone.Layer;
    }

    private PlayerMobile NewPlayer()
    {
        var player = _world.NewPlayer();
        player.Player = true;
        player.RawStr = 100;
        player.RawDex = 100;
        player.RawInt = 100;
        player.MoveToWorld(new Point3D(1100, 1100, 0), Map.Felucca);
        player.AddItem(new Backpack());
        return player;
    }

    private T Carry<T>(PlayerMobile player, T item) where T : Item
    {
        _world.TrackItem(item);
        player.Backpack.DropItem(item);
        return item;
    }

    private class UnwearableRing : GoldRing
    {
        public override bool CanEquip(Mobile from) => false;
    }
}
