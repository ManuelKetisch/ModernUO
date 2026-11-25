using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Mobiles;

namespace Server.Engines.PetBreeding;

public sealed class PetBreedingOuting : GenericPersistence
{
    private const int RequiredFamilies = 3;
    private static readonly TimeSpan _reduction = TimeSpan.FromHours(24);
    private static readonly TimeSpan _minimumRest = TimeSpan.FromHours(24);
    private static readonly Dictionary<BaseCreature, Litter> _litters = new();

    private sealed class Litter(BaseCreature first, BaseCreature second)
    {
        public BaseCreature First = first;
        public BaseCreature Second = second;
        public BaseCreature OutingPet;
        public PlayerMobile Owner;
        public readonly HashSet<string> Families = new(StringComparer.Ordinal);
        public bool Rewarded;

        public void ClearOuting()
        {
            OutingPet = null;
            Owner = null;
            Families.Clear();
        }
    }

    internal PetBreedingOuting() : base("PetBreedingOutings", 11) { }

    public static void Configure() => _ = new PetBreedingOuting();

    internal static void RecordLitter(BaseCreature first, BaseCreature second)
    {
        OnPetDeleted(first);
        OnPetDeleted(second);
        var litter = new Litter(first, second);
        _litters[first] = litter;
        _litters[second] = litter;
    }

    [OnEvent(nameof(CreatureEvents.CreatureDeletedEvent))]
    public static void OnPetDeleted(BaseCreature pet)
    {
        if (!_litters.Remove(pet, out var litter))
        {
            return;
        }

        if (litter.First == pet)
        {
            litter.First = null;
        }
        else
        {
            litter.Second = null;
        }

        if (litter.OutingPet == pet)
        {
            litter.ClearOuting();
        }
    }

    [OnEvent(nameof(PlayerMobile.PlayerDeletedEvent))]
    public static void OnOwnerDeleted(PlayerMobile owner)
    {
        foreach (var litter in _litters.Values)
        {
            if (litter.Owner == owner)
            {
                litter.ClearOuting();
            }
        }
    }

    internal static bool TryStart(PlayerMobile owner, BaseCreature pet, AnimalTrainer trainer, DateTime now)
    {
        if (!PetBreedingSystem.CanUseTrainer(owner, trainer))
        {
            owner?.SendMessage("Visit an animal trainer to begin a breeding outing.");
            return false;
        }

        if (pet?.IsStabled == true ||
            PetBreedingSystem.CheckParent(owner, pet, trainer, now, requireReady: false) != null)
        {
            owner.SendMessage("Choose your living pet nearby. It must still be eligible to breed.");
            return false;
        }

        if (pet.PetBreedingReadyAt - now <= _minimumRest)
        {
            owner.SendMessage("Your pet needs more than one day of breeding rest to benefit from an outing.");
            return false;
        }

        if (!_litters.TryGetValue(pet, out var litter))
        {
            // Existing parents can have a rest date without a saved litter.
            _litters[pet] = litter = new Litter(pet, null);
        }

        if (litter.Rewarded)
        {
            owner.SendMessage("This litter has already earned its outing reward.");
            return false;
        }

        if (litter.Owner != null)
        {
            if (IsActive(litter, now))
            {
                owner.SendMessage("A parent from this litter already has an outing.");
                return false;
            }

            litter.ClearOuting();
        }

        litter.Owner = owner;
        litter.OutingPet = pet;
        owner.SendMessage("Have this pet help defeat and finish three different kinds of wild creatures.");
        return true;
    }

    internal static (int Completed, int Required, bool Active, bool CompletedForLitter)
        GetStatus(PlayerMobile owner, BaseCreature pet, DateTime now)
    {
        if (!IsOwnedBy(owner, pet) || !_litters.TryGetValue(pet, out var litter))
        {
            return (0, RequiredFamilies, false, false);
        }

        var active = litter.Owner == owner && litter.OutingPet == pet && IsActive(litter, now);
        return (active ? litter.Families.Count : 0, RequiredFamilies, active, litter.Rewarded);
    }

    private static bool IsActive(Litter litter, DateTime now) =>
        !litter.Rewarded && IsOwnedBy(litter.Owner, litter.OutingPet) &&
        litter.OutingPet.PetBreedingReadyAt - now > _minimumRest;

    private static bool IsOwnedBy(PlayerMobile owner, BaseCreature pet) =>
        owner is { Deleted: false } && pet is { Deleted: false } &&
        (pet.IsStabled
            ? pet.StabledBy == owner && owner.Stabled?.Contains(pet) == true
            : pet.Controlled && pet.ControlMaster == owner);

    internal static void RecordDefeat(PlayerMobile owner, BaseCreature pet, BaseCreature defeated, DateTime now)
    {
        if (!_litters.TryGetValue(pet, out var litter) || litter.Rewarded || litter.Owner != owner ||
            litter.OutingPet != pet || pet.ControlMaster != owner || defeated.LastKiller != pet ||
            defeated.Alive && !defeated.Deleted)
        {
            return;
        }

        var remaining = pet.PetBreedingReadyAt - now;
        if (remaining <= _minimumRest)
        {
            litter.ClearOuting();
            owner.SendMessage("Your pet's rest ends too soon for an outing reward.");
            return;
        }

        if (!litter.Families.Add(BaseCreature.OpponentTrainingFamily(defeated)))
        {
            return;
        }

        if (litter.Families.Count < RequiredFamilies)
        {
            owner.SendMessage($"Breeding outing: {litter.Families.Count} of {RequiredFamilies} different kinds defeated.");
            return;
        }

        var reduced = remaining - _reduction;
        pet.PetBreedingReadyAt = now + (reduced > _minimumRest ? reduced : _minimumRest);
        litter.Rewarded = true;
        litter.ClearOuting();
        owner.SendMessage("Your outing shortened this pet's breeding rest.");
    }

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0);
        var litters = new HashSet<Litter>(_litters.Values);
        writer.WriteEncodedInt(litters.Count);
        foreach (var litter in litters)
        {
            writer.Write(litter.First);
            writer.Write(litter.Second);
            writer.Write(litter.Owner);
            writer.Write(litter.OutingPet);
            writer.Write(litter.Rewarded);
            writer.WriteEncodedInt(litter.Families.Count);
            foreach (var family in litter.Families)
            {
                writer.Write(family);
            }
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        _ = reader.ReadEncodedInt(); // version
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var first = reader.ReadEntity<BaseCreature>();
            var second = reader.ReadEntity<BaseCreature>();
            var litter = new Litter(first, second)
            {
                Owner = reader.ReadEntity<PlayerMobile>(),
                OutingPet = reader.ReadEntity<BaseCreature>(),
                Rewarded = reader.ReadBool()
            };
            var families = reader.ReadEncodedInt();
            for (var j = 0; j < families; j++)
            {
                var family = reader.ReadString();
                if (litter.Families.Count < RequiredFamilies && !string.IsNullOrEmpty(family))
                {
                    litter.Families.Add(family);
                }
            }

            if (litter.Owner == null || litter.OutingPet == null)
            {
                litter.ClearOuting();
            }

            if (first != null)
            {
                _litters[first] = litter;
            }

            if (second != null)
            {
                _litters[second] = litter;
            }
        }
    }
}
