using System;

namespace Server.Items;

public enum ItemWeightCategory
{
    Reagent,
    Bandage,
    Ammunition,
    Gem,
    Scroll,
    SmallSupply,
    CookingIngredient,
    Potion,
    RefinedMaterial,
    ProcessedMaterial,
    RawWood,
    RawFiber,
    RawHide,
    RawSpecialOre
}

/// <summary>
/// Centralized, config-backed default weight taxonomy for stackable reagents,
/// supplies, ingredients, and crafting materials.
/// </summary>
public static class ItemWeights
{
    // Non-zero floor preserves positive weight semantics and avoids division by zero in Stealing.
    public const double MinWeight = 0.001;
    public const double MaxWeight = 100.0;
    public const double MinGlobalScale = 0.1;
    public const double MaxGlobalScale = 10.0;

    public const double DefaultReagent = 0.01;
    public const double DefaultBandage = 0.01;
    public const double DefaultAmmunition = 0.01;
    public const double DefaultGem = 0.01;
    public const double DefaultScroll = 0.02;
    public const double DefaultSmallSupply = 0.05;
    public const double DefaultCookingIngredient = 0.05;
    public const double DefaultPotion = 0.2;
    public const double DefaultRefinedMaterial = 0.05;
    public const double DefaultProcessedMaterial = 0.2;
    public const double DefaultRawWood = 0.5;
    public const double DefaultRawFiber = 0.5;
    public const double DefaultRawHide = 1.0;
    public const double DefaultRawSpecialOre = 0.1;

    // Empty keg plus per-charge fill keeps a full 100-charge keg lighter than 100 loose potions.
    public const double DefaultPotionKegEmpty = 4.0;
    public const double PotionKegChargeWeightRatio = 0.6;
    public const int ClothPerBolt = 50;
    public const int FlourUsesPerSack = 20;
    public const int SteaksPerFish = 4;

    public static double GlobalScale { get; private set; } = 1.0;
    public static double Reagent { get; private set; } = DefaultReagent;
    public static double Bandage { get; private set; } = DefaultBandage;
    public static double Ammunition { get; private set; } = DefaultAmmunition;
    public static double Gem { get; private set; } = DefaultGem;
    public static double Scroll { get; private set; } = DefaultScroll;
    public static double SmallSupply { get; private set; } = DefaultSmallSupply;
    public static double CookingIngredient { get; private set; } = DefaultCookingIngredient;
    public static double Potion { get; private set; } = DefaultPotion;
    public static double RefinedMaterial { get; private set; } = DefaultRefinedMaterial;
    public static double ProcessedMaterial { get; private set; } = DefaultProcessedMaterial;
    public static double RawWood { get; private set; } = DefaultRawWood;
    public static double RawFiber { get; private set; } = DefaultRawFiber;
    public static double RawHide { get; private set; } = DefaultRawHide;
    public static double RawSpecialOre { get; private set; } = DefaultRawSpecialOre;
    public static double PotionKegEmpty { get; private set; } = DefaultPotionKegEmpty;

    public static double BoltOfCloth => ClampWeight(RefinedMaterial * ClothPerBolt);
    public static double SackFlour => ClampWeight(CookingIngredient * FlourUsesPerSack);
    public static double WholeFish => ClampWeight(CookingIngredient * SteaksPerFish);

    public static void Configure()
    {
        var globalScale = ClampScale(ServerConfiguration.GetSetting("itemWeights.globalScale", 1.0));
        ApplySettings(
            globalScale,
            ServerConfiguration.GetSetting("itemWeights.reagent", DefaultReagent),
            ServerConfiguration.GetSetting("itemWeights.bandage", DefaultBandage),
            ServerConfiguration.GetSetting("itemWeights.ammunition", DefaultAmmunition),
            ServerConfiguration.GetSetting("itemWeights.gem", DefaultGem),
            ServerConfiguration.GetSetting("itemWeights.scroll", DefaultScroll),
            ServerConfiguration.GetSetting("itemWeights.smallSupply", DefaultSmallSupply),
            ServerConfiguration.GetSetting("itemWeights.cookingIngredient", DefaultCookingIngredient),
            ServerConfiguration.GetSetting("itemWeights.potion", DefaultPotion),
            ServerConfiguration.GetSetting("itemWeights.refinedMaterial", DefaultRefinedMaterial),
            ServerConfiguration.GetSetting("itemWeights.processedMaterial", DefaultProcessedMaterial),
            ServerConfiguration.GetSetting("itemWeights.rawWood", DefaultRawWood),
            ServerConfiguration.GetSetting("itemWeights.rawFiber", DefaultRawFiber),
            ServerConfiguration.GetSetting("itemWeights.rawHide", DefaultRawHide),
            ServerConfiguration.GetSetting("itemWeights.rawSpecialOre", DefaultRawSpecialOre),
            ServerConfiguration.GetSetting("itemWeights.potionKegEmpty", DefaultPotionKegEmpty)
        );
    }

    public static double GetWeight(ItemWeightCategory category) =>
        category switch
        {
            ItemWeightCategory.Reagent           => Reagent,
            ItemWeightCategory.Bandage           => Bandage,
            ItemWeightCategory.Ammunition        => Ammunition,
            ItemWeightCategory.Gem               => Gem,
            ItemWeightCategory.Scroll            => Scroll,
            ItemWeightCategory.SmallSupply       => SmallSupply,
            ItemWeightCategory.CookingIngredient => CookingIngredient,
            ItemWeightCategory.Potion            => Potion,
            ItemWeightCategory.RefinedMaterial   => RefinedMaterial,
            ItemWeightCategory.ProcessedMaterial => ProcessedMaterial,
            ItemWeightCategory.RawWood           => RawWood,
            ItemWeightCategory.RawFiber          => RawFiber,
            ItemWeightCategory.RawHide           => RawHide,
            ItemWeightCategory.RawSpecialOre     => RawSpecialOre,
            _                                    => SmallSupply
        };

    public static double GetPotionKegWeight(int held)
    {
        var clampedHeld = Math.Clamp(held, 0, 100);
        return PotionKegEmpty + clampedHeld * (Potion * PotionKegChargeWeightRatio);
    }

    internal static void ApplySettings(
        double globalScale = 1.0,
        double reagent = DefaultReagent,
        double bandage = DefaultBandage,
        double ammunition = DefaultAmmunition,
        double gem = DefaultGem,
        double scroll = DefaultScroll,
        double smallSupply = DefaultSmallSupply,
        double cookingIngredient = DefaultCookingIngredient,
        double potion = DefaultPotion,
        double refinedMaterial = DefaultRefinedMaterial,
        double processedMaterial = DefaultProcessedMaterial,
        double rawWood = DefaultRawWood,
        double rawFiber = DefaultRawFiber,
        double rawHide = DefaultRawHide,
        double rawSpecialOre = DefaultRawSpecialOre,
        double potionKegEmpty = DefaultPotionKegEmpty
    )
    {
        GlobalScale = ClampScale(globalScale);
        Reagent = ScaleWeight(reagent, GlobalScale);
        Bandage = ScaleWeight(bandage, GlobalScale);
        Ammunition = ScaleWeight(ammunition, GlobalScale);
        Gem = ScaleWeight(gem, GlobalScale);
        Scroll = ScaleWeight(scroll, GlobalScale);
        SmallSupply = ScaleWeight(smallSupply, GlobalScale);
        CookingIngredient = ScaleWeight(cookingIngredient, GlobalScale);
        Potion = ScaleWeight(potion, GlobalScale);
        RefinedMaterial = ScaleWeight(refinedMaterial, GlobalScale);
        ProcessedMaterial = ScaleWeight(processedMaterial, GlobalScale);
        RawWood = ScaleWeight(rawWood, GlobalScale);
        RawFiber = ScaleWeight(rawFiber, GlobalScale);
        RawHide = ScaleWeight(rawHide, GlobalScale);
        RawSpecialOre = ScaleWeight(rawSpecialOre, GlobalScale);
        PotionKegEmpty = ScaleWeight(potionKegEmpty, GlobalScale);
    }

    internal static void ResetDefaults() => ApplySettings();

    private static double ClampScale(double scale) =>
        double.IsNaN(scale) || double.IsInfinity(scale)
            ? 1.0
            : Math.Clamp(scale, MinGlobalScale, MaxGlobalScale);

    private static double ScaleWeight(double weight, double scale) =>
        double.IsNaN(weight) || double.IsInfinity(weight)
            ? MinWeight
            : ClampWeight(weight * scale);

    private static double ClampWeight(double weight) =>
        double.IsNaN(weight) || double.IsInfinity(weight)
            ? MinWeight
            : Math.Clamp(weight, MinWeight, MaxWeight);
}
