using System;
using Server.ContextMenus;
using Server.Engines.PetProgression;
using Server.Items;

namespace Server.Mobiles;

internal enum PetTrainingStat
{
    Hits,
    Stamina,
    Mana,
    PhysicalResistance,
    FireResistance,
    ColdResistance,
    PoisonResistance,
    EnergyResistance,
    MinimumDamage,
    MaximumDamage,
    Armor,
    Strength,
    Dexterity,
    Intelligence
}

public partial class BaseCreature
{
    internal const int PetInteractionRange = 8;
    internal const int FirstBreedingLevel = 9;
    internal const int LastBreedingLevel = 42;
    internal const int LastBreedingGeneration = 9;
    private const int HighestPetLevel = 45;
    private const int LowestFounderMaxLevel = 10;
    private const int HighestFounderMaxLevel = 30;
    private const int BreedingPauseOption = 0x100;

    // Serial-derived founder traits stay stable for pets that already exist in saved worlds.
    private uint FounderTraits => unchecked(Serial.Value * 0x9E3779B9u);

    public int PetLevel => Math.Max(1, PetProgressLevel);
    public int PetGeneration => Math.Max(1, PetProgressGeneration);
    public int PetMaxLevel => PetProgressMaxLevel > 0
        ? PetProgressMaxLevel
        : LowestFounderMaxLevel +
          (int)((FounderTraits >> 16) % (HighestFounderMaxLevel - LowestFounderMaxLevel + 1));

    public bool PetFemale => PetProgressSex switch
    {
        1 => false,
        2 => true,
        _ => Female || (FounderTraits & 0x80000000u) != 0
    };

    internal bool CanPausePetAtBreedingLimit =>
        Tamable && PetMaxLevel > LastBreedingLevel && PetLevel <= LastBreedingLevel &&
        PetGeneration <= LastBreedingGeneration;

    internal bool PetBreedingPauseEnabled => (PetTrainingOptions & BreedingPauseOption) != 0;

    private int EffectivePetLevelCap =>
        PetBreedingPauseEnabled && CanPausePetAtBreedingLimit ? LastBreedingLevel : PetMaxLevel;

    internal int PetExperienceForNextLevel => PetLevel >= EffectivePetLevelCap ? 0 : ExperienceAtLevel(PetLevel);

    internal bool TrySetPetBreedingPause(Mobile owner, bool enabled)
    {
        if (!CanManagePetProgression(owner) || !CanPausePetAtBreedingLimit)
        {
            return false;
        }

        PetTrainingOptions = enabled
            ? PetTrainingOptions | BreedingPauseOption
            : PetTrainingOptions & ~BreedingPauseOption;
        return true;
    }

    private static int ExperienceAtLevel(int level) => 25 + 10 * (level - 1);

    internal void InitializePetProgression(int generation, int maxLevel, bool? female = null)
    {
        if (Controlled || HasProgressMaxLevel() || HasProgressLevel())
        {
            return;
        }

        PetProgressGeneration = Math.Clamp(generation, 1, LastBreedingGeneration + 1);
        PetProgressMaxLevel = Math.Clamp(maxLevel, LowestFounderMaxLevel, HighestPetLevel);
        PetProgressSex = female switch
        {
            true  => 2,
            false => 1,
            _     => 0
        };
    }

    private bool CanProgressAsPet =>
        Controlled && ControlMaster is PlayerMobile && !Summoned && (Tamable || HasProgressMaxLevel()) &&
        Alive && !IsDeadPet && !IsStabled;

    internal bool CanManagePetProgression(Mobile from) =>
        from?.Alive == true && from == ControlMaster && CanProgressAsPet && Map != Map.Internal &&
        from.Map == Map && from.InRange(this, PetInteractionRange) && from.CanSee(this) && from.InLOS(this);

    internal int GetPetMeleeOpponentMaxHits(BaseCreature opponent)
    {
        if (!CanProgressAsPet || opponent == this)
        {
            return 0;
        }

        if (opponent is PersonalPetTrainer trainer)
        {
            return trainer.CanSparWith(this) ? PersonalPetTrainer.PracticeHits : 0;
        }

        if (opponent is not { Alive: true, IsDeadPet: false, IsInvulnerable: false, Player: false, Master: null }
            || opponent is BaseVendor || opponent.Controlled || opponent.Summoned)
        {
            return 0;
        }

        return opponent.HitsMax;
    }

    internal void AwardPetExperience(int awarded)
    {
        var cap = EffectivePetLevelCap;
        if (awarded <= 0 || !CanProgressAsPet || PetLevel >= cap)
        {
            return;
        }

        var previousLevel = PetLevel;
        var level = previousLevel;
        var experience = (long)PetExperience + awarded;
        var points = PetAbilityPoints;

        while (level < cap && experience >= ExperienceAtLevel(level))
        {
            experience -= ExperienceAtLevel(level);
            level++;
            points += Utility.RandomMinMax(3, 8);
        }

        PetExperience = level == cap ? 0 : (int)experience;
        if (level != previousLevel)
        {
            PetProgressLevel = level;
            PetAbilityPoints = points;
            ControlMaster?.SendMessage($"{Name} reached level {level}.");
        }
    }

    internal (int Value, int Cap, int Unlock) GetPetTrainingStat(PetTrainingStat stat)
    {
        var pack = PackInstinct != PackInstinct.None;
        var (minimumDamage, maximumDamage) = stat is PetTrainingStat.MinimumDamage or PetTrainingStat.MaximumDamage
            ? GetPetBaseDamage()
            : default;

        return stat switch
        {
            PetTrainingStat.Hits               => (HitsMaxSeed > 0 ? HitsMaxSeed : RawStr, 1000, 2),
            PetTrainingStat.Stamina            => (StamMaxSeed > 0 ? StamMaxSeed : RawDex, 500, 2),
            PetTrainingStat.Mana               => (ManaMaxSeed > 0 ? ManaMaxSeed : RawInt, 1000, 2),
            PetTrainingStat.PhysicalResistance => (PhysicalResistanceSeed, 75, 10),
            PetTrainingStat.FireResistance     => (FireResistSeed, 75, 10),
            PetTrainingStat.ColdResistance     => (ColdResistSeed, 75, 10),
            PetTrainingStat.PoisonResistance   => (PoisonResistSeed, 75, 10),
            PetTrainingStat.EnergyResistance   => (EnergyResistSeed, 75, 10),
            PetTrainingStat.MinimumDamage      => (minimumDamage, Math.Min(17, maximumDamage), 20),
            PetTrainingStat.MaximumDamage      => (maximumDamage, 25, 20),
            PetTrainingStat.Armor              => (VirtualArmor, 70, 30),
            PetTrainingStat.Strength           => (RawStr, pack ? 250 : 1000, 40),
            PetTrainingStat.Dexterity          => (RawDex, pack ? 125 : 250, 40),
            PetTrainingStat.Intelligence       => (RawInt, pack ? 175 : 750, 40),
            _                                 => (0, 0, int.MaxValue)
        };
    }

    private (int Minimum, int Maximum) GetPetBaseDamage()
    {
        if (DamageMin >= 0)
        {
            return (DamageMin, Math.Max(DamageMin, DamageMax));
        }

        if (!Body.IsHuman)
        {
            var baseDamage = RawStr / 28;
            return (baseDamage, baseDamage);
        }

        return GetDefaultWeapon() is BaseWeapon weapon ? (weapon.MinDamage, weapon.MaxDamage) : (1, 4);
    }

    internal int TrySpendPetAbilityPoints(Mobile from, PetTrainingStat stat, int requested)
    {
        if (!PetAbilitySpendQuote.TryCreate(this, from, stat, requested, out var quote))
        {
            return 0;
        }

        CarryDerivedPetDeathLossForward(stat);
        SetPetTrainingStat(stat, quote.Result);
        PetAbilityPoints -= quote.Cost;
        return quote.Cost;
    }

    private void SetPetTrainingStat(PetTrainingStat stat, int newValue)
    {
        switch (stat)
        {
            case PetTrainingStat.Hits:
                HitsMaxSeed = newValue;
                break;
            case PetTrainingStat.Stamina:
                StamMaxSeed = newValue;
                break;
            case PetTrainingStat.Mana:
                ManaMaxSeed = newValue;
                break;
            case PetTrainingStat.PhysicalResistance:
                PhysicalResistanceSeed = newValue;
                break;
            case PetTrainingStat.FireResistance:
                FireResistSeed = newValue;
                break;
            case PetTrainingStat.ColdResistance:
                ColdResistSeed = newValue;
                break;
            case PetTrainingStat.PoisonResistance:
                PoisonResistSeed = newValue;
                break;
            case PetTrainingStat.EnergyResistance:
                EnergyResistSeed = newValue;
                break;
            case PetTrainingStat.MinimumDamage:
                SetDamage(newValue, GetPetBaseDamage().Maximum);
                break;
            case PetTrainingStat.MaximumDamage:
                SetDamage(GetPetBaseDamage().Minimum, newValue);
                break;
            case PetTrainingStat.Armor:
                VirtualArmor = newValue;
                break;
            case PetTrainingStat.Strength:
                RawStr = newValue;
                break;
            case PetTrainingStat.Dexterity:
                RawDex = newValue;
                break;
            case PetTrainingStat.Intelligence:
                RawInt = newValue;
                break;
        }
    }

    private sealed class PetProgressionEntry() : ContextMenuEntry(3005031, PetInteractionRange)
    {
        public override void OnClick(Mobile from, IEntity target)
        {
            if (target is BaseCreature pet)
            {
                PetProgressionGump.DisplayTo(from, pet);
            }
        }
    }
}
