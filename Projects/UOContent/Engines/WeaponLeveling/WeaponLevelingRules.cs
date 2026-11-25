using System;

namespace Server.Engines.WeaponLeveling;

internal enum WeaponLevelingClass
{
    None,
    Crafted,
    Looted,
    Artifact
}

internal readonly record struct WeaponLevelingSnapshot(
    WeaponLevelingClass Class,
    int Experience,
    int Spent,
    bool ArtifactTagRemoved,
    int BoostUses
);

internal static class WeaponLevelingRules
{
    internal const int MaximumLevel = 100;
    internal const int MaximumExperience = 999900;
    internal const int PetCreditLevel = 50;
    internal const int LevelsPerBoost = 10;
    internal const int BonusBoosts = 9;

    internal static int BoostBonus(WeaponLevelingClass weaponClass, int uses) =>
        Math.Clamp(uses, 0, BonusBoosts) * EarnedPoints(weaponClass, 2);

    internal static int ExperienceForLevel(int level) => 100 * (level * level - 1);

    internal static int LevelForExperience(int experience) => (int)Math.Sqrt(Math.Clamp(experience, 0, MaximumExperience) / 100 + 1);

    internal static int EarnedPoints(WeaponLevelingClass weaponClass, int level)
    {
        var levelUps = Math.Clamp(level - 1, 0, MaximumLevel - 1);
        return weaponClass switch
        {
            WeaponLevelingClass.Crafted => Math.Min(295, levelUps * 3),
            WeaponLevelingClass.Looted or WeaponLevelingClass.Artifact => levelUps * 5,
            _ => 0
        };
    }
}

internal readonly record struct WeaponUpgradeQuote(int Index, int Current, int Next, int Cost, int RawCurrent);
