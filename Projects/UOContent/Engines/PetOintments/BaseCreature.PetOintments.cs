using System;
using Server.Items;

namespace Server.Mobiles;

public partial class BaseCreature
{
    internal bool TryApplyMixedOintment(PetOintmentKind kind)
    {
        if (!CanProgressAsPet)
        {
            return false;
        }

        var packAnimal = PackInstinct != PackInstinct.None;
        var (stat, cap) = kind switch
        {
            PetOintmentKind.Hits => (PetTrainingStat.Hits, 1100),
            PetOintmentKind.Stamina => (PetTrainingStat.Stamina, 550),
            PetOintmentKind.Mana => (PetTrainingStat.Mana, 1100),
            PetOintmentKind.Strength => (PetTrainingStat.Strength, packAnimal ? 260 : 1050),
            PetOintmentKind.Dexterity => (PetTrainingStat.Dexterity, packAnimal ? 140 : 275),
            PetOintmentKind.Intelligence => (PetTrainingStat.Intelligence, packAnimal ? 175 : 800),
            PetOintmentKind.PhysicalResistance => (PetTrainingStat.PhysicalResistance, 75),
            PetOintmentKind.FireResistance => (PetTrainingStat.FireResistance, 75),
            PetOintmentKind.ColdResistance => (PetTrainingStat.ColdResistance, 75),
            PetOintmentKind.PoisonResistance => (PetTrainingStat.PoisonResistance, 75),
            PetOintmentKind.EnergyResistance => (PetTrainingStat.EnergyResistance, 75),
            PetOintmentKind.Armor => (PetTrainingStat.Armor, 70),
            _ => (default, 0)
        };

        var (value, _, _) = GetPetTrainingStat(stat);
        if (cap == 0 || value >= cap)
        {
            return false;
        }

        CarryDerivedPetDeathLossForward(stat);
        SetPetTrainingStat(stat, Math.Min(cap, value + Math.Max(1, value / 20)));
        return true;
    }
}
