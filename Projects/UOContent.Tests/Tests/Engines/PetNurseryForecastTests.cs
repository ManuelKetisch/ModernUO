using System;
using Server;
using Server.Engines.PetBreeding;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public class PetNurseryForecastTests : IDisposable
{
    private readonly MobileTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void ForecastUsesTheParentsCurrentLevelsAndShowsBothGenerations()
    {
        var owner = _harness.NewPlayer();
        var first = _harness.NewCreature();
        var second = _harness.NewCreature();
        first.InitializePetProgression(2, 40, false);
        second.InitializePetProgression(6, 35, true);
        second.ControlSlots = 3;
        Assert.True(first.SetControlMaster(owner));
        Assert.True(second.SetControlMaster(owner));
        first.AwardPetExperience(585);
        second.AwardPetExperience(825);
        Assert.Equal(10, first.PetLevel);
        Assert.Equal(12, second.PetLevel);

        var result = PetNurseryForecast.Create(first, second);

        Assert.Equal(new PetNurseryForecast(3, 7, 12, 14, 3), result);
    }

    [Fact]
    public void StatForecastReportsRoundedMeanBoundedVariationAndCaps()
    {
        var first = _harness.NewCreature();
        var second = _harness.NewCreature();
        first.RawStr = 100;
        second.RawStr = 201;
        first.PhysicalResistanceSeed = 72;
        second.PhysicalResistanceSeed = 100;
        first.SetDamage(15, 15);
        second.SetDamage(17, 17);

        Assert.Equal((150, 155), PetNurseryForecast.GetStatRange(first, second, PetTrainingStat.Strength));
        Assert.Equal((75, 75), PetNurseryForecast.GetStatRange(first, second, PetTrainingStat.PhysicalResistance));
        Assert.Equal((16, 16), PetNurseryForecast.GetStatRange(first, second, PetTrainingStat.MinimumDamage));
        Assert.Equal((16, 16), PetNurseryForecast.GetStatRange(first, second, PetTrainingStat.MaximumDamage));
    }
}
