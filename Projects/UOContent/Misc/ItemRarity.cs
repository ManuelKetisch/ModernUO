using System;
using System.Collections.Generic;
using Server.Engines.Stealables;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Mobiles;

namespace Server
{
    public enum MagicItemRarity
    {
        None,
        MinorMagicItem,
        LesserMagicItem,
        GreaterMagicItem,
        MajorMagicItem,
        LesserArtifact,
        GreaterArtifact,
        MajorArtifact,
        LegendaryArtifact
    }

    public static class ItemRarity
    {
        private const int BaseRarityCliloc = 1151487; // 1151488 = Minor Magic Item .. 1151495 = Legendary Artifact

        private static HashSet<Type> _fixedArtifactTypes;

        private static HashSet<Type> FixedArtifactTypes => _fixedArtifactTypes ??= BuildFixedArtifactTypes();

        private static HashSet<Type> BuildFixedArtifactTypes()
        {
            var set = new HashSet<Type>
            {
                // Artifact spellbooks
                typeof(OssianGrimoire),
                typeof(TomeOfLostKnowledge),
                typeof(TomeOfEnlightenment),
                typeof(HallowedSpellbook),

                // Champion replicas (ArtifactRarity == 0)
                typeof(ANecromancerShroud),
                typeof(BraveKnightOfTheBritannia),
                typeof(CaptainJohnsHat),
                typeof(DetectiveBoots),
                typeof(DjinnisRing),
                typeof(EmbroideredOakLeafCloak),
                typeof(GuantletsOfAnger),
                typeof(LieutenantOfTheBritannianRoyalGuard),
                typeof(OblivionsNeedle),
                typeof(RoyalGuardSurvivalKnife),
                typeof(SamaritanRobe),
                typeof(TheMostKnowledgePerson),
                typeof(TheRobeOfBritanniaAri),
                typeof(AcidProofRobe),
                typeof(Calm),
                typeof(CrownOfTalKeesh),
                typeof(FangOfRactus),
                typeof(GladiatorsCollar),
                typeof(OrcChieftainHelm),
                typeof(Pacify),
                typeof(Quell),
                typeof(ShroudOfDeciet),
                typeof(Subdue),

                // Craftable / ML / SA / Heritage named artifacts (ArtifactRarity == 0)
                typeof(MelisandesCorrodedHatchet),
                typeof(BlightGrippedLongbow),
                typeof(ColdForgedBlade),
                typeof(FaerieFire),
                typeof(LuminousRuneBlade),
                typeof(MischiefMaker),
                typeof(OverseerSunderedBlade),
                typeof(PhantomStaff),
                typeof(RuneCarvingKnife),
                typeof(ShardThrasher),
                typeof(SilvanisFeywoodBow),
                typeof(TheNightReaper),
                typeof(BrambleCoat),
                typeof(IronwoodCrown),
                typeof(SongWovenMantle),
                typeof(SpellWovenBritches),
                typeof(StitchersMittens),
                typeof(CrimsonCincture),
                typeof(EssenceOfBattle),
                typeof(PendantOfTheMagi),
                typeof(ResilientBracer),
                typeof(AbyssReaver),
                typeof(QuiverOfInfinity),
                typeof(ChargerOfTheFallen),
                typeof(SamuraiHelm),
                typeof(HolySword),
                typeof(LeggingsOfEmbers),
                typeof(ShaminoCrossbow)
            };

            AddTypes(set, Paragon.Artifacts);
            AddTypes(set, DemonKnight.ArtifactRarity10);
            AddTypes(set, DemonKnight.ArtifactRarity11);
            AddTypes(set, TreasuresOfTokuno.LesserArtifactsTotal);
            AddTypes(set, TreasuresOfTokuno.TokunoDyable);
            AddTypes(set, MondainsLegacy.Artifacts);
            AddTypes(set, Leviathan.Artifacts);
            AddTypes(set, TreasureMapChest.Artifacts);
            AddTypes(set, StealableArtifacts.TypesOfEntries);

            var totRewards = ToTRedeemGump.NormalRewards;
            for (var era = 0; era < totRewards.Length; ++era)
            {
                var eraRewards = totRewards[era];
                for (var i = 0; i < eraRewards.Length; ++i)
                {
                    if (eraRewards[i].Type != null)
                    {
                        set.Add(eraRewards[i].Type);
                    }
                }
            }

            return set;
        }

        private static void AddTypes(HashSet<Type> set, Type[] types)
        {
            if (types == null)
            {
                return;
            }

            for (var i = 0; i < types.Length; ++i)
            {
                if (types[i] != null)
                {
                    set.Add(types[i]);
                }
            }
        }

        public static bool IsFixedArtifact(Item item)
        {
            if (item == null)
            {
                return false;
            }

            if (item is BaseWeapon weapon)
            {
                if (weapon.ArtifactRarity > 0 || weapon is { InitMinHits: 255, InitMaxHits: 255 })
                {
                    return true;
                }
            }
            else if (item is BaseArmor armor)
            {
                if (armor.ArtifactRarity > 0 || armor is { InitMinHits: 255, InitMaxHits: 255 })
                {
                    return true;
                }
            }
            else if (item is BaseClothing clothing)
            {
                if (clothing.ArtifactRarity > 0 || clothing is { InitMinHits: 255, InitMaxHits: 255 })
                {
                    return true;
                }
            }
            else if (item is BaseJewel jewel)
            {
                if (jewel.ArtifactRarity > 0)
                {
                    return true;
                }
            }

            return FixedArtifactTypes.Contains(item.GetType());
        }

        public static int ComputeItemWeight(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            if (item is BaseWeapon weapon)
            {
                var weight = ComputeAosAttributesWeight(weapon.Attributes, isJewel: false);
                weight += ComputeWeaponAttributesWeight(weapon.WeaponAttributes);
                weight += ComputeSkillBonusesWeight(weapon.SkillBonuses);
                weight += GetSlayerWeight(weapon.Slayer);
                weight += GetSlayerWeight(weapon.Slayer2);
                weight += ComputeElementalDamageWeight(weapon.AosElementDamages);

                if (weapon is BaseRanged ranged)
                {
                    if (ranged.Balanced)
                    {
                        weight += 150;
                    }

                    weight += ScaleWeight(ranged.Velocity, 50, 130);
                }

                return weight;
            }

            if (item is BaseArmor armor)
            {
                var totalResistBonus = Math.Max(0, armor.PhysicalBonus) +
                    Math.Max(0, armor.FireBonus) +
                    Math.Max(0, armor.ColdBonus) +
                    Math.Max(0, armor.PoisonBonus) +
                    Math.Max(0, armor.EnergyBonus);

                // Plain exceptional armor with only standard crafted resist distribution is non-magical.
                if (armor.Quality == ArmorQuality.Exceptional &&
                    (armor.Attributes == null || armor.Attributes.IsEmpty) &&
                    (armor.ArmorAttributes == null || armor.ArmorAttributes.IsEmpty) &&
                    (armor.SkillBonuses == null || armor.SkillBonuses.IsEmpty) &&
                    totalResistBonus <= 20)
                {
                    return 0;
                }

                var weight = ComputeAosAttributesWeight(armor.Attributes, isJewel: false);
                weight += ComputeArmorAttributesWeight(armor.ArmorAttributes);
                weight += ComputeSkillBonusesWeight(armor.SkillBonuses);
                weight += ScaleWeight(armor.PhysicalBonus, 15, 100);
                weight += ScaleWeight(armor.FireBonus, 15, 100);
                weight += ScaleWeight(armor.ColdBonus, 15, 100);
                weight += ScaleWeight(armor.PoisonBonus, 15, 100);
                weight += ScaleWeight(armor.EnergyBonus, 15, 100);

                return weight;
            }

            if (item is BaseJewel jewel)
            {
                var weight = ComputeAosAttributesWeight(jewel.Attributes, isJewel: true);
                weight += ComputeElementResistWeight(jewel.Resistances);
                weight += ComputeSkillBonusesWeight(jewel.SkillBonuses);

                return weight;
            }

            if (item is BaseClothing clothing)
            {
                var resists = clothing.Resistances;
                var totalResistBonus = resists == null || resists.IsEmpty
                    ? 0
                    : Math.Max(0, resists.Physical) +
                      Math.Max(0, resists.Fire) +
                      Math.Max(0, resists.Cold) +
                      Math.Max(0, resists.Poison) +
                      Math.Max(0, resists.Energy);

                if (clothing.Quality == ClothingQuality.Exceptional &&
                    (clothing.Attributes == null || clothing.Attributes.IsEmpty) &&
                    (clothing.ClothingAttributes == null || clothing.ClothingAttributes.IsEmpty) &&
                    (clothing.SkillBonuses == null || clothing.SkillBonuses.IsEmpty) &&
                    totalResistBonus <= 20)
                {
                    return 0;
                }

                var weight = ComputeAosAttributesWeight(clothing.Attributes, isJewel: false);
                weight += ComputeArmorAttributesWeight(clothing.ClothingAttributes);
                weight += ComputeElementResistWeight(resists);
                weight += ComputeSkillBonusesWeight(clothing.SkillBonuses);

                return weight;
            }

            if (item is Spellbook spellbook)
            {
                var weight = ComputeAosAttributesWeight(spellbook.Attributes, isJewel: false);
                weight += ComputeSkillBonusesWeight(spellbook.SkillBonuses);
                weight += GetSlayerWeight(spellbook.Slayer);
                weight += GetSlayerWeight(spellbook.Slayer2);

                return weight;
            }

            if (item is BaseTalisman talisman)
            {
                var weight = ComputeAosAttributesWeight(talisman.Attributes, isJewel: false);
                weight += ComputeSkillBonusesWeight(talisman.SkillBonuses);

                if (talisman.Slayer != TalismanSlayerName.None)
                {
                    weight += 120;
                }

                if (talisman.Protection is { IsEmpty: false })
                {
                    weight += talisman.Protection.Amount > 0 ? ScaleWeight(talisman.Protection.Amount, 60, 100) : 60;
                }

                if (talisman.Killer is { IsEmpty: false })
                {
                    weight += talisman.Killer.Amount > 0 ? ScaleWeight(talisman.Killer.Amount, 60, 100) : 60;
                }

                if (talisman.Summoner is { IsEmpty: false })
                {
                    weight += 100;
                }

                if (talisman.Removal != TalismanRemoval.None)
                {
                    weight += 80;
                }

                weight += ScaleWeight(talisman.SuccessBonus, 30, 100);
                weight += ScaleWeight(talisman.ExceptionalBonus, 30, 100);

                return weight;
            }

            if (item is BaseQuiver quiver)
            {
                var weight = ComputeAosAttributesWeight(quiver.Attributes, isJewel: false);
                weight += ScaleWeight(quiver.LowerAmmoCost, 30, 100);

                // BaseQuiver initializes DamageIncrease to 10; count only bonus damage modifier above default.
                if (quiver.DamageIncrease > 10)
                {
                    weight += ScaleWeight(quiver.DamageIncrease - 10, 20, 100);
                }

                return weight;
            }

            return 0;
        }

        public static MagicItemRarity GetRarity(Item item)
        {
            var weight = ComputeItemWeight(item);

            if (weight <= 0)
            {
                return MagicItemRarity.None;
            }

            return weight switch
            {
                < 150 => MagicItemRarity.MinorMagicItem,
                < 250 => MagicItemRarity.LesserMagicItem,
                < 350 => MagicItemRarity.GreaterMagicItem,
                < 450 => MagicItemRarity.MajorMagicItem,
                < 550 => MagicItemRarity.LesserArtifact,
                < 650 => MagicItemRarity.GreaterArtifact,
                < 750 => MagicItemRarity.MajorArtifact,
                _     => MagicItemRarity.LegendaryArtifact
            };
        }

        public static void AddRarityProperty(Item item, IPropertyList list)
        {
            if (!Core.AOS || item == null || list == null || IsFixedArtifact(item))
            {
                return;
            }

            var rarity = GetRarity(item);

            if (rarity != MagicItemRarity.None)
            {
                list.Add(BaseRarityCliloc + (int)rarity);
            }
        }

        private static int ComputeAosAttributesWeight(AosAttributes attrs, bool isJewel)
        {
            if (attrs == null || attrs.IsEmpty)
            {
                return 0;
            }

            var weight = 0;

            weight += ScaleWeight(attrs.RegenHits, 2, 100);
            weight += ScaleWeight(attrs.RegenStam, 3, 100);
            weight += ScaleWeight(attrs.RegenMana, 2, 100);
            weight += ScaleWeight(attrs.DefendChance, 15, 130);
            weight += ScaleWeight(attrs.AttackChance, 15, 130);
            weight += ScaleWeight(attrs.BonusStr, 8, 110);
            weight += ScaleWeight(attrs.BonusDex, 8, 110);
            weight += ScaleWeight(attrs.BonusInt, 8, 110);
            weight += ScaleWeight(attrs.BonusHits, 5, 110);
            weight += ScaleWeight(attrs.BonusStam, 8, 110);
            weight += ScaleWeight(attrs.BonusMana, 8, 110);
            weight += ScaleWeight(attrs.WeaponDamage, isJewel ? 25 : 50, 100);
            weight += ScaleWeight(attrs.WeaponSpeed, 30, 110);
            weight += ScaleWeight(attrs.SpellDamage, 12, 100);
            weight += ScaleWeight(attrs.CastRecovery, 3, 120);
            weight += ScaleWeight(attrs.CastSpeed, 1, 140);
            weight += ScaleWeight(attrs.LowerManaCost, 8, 110);
            weight += ScaleWeight(attrs.LowerRegCost, 20, 100);
            weight += ScaleWeight(attrs.ReflectPhysical, 15, 100);
            weight += ScaleWeight(attrs.EnhancePotions, 25, 100);
            weight += ScaleWeight(attrs.Luck, 100, 100);

            if (attrs.SpellChanneling > 0)
            {
                weight += 100;
            }

            if (attrs.NightSight > 0)
            {
                weight += 50;
            }

            weight += ScaleWeight(attrs.CastingFocus, isJewel ? 4 : 6, 120);
            weight += ScaleWeight(attrs.DamageEater, isJewel ? 4 : 6, 140);
            weight += ScaleWeight(attrs.KineticEater, 15, 115);
            weight += ScaleWeight(attrs.FireEater, 15, 115);
            weight += ScaleWeight(attrs.ColdEater, 15, 115);
            weight += ScaleWeight(attrs.PoisonEater, 15, 115);
            weight += ScaleWeight(attrs.EnergyEater, 15, 115);

            return weight;
        }

        private static int ComputeWeaponAttributesWeight(AosWeaponAttributes attrs)
        {
            if (attrs == null || attrs.IsEmpty)
            {
                return 0;
            }

            var weight = 0;

            weight += ScaleWeight(attrs.LowerStatReq, 100, 100);
            weight += ScaleWeight(attrs.SelfRepair, 5, 100);
            weight += ScaleWeight(attrs.HitLeechHits, 50, 110);
            weight += ScaleWeight(attrs.HitLeechStam, 50, 100);
            weight += ScaleWeight(attrs.HitLeechMana, 50, 110);
            weight += ScaleWeight(attrs.HitLowerAttack, 50, 110);
            weight += ScaleWeight(attrs.HitLowerDefend, 50, 130);
            weight += ScaleWeight(attrs.HitMagicArrow, 50, 120);
            weight += ScaleWeight(attrs.HitHarm, 50, 110);
            weight += ScaleWeight(attrs.HitFireball, 50, 140);
            weight += ScaleWeight(attrs.HitLightning, 50, 140);
            weight += ScaleWeight(attrs.HitDispel, 50, 100);
            weight += ScaleWeight(attrs.HitColdArea, 50, 100);
            weight += ScaleWeight(attrs.HitFireArea, 50, 100);
            weight += ScaleWeight(attrs.HitPoisonArea, 50, 100);
            weight += ScaleWeight(attrs.HitEnergyArea, 50, 100);
            weight += ScaleWeight(attrs.HitPhysicalArea, 50, 100);
            weight += ScaleWeight(attrs.ResistPhysicalBonus, 15, 100);
            weight += ScaleWeight(attrs.ResistFireBonus, 15, 100);
            weight += ScaleWeight(attrs.ResistColdBonus, 15, 100);
            weight += ScaleWeight(attrs.ResistPoisonBonus, 15, 100);
            weight += ScaleWeight(attrs.ResistEnergyBonus, 15, 100);

            if (attrs.UseBestSkill > 0)
            {
                weight += 140;
            }

            weight += ScaleWeight(attrs.MageWeapon, 10, 100);
            weight += ScaleWeight(attrs.DurabilityBonus, 100, 50);

            if (attrs.BattleLust > 0)
            {
                weight += 130;
            }

            weight += ScaleWeight(attrs.SplinteringWeapon, 30, 140);
            weight += ScaleWeight(attrs.HitCurse, 50, 130);
            weight += ScaleWeight(attrs.HitFatigue, 50, 115);
            weight += ScaleWeight(attrs.HitManaDrain, 50, 120);

            if (attrs.BloodDrinker > 0)
            {
                weight += 125;
            }

            if (attrs.Bane > 0)
            {
                weight += 140;
            }

            return weight;
        }

        private static int ComputeElementalDamageWeight(AosElementAttributes damages)
        {
            if (damages == null || damages.IsEmpty)
            {
                return 0;
            }

            var elemental = Math.Max(0, damages.Fire) +
                Math.Max(0, damages.Cold) +
                Math.Max(0, damages.Poison) +
                Math.Max(0, damages.Energy) +
                Math.Max(0, damages.Chaos) +
                Math.Max(0, damages.Direct);

            if (elemental <= 0)
            {
                return 0;
            }

            return ScaleWeight(Math.Min(elemental, 100), 100, 100);
        }

        private static int ComputeArmorAttributesWeight(AosArmorAttributes attrs)
        {
            if (attrs == null || attrs.IsEmpty)
            {
                return 0;
            }

            var weight = 0;

            weight += ScaleWeight(attrs.LowerStatReq, 100, 100);
            weight += ScaleWeight(attrs.SelfRepair, 5, 100);

            if (attrs.MageArmor > 0)
            {
                weight += 100;
            }

            weight += ScaleWeight(attrs.DurabilityBonus, 100, 50);

            if (attrs.ReactiveParalyze > 0)
            {
                weight += 135;
            }

            weight += ScaleWeight(attrs.SoulCharge, 25, 125);

            if (attrs.ManaPhase > 0)
            {
                weight += 140;
            }

            weight += ScaleWeight(attrs.KineticResonance, 20, 110);
            weight += ScaleWeight(attrs.FireResonance, 20, 110);
            weight += ScaleWeight(attrs.ColdResonance, 20, 110);
            weight += ScaleWeight(attrs.PoisonResonance, 20, 110);
            weight += ScaleWeight(attrs.EnergyResonance, 20, 110);

            return weight;
        }

        private static int ComputeElementResistWeight(AosElementAttributes resists)
        {
            if (resists == null || resists.IsEmpty)
            {
                return 0;
            }

            var weight = 0;

            weight += ScaleWeight(resists.Physical, 15, 100);
            weight += ScaleWeight(resists.Fire, 15, 100);
            weight += ScaleWeight(resists.Cold, 15, 100);
            weight += ScaleWeight(resists.Poison, 15, 100);
            weight += ScaleWeight(resists.Energy, 15, 100);

            return weight;
        }

        private static int ComputeSkillBonusesWeight(AosSkillBonuses skills)
        {
            if (skills == null || skills.IsEmpty)
            {
                return 0;
            }

            var weight = 0;

            for (var i = 0; i < 5; ++i)
            {
                if (skills.GetValues(i, out _, out var bonus) && bonus > 0)
                {
                    weight += ScaleWeight((int)Math.Round(bonus), 15, 140);
                }
            }

            return weight;
        }

        private static int GetSlayerWeight(SlayerName slayer) => slayer != SlayerName.None ? 120 : 0;

        private static int ScaleWeight(int value, int standardCap, int maxWeight)
        {
            if (value <= 0 || standardCap <= 0)
            {
                return 0;
            }

            return Math.Max(1, (int)Math.Round(value * (double)maxWeight / standardCap));
        }
    }
}
