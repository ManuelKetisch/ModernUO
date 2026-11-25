using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Mobiles;

[Collection("Sequential UOContent Tests")]
public class EquipmentInteractionTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DistinctOneHandedWeaponsUseIndependentCooldowns(bool matchingWeapons)
    {
        var player = NewPlayer();
        var target = NewPlayer();
        var primary = Carry(player, new SwingKatana());
        BaseWeapon second = matchingWeapons ? new SwingKatana() : new SwingDagger();
        Carry(player, second);

        Assert.True(player.EquipItem(primary));
        var mainDeadline = player.NextCombatTime;
        Assert.True(player.EquipItem(second));
        Assert.Equal(mainDeadline, player.NextCombatTime);
        Assert.Same(primary, player.FindItemOnLayer(Layer.OneHanded));
        Assert.Same(second, player.FindItemOnLayer(Layer.TwoHanded));

        player.Combatant = target;
        Assert.Same(target, player.Combatant);
        var firstCheck = Core.TickCount + 5000;
        var offHandDelay = matchingWeapons ? 2000 : 3000;
        IWeapon weapon = player.Weapon;
        weapon.OnBeforeSwing(player, target);
        Assert.Equal(TimeSpan.FromSeconds(2), weapon.OnSwing(player, target));
        player.CheckOffHandCombatTime(firstCheck);

        // Only the off-hand deadline can delay its next attack.
        player.NextCombatTime = firstCheck + 60_000;
        player.CheckOffHandCombatTime(firstCheck + offHandDelay - 1);
        Assert.Equal(1, second is SwingDagger dagger ? dagger.Swings : ((SwingKatana)second).Swings);
        player.CheckOffHandCombatTime(firstCheck + offHandDelay);
        Assert.Equal(2, second is SwingDagger knife ? knife.Swings : ((SwingKatana)second).Swings);
        Assert.Equal(firstCheck + 60_000, player.NextCombatTime);
        Assert.Equal(1, primary.Swings);
        Assert.Same(primary, player.Weapon);

        Assert.False(player.EquipItem(Carry(player, new WoodenShield { Layer = Layer.TwoHanded })));
        Assert.False(player.EquipItem(Carry(player, new SwingKatana())));
        player.Backpack.DropItem(second);
        Assert.Equal(firstCheck + 60_000, player.NextCombatTime);
        player.CheckOffHandCombatTime(firstCheck + 2 * offHandDelay);
        Assert.Equal(2, second is SwingDagger removed ? removed.Swings : ((SwingKatana)second).Swings);
        Assert.Equal(Layer.OneHanded, second.Layer);
        Assert.True(player.EquipItem(Carry(player, new WoodenShield { Layer = Layer.TwoHanded })));
        Assert.False(player.EquipItem(second));
    }

    [Fact]
    public void RemovingThePrimaryWeaponFreesTheSecondHand()
    {
        var player = NewPlayer();
        var primary = Carry(player, new SwingKatana());
        var offHand = Carry(player, new SwingDagger());
        Assert.True(player.EquipItem(primary));
        Assert.True(player.EquipItem(offHand));

        player.Backpack.DropItem(primary);

        Assert.True(player.HasFreeHand());
        Assert.Same(offHand, player.FindItemOnLayer(Layer.OneHanded));
        Assert.Same(offHand, player.Weapon);
        Assert.True(player.EquipItem(primary));
        Assert.Same(primary, player.FindItemOnLayer(Layer.TwoHanded));
    }

    [Fact]
    public void DoubleClickEquipsAndSwapsArmorFromYourBackpack()
    {
        var player = NewPlayer();
        var original = Carry(player, new LeatherChest { Layer = Layer.InnerTorso });
        var replacement = new RingmailChest { Layer = Layer.InnerTorso };
        var nested = Carry(player, new Bag());
        nested.DropItem(replacement);

        player.Use(original);
        Assert.Same(player, original.Parent);
        player.Use(replacement);

        Assert.Same(player, replacement.Parent);
        Assert.Same(player.Backpack, original.Parent);
        Assert.Same(replacement, player.FindItemOnLayer(Layer.InnerTorso));
    }

    [Fact]
    public void RejectedReplacementLeavesBothArmorPiecesWhereTheyWere()
    {
        var player = NewPlayer();
        player.RawStr = 35;
        var original = Carry(player, new LeatherChest { Layer = Layer.InnerTorso });
        var replacement = Carry(player, new PlateChest { Layer = Layer.InnerTorso });
        Assert.True(player.EquipItem(original));

        player.Use(replacement);

        Assert.Same(player.Backpack, replacement.Parent);
        Assert.Same(original, player.FindItemOnLayer(Layer.InnerTorso));
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

    private class SwingKatana : Katana
    {
        public SwingKatana() => Layer = Layer.OneHanded;
        public int Swings { get; private set; }

        public override TimeSpan OnSwing(Mobile attacker, Mobile defender, double damageBonus = 1)
        {
            Assert.Same(this, attacker.Weapon);
            ++Swings;
            return TimeSpan.FromSeconds(2);
        }
    }

    private class SwingDagger : Dagger
    {
        public SwingDagger() => Layer = Layer.OneHanded;
        public int Swings { get; private set; }

        public override TimeSpan OnSwing(Mobile attacker, Mobile defender, double damageBonus = 1)
        {
            Assert.Same(this, attacker.Weapon);
            ++Swings;
            return TimeSpan.FromSeconds(3);
        }
    }
}
