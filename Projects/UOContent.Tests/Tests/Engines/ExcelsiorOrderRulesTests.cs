using System;
using Server;
using Server.Engines.BulkOrders;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public sealed class ExcelsiorOrderRulesTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void SwitchingRulesKeepsTheActiveClassicOfferClock()
    {
        var player = _world.Track(new PlayerMobile { Player = true, Race = Race.Human });
        player.Skills.Blacksmith.Base = 95;
        player.NextSmithBulkOrder = TimeSpan.FromHours(6);

        try
        {
            Assert.False(ExcelsiorOrderRules.Selected(player));
            ExcelsiorOrderRules.Select(player, true);
            Assert.True(ExcelsiorOrderRules.Selected(player));
            Assert.Null(Blacksmith.CreateSmithOrder(player, true));
            Assert.InRange(player.NextSmithBulkOrder, TimeSpan.FromMinutes(359), TimeSpan.FromHours(6));

            ExcelsiorOrderRules.Select(player, false);
            Assert.False(ExcelsiorOrderRules.Selected(player));
            Assert.Null(Blacksmith.CreateSmithOrder(player, true));
        }
        finally
        {
            ExcelsiorOrderRules.OnPlayerDeleted(player);
        }
    }

    [Fact]
    public void ExcelsiorDeedVariantsRetainTheirBookKindAndCompleteRewardTables()
    {
        var classic = _world.TrackItem(new SmallSmithBOD(10, 10, typeof(PlateGorget), 1025143, 0x1413,
            false, BulkMaterialType.None));
        var extra = _world.TrackItem(ExcelsiorOrderRules.NewSmall(
            BODType.ExcelsiorSmith, 10, 10, typeof(PlateGorget), 1025143, 0x1413, false, BulkMaterialType.None
        ));

        Assert.Equal(BODType.Smith, ExcelsiorOrderRules.BookKind(classic));
        Assert.Equal(BODType.Smith, extra.Profession);
        Assert.Equal(BODType.ExcelsiorSmith, ExcelsiorOrderRules.BookKind(extra));

        var smithTop = ExcelsiorBodRewardGroups.Smith(1450);
        Assert.Equal(1450, smithTop.Points);
        var hammer = _world.TrackItem(Assert.IsType<RunicHammer>(smithTop.Items[0].Construct()));
        Assert.Equal(CraftResource.Platinum, hammer.Resource);

        var tailorTop = ExcelsiorBodRewardGroups.Tailor(1150);
        Assert.Equal(1150, tailorTop.Points);
        var sewingKit = _world.TrackItem(Assert.IsType<RunicSewingKit>(tailorTop.Items[0].Construct()));
        Assert.Equal(CraftResource.EtherealLeather, sewingKit.Resource);
        Assert.Equal(BulkMaterialType.Platinum, SmallBOD.GetMaterial(CraftResource.Platinum));
        Assert.Equal(BulkMaterialType.EtherealLeather, SmallBOD.GetMaterial(CraftResource.EtherealLeather));
        Assert.Equal(BulkMaterialType.Petrified, SmallBOD.GetMaterial(CraftResource.PetrifiedWood));
    }
}
