using System;
using System.Reflection;
using Server.Items;
using Server.Mobiles;
using Xunit;

namespace Server.Tests;

[Collection("Sequential UOContent Tests")]
public class SoloDefenseTests : IDisposable
{
    private readonly MobileTestHarness _world = new();
    private readonly bool _previous = ServerConfiguration.GetSetting("progression.soloEnabled", false);

    [Theory]
    [InlineData(false, false, 70)]
    [InlineData(false, true, 75)]
    [InlineData(true, false, 85)]
    [InlineData(true, true, 85)]
    public void PlayerResistanceCapsApplyToEffectiveValues(bool solo, bool elf, int expectedEnergy)
    {
        SetMode(solo);
        var player = _world.NewPlayer();
        player.Player = true;
        player.Race = elf ? Race.Elf : Race.Human;

        for (var index = 0; index < 5; index++)
        {
            var type = (ResistanceType)index;
            var expected = type == ResistanceType.Energy ? expectedEnergy : solo ? 85 : 70;

            player.AddResistanceMod(new ResistanceMod(type, $"Resistance cap test {index}", 100));
            Assert.Equal(expected, player.GetMaxResistance(type));
            Assert.Equal(expected, player.GetResistance(type));
            Assert.Equal(expected, AOS.GetStatus(player, index));
        }
    }

    [Theory]
    [InlineData(false, 45)]
    [InlineData(true, 85)]
    public void CombatAndStatusUseTheSameDefenseChanceCap(bool solo, int expectedCap)
    {
        SetMode(solo);
        var attacker = _world.NewPlayer();
        var defender = _world.NewPlayer();
        attacker.Player = defender.Player = true;
        attacker.Race = defender.Race = Race.Elf;
        attacker.MoveToWorld(new Point3D(100, 100, 0), Map.Felucca);
        defender.MoveToWorld(new Point3D(101, 100, 0), Map.Felucca);

        var sword = _world.TrackItem(new Broadsword { Layer = Layer.OneHanded });
        attacker.AddItem(sword);
        var ring = _world.TrackItem(new GoldRing { Layer = Layer.Ring });
        ring.Attributes.DefendChance = 100;
        defender.AddItem(ring);
        Assert.Equal(100, AosAttributes.GetValue(defender, AosAttribute.DefendChance));

        var original = Mobile.SkillCheckDirectLocationHandler;
        var chance = 0.0;
        Mobile.SkillCheckDirectLocationHandler = (_, _, value) =>
        {
            chance = value;
            return true;
        };

        try
        {
            var expectedChance = 100.0 / (2 * (100 + expectedCap));
            Assert.True(sword.CheckHit(attacker, defender));
            Assert.Equal(expectedChance, chance, 8);

            chance = 0;
            var ninjaCheck = typeof(NinjaWeapon).GetMethod("CombatCheck", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(ninjaCheck);
            Assert.True((bool)ninjaCheck.Invoke(null, [attacker, defender]));
            Assert.Equal(expectedChance, chance, 8);
            Assert.Equal(expectedCap, AOS.GetStatus(defender, 6));
        }
        finally
        {
            Mobile.SkillCheckDirectLocationHandler = original;
        }
    }

    [Fact]
    public void SoloCapsApplyToOwnerCharactersAndKeepCreatureDefenseAt45()
    {
        SetMode(true);
        var owner = _world.NewPlayer();
        owner.Player = true;
        owner.AccessLevel = AccessLevel.Owner;
        Assert.Equal(85, AOS.GetStatus(owner, 0));
        Assert.Equal(85, AOS.GetStatus(owner, 6));
        Assert.Equal(45, AOS.GetStatus(_world.NewCreature(), 6));
    }

    private static void SetMode(bool enabled)
    {
        ServerConfiguration.SetSetting("progression.soloEnabled", enabled);
        SoloProgression.Configure();
    }

    public void Dispose()
    {
        _world.Dispose();
        SetMode(_previous);
    }
}
