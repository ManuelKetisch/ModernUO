using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Engines.PetBreeding;
using Server.Engines.PetProgression;

namespace Server.Mobiles;

public partial class BaseCreature
{
    private const int MaxPetTrainingContributors = 8;
    private Dictionary<BaseCreature, (PlayerMobile Owner, int Damage)> _petTrainingContributions;
    private int _petTrainingOpponentMaxHits;
    private bool _petTrainingDefeatSettled;

    internal bool HasPetTrainingContributions => _petTrainingContributions?.Count > 0;

    internal void TrackPetMeleeHit(BaseCreature opponent, int opponentMaxHits, int actualDamage)
    {
        if (actualDamage <= 0 || !CanProgressAsPet || ControlMaster is not PlayerMobile trainer)
        {
            return;
        }

        opponent.RecordPetTrainingDamage(this, trainer, opponentMaxHits, actualDamage);
        if (opponent is PersonalPetTrainer)
        {
            return;
        }

        if (!opponent.Alive || opponent.Deleted)
        {
            opponent.SettlePetTrainingDefeat();
        }
        else
        {
            OnPetRoleHit(opponent, actualDamage);
        }
    }

    private void RecordPetTrainingDamage(BaseCreature pet, PlayerMobile owner, int opponentMaxHits, int actualDamage)
    {
        if (_petTrainingDefeatSettled)
        {
            return;
        }

        _petTrainingContributions ??= new(MaxPetTrainingContributors);
        _petTrainingOpponentMaxHits = _petTrainingOpponentMaxHits > 0 ? _petTrainingOpponentMaxHits : opponentMaxHits;
        if (_petTrainingContributions.TryGetValue(pet, out var contribution))
        {
            if (contribution.Owner != owner)
            {
                return;
            }
        }
        else if (_petTrainingContributions.Count >= MaxPetTrainingContributors)
        {
            return;
        }

        int award;
        int credited;
        if (this is PersonalPetTrainer)
        {
            credited = Math.Min(PersonalPetTrainer.PracticeHits, contribution.Damage + actualDamage);
            award = 2 * (credited - contribution.Damage);
        }
        else
        {
            award = PetExperiencePolicy.AwardForDamage(_petTrainingOpponentMaxHits, contribution.Damage,
                actualDamage, out credited);
        }

        _petTrainingContributions[pet] = (owner, credited);
        pet.AwardPetExperience(award);
    }

    [OnEvent(nameof(CreatureEvents.CreatureDeathEvent))]
    public static void OnPetTrainingOpponentDefeated(BaseCreature opponent)
    {
        if (opponent.HasPetTrainingContributions)
        {
            // The current weapon hit records HP lost after the death event returns.
            Timer.StartTimer(opponent.SettlePetTrainingDefeat);
        }
    }

    internal void SettlePetTrainingDefeat()
    {
        if (_petTrainingDefeatSettled || this is PersonalPetTrainer || !HasPetTrainingContributions ||
            LastKiller == null || Alive && !Deleted || IsDeadPet)
        {
            return;
        }

        _petTrainingDefeatSettled = true;
        var minimumDamage = Math.Max(1, (_petTrainingOpponentMaxHits + 4) / 5);
        var participationAward = Math.Clamp(_petTrainingOpponentMaxHits / 75, 2, 12);
        foreach (var (pet, (owner, damage)) in _petTrainingContributions)
        {
            if (damage < minimumDamage || pet.ControlMaster != owner || !pet.CanProgressAsPet)
            {
                continue;
            }

            var award = participationAward;
            if (LastKiller == pet)
            {
                if (pet.PetExperienceForNextLevel > 0)
                {
                    award += pet.TakePetDiversityBonus(this);
                }


                PetBreedingOuting.RecordDefeat(owner, pet, this, Core.Now);
            }

            pet.AwardPetExperience(award);
        }

        _petTrainingContributions = null;
    }
}
