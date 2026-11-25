using System;
using Server.Items;
using Server.Mobiles;
using Xunit;

namespace Server.Tests;

[Collection("Sequential UOContent Tests")]
public class SwordsmanshipEssenceTests : IDisposable
{
    private readonly MobileTestHarness _world = new();
    private readonly bool _previous = ServerConfiguration.GetSetting("progression.soloEnabled", false);

    public SwordsmanshipEssenceTests() => SetSoloMode(true);

    [Fact]
    public void EssencesAddATenthEachAndLeaveExcessInTheBackpack()
    {
        var player = CreatePlayer(1200, 1200);
        var skill = player.Skills[SkillName.Swords];
        var single = _world.TrackItem(new SwordsmanshipEssence());
        player.Backpack.DropItem(single);
        single.OnDoubleClick(player);
        Assert.Equal(1201, skill.BaseFixedPoint);
        Assert.True(single.Deleted);

        var stack = _world.TrackItem(new SwordsmanshipEssence(100));
        player.Backpack.DropItem(stack);
        stack.OnDoubleClick(player);
        Assert.Equal(1300, skill.BaseFixedPoint);
        Assert.Equal(130.0, skill.Value);
        Assert.Equal(1200, skill.CapFixedPoint);
        Assert.Equal(1, stack.Amount);

        stack.OnDoubleClick(player);
        Assert.False(stack.Deleted);
        Assert.Equal(1, stack.Amount);
        Assert.Equal(1300, skill.BaseFixedPoint);
    }

    [Theory]
    [InlineData(1200, 1199, SkillLock.Up, true, true)]
    [InlineData(1150, 1200, SkillLock.Up, true, true)]
    [InlineData(1200, 1200, SkillLock.Locked, true, true)]
    [InlineData(1200, 1200, SkillLock.Down, true, true)]
    [InlineData(1200, 1200, SkillLock.Up, false, true)]
    [InlineData(1200, 1200, SkillLock.Up, true, false)]
    public void IneligibleUseLeavesTheSkillAndStackAlone(
        int cap, int trained, SkillLock skillLock, bool solo, bool inBackpack
    )
    {
        SetSoloMode(solo);
        var player = CreatePlayer(cap, trained);
        var skill = player.Skills[SkillName.Swords];
        skill.SetLockNoRelay(skillLock);
        var stack = _world.TrackItem(new SwordsmanshipEssence(2));
        if (inBackpack)
        {
            player.Backpack.DropItem(stack);
        }

        stack.OnDoubleClick(player);

        Assert.Equal(trained, skill.BaseFixedPoint);
        Assert.False(stack.Deleted);
        Assert.Equal(2, stack.Amount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BalronDropsOneEssenceOnlyOnDeathInSoloMode(bool solo)
    {
        SetSoloMode(solo);
        var balron = _world.Track(new Balron());
        Assert.Null(balron.Backpack.FindItemByType<SwordsmanshipEssence>());

        balron.GenerateLoot(false);

        var essence = balron.Backpack.FindItemByType<SwordsmanshipEssence>();
        if (solo)
        {
            Assert.NotNull(essence);
            Assert.Equal(1, essence.Amount);
        }
        else
        {
            Assert.Null(essence);
        }
    }

    public void Dispose()
    {
        _world.Dispose();
        SetSoloMode(_previous);
    }

    private PlayerMobile CreatePlayer(int cap, int trained)
    {
        var player = _world.NewPlayer();
        player.MoveToWorld(new Point3D(100, 100, 0), Map.Felucca);
        player.AddItem(_world.TrackItem(new Backpack()));
        var skill = player.Skills[SkillName.Swords];
        skill.CapFixedPoint = cap;
        skill.BaseFixedPoint = trained;
        return player;
    }

    private static void SetSoloMode(bool enabled)
    {
        ServerConfiguration.SetSetting("progression.soloEnabled", enabled);
        SoloProgression.Configure();
    }
}
