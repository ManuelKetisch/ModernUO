using System;
using Server;
using Server.Engines.BuffIcons;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Items.Potions;

[Collection("Sequential UOContent Tests")]
public class BuffPotionDurationTests : IDisposable
{
    private readonly MobileTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    private PlayerMobile CreatePlayer(
        int rawStr = 1,
        int rawDex = 1,
        int rawInt = 1,
        double alchemy = 0.0,
        double tasteId = 0.0,
        Race race = null
    )
    {
        var player = _harness.NewPlayer();
        player.Race = race ?? Race.Elf;
        player.RawStr = rawStr;
        player.RawDex = rawDex;
        player.RawInt = rawInt;
        player.Skills.Alchemy.Base = alchemy;
        player.Skills.TasteID.Base = tasteId;
        player.MoveToWorld(new Point3D(1100, 1100, 0), Map.Felucca);
        return player;
    }

    [Fact]
    public void BuffPotionsEnforceTwentyMinuteMinimumAndScaleByQuality()
    {
        var novice = CreatePlayer();
        var strPot = _harness.TrackItem(new StrengthPotion());
        var greaterStrPot = _harness.TrackItem(new GreaterStrengthPotion());
        var agilPot = _harness.TrackItem(new AgilityPotion());
        var greaterAgilPot = _harness.TrackItem(new GreaterAgilityPotion());
        var nsPot = _harness.TrackItem(new NightSightPotion());

        Assert.Equal(TimeSpan.FromMinutes(20), BasePotion.GetBuffDuration(novice, strPot.Duration));
        Assert.Equal(TimeSpan.FromMinutes(30), BasePotion.GetBuffDuration(novice, greaterStrPot.Duration));
        Assert.Equal(TimeSpan.FromMinutes(20), BasePotion.GetBuffDuration(novice, agilPot.Duration));
        Assert.Equal(TimeSpan.FromMinutes(30), BasePotion.GetBuffDuration(novice, greaterAgilPot.Duration));
        Assert.Equal(TimeSpan.FromMinutes(20), BasePotion.GetBuffDuration(novice, nsPot.Duration));

        // Even if a caller passes a shorter base duration, the 20-minute floor holds.
        Assert.Equal(TimeSpan.FromMinutes(20), BasePotion.GetBuffDuration(novice, TimeSpan.FromMinutes(2)));
        Assert.Equal(TimeSpan.FromMinutes(20), BasePotion.GetBuffDuration(null, TimeSpan.FromMinutes(2)));

        // Humans also receive Jack of All Trades (20.0 effective Alchemy/TasteID -> +10% -> 22m).
        var humanNovice = CreatePlayer(race: Race.Human);
        Assert.Equal(TimeSpan.FromMinutes(22), BasePotion.GetBuffDuration(humanNovice, strPot.Duration));
    }

    [Fact]
    public void BuffDurationScalesWithAlchemyTasteIdStatsAndEnhancePotions()
    {
        // 100 Alchemy (+30%) + 100 TasteID (+20%) + 100 STR & 100 INT (+20%) = +70%
        var alchemist = CreatePlayer(rawStr: 100, rawDex: 80, rawInt: 100, alchemy: 100.0, tasteId: 100.0);
        Assert.Equal(70, BasePotion.GetBuffDurationBonusPercent(alchemist));
        Assert.Equal(TimeSpan.FromMinutes(34), BasePotion.GetBuffDuration(alchemist, TimeSpan.FromMinutes(20)));
        Assert.Equal(TimeSpan.FromMinutes(51), BasePotion.GetBuffDuration(alchemist, TimeSpan.FromMinutes(30)));

        // Adding 50% Enhance Potions jewelry raises bonus to +120% (44m standard, 66m greater).
        var ring = _harness.TrackItem(new GoldRing());
        ring.Attributes.EnhancePotions = 50;
        Assert.True(alchemist.EquipItem(ring));

        Assert.Equal(120, BasePotion.GetBuffDurationBonusPercent(alchemist));
        Assert.Equal(TimeSpan.FromMinutes(44), BasePotion.GetBuffDuration(alchemist, TimeSpan.FromMinutes(20)));
        Assert.Equal(TimeSpan.FromMinutes(66), BasePotion.GetBuffDuration(alchemist, TimeSpan.FromMinutes(30)));

        // Active temporary stat mods do not recursively inflate RawStr/RawInt bonus.
        alchemist.AddStatMod(new StatMod(StatType.Str, "TestTempStr", 50, TimeSpan.FromMinutes(5)));
        Assert.Equal(120, BasePotion.GetBuffDurationBonusPercent(alchemist));
    }

    [Fact]
    public void DrinkingStrengthAndAgilityPotionsAppliesScaledStatModDuration()
    {
        var player = CreatePlayer(rawStr: 100, rawDex: 100, rawInt: 100, alchemy: 100.0, tasteId: 0.0);
        // 100 Alchemy (+30%) + 200 raw STR+INT (+20%) = +50% -> 30m standard, 45m greater.

        var strPot = _harness.TrackItem(new StrengthPotion());
        strPot.MoveToWorld(player.Location, player.Map);
        Assert.True(strPot.CanDrink(player));
        strPot.Drink(player);

        var strMod = player.GetStatMod("[Magic] Str Buff");
        Assert.NotNull(strMod);
        Assert.Equal(TimeSpan.FromMinutes(30), PlayerBuffPersistence.GetStatModRemaining(player, "[Magic] Str Buff"));

        var greaterAgilPot = _harness.TrackItem(new GreaterAgilityPotion());
        greaterAgilPot.MoveToWorld(player.Location, player.Map);
        Assert.True(greaterAgilPot.CanDrink(player));
        greaterAgilPot.Drink(player);

        var dexMod = player.GetStatMod("[Magic] Dex Buff");
        Assert.NotNull(dexMod);
        Assert.Equal(TimeSpan.FromMinutes(45), PlayerBuffPersistence.GetStatModRemaining(player, "[Magic] Dex Buff"));
    }
}
