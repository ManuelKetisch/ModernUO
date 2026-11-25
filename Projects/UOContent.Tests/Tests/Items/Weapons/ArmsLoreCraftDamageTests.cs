using Server;
using Server.Engines.Craft;
using Server.Items;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class ArmsLoreCraftDamageTests
{
    [Theory]
    [InlineData(-10.0, 0)]
    [InlineData(0.0, 0)]
    [InlineData(9.9, 0)]
    [InlineData(10.0, 1)]
    [InlineData(50.0, 5)]
    [InlineData(99.9, 9)]
    [InlineData(100.0, 10)]
    [InlineData(104.0, 11)]
    [InlineData(108.0, 12)]
    [InlineData(112.0, 13)]
    [InlineData(116.0, 14)]
    [InlineData(120.0, 15)]
    [InlineData(124.0, 16)]
    [InlineData(140.0, 20)]
    [InlineData(200.0, 35)]
    public void GetExceptionalArmsLoreDamageBonus_ScalesAndIsUncapped(double armsLore, int expectedBonus)
    {
        Assert.Equal(expectedBonus, BaseWeapon.GetExceptionalArmsLoreDamageBonus(armsLore));
    }

    [Theory]
    [InlineData(0.0, 35)]
    [InlineData(50.0, 40)]
    [InlineData(100.0, 45)]
    [InlineData(120.0, 50)]
    [InlineData(140.0, 55)]
    public void OnCraft_ExceptionalWeapon_AppliesUncappedArmsLoreBonus(double armsLore, int expectedDamageIncrease)
    {
        var previousExpansion = Core.Expansion;
        var crafter = new Mobile();
        var weapon = new Katana();

        try
        {
            Core.Expansion = Expansion.ML;
            if (DefBlacksmithy.CraftSystem == null)
            {
                DefBlacksmithy.Initialize();
            }

            crafter.Skills.ArmsLore.Cap = 200.0;
            crafter.Skills.ArmsLore.Base = armsLore;

            var craftItem = DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(Katana));
            Assert.NotNull(craftItem);

            weapon.OnCraft(
                (int)WeaponQuality.Exceptional,
                false,
                crafter,
                DefBlacksmithy.CraftSystem,
                typeof(IronIngot),
                null,
                craftItem,
                0
            );

            Assert.Equal(expectedDamageIncrease, weapon.Attributes.WeaponDamage);
        }
        finally
        {
            Core.Expansion = previousExpansion;
            weapon.Delete();
            crafter.Delete();
        }
    }

    [Fact]
    public void OnCraft_RegularWeapon_DoesNotApplyExceptionalArmsLoreBonus()
    {
        var previousExpansion = Core.Expansion;
        var crafter = new Mobile();
        var weapon = new Katana();

        try
        {
            Core.Expansion = Expansion.ML;
            if (DefBlacksmithy.CraftSystem == null)
            {
                DefBlacksmithy.Initialize();
            }

            crafter.Skills.ArmsLore.Cap = 200.0;
            crafter.Skills.ArmsLore.Base = 120.0;

            var craftItem = DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(Katana));
            Assert.NotNull(craftItem);

            weapon.OnCraft(
                (int)WeaponQuality.Regular,
                false,
                crafter,
                DefBlacksmithy.CraftSystem,
                typeof(IronIngot),
                null,
                craftItem,
                0
            );

            Assert.Equal(0, weapon.Attributes.WeaponDamage);
        }
        finally
        {
            Core.Expansion = previousExpansion;
            weapon.Delete();
            crafter.Delete();
        }
    }
}
