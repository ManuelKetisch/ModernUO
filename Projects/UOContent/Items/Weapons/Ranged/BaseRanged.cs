using System;
using ModernUO.Serialization;
using Server.Mobiles;
using Server.Network;
using Server.Spells;
using Server.Engines.Craft;
using Server.Engines.Imbuing;
using Server.Engines.WeaponLeveling;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public abstract partial class BaseRanged : BaseMeleeWeapon
    {
        [SerializableField(0)]
        [InvalidateProperties]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private bool _balanced;

        [SerializableField(1)]
        [InvalidateProperties]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _velocity;

        public BaseRanged(int itemID) : base(itemID)
        {
        }

        public abstract int EffectID { get; }
        public abstract Type AmmoType { get; }
        public abstract Item Ammo { get; }

        public override int DefHitSound => 0x234;
        public override int DefMissSound => 0x238;

        public override SkillName DefSkill => SkillName.Archery;
        public override WeaponType DefType => WeaponType.Ranged;
        public override WeaponAnimation DefAnimation => WeaponAnimation.ShootXBow;

        public override SkillName AccuracySkill => SkillName.Archery;

        public override TimeSpan OnSwing(Mobile attacker, Mobile defender, double damageBonus = 1.0)
        {
            // WeaponAbility a = WeaponAbility.GetCurrentAbility( attacker );

            // Make sure we've been standing still for .25/.5/1 second depending on Era
            if (Core.TickCount - attacker.LastMoveTime >= (Core.SE ? 250 : Core.AOS ? 500 : 1000) ||
                Core.AOS && WeaponAbility.GetCurrentAbility(attacker) is MovingShot)
            {
                var canSwing = true;

                if (Core.AOS)
                {
                    canSwing = !attacker.Paralyzed && !attacker.Frozen;

                    if (canSwing)
                    {
                        // On OSI you can swing + hold a cast at the same time
                        canSwing = attacker.Spell is not Spell { IsCasting: true, BlocksMovement: true };
                    }
                }

                if ((attacker as PlayerMobile)?.DuelContext?.CheckItemEquip(attacker, this) == false)
                {
                    canSwing = false;
                }

                if (canSwing && attacker.HarmfulCheck(defender))
                {
                    attacker.DisruptiveAction();
                    attacker.NetState.SendSwing(attacker.Serial, defender.Serial);

                    if (attacker is BaseCreature bc && bc.TriggerAbility(MonsterAbilityTrigger.CombatAction, defender))
                    {
                        return GetDelay(attacker);
                    }

                    if (AmmoType == typeof(Arrow) && DippedArrow.TryFire(attacker, out var dipped))
                    {
                        attacker.MovingParticles(defender, EffectID, 18, 1, false, false, 0, 0, 0, 0, 0, EffectLayer.RightHand, 0);
                        if (CheckHit(attacker, defender))
                        {
                            ResolveHit(attacker, defender, 1, false);
                            DippedArrowEffects.Apply(this, attacker, defender, dipped);
                        }
                        else
                        {
                            base.OnMiss(attacker, defender);
                        }
                    }
                    else if (OnFired(attacker, defender))
                    {
                        if (CheckHit(attacker, defender))
                        {
                            OnHit(attacker, defender);
                        }
                        else
                        {
                            OnMiss(attacker, defender);
                        }
                    }
                    else if (WeaponAbility.GetCurrentAbility(attacker) is LightningArrow lightningArrow)
                    {
                        // Lightning Arrow doesn't require ammunition
                        attacker.MovingParticles(defender, EffectID, 18, 1, false, false, 0, 0, 0, 0, 0, EffectLayer.RightHand, 0);
                        using (WeaponLevelingCombat.BeginWeaponAbility(this))
                        {
                            lightningArrow.OnHit(attacker, defender, 0, new WorldLocation(defender));
                        }
                    }
                }

                attacker.RevealingAction();

                return GetDelay(attacker);
            }

            attacker.RevealingAction();

            return TimeSpan.FromSeconds(0.25);
        }

        public override void OnHit(Mobile attacker, Mobile defender, double damageBonus = 1) =>
            ResolveHit(attacker, defender, damageBonus, true);

        private void ResolveHit(Mobile attacker, Mobile defender, double damageBonus, bool recoverAmmo)
        {
            if (recoverAmmo && attacker.Player && !defender.Player && (defender.Body.IsAnimal || defender.Body.IsMonster) &&
                Utility.RandomDouble() < 0.4 && Ammo is { } ammo && !defender.AddToBackpack(ammo))
            {
                ammo.Delete();
            }

            if (Core.ML && _velocity > 0)
            {
                var bonus = (int)attacker.GetDistanceToSqrt(defender);

                if (bonus > 0 && _velocity > Utility.Random(100))
                {
                    using (WeaponLevelingCombat.BeginWeaponHit(this, attacker, defender as BaseCreature))
                    {
                        AOS.Damage(defender, attacker, bonus * 3, 100, 0, 0, 0, 0);
                    }

                    if (attacker.Player)
                    {
                        if (this is BaseThrown)
                        {
                            attacker.SendMessage("Your throw hits its mark with velocity!");
                        }
                        else
                        {
                            attacker.SendLocalizedMessage(1072794); // Your arrow hits its mark with velocity!
                        }
                    }

                    if (defender.Player)
                    {
                        if (this is BaseThrown)
                        {
                            defender.SendMessage("You have been hit by a thrown weapon with velocity!");
                        }
                        else
                        {
                            defender.SendLocalizedMessage(1072795); // You have been hit by an arrow with velocity!
                        }
                    }
                }
            }

            base.OnHit(attacker, defender, damageBonus);
        }

        public override void OnMiss(Mobile attacker, Mobile defender)
        {
            if (attacker.Player && Utility.RandomDouble() < 0.4)
            {
                if (Core.SE)
                {
                    if (attacker is PlayerMobile pm)
                    {
                        // Bank the spent ammo; it is gathered back up once the archer disengages.
                        AmmoRecovery.Bank(pm, AmmoType);
                    }
                }
                else
                {
                    Ammo?.MoveToWorld(
                        new Point3D(
                            defender.X + Utility.RandomMinMax(-1, 1),
                            defender.Y + Utility.RandomMinMax(-1, 1),
                            defender.Z
                        ),
                        defender.Map
                    );
                }
            }

            base.OnMiss(attacker, defender);
        }

        public virtual bool OnFired(Mobile attacker, Mobile defender)
        {
            if (attacker.Player)
            {
                var quiver = attacker.FindItemOnLayer<BaseQuiver>(Layer.Cloak);
                var pack = attacker.Backpack;

                if (quiver == null || Utility.Random(100) >= quiver.LowerAmmoCost)
                {
                    // consume ammo
                    if (quiver?.ConsumeTotal(AmmoType) == true)
                    {
                        quiver.InvalidateWeight();
                    }
                    else if (pack?.ConsumeTotal(AmmoType) != true)
                    {
                        return false;
                    }
                }
                else if (quiver.FindItemByType(AmmoType) == null && pack?.FindItemByType(AmmoType) == null)
                {
                    // lower ammo cost should not work when we have no ammo at all
                    return false;
                }
            }

            attacker.MovingParticles(defender, EffectID, 18, 1, false, false, 0, 0, 0, 0, 0, EffectLayer.RightHand, 0);

            return true;
        }

        public override int OnCraft(
            int quality,
            bool makersMark,
            Mobile from,
            CraftSystem craftSystem,
            Type typeRes,
            BaseTool tool,
            CraftItem craftItem,
            int resHue
        )
        {
            var result = base.OnCraft(quality, makersMark, from, craftSystem, typeRes, tool, craftItem, resHue);
            if (Core.ML)
            {
                ApplyWoodBonuses(typeRes ?? craftItem.Resources[0].ItemType);
            }

            return result;
        }

        private void ApplyWoodBonuses(Type resource)
        {
            var craftResource = CraftResources.GetFromType(resource);

            // Inherent Luck, LowerRequirements, Durability, and ColdDamage are provided by ResourceInfo.
            // Clamp additive combat bonuses to standard crafting caps and respect the 5-property ceiling.
            switch (craftResource)
            {
                case CraftResource.OakWood:
                    {
                        AddClampedWeaponAttr(AosAttribute.WeaponDamage, 5, 50);
                        break;
                    }
                case CraftResource.AshWood:
                    {
                        AddClampedWeaponAttr(AosAttribute.WeaponSpeed, 10, 30);
                        break;
                    }
                case CraftResource.YewWood:
                    {
                        AddClampedWeaponAttr(AosAttribute.AttackChance, 5, 15);
                        AddClampedWeaponAttr(AosAttribute.WeaponDamage, 10, 50);
                        break;
                    }
                case CraftResource.Bloodwood:
                    {
                        AddClampedWeaponAttr(AosAttribute.RegenHits, 2, 2);
                        AddClampedHitAttr(AosWeaponAttribute.HitLeechHits, 16, 50);
                        break;
                    }
                case CraftResource.Heartwood:
                    {
                        ApplyHeartwoodBonus();
                        break;
                    }
                case CraftResource.Frostwood:
                    {
                        AddClampedWeaponAttr(AosAttribute.WeaponDamage, 12, 50);
                        break;
                    }
            }
        }

        private void ApplyHeartwoodBonus()
        {
            switch (Utility.Random(4))
            {
                case 0:
                    {
                        AddClampedWeaponAttr(AosAttribute.WeaponSpeed, 10, 30);
                        break;
                    }
                case 1:
                    {
                        AddClampedWeaponAttr(AosAttribute.AttackChance, 5, 15);
                        break;
                    }
                case 2:
                    {
                        AddClampedHitAttr(AosWeaponAttribute.HitLeechHits, 10, 50);
                        break;
                    }
                default:
                    {
                        AddClampedWeaponAttr(AosAttribute.WeaponDamage, 10, 50);
                        break;
                    }
            }
        }

        private void AddClampedWeaponAttr(AosAttribute attr, int bonus, int cap)
        {
            var current = Attributes[attr];
            if (current <= 0 && ImbuingCatalog.CountProperties(this) >= ImbuingCatalog.MaxProperties)
            {
                return;
            }

            if (current < cap)
            {
                var next = Math.Min(cap, current + bonus);
                var mod = ImbuingCatalog.FindMod(attr);
                if (Quality == WeaponQuality.Exceptional && mod != null &&
                    ImbuingCatalog.ComputeProjectedWeight(this, mod, next) > 535)
                {
                    return;
                }

                Attributes[attr] = next;
            }
        }

        private void AddClampedHitAttr(AosWeaponAttribute attr, int bonus, int cap)
        {
            var current = WeaponAttributes[attr];
            if (current <= 0 && ImbuingCatalog.CountProperties(this) >= ImbuingCatalog.MaxProperties)
            {
                return;
            }

            if (current < cap)
            {
                var next = Math.Min(cap, current + bonus);
                var mod = ImbuingCatalog.FindMod(attr);
                if (Quality == WeaponQuality.Exceptional && mod != null &&
                    ImbuingCatalog.ComputeProjectedWeight(this, mod, next) > 535)
                {
                    return;
                }

                WeaponAttributes[attr] = next;
            }
        }
    }
}
