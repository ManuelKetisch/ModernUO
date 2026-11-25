using System;

namespace Server.Mobiles;

internal readonly record struct PetAbilitySpendQuote(
    PetTrainingStat Stat, int Requested, int Current, int Cap, int Result, int PointsBefore
)
{
    public int Cost => Result - Current;
    public int PointsAfter => PointsBefore - Cost;

    internal static bool TryCreate(
        BaseCreature pet, Mobile owner, PetTrainingStat stat, int requested, out PetAbilitySpendQuote quote
    )
    {
        quote = default;
        if (requested <= 0 || pet.PetAbilityPoints <= 0 || !pet.CanManagePetProgression(owner))
        {
            return false;
        }

        var (current, cap, unlock) = pet.GetPetTrainingStat(stat);
        if (pet.PetLevel < unlock || current >= cap)
        {
            return false;
        }

        var points = pet.PetAbilityPoints;
        var cost = Math.Min(requested, Math.Min(points, cap - current));
        quote = new PetAbilitySpendQuote(stat, requested, current, cap, current + cost, points);
        return true;
    }
}
