using System;
using Server.Engines.BioEngineering;
using Server.Engines.CannedEvil;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.WeaponLeveling;

public static class WeaponLevelingCombat
{
    private const int BioOwnerCreditRange = 18;
    private static WeaponHit _currentHit;
    private static BaseWeapon _currentAbilityWeapon;

    internal static AbilityScope BeginWeaponAbility(BaseWeapon weapon)
    {
        var previous = _currentAbilityWeapon;
        _currentAbilityWeapon = weapon;
        return new AbilityScope(previous);
    }

    internal static HitScope BeginAbilityStrike(Mobile attacker, Mobile victim) =>
        BeginWeaponHit(_currentAbilityWeapon, attacker, victim as BaseCreature);

    internal static HitScope BeginWeaponHit(BaseWeapon weapon, Mobile attacker, BaseCreature victim)
    {
        var previous = _currentHit;
        _currentHit = default;
        if (weapon is { IsWeaponLevelingEnabled: true } && weapon.WeaponLevel < WeaponLevelingRules.MaximumLevel &&
            IsWieldedBy(weapon, attacker))
        {
            var owner = attacker switch
            {
                PlayerMobile player => player,
                BioConstruct { HasHumanForm: true } bio => GetNearbyBioOwner(bio),
                _ => null
            };
            if (owner != null && CanCredit(victim, owner))
            {
                _currentHit = new WeaponHit(weapon, attacker, owner, victim, owner.NetState);
            }
        }

        return new HitScope(previous);
    }

    public static void OnCreatureDeath(BaseCreature victim)
    {
        var killer = victim.LastKiller;
        var hit = _currentHit;
        if (hit.Victim == victim && hit.Attacker == killer)
        {
            // Settle the direct hit once, before granting any legacy pet-owner credit.
            _currentHit = default;
            if (IsWieldedBy(hit.Weapon, hit.Attacker) && CanCredit(victim, hit.Owner) &&
                (killer is not BioConstruct bio ||
                bio.HasHumanForm && GetNearbyBioOwner(bio) == hit.Owner && hit.Owner.NetState == hit.OwnerSession))
            {
                hit.Weapon.AwardWeaponExperience(
                    KillExperience(victim.Fame, victim.Map == Map.Felucca,
                        ChampionSpawn.HasSpawnProvenance(victim), false), hit.Owner
                );
                return;
            }
        }

        if (killer is BaseCreature
            {
                Controlled: true, Summoned: false, IsDeadPet: false, ControlMaster: PlayerMobile owner
            } &&
            owner.Weapon is BaseWeapon weapon && IsWieldedBy(weapon, owner) &&
            weapon.IsWeaponLevelingEnabled && weapon.WeaponLevel >= WeaponLevelingRules.PetCreditLevel &&
            weapon.WeaponLevel < WeaponLevelingRules.MaximumLevel && CanCredit(victim, owner))
        {
            weapon.AwardWeaponExperience(
                KillExperience(victim.Fame, victim.Map == Map.Felucca,
                    ChampionSpawn.HasSpawnProvenance(victim), true), owner
            );
        }
    }

    internal static int KillExperience(int fame, bool felucca, bool championSpawn, bool petKill)
    {
        // The archive gives Dragon as 80 XP. Its base Fame is 15,000.
        var experience = (int)Math.Clamp(80L * fame / 15000, 1, int.MaxValue / 2);
        var adjusted = experience * (felucca ? 2 : 1);
        return adjusted / ((championSpawn ? 8 : 1) * (petKill ? 8 : 1));
    }

    private static PlayerMobile GetNearbyBioOwner(BioConstruct bio) =>
        bio is
        {
            Assembled: true, Controlled: true, Summoned: false, Alive: true, Deleted: false,
            IsDeadPet: false, IsStabled: false, Map: not null,
            ControlMaster: PlayerMobile { Alive: true, Deleted: false, NetState: { Running: true } state } owner
        } && bio.Map != Map.Internal && state.Mobile == owner && owner.Map == bio.Map &&
        owner.InRange(bio, BioOwnerCreditRange) ? owner : null;

    private static bool IsWieldedBy(BaseWeapon weapon, Mobile wielder) =>
        wielder is { Alive: true, Deleted: false } && weapon is { Deleted: false } &&
        weapon.Parent == wielder && wielder.Weapon == weapon;

    private static bool CanCredit(BaseCreature victim, PlayerMobile player) =>
        victim is
        {
            Deleted: false, Player: false, Controlled: false, Summoned: false, IsBonded: false,
            IsDeadPet: false, NoKillAwards: false, IsInvulnerable: false, Map: not null, LastOwner: null
        } && victim is not BaseVendor && victim.Map != Map.Internal && victim.Map == player.Map &&
        victim.GetMaster() == null;

    internal readonly record struct WeaponHit(
        BaseWeapon Weapon, Mobile Attacker, PlayerMobile Owner, BaseCreature Victim, NetState OwnerSession
    );

    internal readonly struct AbilityScope(BaseWeapon previous) : IDisposable
    {
        public void Dispose() => _currentAbilityWeapon = previous;
    }

    internal readonly struct HitScope(WeaponHit previous) : IDisposable
    {
        public void Dispose() => _currentHit = previous;
    }
}
