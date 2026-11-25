using System;
using Server.Collections;
using Server.Mobiles;

namespace Server.Engines.BioEngineering;

internal enum BioTrait
{
    None = 0,
    SearingStrike = 1,
    QuickRecovery = 2,
    AcidBurst = 3,
    MeteorImpact = 4,
    WildCall = 5,
    RimeBurst = 6,
    StoneRiposte = 7,
    VenomCounter = 8
}

public partial class BioConstruct
{
    private const int TraitStaminaCost = 12;
    private const int TraitRange = 5;
    private const int SplashRange = 2;
    private const int MaxSplashTargets = 3;

    private static readonly TimeSpan _traitCooldown = TimeSpan.FromSeconds(12);

    internal static BioTrait ChooseTrait(BaseCreature donor)
    {
        var familyTrait = donor switch
        {
            Slime or AcidElemental                      => BioTrait.AcidBurst,
            FireElemental or Efreet                     => BioTrait.SearingStrike,
            IceElemental or FrostTroll                   => BioTrait.RimeBurst,
            StoneHarpy                                   => BioTrait.StoneRiposte,
            GiantSpider or DreadSpider                  => BioTrait.VenomCounter,
            DireWolf or TimberWolf                      => BioTrait.WildCall,
            _                                            => BioTrait.None
        };
        if (familyTrait != BioTrait.None)
        {
            return familyTrait;
        }

        if (donor.PoisonDamage >= 50)
        {
            return BioTrait.AcidBurst;
        }

        if (donor.HitPoison != null)
        {
            return BioTrait.VenomCounter;
        }

        if (donor.ColdDamage >= 50)
        {
            return BioTrait.RimeBurst;
        }

        if (donor.FireDamage >= 50 || donor.Skills.Magery.Base >= 50)
        {
            return BioTrait.SearingStrike;
        }

        if (donor.Body.IsAnimal && donor.PackInstinct != PackInstinct.None)
        {
            return BioTrait.WildCall;
        }

        if (donor.VirtualArmor >= 50)
        {
            return BioTrait.MeteorImpact;
        }

        return donor.DamageMin >= 12 ? BioTrait.StoneRiposte : BioTrait.QuickRecovery;
    }

    internal static string TraitName(BioTrait trait) => trait switch
    {
        BioTrait.SearingStrike => "Searing Strike",
        BioTrait.QuickRecovery => "Quick Recovery",
        BioTrait.AcidBurst     => "Acid Burst",
        BioTrait.MeteorImpact  => "Meteor Impact",
        BioTrait.WildCall      => "Wild Call",
        BioTrait.RimeBurst     => "Rime Burst",
        BioTrait.StoneRiposte  => "Stone Riposte",
        BioTrait.VenomCounter  => "Venom Counter",
        _                      => "None"
    };

    internal static string TraitDescription(BioTrait trait) => trait switch
    {
        BioTrait.SearingStrike => "Follows a melee hit with two bursts of fire.",
        BioTrait.QuickRecovery => "Restores health after taking a melee hit.",
        BioTrait.AcidBurst     => "Splashes nearby attackers with poison.",
        BioTrait.MeteorImpact  => "Hits nearby attackers with falling stone.",
        BioTrait.WildCall      => "Calls nearby attacking animals to aid a strike.",
        BioTrait.RimeBurst     => "Spreads cold damage to nearby moving attackers.",
        BioTrait.StoneRiposte  => "Answers a melee hit with physical damage.",
        BioTrait.VenomCounter  => "Answers a melee hit with poison damage.",
        _                      => "This donor has no special move."
    };

    private bool CanUseTraitAgainst(BaseCreature target) =>
        target != this && ControlMaster is PlayerMobile { Deleted: false, Alive: true } owner &&
        owner.Map == Map && owner.InRange(this, 18) && BioVial.IsWildCreature(target) &&
        target.Map == Map && InRange(target, TraitRange) && InLOS(target) &&
        CanBeHarmful(target, false) && owner.CanBeHarmful(target, false);

    private bool IsAggressiveSecondary(BaseCreature creature) =>
        (creature.Combatant == this || creature.Combatant == ControlMaster) && CanUseTraitAgainst(creature);

    internal void OnLandedMeleeAttack(Mobile defender)
    {
        if (defender is not BaseCreature target || !CanUseTraitAgainst(target))
        {
            return;
        }

        if (CanApplyRefitEffects(target))
        {
            ApplyFrameEffect(target, landedHit: true);
        }

        switch (Trait)
        {
            case BioTrait.SearingStrike when TrySpendTraitStamina():
                for (var hit = 0; hit < 2 && CanUseTraitAgainst(target); hit++)
                {
                    AOS.Damage(target, this, 6, 0, 100, 0, 0, 0);
                }

                break;
            case BioTrait.AcidBurst when TrySpendTraitStamina():
                InflictSplash(target, ResistanceType.Poison);
                break;
            case BioTrait.MeteorImpact when TrySpendTraitStamina():
                InflictSplash(target, ResistanceType.Physical);
                break;
            case BioTrait.WildCall:
                TryCallWild(target);
                break;
            case BioTrait.RimeBurst when TrySpendTraitStamina():
                InflictSplash(target, ResistanceType.Cold);
                break;
        }
    }

    internal void OnReceivedMeleeHit(Mobile attacker)
    {
        if (attacker is not BaseCreature target || !CanUseTraitAgainst(target))
        {
            return;
        }

        var applyRefitEffects = CanApplyRefitEffects(target);
        if (applyRefitEffects)
        {
            ApplyUtilityEffect();
        }

        switch (Trait)
        {
            case BioTrait.QuickRecovery when HitsMax - Hits >= 25 && TrySpendTraitStamina():
                Hits += 25;
                break;
            case BioTrait.StoneRiposte when TrySpendTraitStamina():
                AOS.Damage(target, this, 14, 100, 0, 0, 0, 0);
                break;
            case BioTrait.VenomCounter when TrySpendTraitStamina():
                AOS.Damage(target, this, 10, 0, 0, 0, 100, 0);
                break;
        }

        if (applyRefitEffects)
        {
            // Donor recovery gets first use of the missing health.
            ApplyFrameEffect(target, landedHit: false);
        }
    }

    private void InflictSplash(BaseCreature target, ResistanceType element)
    {
        using var secondaries = PooledRefList<BaseCreature>.Create(MaxSplashTargets);
        foreach (var creature in target.GetMobilesInRange<BaseCreature>(SplashRange))
        {
            if (creature == target || !IsAggressiveSecondary(creature) ||
                element == ResistanceType.Cold && Core.TickCount - creature.LastMoveTime >= 1500)
            {
                continue;
            }

            secondaries.Add(creature);
            if (secondaries.Count == MaxSplashTargets)
            {
                break;
            }
        }

        InflictElementalDamage(target, 12, element);
        for (var i = 0; i < secondaries.Count; i++)
        {
            var creature = secondaries[i];
            if (IsAggressiveSecondary(creature))
            {
                DoHarmful(creature, true);
                InflictElementalDamage(creature, 6, element);
            }
        }
    }

    private void InflictElementalDamage(BaseCreature target, int amount, ResistanceType element)
    {
        AOS.Damage(
            target, this, amount, element == ResistanceType.Physical ? 100 : 0, 0,
            element == ResistanceType.Cold ? 100 : 0, element == ResistanceType.Poison ? 100 : 0, 0
        );
    }

    private void TryCallWild(BaseCreature target)
    {
        using var helpers = PooledRefList<BaseCreature>.Create(2);
        foreach (var creature in target.GetMobilesInRange<BaseCreature>(SplashRange))
        {
            if (creature == target || !creature.Body.IsAnimal || !IsAggressiveSecondary(creature) ||
                !creature.InLOS(target) || !creature.CanBeHarmful(target, false))
            {
                continue;
            }

            helpers.Add(creature);
            if (helpers.Count == 2)
            {
                break;
            }
        }

        if (helpers.Count == 0 || !TrySpendTraitStamina())
        {
            return;
        }

        // A brief assist keeps wild animals in their own combat afterward.
        for (var i = 0; i < helpers.Count && CanUseTraitAgainst(target); i++)
        {
            helpers[i].PlaySound(helpers[i].GetAttackSound());
            AOS.Damage(target, this, 8, 100, 0, 0, 0, 0);
        }
    }

    private bool TrySpendTraitStamina()
    {
        var now = Core.Now;
        var staminaCost = FocusedCircuit ? CircuitStaminaCost : TraitStaminaCost;
        if (!Assembled || !Controlled || !Alive || IsDeadPet || Stam < staminaCost || now < NextTraitAt)
        {
            return false;
        }

        Stam -= staminaCost;
        NextTraitAt = now + (FocusedCircuit ? _circuitCooldown : _traitCooldown);
        return true;
    }
}
