using System;
using Server.Items;

namespace Server.Mobiles;

internal enum PetCombatRole
{
    None,
    Guardian,
    Striker,
    Scout
}

public partial class BaseCreature
{
    private const int PetCombatRoleMask = 0xF;
    internal const int PetCombatRoleLevel = 10;
    private static readonly TimeSpan _roleInterval = TimeSpan.FromSeconds(10);
    private DateTime _petRoleReadyAt;

    internal PetCombatRole PetRole => (PetCombatRole)(PetTrainingOptions & PetCombatRoleMask);

    internal bool TryChoosePetCombatRole(Mobile owner, PetCombatRole role)
    {
        if (role is < PetCombatRole.Guardian or > PetCombatRole.Scout ||
            !CanManagePetProgression(owner) || PetLevel < PetCombatRoleLevel)
        {
            return false;
        }

        if (Combatant is { Alive: true, Deleted: false } || owner.Combatant is { Alive: true, Deleted: false })
        {
            owner.SendMessage("Finish the fight before changing your pet's role.");
            return false;
        }

        PetTrainingOptions = (PetTrainingOptions & ~PetCombatRoleMask) | (int)role;
        owner.SendMessage($"{Name} is now a {role}.");
        return true;
    }

    internal void OnPetRoleHit(BaseCreature defender, int actualDamage)
    {
        if (actualDamage <= 0 || !CanProgressAsPet || PetRole == PetCombatRole.None ||
            defender is not { Alive: true, Deleted: false, Player: false, Controlled: false, Summoned: false, IsDeadPet: false }
            || defender.Master != null || defender.IsInvulnerable || defender is BaseVendor || defender.Map != Map ||
            ControlMaster is not PlayerMobile { Alive: true, Deleted: false } owner || owner.Map != Map ||
            !owner.InRange(this, PetInteractionRange) || Core.Now < _petRoleReadyAt)
        {
            return;
        }

        // Arm the cooldown before an effect can cause another damage callback.
        var now = Core.Now;
        _petRoleReadyAt = now + _roleInterval;
        var activated = PetRole switch
        {
            PetCombatRole.Guardian => GuardOwner(owner, defender),
            PetCombatRole.Striker  => StrikeAgain(owner, defender, actualDamage),
            PetCombatRole.Scout    => SlowOpponent(owner, defender),
            _                      => false
        };

        if (!activated)
        {
            _petRoleReadyAt = now;
        }
    }

    private bool GuardOwner(PlayerMobile owner, BaseCreature defender)
    {
        if (!owner.InRange(this, 2) || !InLOS(owner) || owner.Hits >= owner.HitsMax ||
            defender.Combatant != owner || owner.Poisoned || MortalStrike.IsWounded(owner))
        {
            return false;
        }

        owner.Heal(8, this, false);
        owner.SendMessage($"{Name} protects you and restores some health.");
        return true;
    }

    private bool StrikeAgain(PlayerMobile owner, BaseCreature defender, int actualDamage)
    {
        if (!CanBeHarmful(defender, false))
        {
            return false;
        }

        AOS.Damage(defender, this, Math.Clamp(actualDamage / 4, 2, 10), 100, 0, 0, 0, 0);
        owner.SendMessage($"{Name} lands a follow-up strike.");
        return true;
    }

    private bool SlowOpponent(PlayerMobile owner, BaseCreature defender)
    {
        if (!CanBeHarmful(defender, false) || defender.Stam <= 0)
        {
            return false;
        }

        defender.Stam = Math.Max(0, defender.Stam - Math.Clamp(defender.StamMax / 10, 8, 25));
        owner.SendMessage($"{Name} tires the opposing creature.");
        return true;
    }
}
