using System;
using Server.Mobiles;

namespace Server.Engines.PetBreeding;

internal static class PetBreedingFocus
{
    internal const int MaxTransferredBonus = 5;

    private static readonly (PetTrainingStat Stat, string Label)[] _stats =
    [
        (PetTrainingStat.Strength, "Strength"),
        (PetTrainingStat.Dexterity, "Dexterity"),
        (PetTrainingStat.Intelligence, "Intelligence"),
        (PetTrainingStat.Hits, "Hit points"),
        (PetTrainingStat.Stamina, "Stamina"),
        (PetTrainingStat.Mana, "Mana"),
        (PetTrainingStat.MinimumDamage, "Minimum damage"),
        (PetTrainingStat.MaximumDamage, "Maximum damage"),
        (PetTrainingStat.PhysicalResistance, "Physical resist"),
        (PetTrainingStat.FireResistance, "Fire resist"),
        (PetTrainingStat.ColdResistance, "Cold resist"),
        (PetTrainingStat.PoisonResistance, "Poison resist"),
        (PetTrainingStat.EnergyResistance, "Energy resist")
    ];

    internal static ReadOnlySpan<(PetTrainingStat Stat, string Label)> Stats => _stats;

    internal readonly record struct Preview(int First, int Second, int Minimum, int Maximum);

    internal static int IndexOf(PetTrainingStat? stat)
    {
        var stats = Stats;
        for (var i = 0; i < stats.Length; i++)
        {
            if (stats[i].Stat == stat)
            {
                return i;
            }
        }

        return -1;
    }

    internal static Preview GetPreview(BaseCreature first, BaseCreature second, PetTrainingStat stat)
    {
        var inputs = PetBreedingTraits.GetStatInputs(first, second, stat);
        var (minimum, maximum) = PetNurseryForecast.GetStatRange(first, second, stat);
        var cap = stat == PetTrainingStat.MinimumDamage
            ? Math.Min(inputs.Cap, PetNurseryForecast.GetStatRange(first, second, PetTrainingStat.MaximumDamage).Max)
            : inputs.Cap;
        var sourceBonus = 0;
        foreach (var (source, _) in Stats)
        {
            if (source != stat && CanDonate(source))
            {
                var range = PetNurseryForecast.GetStatRange(first, second, source);
                sourceBonus += range.Max - range.Min;
            }
        }

        var transfer = Math.Min(MaxTransferredBonus, sourceBonus);
        return new Preview(inputs.First, inputs.Second, minimum, Math.Min(cap, maximum + transfer));
    }

    internal static void MoveBonus(Span<int> rolled, ReadOnlySpan<int> baseline, int selected, int cap)
    {
        var stats = Stats;
        var transfers = Math.Min(MaxTransferredBonus, cap - rolled[selected]);
        for (var point = 0; point < transfers; point++)
        {
            var donor = -1;
            var largestBonus = 0;
            for (var i = 0; i < stats.Length; i++)
            {
                if (i != selected && CanDonate(stats[i].Stat) && rolled[i] - baseline[i] > largestBonus)
                {
                    donor = i;
                    largestBonus = rolled[i] - baseline[i];
                }
            }

            if (donor < 0)
            {
                break;
            }

            rolled[donor]--;
            rolled[selected]++;
        }
    }

    // Damage bounds affect each other when applied, so they can receive a transfer but never fund one.
    private static bool CanDonate(PetTrainingStat stat) =>
        stat is not (PetTrainingStat.MinimumDamage or PetTrainingStat.MaximumDamage);
}
