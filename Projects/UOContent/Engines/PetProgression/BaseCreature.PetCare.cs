using System;

namespace Server.Mobiles;

public partial class BaseCreature
{
    private static readonly PetTrainingStat[] _penaltyStats = Enum.GetValues<PetTrainingStat>();

    internal void ApplyPetDeathPenalty(int roll)
    {
        if (!IsBonded || !IsDeadPet || !Controlled || ControlMaster is not PlayerMobile owner || Summoned ||
            !(Tamable || HasProgressMaxLevel()))
        {
            return;
        }

        // Each outcome occupies one of twelve equally likely rolls.
        switch (roll)
        {
            case 0 when PetAbilityPoints > 0:
                PetAbilityPoints /= 2;
                owner.SendMessage($"{Name} lost some unspent ability points.");
                break;
            case 1 when PetExperience > 0:
                PetExperience /= 2;
                owner.SendMessage($"{Name} lost some progress toward the next level.");
                break;
            case 2 when ApplyPetDeathStatLoss():
                owner.SendMessage($"{Name} lost some stats. A restorative pet ointment can recover them.");
                break;
        }
    }

    private bool ApplyPetDeathStatLoss()
    {
        var changed = false;
        foreach (var stat in _penaltyStats)
        {
            if (!HasIndependentPetStat(stat))
            {
                continue;
            }

            var (value, _, _) = GetPetTrainingStat(stat);
            var loss = value / 20;
            if (loss <= 0)
            {
                continue;
            }

            RecordPetDeathStatLoss(stat, value, loss);
            SetPetTrainingStat(stat, value - loss);
            changed = true;
        }

        if (changed)
        {
            this.MarkDirty();
        }

        return changed;
    }

    private bool HasIndependentPetStat(PetTrainingStat stat) => stat switch
    {
        PetTrainingStat.Hits               => HitsMaxSeed > 0,
        PetTrainingStat.Stamina            => StamMaxSeed > 0,
        PetTrainingStat.Mana               => ManaMaxSeed > 0,
        PetTrainingStat.MinimumDamage or
        PetTrainingStat.MaximumDamage      => DamageMin >= 0,
        _                                  => true
    };

    private void RecordPetDeathStatLoss(PetTrainingStat stat, int valueBeforeLoss, int amount)
    {
        PetDeathStatLoss ??= new int[_penaltyStats.Length * 2];
        var index = (int)stat * 2;

        // The peak preserves native values above the training cap during recovery.
        PetDeathStatLoss[index] = Math.Min(65000, PetDeathStatLoss[index] + amount);
        PetDeathStatLoss[index + 1] = Math.Max(PetDeathStatLoss[index + 1], valueBeforeLoss);
    }

    private void CarryDerivedPetDeathLossForward(PetTrainingStat stat)
    {
        if (PetDeathStatLoss == null || HasIndependentPetStat(stat))
        {
            return;
        }

        // Allocating points to an unseeded stat replaces its dependence on the raw attribute.
        var rawStat = stat switch
        {
            PetTrainingStat.Hits or PetTrainingStat.MinimumDamage or PetTrainingStat.MaximumDamage =>
                PetTrainingStat.Strength,
            PetTrainingStat.Stamina => PetTrainingStat.Dexterity,
            _                       => PetTrainingStat.Intelligence
        };
        var rawLoss = PetDeathStatLoss[(int)rawStat * 2];
        if (rawLoss <= 0)
        {
            return;
        }

        if (stat is PetTrainingStat.MinimumDamage or PetTrainingStat.MaximumDamage)
        {
            if (!Body.IsHuman)
            {
                var before = RawStr / 28;
                var after = (RawStr + rawLoss) / 28;
                if (after > before)
                {
                    RecordPetDeathStatLoss(PetTrainingStat.MinimumDamage, after, after - before);
                    RecordPetDeathStatLoss(PetTrainingStat.MaximumDamage, after, after - before);
                }
            }
        }
        else
        {
            var (current, _, _) = GetPetTrainingStat(stat);
            RecordPetDeathStatLoss(stat, current + rawLoss, rawLoss);
        }
    }

    internal bool TryRestorePetDeathStats()
    {
        if (PetDeathStatLoss == null)
        {
            return false;
        }

        var changed = false;
        foreach (var stat in _penaltyStats)
        {
            if (stat != PetTrainingStat.MinimumDamage)
            {
                changed |= RestorePetDeathStat(stat);
            }
        }

        // The minimum-damage ceiling depends on the maximum damage after restoration.
        changed |= RestorePetDeathStat(PetTrainingStat.MinimumDamage);
        PetDeathStatLoss = null;
        return changed;
    }

    private bool RestorePetDeathStat(PetTrainingStat stat)
    {
        var index = (int)stat * 2;
        if (PetDeathStatLoss[index] == 0)
        {
            return false;
        }

        var (current, cap, _) = GetPetTrainingStat(stat);
        var ceiling = Math.Max(cap, PetDeathStatLoss[index + 1]);
        if (stat == PetTrainingStat.MinimumDamage)
        {
            ceiling = Math.Min(ceiling, GetPetBaseDamage().Maximum);
        }

        var restored = Math.Min(current + PetDeathStatLoss[index], ceiling);
        if (restored <= current)
        {
            return false;
        }

        SetPetTrainingStat(stat, restored);
        return true;
    }
}
