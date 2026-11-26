using System;
using System.Runtime.CompilerServices;
using Server.Engines.BuffIcons;
using Server.Items;
using Server.Mobiles;
using Server.Spells.Fourth;

namespace Server
{
    /// <summary>
    /// Implements Stygian Abyss / High Seas / Time of Legends combat &amp; caster item property mechanics:
    /// Casting Focus, Elemental/Damage Eaters, Soul Charge, Reactive Paralyze, Mana Phase,
    /// Elemental Resonances, Battle Lust, Splintering Weapon, Hit Curse, Hit Fatigue,
    /// Hit Mana Drain, Blood Drinker, and Bane.
    /// </summary>
    public static class SAItemCombatMechanics
    {
        public const int MaxCastingFocus = 12;
        public const int MaxDamageEater = 18;
        public const int MaxTotalEater = 30;
        public const int MaxSoulCharge = 30;
        public const int MaxResonance = 20;
        public const int MaxBattleLustPvM = 45;
        public const int MaxBattleLustPvP = 15;

        private const long ElementMemoryMs = 1500;
        private const long BattleLustDecayMs = 8000;
        private const long ReactiveParalyzeCooldownMs = 12000;
        private const long ManaPhaseCooldownMs = 20000;
        private const long BaneCooldownMs = 3000;
        private const long SplinteringCooldownMs = 6000;

        private static readonly ConditionalWeakTable<Mobile, CombatState> _states = new();

        private sealed class CombatState
        {
            public long LastElementTick;
            public int LastPhys;
            public int LastFire;
            public int LastCold;
            public int LastPois;
            public int LastNrgy;

            public int BattleLustBonus;
            public long BattleLustExpiresTick;

            public long ReactiveParalyzeReadyTick;
            public long ManaPhaseReadyTick;
            public long BaneReadyTick;
            public long SplinteringReadyTick;

            public int PendingEaterHeal;
            public int EaterTicksRemaining;
            public Timer EaterTimer;

            public int PendingSoulChargeMana;
            public int SoulChargeTicksRemaining;
            public Timer SoulChargeTimer;
        }

        private static CombatState GetState(Mobile m) => _states.GetOrCreateValue(m);

        /// <summary>
        /// Records incoming elemental split right before <see cref="Mobile.Damage(int, Mobile)"/> so
        /// <see cref="TryResistSpellDisturb"/> can check matching Elemental Resonance.
        /// </summary>
        public static void RecordIncomingElement(Mobile defender, int phys, int fire, int cold, int pois, int nrgy)
        {
            if (defender == null || defender.Deleted || !defender.Player)
            {
                return;
            }

            var state = GetState(defender);
            state.LastElementTick = Core.TickCount;
            state.LastPhys = phys;
            state.LastFire = fire;
            state.LastCold = cold;
            state.LastPois = pois;
            state.LastNrgy = nrgy;
        }

        /// <summary>
        /// Processes Damage/Elemental Eaters, Soul Charge, and Battle Lust buildup after damage is applied.
        /// </summary>
        public static void OnDamageApplied(
            Mobile defender, Mobile attacker, int totalDamage, int phys, int fire, int cold, int pois, int nrgy,
            int direct = 0
        )
        {
            if (!Core.AOS || defender == null || defender.Deleted)
            {
                return;
            }

            if (!defender.Alive)
            {
                ClearPendingPools(defender);
                return;
            }

            if (totalDamage <= 0)
            {
                return;
            }

            if (attacker != defender)
            {
                ApplyEaters(defender, totalDamage, phys, fire, cold, pois, nrgy, direct);
                ApplySoulCharge(defender, totalDamage);
            }

            BuildBattleLust(defender, attacker);
        }

        private static void ClearPendingPools(Mobile defender)
        {
            if (!_states.TryGetValue(defender, out var state))
            {
                return;
            }

            state.PendingEaterHeal = 0;
            state.EaterTicksRemaining = 0;
            state.EaterTimer?.Stop();
            state.EaterTimer = null;

            state.PendingSoulChargeMana = 0;
            state.SoulChargeTicksRemaining = 0;
            state.SoulChargeTimer?.Stop();
            state.SoulChargeTimer = null;
        }

        public static int ComputeEaterHeal(
            Mobile defender, int totalDamage, int phys, int fire, int cold, int pois, int nrgy, int direct = 0
        )
        {
            if (defender == null || totalDamage <= 0)
            {
                return 0;
            }

            var damageEater = Math.Min(MaxDamageEater, AosAttributes.GetValue(defender, AosAttribute.DamageEater));
            var kineticEater = AosAttributes.GetValue(defender, AosAttribute.KineticEater);
            var fireEater = AosAttributes.GetValue(defender, AosAttribute.FireEater);
            var coldEater = AosAttributes.GetValue(defender, AosAttribute.ColdEater);
            var poisonEater = AosAttributes.GetValue(defender, AosAttribute.PoisonEater);
            var energyEater = AosAttributes.GetValue(defender, AosAttribute.EnergyEater);

            if (damageEater <= 0 && kineticEater <= 0 && fireEater <= 0 && coldEater <= 0 &&
                poisonEater <= 0 && energyEater <= 0)
            {
                return 0;
            }

            var elementalWeight = phys + fire + cold + pois + nrgy;
            var directWeight = Math.Max(0, direct);

            if (elementalWeight <= 0)
            {
                return directWeight > 0 ? 0 : totalDamage * damageEater / 100;
            }

            var weightedPercent = 0;
            if (phys > 0)
            {
                weightedPercent += phys * Math.Min(MaxTotalEater, damageEater + kineticEater);
            }

            if (fire > 0)
            {
                weightedPercent += fire * Math.Min(MaxTotalEater, damageEater + fireEater);
            }

            if (cold > 0)
            {
                weightedPercent += cold * Math.Min(MaxTotalEater, damageEater + coldEater);
            }

            if (pois > 0)
            {
                weightedPercent += pois * Math.Min(MaxTotalEater, damageEater + poisonEater);
            }

            if (nrgy > 0)
            {
                weightedPercent += nrgy * Math.Min(MaxTotalEater, damageEater + energyEater);
            }

            var totalWeight = elementalWeight + directWeight;
            var effectivePercent = weightedPercent / totalWeight;
            return totalDamage * effectivePercent / 100;
        }

        private static void ApplyEaters(
            Mobile defender, int totalDamage, int phys, int fire, int cold, int pois, int nrgy, int direct = 0
        )
        {
            var healAmount = ComputeEaterHeal(defender, totalDamage, phys, fire, cold, pois, nrgy, direct);
            if (healAmount <= 0)
            {
                return;
            }

            var state = GetState(defender);
            state.PendingEaterHeal += healAmount;
            state.EaterTicksRemaining = 3;

            if (state.EaterTimer != null)
            {
                return;
            }

            state.EaterTimer = Timer.DelayCall(
                TimeSpan.FromSeconds(1.0),
                TimeSpan.FromSeconds(1.0),
                () =>
                {
                    if (defender.Deleted || !defender.Alive || state.PendingEaterHeal <= 0)
                    {
                        state.PendingEaterHeal = 0;
                        state.EaterTicksRemaining = 0;
                        state.EaterTimer?.Stop();
                        state.EaterTimer = null;
                        return;
                    }

                    var ticksLeft = Math.Max(1, state.EaterTicksRemaining);
                    var chunk = ticksLeft == 1
                        ? state.PendingEaterHeal
                        : Math.Max(1, (state.PendingEaterHeal + ticksLeft - 1) / ticksLeft);

                    if (chunk > state.PendingEaterHeal)
                    {
                        chunk = state.PendingEaterHeal;
                    }

                    state.PendingEaterHeal -= chunk;
                    state.EaterTicksRemaining = Math.Max(0, ticksLeft - 1);

                    if (!MortalStrike.IsWounded(defender))
                    {
                        defender.Heal(chunk);
                    }

                    if (state.PendingEaterHeal <= 0 || state.EaterTicksRemaining <= 0)
                    {
                        state.PendingEaterHeal = 0;
                        state.EaterTicksRemaining = 0;
                        state.EaterTimer?.Stop();
                        state.EaterTimer = null;
                    }
                }
            );
        }

        public static int ComputeSoulChargeMana(Mobile defender, int totalDamage)
        {
            if (defender == null || totalDamage <= 0)
            {
                return 0;
            }

            var soulCharge = Math.Min(MaxSoulCharge, AosArmorAttributes.GetValue(defender, AosArmorAttribute.SoulCharge));
            if (soulCharge <= 0)
            {
                return 0;
            }

            return totalDamage * soulCharge / 100;
        }

        private static void ApplySoulCharge(Mobile defender, int totalDamage)
        {
            var manaAmount = ComputeSoulChargeMana(defender, totalDamage);
            if (manaAmount <= 0)
            {
                return;
            }

            var state = GetState(defender);
            state.PendingSoulChargeMana += manaAmount;
            state.SoulChargeTicksRemaining = 3;

            if (state.SoulChargeTimer != null)
            {
                return;
            }

            state.SoulChargeTimer = Timer.DelayCall(
                TimeSpan.FromSeconds(1.0),
                TimeSpan.FromSeconds(1.0),
                () =>
                {
                    if (defender.Deleted || !defender.Alive || state.PendingSoulChargeMana <= 0)
                    {
                        state.PendingSoulChargeMana = 0;
                        state.SoulChargeTicksRemaining = 0;
                        state.SoulChargeTimer?.Stop();
                        state.SoulChargeTimer = null;
                        return;
                    }

                    var ticksLeft = Math.Max(1, state.SoulChargeTicksRemaining);
                    var chunk = ticksLeft == 1
                        ? state.PendingSoulChargeMana
                        : Math.Max(1, (state.PendingSoulChargeMana + ticksLeft - 1) / ticksLeft);

                    if (chunk > state.PendingSoulChargeMana)
                    {
                        chunk = state.PendingSoulChargeMana;
                    }

                    state.PendingSoulChargeMana -= chunk;
                    state.SoulChargeTicksRemaining = Math.Max(0, ticksLeft - 1);
                    defender.Mana = Math.Min(defender.ManaMax, defender.Mana + chunk);

                    if (state.PendingSoulChargeMana <= 0 || state.SoulChargeTicksRemaining <= 0)
                    {
                        state.PendingSoulChargeMana = 0;
                        state.SoulChargeTicksRemaining = 0;
                        state.SoulChargeTimer?.Stop();
                        state.SoulChargeTimer = null;
                    }
                }
            );
        }

        /// <summary>
        /// Checks Casting Focus + matching Elemental Resonance to resist spell disturbance when hurt.
        /// </summary>
        public static bool TryResistSpellDisturb(Mobile caster)
        {
            if (caster == null || caster.Deleted || !caster.Alive)
            {
                return false;
            }

            var chance = GetSpellDisturbResistChance(caster);
            if (chance <= 0)
            {
                return false;
            }

            if (Utility.Random(100) < chance)
            {
                caster.FixedParticles(0x376A, 9, 20, 5013, 0x48F, 0, EffectLayer.Waist);
                return true;
            }

            return false;
        }

        public static int GetSpellDisturbResistChance(Mobile caster)
        {
            var castingFocus = Math.Min(MaxCastingFocus, AosAttributes.GetValue(caster, AosAttribute.CastingFocus));
            var resonanceBonus = 0;

            if (_states.TryGetValue(caster, out var state) &&
                Core.TickCount - state.LastElementTick <= ElementMemoryMs)
            {
                var totalWeight = state.LastPhys + state.LastFire + state.LastCold + state.LastPois + state.LastNrgy;
                if (totalWeight > 0)
                {
                    var weighted = 0;
                    if (state.LastPhys > 0)
                    {
                        weighted += state.LastPhys * Math.Min(MaxResonance, AosArmorAttributes.GetValue(caster, AosArmorAttribute.KineticResonance));
                    }

                    if (state.LastFire > 0)
                    {
                        weighted += state.LastFire * Math.Min(MaxResonance, AosArmorAttributes.GetValue(caster, AosArmorAttribute.FireResonance));
                    }

                    if (state.LastCold > 0)
                    {
                        weighted += state.LastCold * Math.Min(MaxResonance, AosArmorAttributes.GetValue(caster, AosArmorAttribute.ColdResonance));
                    }

                    if (state.LastPois > 0)
                    {
                        weighted += state.LastPois * Math.Min(MaxResonance, AosArmorAttributes.GetValue(caster, AosArmorAttribute.PoisonResonance));
                    }

                    if (state.LastNrgy > 0)
                    {
                        weighted += state.LastNrgy * Math.Min(MaxResonance, AosArmorAttributes.GetValue(caster, AosArmorAttribute.EnergyResonance));
                    }

                    resonanceBonus = weighted / totalWeight;
                }
            }

            return Math.Min(50, castingFocus * 2 + resonanceBonus);
        }

        /// <summary>
        /// Casting Focus grants a chance equal to CastingFocus% (max 12%) to focus a spell for +15% spell damage.
        /// </summary>
        public static int RollCastingFocusDamageBonus(Mobile caster)
        {
            if (caster == null || caster.Deleted)
            {
                return 0;
            }

            var castingFocus = Math.Min(MaxCastingFocus, AosAttributes.GetValue(caster, AosAttribute.CastingFocus));
            if (castingFocus <= 0)
            {
                return 0;
            }

            if (Utility.Random(100) < castingFocus)
            {
                caster.FixedParticles(0x375A, 10, 15, 5017, 0x489, 0, EffectLayer.Waist);
                return 15;
            }

            return 0;
        }

        /// <summary>
        /// Mana Phase grants a 25% chance (20s cooldown) to negate the mana cost of a spell or weapon special move.
        /// </summary>
        public static bool TryTriggerManaPhase(Mobile caster, ref int mana, bool forceRoll = false)
        {
            if (caster == null || caster.Deleted || mana <= 0)
            {
                return false;
            }

            if (AosArmorAttributes.GetValue(caster, AosArmorAttribute.ManaPhase) <= 0)
            {
                return false;
            }

            var state = GetState(caster);
            if (Core.TickCount - state.ManaPhaseReadyTick < 0)
            {
                return false;
            }

            if (!forceRoll && Utility.Random(100) >= 25)
            {
                return false;
            }

            mana = 0;
            state.ManaPhaseReadyTick = Core.TickCount + ManaPhaseCooldownMs;
            caster.FixedParticles(0x376A, 9, 20, 5042, 0x48D, 0, EffectLayer.Waist);
            caster.PlaySound(0x1ED);

            if (caster is PlayerMobile pm)
            {
                pm.AddBuff(new BuffInfo(BuffIcon.ManaPhase, 1116158, 1153816, TimeSpan.FromSeconds(2.0)));
            }

            return true;
        }

        /// <summary>
        /// Reactive Paralyze triggers on a successful melee parry to briefly paralyze the attacker.
        /// </summary>
        public static bool TryReactiveParalyze(Mobile defender, Mobile attacker, bool forceRoll = false)
        {
            if (!Core.AOS || defender == null || attacker == null || defender.Deleted || attacker.Deleted || !attacker.Alive)
            {
                return false;
            }

            if (AosArmorAttributes.GetValue(defender, AosArmorAttribute.ReactiveParalyze) <= 0)
            {
                return false;
            }

            if (attacker.Paralyzed || attacker.Frozen || ParalyzingBlow.IsImmune(attacker) ||
                !defender.InRange(attacker, 2) || !defender.CanBeHarmful(attacker, false))
            {
                return false;
            }

            if (attacker is BaseChampion or Harrower or BaseCreature { Unprovokable: true })
            {
                return false;
            }

            var state = GetState(defender);
            if (Core.TickCount - state.ReactiveParalyzeReadyTick < 0)
            {
                return false;
            }

            if (!forceRoll && Utility.Random(100) >= 30)
            {
                return false;
            }

            state.ReactiveParalyzeReadyTick = Core.TickCount + ReactiveParalyzeCooldownMs;
            attacker.Paralyze(TimeSpan.FromSeconds(attacker.Player ? 2.5 : 4.0));
            ParalyzingBlow.BeginImmunity(attacker, TimeSpan.FromSeconds(8.0));
            attacker.FixedEffect(0x376A, 6, 1);
            attacker.PlaySound(0x204);
            defender.SendLocalizedMessage(1112364); // reactive paralyze
            return true;
        }

        public static void BuildBattleLust(Mobile defender, Mobile attacker)
        {
            if (defender == null || attacker == null || defender == attacker || defender.Deleted)
            {
                return;
            }

            if (AosWeaponAttributes.GetValue(defender, AosWeaponAttribute.BattleLust) <= 0)
            {
                return;
            }

            var state = GetState(defender);
            if (Core.TickCount - state.BattleLustExpiresTick > 0)
            {
                state.BattleLustBonus = 0;
            }

            var cap = attacker.Player ? MaxBattleLustPvP : MaxBattleLustPvM;
            state.BattleLustBonus = Math.Min(cap, state.BattleLustBonus + 15);
            state.BattleLustExpiresTick = Core.TickCount + BattleLustDecayMs;
        }

        public static int GetBattleLustDamageBonus(Mobile attacker, Mobile defender)
        {
            if (attacker == null || attacker.Deleted || AosWeaponAttributes.GetValue(attacker, AosWeaponAttribute.BattleLust) <= 0)
            {
                return 0;
            }

            if (!_states.TryGetValue(attacker, out var state))
            {
                return 0;
            }

            if (Core.TickCount - state.BattleLustExpiresTick > 0)
            {
                state.BattleLustBonus = 0;
                return 0;
            }

            var cap = defender?.Player == true ? MaxBattleLustPvP : MaxBattleLustPvM;
            return Math.Min(cap, state.BattleLustBonus);
        }

        private static int GetActiveWeaponAttribute(BaseWeapon weapon, Mobile attacker, AosWeaponAttribute attribute)
        {
            var value = weapon.WeaponAttributes[attribute];

            if (attacker.FindItemOnLayer(Layer.Helm) is ElvenGlasses { _weaponAttributes: { } glassesAttrs })
            {
                value += glassesAttrs[attribute];
            }

            return value;
        }

        /// <summary>
        /// Executes SA/ToL weapon on-hit procs: Splintering Weapon, Hit Curse, Hit Fatigue, Hit Mana Drain, and Bane.
        /// </summary>
        public static void OnWeaponHitEffects(
            BaseWeapon weapon, Mobile attacker, Mobile defender, int damageGiven, double propertyBonus
        )
        {
            if (weapon == null || attacker == null || defender == null || attacker.Deleted || defender.Deleted ||
                !defender.Alive || damageGiven <= 0)
            {
                return;
            }

            var splinterChance = Math.Min(
                35,
                (int)(GetActiveWeaponAttribute(weapon, attacker, AosWeaponAttribute.SplinteringWeapon) * propertyBonus)
            );
            if (splinterChance > 0 && Utility.Random(100) < splinterChance)
            {
                TriggerSplinteringWeapon(attacker, defender, damageGiven);
                if (defender.Deleted || !defender.Alive)
                {
                    return;
                }
            }

            var curseChance = (int)(GetActiveWeaponAttribute(weapon, attacker, AosWeaponAttribute.HitCurse) * propertyBonus);
            if (curseChance > 0 && Utility.Random(100) < curseChance)
            {
                CurseSpell.DoWeaponCurse(attacker, defender);
            }

            var fatigueChance =
                (int)(GetActiveWeaponAttribute(weapon, attacker, AosWeaponAttribute.HitFatigue) * propertyBonus);
            if (fatigueChance > 0 && Utility.Random(100) < fatigueChance)
            {
                var drain = Math.Max(4, AOS.Scale(damageGiven, 30));
                defender.Stam = Math.Max(0, defender.Stam - drain);
                defender.FixedParticles(0x374A, 10, 15, 5028, 0x3F, 0, EffectLayer.Waist);
                defender.PlaySound(0x1F8);
            }

            var manaDrainChance =
                (int)(GetActiveWeaponAttribute(weapon, attacker, AosWeaponAttribute.HitManaDrain) * propertyBonus);
            if (manaDrainChance > 0 && Utility.Random(100) < manaDrainChance)
            {
                var drain = Math.Max(4, AOS.Scale(damageGiven, 25));
                defender.Mana = Math.Max(0, defender.Mana - drain);
                defender.FixedParticles(0x374A, 10, 15, 5032, 0x48D, 0, EffectLayer.Head);
                defender.PlaySound(0x1F8);
            }

            if (GetActiveWeaponAttribute(weapon, attacker, AosWeaponAttribute.Bane) > 0 &&
                defender.HitsMax > 0 && defender.Hits * 2 < defender.HitsMax)
            {
                TryTriggerBane(attacker, defender, damageGiven);
            }
        }

        public static bool TriggerSplinteringWeapon(Mobile attacker, Mobile defender, int damageGiven)
        {
            var state = GetState(defender);
            if (Core.TickCount - state.SplinteringReadyTick < 0)
            {
                return false;
            }

            state.SplinteringReadyTick = Core.TickCount + SplinteringCooldownMs;
            if (!BleedAttack.IsBleedImmune(defender))
            {
                BleedAttack.BeginBleed(defender, attacker);
            }

            var bonus = Math.Max(5, AOS.Scale(damageGiven, 15));
            defender.Damage(bonus, attacker);
            defender.FixedParticles(0x377A, 244, 25, 9950, 31, 0, EffectLayer.Waist);
            defender.PlaySound(0x133);

            if (defender is PlayerMobile pm)
            {
                pm.AddBuff(new BuffInfo(BuffIcon.SplinteringEffect, 1154670, 1153818, TimeSpan.FromSeconds(4.0)));
            }

            return true;
        }

        public static int TryTriggerBane(Mobile attacker, Mobile defender, int damageGiven, bool forceRoll = false)
        {
            if (defender == null || defender.Deleted || !defender.Alive || defender.HitsMax <= 0)
            {
                return 0;
            }

            if (defender.Hits * 2 >= defender.HitsMax)
            {
                return 0;
            }

            var state = GetState(defender);
            if (Core.TickCount - state.BaneReadyTick < 0)
            {
                return 0;
            }

            if (!forceRoll && Utility.Random(100) >= 25)
            {
                return 0;
            }

            state.BaneReadyTick = Core.TickCount + BaneCooldownMs;
            var missingRatio = Math.Clamp(1.0 - (double)defender.Hits / defender.HitsMax, 0.5, 1.0);
            var baneDamage = Math.Clamp((int)(damageGiven * 0.40 * missingRatio) + 12, 12, 350);

            if (attacker is { Player: true } && defender.Player && baneDamage > 35)
            {
                baneDamage = 35;
            }

            defender.FixedParticles(0x374A, 10, 15, 5021, 0x455, 0, EffectLayer.Waist);
            defender.PlaySound(0x1FB);
            defender.Damage(baneDamage, attacker);
            return baneDamage;
        }

        /// <summary>
        /// Blood Drinker heals the attacker for 100% of bleed tick damage while wielding a Blood Drinker weapon.
        /// </summary>
        public static void OnBleedTick(Mobile victim, Mobile attacker, int bleedDamage)
        {
            if (!Core.AOS || attacker == null || attacker.Deleted || !attacker.Alive || bleedDamage <= 0)
            {
                return;
            }

            if (MortalStrike.IsWounded(attacker))
            {
                return;
            }

            if (AosWeaponAttributes.GetValue(attacker, AosWeaponAttribute.BloodDrinker) <= 0)
            {
                return;
            }

            attacker.Heal(bleedDamage);
            attacker.FixedParticles(0x376A, 9, 16, 5005, 0x21, 0, EffectLayer.Waist);
        }
    }
}
