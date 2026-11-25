using System;
using Server;
using Server.Engines.Metallurgy;
using Server.Items;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public sealed class ExcelsiorMaterialsTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void ExtraGradesRegisterBothRawAndProcessedItemsWithoutShiftingStockIds()
    {
        Assert.Equal(9, (int)CraftResource.Valorite);
        Assert.Equal(104, (int)CraftResource.BarbedLeather);
        Assert.Equal(307, (int)CraftResource.Frostwood);
        Assert.Equal(36, ExcelsiorMaterials.CraftingGrades.Count);

        foreach (var grade in ExcelsiorMaterials.CraftingGrades)
        {
            Assert.Equal(grade.Family, CraftResources.GetType(grade.Info.Resource));
            Assert.Same(grade.Info, CraftResources.GetInfo(grade.Info.Resource));
            foreach (var type in grade.Info.ResourceTypes)
            {
                Assert.Equal(grade.Info.Resource, CraftResources.GetFromType(type));
            }
        }

        var ore = _world.TrackItem(new BlazeOre());
        Assert.Equal(CraftResource.Blaze, _world.TrackItem(ore.GetIngot()).Resource);
        Assert.Equal(119, ExcelsiorMaterials.RequiredSkill(CraftResource.Platinum));
        Assert.Equal(119, ExcelsiorMaterials.RequiredSkill(CraftResource.PetrifiedWood));
    }

    [Fact]
    public void RefinedAndExoticGradesUseTheirOwnDamageAndRequirements()
    {
        var player = _world.NewPlayer();
        player.Player = true;
        var ordinary = _world.TrackItem(new Dagger { Resource = CraftResource.Bronze });
        var refined = _world.TrackItem(new Dagger { Resource = CraftResource.ExcelsiorBronze });
        var exotic = _world.TrackItem(new Dagger { Resource = CraftResource.Blaze });
        Assert.NotSame(CraftResources.GetInfo(ordinary.Resource), CraftResources.GetInfo(refined.Resource));

        refined.GetDamageTypes(player, out var physical, out var fire, out _, out _, out _, out _, out _);
        Assert.Equal((60, 40), (physical, fire));
        exotic.GetDamageTypes(player, out physical, out fire, out _, out _, out _, out _, out _);
        Assert.Equal((0, 100), (physical, fire));

        var yew = CraftResources.GetInfo(CraftResource.ExcelsiorYewWood).AttributeInfo;
        Assert.Equal((30, 30, 20), (yew.WeaponLowerRequirements, yew.WeaponColdDamage, yew.WeaponEnergyDamage));
        var ethereal = CraftResources.GetInfo(CraftResource.EtherealLeather).AttributeInfo;
        Assert.Equal(100, ethereal.ArmorLowerRequirements);
        var armor = _world.TrackItem(new PlateChest { Resource = CraftResource.Blaze, Quality = ArmorQuality.Exceptional });
        Assert.Equal(145, armor.GetDurabilityBonus());
    }

    [Fact]
    public void RefiningRevalidatesSkillBenchAndExactStackBeforeCharging()
    {
        var player = _world.Track(new Server.Mobiles.PlayerMobile { Player = true, Race = Race.Human });
        player.AddItem(_world.TrackItem(new Backpack()));
        var at = new Point3D(1700, 1700, 0);
        player.MoveToWorld(at, Map.Felucca);
        var source = _world.TrackItem(new BronzeIngot(12));
        player.Backpack.DropItem(source);
        player.Skills.Blacksmith.Base = 79;
        Assert.False(ExcelsiorRefinery.TryRefine(player, source, 12, CraftResource.ExcelsiorBronze));
        player.Skills.Blacksmith.Base = 80;
        Assert.False(ExcelsiorRefinery.TryRefine(player, source, 12, CraftResource.ExcelsiorBronze));

        _world.TrackItem(new Anvil()).MoveToWorld(at, player.Map);
        _world.TrackItem(new Forge()).MoveToWorld(at, player.Map);
        Assert.False(ExcelsiorRefinery.TryRefine(player, source, 11, CraftResource.ExcelsiorBronze));
        Assert.Equal(12, source.Amount);
        Assert.True(ExcelsiorRefinery.TryRefine(player, source, 12, CraftResource.ExcelsiorBronze));
        Assert.True(source.Deleted);
        Assert.Equal(12, player.Backpack.FindItemByType<ExcelsiorBronzeIngot>()?.Amount);
        Assert.False(ExcelsiorRefinery.TryRefine(player, source, 12, CraftResource.ExcelsiorBronze));
    }
}
