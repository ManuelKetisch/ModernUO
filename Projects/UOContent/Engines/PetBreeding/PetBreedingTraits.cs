using System;
using Server.Mobiles;

namespace Server.Engines.PetBreeding;

internal static class PetBreedingTraits
{
    private enum Trait
    {
        Strength,
        Dexterity,
        Intelligence,
        Hits,
        Stamina,
        Mana,
        MinimumDamage,
        MaximumDamage,
        PhysicalResistance,
        FireResistance,
        ColdResistance,
        PoisonResistance,
        EnergyResistance,
        Count
    }

    internal static bool IsValid(int[] traits) => traits?.Length == (int)Trait.Count;

    internal static int[] Capture(BaseCreature parent, BaseCreature mate, PetTrainingStat? focus = null)
    {
        var stats = PetBreedingFocus.Stats;
        var traits = new int[(int)Trait.Count];
        Span<int> baseline = stackalloc int[(int)Trait.Count];
        for (var i = 0; i < stats.Length; i++)
        {
            var (first, second, cap, floor) = GetStatInputs(parent, mate, stats[i].Stat);
            var average = ((long)first + second) / 2;
            var bonusLimit = GetBonusLimit(first, second);
            var bonus = bonusLimit > 0 ? Utility.RandomMinMax(0, bonusLimit) : 0;
            baseline[i] = (int)Math.Clamp(average, floor, cap);
            traits[i] = (int)Math.Clamp(average + bonus, floor, cap);
        }

        var selected = PetBreedingFocus.IndexOf(focus);
        if (selected >= 0)
        {
            const int minimumDamage = (int)Trait.MinimumDamage;
            const int maximumDamage = (int)Trait.MaximumDamage;
            traits[maximumDamage] = Math.Max(traits[minimumDamage], traits[maximumDamage]);
            var cap = GetStatInputs(parent, mate, stats[selected].Stat).Cap;
            if (selected == minimumDamage)
            {
                cap = Math.Min(cap, traits[maximumDamage]);
            }

            PetBreedingFocus.MoveBonus(traits, baseline, selected, cap);
        }

        return traits;
    }

    internal static (int Min, int Max) GetOffspringMaximumLevelRange(BaseCreature first, BaseCreature second)
    {
        var average = (first.PetLevel + second.PetLevel) / 2;
        return (average + 1, average + 3);
    }

    internal static (int Min, int Max) GetStatInheritanceRange(BaseCreature parent, BaseCreature mate, PetTrainingStat stat)
    {
        var (first, second, cap, floor) = GetStatInputs(parent, mate, stat);
        return InheritanceRange(first, second, cap, floor);
    }

    internal static (int First, int Second, int Cap, int Floor) GetStatInputs(
        BaseCreature parent, BaseCreature mate, PetTrainingStat stat)
    {
        var (parentValue, parentCap, _) = parent.GetPetTrainingStat(stat);
        var (mateValue, mateCap, _) = mate.GetPetTrainingStat(stat);
        var floor = stat is >= PetTrainingStat.PhysicalResistance and <= PetTrainingStat.EnergyResistance ? 0 : 1;
        var cap = stat == PetTrainingStat.MinimumDamage ? 17 : Math.Min(parentCap, mateCap);
        return (parentValue, mateValue, cap, floor);
    }

    internal static void Apply(BaseCreature offspring, int[] traits)
    {
        offspring.RawStr = traits[(int)Trait.Strength];
        offspring.RawDex = traits[(int)Trait.Dexterity];
        offspring.RawInt = traits[(int)Trait.Intelligence];
        offspring.HitsMaxSeed = traits[(int)Trait.Hits];
        offspring.StamMaxSeed = traits[(int)Trait.Stamina];
        offspring.ManaMaxSeed = traits[(int)Trait.Mana];
        offspring.Hits = offspring.HitsMax;
        offspring.Stam = offspring.StamMax;
        offspring.Mana = offspring.ManaMax;
        var minDamage = traits[(int)Trait.MinimumDamage];
        offspring.SetDamage(minDamage, Math.Max(minDamage, traits[(int)Trait.MaximumDamage]));
        offspring.SetResistance(ResistanceType.Physical, traits[(int)Trait.PhysicalResistance]);
        offspring.SetResistance(ResistanceType.Fire, traits[(int)Trait.FireResistance]);
        offspring.SetResistance(ResistanceType.Cold, traits[(int)Trait.ColdResistance]);
        offspring.SetResistance(ResistanceType.Poison, traits[(int)Trait.PoisonResistance]);
        offspring.SetResistance(ResistanceType.Energy, traits[(int)Trait.EnergyResistance]);
    }

    internal static (int Min, int Max) InheritanceRange(int first, int second, int cap, int floor)
    {
        var average = ((long)first + second) / 2;
        var bonusLimit = GetBonusLimit(first, second);
        return ((int)Math.Clamp(average, floor, cap), (int)Math.Clamp(average + bonusLimit, floor, cap));
    }

    private static int GetBonusLimit(int first, int second) =>
        (int)Math.Min(5, Math.Abs((long)first - second) / 8);
}
