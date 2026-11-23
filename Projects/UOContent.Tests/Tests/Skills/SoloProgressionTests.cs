using System;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Xunit;

namespace Server.Tests;

[Collection("Sequential UOContent Tests")]
public class SoloProgressionTests : IDisposable
{
    private readonly MobileTestHarness _world = new();
    private readonly bool _previous = ServerConfiguration.GetSetting("progression.soloEnabled", false);

    public SoloProgressionTests()
    {
        ServerConfiguration.SetSetting("progression.soloEnabled", true);
        SoloProgression.Configure();
        SkillCheck.Configure();
    }

    [Fact]
    public void EverySkillCanReachItsOwnCapWithoutLosingAnotherSkill()
    {
        var player = _world.NewPlayer();
        player.Player = true;
        SoloProgression.Apply(player);
        Assert.Equal(69600, player.SkillsCap);

        for (var index = 0; index < player.Skills.Length; index++)
        {
            var skill = player.Skills[index];
            Assert.Equal(100.0, skill.Cap);
            skill.BaseFixedPoint = 999;
            SkillCheck.Gain(player, skill);
            Assert.Equal(1000, skill.BaseFixedPoint);
            SkillCheck.Gain(player, skill);
            Assert.Equal(1000, skill.BaseFixedPoint);
        }

        Assert.Equal(58000, player.SkillsTotal);
    }

    [Fact]
    public void AllStatsCanReach125AndTheTopScrollUnlocks150Each()
    {
        var player = _world.NewPlayer();
        player.Player = true;
        player.MoveToWorld(new Point3D(100, 100, 0), Map.Felucca);
        SoloProgression.Apply(player);
        Assert.Equal(375, player.StatCap);

        player.RawStr = player.RawDex = player.RawInt = 124;
        foreach (var stat in new[] { Stat.Str, Stat.Dex, Stat.Int })
        {
            SkillCheck.IncreaseStat(player, stat, true);
            Assert.False(SkillCheck.CanRaise(player, stat));
        }
        Assert.Equal(375, player.RawStatTotal);

        var pack = _world.TrackItem(new Backpack());
        player.AddItem(pack);
        var scroll = _world.TrackItem(new StatCapScroll(250));
        pack.DropItem(scroll);
        Assert.True(scroll.CanUse(player));
        scroll.Use(player);
        Assert.True(scroll.Deleted);
        Assert.Equal(450, player.StatCap);
        Assert.Equal(375, player.RawStatTotal);

        player.RawStr = player.RawDex = player.RawInt = 149;
        foreach (var stat in new[] { Stat.Str, Stat.Dex, Stat.Int })
        {
            Assert.True(SkillCheck.CanRaise(player, stat));
            SkillCheck.IncreaseStat(player, stat, true);
            Assert.False(SkillCheck.CanRaise(player, stat));
        }
        Assert.Equal(450, player.RawStatTotal);

        var weaker = _world.TrackItem(new StatCapScroll(245));
        pack.DropItem(weaker);
        weaker.Use(player);
        Assert.False(weaker.Deleted);
        Assert.Equal(450, player.StatCap);
    }

    [Theory]
    [InlineData(225, false, 375)]
    [InlineData(245, false, 435)]
    [InlineData(250, true, 435)]
    [InlineData(255, true, 450)]
    [InlineData(435, true, 435)]
    public void ExistingStatScrollTierSurvivesRepeatedLogins(int original, bool veteran, int expected)
    {
        var player = _world.NewPlayer();
        player.StatCap = original;
        player.HasStatReward = veteran;
        SoloProgression.Apply(player);
        SoloProgression.Apply(player);
        Assert.Equal(expected, player.StatCap);
    }

    [Fact]
    public void EveryConfiguredSkillHasAnEligibleScrollAndOnlyThatSkillUnlocks()
    {
        var player = _world.NewPlayer();
        player.MoveToWorld(new Point3D(100, 100, 0), Map.Felucca);
        var pack = _world.TrackItem(new Backpack());
        player.AddItem(pack);

        Assert.Equal(player.Skills.Length, PowerScroll.Skills.Length);
        for (var index = 0; index < player.Skills.Length; index++)
        {
            Assert.Contains(player.Skills[index].SkillName, PowerScroll.Skills);
        }

        var scroll = _world.TrackItem(new PowerScroll(SkillName.Imbuing, 120));
        pack.DropItem(scroll);
        scroll.Use(player);
        Assert.True(scroll.Deleted);
        Assert.Equal(120.0, player.Skills[SkillName.Imbuing].Cap);
        Assert.Equal(100.0, player.Skills[SkillName.Alchemy].Cap);
        Assert.Equal(0.0, player.Skills[SkillName.Imbuing].Base);
    }

    [Fact]
    public void All58SkillsCanTrainTo120AfterTheirScroll()
    {
        var player = _world.Track(new PlayerMobile());
        player.Player = true;
        player.MoveToWorld(new Point3D(100, 100, 0), Map.Felucca);
        Assert.Equal(69600, player.SkillsCap);
        Assert.Equal(375, player.StatCap);
        var pack = _world.TrackItem(new Backpack());
        player.AddItem(pack);

        for (var index = 0; index < player.Skills.Length; index++)
        {
            var skill = player.Skills[index];
            var scroll = _world.TrackItem(new PowerScroll(skill.SkillName, 120));
            pack.DropItem(scroll);
            scroll.Use(player);
            Assert.True(scroll.Deleted);

            skill.BaseFixedPoint = 1199;
            SkillCheck.Gain(player, skill);
            SkillCheck.Gain(player, skill);
            Assert.Equal(1200, skill.BaseFixedPoint);
        }

        Assert.Equal(69600, player.SkillsTotal);
    }

    [Fact]
    public void ADownLockDoesNotMakeAnotherSkillOrStatAtrophy()
    {
        var player = _world.NewPlayer();
        player.Player = true;
        SoloProgression.Apply(player);

        player.Skills.Alchemy.BaseFixedPoint = 1000;
        player.Skills.Alchemy.SetLockNoRelay(SkillLock.Down);
        player.Skills.Mining.BaseFixedPoint = 999;
        player.SkillsCap = player.SkillsTotal;
        SkillCheck.Gain(player, player.Skills.Mining);
        Assert.Equal(1000, player.Skills.Alchemy.BaseFixedPoint);
        Assert.Equal(1000, player.Skills.Mining.BaseFixedPoint);

        player.RawStr = 124;
        player.RawDex = 80;
        player.DexLock = StatLockType.Down;
        SkillCheck.IncreaseStat(player, Stat.Str, true);
        Assert.Equal(125, player.RawStr);
        Assert.Equal(80, player.RawDex);
    }

    [Fact]
    public void DisabledModeKeepsStockCapsAndScrollPool()
    {
        ServerConfiguration.SetSetting("progression.soloEnabled", false);
        SoloProgression.Configure();
        var player = _world.Track(new PlayerMobile());
        SoloProgression.Apply(player);

        Assert.Equal(225, player.StatCap);
        Assert.Equal(7000, player.SkillsCap);
        Assert.DoesNotContain(SkillName.Imbuing, PowerScroll.Skills);
    }

    public void Dispose()
    {
        _world.Dispose();
        ServerConfiguration.SetSetting("progression.soloEnabled", _previous);
        SoloProgression.Configure();
    }
}
