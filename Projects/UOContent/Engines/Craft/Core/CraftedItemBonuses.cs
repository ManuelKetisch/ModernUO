using System;
using Server.Engines.Imbuing;
using Server.Items;

namespace Server.Engines.Craft
{
    public static class CraftedItemBonuses
    {
        private const int MaxMasterworkWeight = 515;

        public static int GetSkillTier(double skillValue) =>
            skillValue switch
            {
                >= 120.0 => 5,
                >= 110.0 => 4,
                >= 100.0 => 3,
                >= 85.0  => 2,
                >= 70.0  => 1,
                _        => 0
            };

        public static int GetMaterialTier(CraftResource resource) =>
            resource switch
            {
                CraftResource.None or CraftResource.Iron or CraftResource.RegularLeather or CraftResource.RegularWood => 0,

                CraftResource.DullCopper or CraftResource.ShadowIron or CraftResource.ExcelsiorIron or
                    CraftResource.ExcelsiorDullCopper or CraftResource.SpinedLeather or
                    CraftResource.ExcelsiorRegularLeather or CraftResource.ExcelsiorSpinedLeather or
                    CraftResource.OakWood or CraftResource.PineWood or CraftResource.MahoganyWood or
                    CraftResource.ExcelsiorAshWood or CraftResource.RedScales or CraftResource.YellowScales => 1,

                CraftResource.Copper or CraftResource.Bronze or CraftResource.ExcelsiorShadowIron or
                    CraftResource.ExcelsiorCopper or CraftResource.HornedLeather or
                    CraftResource.ExcelsiorHornedLeather or CraftResource.AshWood or CraftResource.YewWood or
                    CraftResource.ZircoteWood or CraftResource.ExcelsiorYewWood or
                    CraftResource.GreenScales or CraftResource.WhiteScales => 2,

                CraftResource.Gold or CraftResource.Agapite or CraftResource.ExcelsiorBronze or
                    CraftResource.ExcelsiorGold or CraftResource.BarbedLeather or CraftResource.PolarLeather or
                    CraftResource.SyntheticLeather or CraftResource.ExcelsiorBarbedLeather or
                    CraftResource.Heartwood or CraftResource.EbonyWood or CraftResource.BambooWood or
                    CraftResource.ExcelsiorOakWood or CraftResource.BlackScales or CraftResource.BlueScales => 3,

                CraftResource.Verite or CraftResource.Valorite or CraftResource.Blaze or CraftResource.Ice or
                    CraftResource.ExcelsiorAgapite or CraftResource.ExcelsiorVerite or
                    CraftResource.BlazeLeather or CraftResource.DaemonicLeather or CraftResource.ShadowLeather or
                    CraftResource.Bloodwood or CraftResource.Frostwood or CraftResource.PurpleHeartWood or
                    CraftResource.Redwood => 4,

                CraftResource.Toxic or CraftResource.Electrum or CraftResource.Platinum or
                    CraftResource.ExcelsiorValorite or CraftResource.FrostLeather or
                    CraftResource.EtherealLeather or CraftResource.PetrifiedWood => 5,

                _ => 0
            };

        public static void ApplyWeaponBonuses(BaseWeapon weapon, Mobile crafter, CraftSystem craftSystem, BaseTool tool)
        {
            if (!Core.AOS || weapon == null || crafter == null)
            {
                return;
            }

            if (weapon.Quality != WeaponQuality.Exceptional)
            {
                if (Core.ML && weapon is not BaseRanged)
                {
                    ApplyInherentWoodWeaponBonuses(weapon);
                }

                return;
            }

            var skill = GetWeaponCraftSkill(crafter, craftSystem);
            var skillTier = GetSkillTier(skill);
            var materialTier = GetMaterialTier(weapon.Resource);

            // Preserve exact base exceptional Arms Lore damage behavior for unskilled Iron crafts.
            if (skillTier == 0 && materialTier == 0)
            {
                return;
            }

            var power = skillTier + materialTier;
            var attrs = weapon.Attributes;
            var weaponAttrs = weapon.WeaponAttributes;

            if (tool is BaseRunicTool)
            {
                ApplyRunicWeaponPolish(weapon, attrs, skillTier, materialTier, power);
                if (Core.ML && weapon is not BaseRanged)
                {
                    ApplyInherentWoodWeaponBonuses(weapon);
                }

                return;
            }

            if (weapon is BaseStaff && skillTier >= 3)
            {
                ApplyStaffMasterworkBonuses(weapon, attrs, skillTier, materialTier, power);
                if (Core.ML)
                {
                    ApplyInherentWoodWeaponBonuses(weapon);
                }

                return;
            }

            if (power >= 4)
            {
                var damageFloor = Math.Clamp(35 + skillTier * 2 + materialTier * 2, 35, 48);
                SetMax(attrs, AosAttribute.WeaponDamage, damageFloor);
            }

            TrySetMax(weapon, attrs, AosAttribute.WeaponSpeed, ComputeSwingSpeedBonus(skillTier, materialTier, power));
            TrySetMax(weapon, attrs, AosAttribute.AttackChance, Math.Clamp(4 + skillTier + materialTier, 5, 12));

            if (weapon is BaseRanged ranged)
            {
                if (ranged is not BaseThrown && skillTier >= 3)
                {
                    var maxRangedWeight = GetMasterworkWeightCap(weapon);
                    if (CanAddProperty(weapon) &&
                        ImbuingCatalog.ComputeProjectedWeight(weapon, ImbuingCatalog.FindRangedBalancedMod(), 1) <= maxRangedWeight)
                    {
                        ranged.Balanced = true;
                    }

                    TrySetRangedVelocity(ranged, Math.Clamp(12 + power * 2, 16, 26), maxRangedWeight);
                    return;
                }

                if (ranged is BaseThrown && skillTier >= 3)
                {
                    TrySetRangedVelocity(ranged, Math.Clamp(12 + power * 2, 16, 26), MaxMasterworkWeight);
                }
            }

            if (Core.ML && weapon is not BaseRanged)
            {
                ApplyInherentWoodWeaponBonuses(weapon);
            }

            if (skillTier >= 2 || materialTier >= 2)
            {
                var primaryProcStrength = Math.Clamp(14 + power * 3, 20, 36);
                ApplyPrimaryWeaponProc(weapon, weaponAttrs, weapon.Resource, primaryProcStrength);
            }

            if (CanAddProperty(weapon))
            {
                if (skillTier >= 4 && power >= 7)
                {
                    ApplyMasterworkWeaponSignature(weapon, weaponAttrs, power);
                }
                else if (power >= 5)
                {
                    var areaStrength = Math.Clamp(16 + power * 3, 24, 42);
                    ApplyElementalWeaponProc(weapon, weaponAttrs, weapon.Resource, areaStrength);
                }
            }
        }

        public static void ApplyArmorBonuses(BaseArmor armor, Mobile crafter, CraftSystem craftSystem, BaseTool tool)
        {
            if (!Core.AOS || armor == null || crafter == null)
            {
                return;
            }

            if (armor.Quality != ArmorQuality.Exceptional)
            {
                if (Core.ML)
                {
                    ApplyInherentWoodArmorBonuses(armor);
                }

                return;
            }

            var skill = GetArmorCraftSkill(crafter, craftSystem);
            var skillTier = GetSkillTier(skill);
            var materialTier = GetMaterialTier(armor.Resource);

            if (skillTier == 0 && materialTier == 0)
            {
                if (Core.ML)
                {
                    ApplyInherentWoodArmorBonuses(armor);
                }

                return;
            }

            var power = skillTier + materialTier;
            var isRunic = tool is BaseRunicTool;

            if (armor is BaseShield shield)
            {
                ApplyShieldBonuses(shield, skillTier, materialTier, power, isRunic);
                return;
            }

            var attrs = armor.Attributes;
            var armorAttrs = armor.ArmorAttributes;
            var isMedFriendly = IsMedFriendlyArmor(armor);

            if (isRunic)
            {
                ApplyRunicArmorPolish(armor, attrs, isMedFriendly, skillTier, materialTier);
                if (Core.ML)
                {
                    ApplyInherentWoodArmorBonuses(armor);
                }

                return;
            }

            if (Core.ML)
            {
                ApplyInherentWoodArmorBonuses(armor);
            }

            if (materialTier >= 2)
            {
                ApplyMaterialResistBonus(armor, Math.Clamp(materialTier, 2, 3));
            }

            if (isMedFriendly)
            {
                var grantManaPhase = skill >= 115.0 && materialTier >= 4 &&
                                     (materialTier >= 5 || Utility.RandomDouble() < 0.35);

                TrySetMax(
                    armor,
                    attrs,
                    AosAttribute.LowerManaCost,
                    Math.Clamp(3 + skillTier + materialTier / 2, 4, grantManaPhase ? 6 : 7)
                );
                TrySetMax(
                    armor,
                    attrs,
                    AosAttribute.LowerRegCost,
                    Math.Clamp(8 + skillTier * 2 + materialTier, 10, grantManaPhase ? 15 : 18)
                );

                if (power >= 3)
                {
                    TrySetMax(armor, attrs, AosAttribute.RegenMana, power >= 7 ? 2 : 1);
                }

                if (skillTier >= 3)
                {
                    TrySetMax(
                        armor,
                        attrs,
                        AosAttribute.CastingFocus,
                        Math.Clamp(skillTier - 1 + materialTier / 2, 2, grantManaPhase ? 3 : 4)
                    );
                }

                if (grantManaPhase)
                {
                    TrySetMax(armor, armorAttrs, AosArmorAttribute.ManaPhase, 1);
                }
                else if (materialTier >= 3 && armor.GetLuckBonus() == 0)
                {
                    TrySetMax(armor, attrs, AosAttribute.Luck, Math.Clamp(25 + materialTier * 12, 45, 75));
                }
            }
            else
            {
                var grantMageArmor = skillTier >= 3 &&
                                     armor.MeditationAllowance != ArmorMeditationAllowance.All &&
                                     (crafter.Skills.Magery.Value >= 80.0 ||
                                      materialTier >= 4 && Utility.RandomDouble() < 0.35);

                TrySetMax(
                    armor,
                    attrs,
                    AosAttribute.BonusHits,
                    Math.Clamp(2 + power / 3, 3, grantMageArmor ? 4 : 5)
                );
                TrySetMax(armor, attrs, AosAttribute.LowerManaCost, Math.Clamp(3 + power / 2, 4, 6));

                if (power >= 3)
                {
                    if (attrs.RegenHits == 0 &&
                        armor.BodyPosition is ArmorBodyType.Gloves or ArmorBodyType.Arms or ArmorBodyType.Gorget)
                    {
                        TrySetMax(armor, attrs, AosAttribute.RegenStam, power >= 8 ? 2 : 1);
                    }
                    else
                    {
                        TrySetMax(armor, attrs, AosAttribute.RegenHits, power >= 7 ? 2 : 1);
                    }
                }

                if (grantMageArmor)
                {
                    TrySetMax(armor, armorAttrs, AosArmorAttribute.MageArmor, 1);
                }
                else if (power >= 4)
                {
                    TrySetMax(armor, attrs, AosAttribute.ReflectPhysical, Math.Clamp(4 + power, 6, 10));
                }

                if (skillTier >= 3)
                {
                    ApplyMaterialEater(armor, attrs, armor.Resource, power, grantMageArmor);
                }
            }
        }

        public static void ApplyClothingBonuses(
            BaseClothing clothing, Mobile crafter, CraftSystem craftSystem, BaseTool tool
        )
        {
            if (!Core.AOS || clothing == null || crafter == null)
            {
                return;
            }

            if (clothing.Quality != ClothingQuality.Exceptional)
            {
                return;
            }

            var skill = craftSystem != null
                ? crafter.Skills[craftSystem.MainSkill].Value
                : crafter.Skills.Tailoring.Value;
            var skillTier = GetSkillTier(skill);

            if (skillTier == 0)
            {
                return;
            }

            var materialTier = GetMaterialTier(clothing.Resource);
            var power = skillTier + materialTier;
            var attrs = clothing.Attributes;

            // Non-hat decorative clothing occupies extra worn layers outside armor; grant modest utility polish only.
            if (clothing is not BaseHat)
            {
                SetMax(attrs, AosAttribute.Luck, Math.Clamp(10 + skillTier * 5 + materialTier * 3, 15, 40));
                if (skillTier >= 3)
                {
                    SetMax(attrs, AosAttribute.LowerRegCost, Math.Clamp(skillTier * 2 - 2, 4, 8));
                }

                return;
            }

            var isRunic = tool is BaseRunicTool;
            var clothingAttrs = clothing.ClothingAttributes;

            if (isRunic)
            {
                // BaseHat.OnCraft distributes only 6 points for runic tools; restore the full exceptional budget.
                clothing.DistributeBonuses((Core.SE ? 15 : 14) - 6);
                ApplyRunicHatPolish(clothing, attrs, skillTier);
                return;
            }

            if (materialTier > 0)
            {
                clothing.DistributeBonuses(Math.Clamp(materialTier, 1, 3));
            }

            TrySetMax(clothing, attrs, AosAttribute.LowerRegCost, Math.Clamp(7 + skillTier * 2 + materialTier, 10, 16));

            if (skillTier >= 2)
            {
                TrySetMax(clothing, attrs, AosAttribute.LowerManaCost, Math.Clamp(2 + skillTier + materialTier / 2, 4, 6));
            }

            if (skillTier >= 3)
            {
                TrySetMax(clothing, attrs, AosAttribute.RegenMana, power >= 6 ? 2 : 1);
                TrySetMax(clothing, attrs, AosAttribute.CastingFocus, Math.Clamp(skillTier - 1 + materialTier / 2, 2, 4));
                if (skillTier >= 4)
                {
                    ApplyClothingResonance(clothing, clothingAttrs, clothing.Resource, 10);
                }
            }
            else
            {
                TrySetMax(clothing, attrs, AosAttribute.Luck, Math.Clamp(25 + skillTier * 15 + materialTier * 10, 40, 75));
            }
        }

        public static void ApplyJewelryBonuses(
            BaseJewel jewel, int quality, Mobile crafter, CraftSystem craftSystem, BaseTool tool
        )
        {
            if (!Core.AOS || jewel == null || crafter == null || quality != 2)
            {
                return;
            }

            var skill = craftSystem != null
                ? crafter.Skills[craftSystem.MainSkill].Value
                : crafter.Skills.Tinkering.Value;
            var skillTier = GetSkillTier(skill);

            if (skillTier == 0)
            {
                return;
            }

            var materialTier = GetMaterialTier(jewel.Resource);
            var attrs = jewel.Attributes;
            var resists = jewel.Resistances;

            if (tool is BaseRunicTool)
            {
                ApplyRunicJewelryPolish(jewel, attrs, resists, skillTier, materialTier);
                return;
            }

            var resistBonus = Math.Clamp(4 + skillTier * 2 + materialTier, 6, 12);
            var statBonus = Math.Clamp(2 + skillTier + materialTier / 2, 3, 7);
            var weaponDamage = Math.Clamp(10 + skillTier * 3 + materialTier, 13, 24);
            var hitOrDefChance = Math.Clamp(5 + skillTier * 2 + materialTier / 2, 7, 13);
            var spellDamage = Math.Clamp(2 + skillTier * 2 + materialTier / 2, 4, 11);
            var fcr = skillTier >= 4 ? 3 : skillTier >= 2 ? 2 : 1;

            switch (jewel.GemType)
            {
                case GemType.Diamond:
                    {
                        TrySetMax(jewel, attrs, AosAttribute.WeaponDamage, weaponDamage);
                        TrySetMax(jewel, attrs, AosAttribute.AttackChance, hitOrDefChance);
                        if (skillTier >= 3)
                        {
                            TrySetMax(jewel, attrs, AosAttribute.DefendChance, hitOrDefChance);
                        }

                        if (skillTier >= 4)
                        {
                            TrySetMax(jewel, resists, AosElementAttribute.Physical, resistBonus);
                        }

                        break;
                    }
                case GemType.Ruby:
                    {
                        TrySetMax(jewel, attrs, AosAttribute.BonusStr, statBonus);
                        TrySetMax(jewel, attrs, AosAttribute.WeaponDamage, weaponDamage);
                        TrySetMax(jewel, resists, AosElementAttribute.Fire, resistBonus);
                        if (skillTier >= 3)
                        {
                            TrySetMax(jewel, attrs, AosAttribute.AttackChance, Math.Clamp(4 + skillTier * 2, 10, 13));
                        }

                        break;
                    }
                case GemType.Sapphire:
                case GemType.StarSapphire:
                    {
                        if (skillTier >= 3)
                        {
                            TrySetMax(jewel, attrs, AosAttribute.CastSpeed, 1);
                        }

                        TrySetMax(jewel, attrs, AosAttribute.CastRecovery, fcr);
                        TrySetMax(jewel, attrs, AosAttribute.SpellDamage, spellDamage);
                        TrySetMax(jewel, attrs, AosAttribute.BonusInt, statBonus);
                        break;
                    }
                case GemType.Emerald:
                    {
                        TrySetMax(jewel, attrs, AosAttribute.LowerRegCost, Math.Clamp(8 + skillTier * 2 + materialTier, 10, 18));
                        TrySetMax(jewel, attrs, AosAttribute.LowerManaCost, Math.Clamp(3 + skillTier + materialTier / 2, 4, 7));
                        TrySetMax(jewel, resists, AosElementAttribute.Poison, resistBonus);
                        if (skillTier >= 3)
                        {
                            TrySetMax(jewel, attrs, AosAttribute.CastRecovery, fcr);
                        }

                        break;
                    }
                case GemType.Amethyst:
                    {
                        TrySetMax(jewel, attrs, AosAttribute.CastingFocus, Math.Clamp(skillTier - 1 + materialTier / 3, 1, 4));
                        TrySetMax(jewel, attrs, AosAttribute.SpellDamage, spellDamage);
                        TrySetMax(jewel, resists, AosElementAttribute.Energy, resistBonus);
                        if (skillTier >= 3)
                        {
                            TrySetMax(jewel, attrs, AosAttribute.LowerManaCost, Math.Clamp(3 + skillTier, 6, 7));
                        }

                        break;
                    }
                case GemType.Citrine:
                case GemType.Amber:
                    {
                        TrySetMax(jewel, attrs, AosAttribute.Luck, Math.Clamp(30 + skillTier * 12 + materialTier * 5, 45, 95));
                        TrySetMax(jewel, attrs, AosAttribute.EnhancePotions, Math.Clamp(10 + skillTier * 3, 15, 25));
                        TrySetMax(jewel, attrs, AosAttribute.BonusDex, statBonus);
                        if (skillTier >= 3)
                        {
                            TrySetMax(jewel, attrs, AosAttribute.DamageEater, Math.Clamp(skillTier - 1, 2, 4));
                        }

                        break;
                    }
                case GemType.Tourmaline:
                    {
                        TrySetMax(jewel, attrs, AosAttribute.DefendChance, hitOrDefChance);
                        TrySetMax(jewel, attrs, AosAttribute.BonusDex, statBonus);
                        TrySetMax(jewel, resists, AosElementAttribute.Cold, resistBonus);
                        if (skillTier >= 3)
                        {
                            TrySetMax(jewel, attrs, AosAttribute.DamageEater, Math.Clamp(skillTier - 1, 2, 4));
                        }

                        break;
                    }
                default:
                    {
                        TrySetMax(jewel, attrs, AosAttribute.Luck, Math.Clamp(25 + skillTier * 12 + materialTier * 5, 40, 90));
                        TrySetMax(jewel, attrs, AosAttribute.LowerManaCost, Math.Clamp(2 + skillTier + materialTier / 2, 3, 7));
                        if (skillTier >= 3)
                        {
                            TrySetMax(jewel, attrs, AosAttribute.AttackChance, Math.Clamp(4 + skillTier * 2, 10, 12));
                            TrySetMax(jewel, attrs, AosAttribute.CastRecovery, fcr);
                        }
                        else
                        {
                            TrySetMax(jewel, resists, GetRandomElementAttribute(), resistBonus);
                        }

                        break;
                    }
            }
        }

        public static void ApplySpellbookBonuses(Spellbook book, Mobile crafter, CraftSystem craftSystem)
        {
            if (!Core.AOS || book == null || crafter == null)
            {
                return;
            }

            var effectiveSkill = Math.Max(crafter.Skills.Magery.Value, crafter.Skills.Inscribe.Value);
            if (craftSystem != null)
            {
                effectiveSkill = Math.Max(effectiveSkill, crafter.Skills[craftSystem.MainSkill].Value);
            }

            var skillTier = GetSkillTier(effectiveSkill);
            if (skillTier < 3)
            {
                return;
            }

            var attrs = book.Attributes;

            if (book.Quality == BookQuality.Exceptional)
            {
                var sdi = Math.Clamp(2 + skillTier * 2, 8, 11);
                var focus = Math.Clamp(2 + (skillTier - 3) * 2, 2, 5);
                var lmc = Math.Clamp(2 + skillTier, 5, 7);
                var fcr = skillTier >= 4 ? 3 : 2;
                var regenMana = skillTier >= 5 ? 2 : 1;

                // Polish any already-rolled caster attributes first without consuming slots.
                PolishIfPresent(attrs, AosAttribute.SpellDamage, sdi);
                PolishIfPresent(attrs, AosAttribute.CastingFocus, focus);
                PolishIfPresent(attrs, AosAttribute.LowerManaCost, lmc);
                PolishIfPresent(attrs, AosAttribute.CastRecovery, fcr);
                PolishIfPresent(attrs, AosAttribute.RegenMana, regenMana);

                // Fill prioritized caster affixes strictly within the 5-property and Masterwork weight ceilings.
                TryAddSpellbookAttr(book, attrs, AosAttribute.SpellDamage, sdi);
                TryAddSpellbookAttr(book, attrs, AosAttribute.CastingFocus, focus);
                TryAddSpellbookAttr(book, attrs, AosAttribute.LowerManaCost, lmc);
                TryAddSpellbookAttr(book, attrs, AosAttribute.CastRecovery, fcr);
                if (skillTier >= 4)
                {
                    TryAddSpellbookAttr(book, attrs, AosAttribute.RegenMana, regenMana);
                }
            }
            else if (Utility.RandomDouble() < 0.5)
            {
                TryAddSpellbookAttr(book, attrs, AosAttribute.CastingFocus, Math.Clamp(2 + (skillTier - 3), 2, 4));
            }
        }

        private static void ApplyStaffMasterworkBonuses(
            BaseWeapon weapon, AosAttributes attrs, int skillTier, int materialTier, int power
        )
        {
            if (CanAddProperty(weapon) || attrs.SpellChanneling > 0)
            {
                ApplySpellChanneling(attrs);
            }

            TrySetMax(weapon, attrs, AosAttribute.SpellDamage, Math.Clamp(4 + skillTier + materialTier, 6, 12));
            TrySetMax(weapon, attrs, AosAttribute.CastRecovery, Math.Clamp(skillTier - 1, 1, 3));

            if (power >= 5 && weapon.Resource != CraftResource.Bloodwood)
            {
                TrySetMax(weapon, attrs, AosAttribute.WeaponSpeed, Math.Clamp((10 + power * 2) / 5 * 5, 15, 20));
            }
        }

        private static void ApplyInherentWoodWeaponBonuses(BaseWeapon weapon)
        {
            var attrs = weapon.Attributes;
            var weaponAttrs = weapon.WeaponAttributes;

            switch (weapon.Resource)
            {
                case CraftResource.OakWood:
                    {
                        TryAddClamped(weapon, attrs, AosAttribute.WeaponDamage, 5, 50);
                        break;
                    }
                case CraftResource.AshWood:
                    {
                        TryAddClamped(weapon, attrs, AosAttribute.WeaponSpeed, 10, 30);
                        break;
                    }
                case CraftResource.YewWood:
                    {
                        TryAddClamped(weapon, attrs, AosAttribute.AttackChance, 5, 15);
                        TryAddClamped(weapon, attrs, AosAttribute.WeaponDamage, 10, 50);
                        break;
                    }
                case CraftResource.Heartwood:
                    {
                        switch (Utility.Random(4))
                        {
                            case 0:
                                {
                                    TryAddClamped(weapon, attrs, AosAttribute.WeaponSpeed, 10, 30);
                                    break;
                                }
                            case 1:
                                {
                                    TryAddClamped(weapon, attrs, AosAttribute.AttackChance, 5, 15);
                                    break;
                                }
                            case 2:
                                {
                                    TryAddClamped(weapon, weaponAttrs, AosWeaponAttribute.HitLeechHits, 10, 50);
                                    break;
                                }
                            default:
                                {
                                    TryAddClamped(weapon, attrs, AosAttribute.WeaponDamage, 10, 50);
                                    break;
                                }
                        }

                        break;
                    }
                case CraftResource.Bloodwood:
                    {
                        TryAddClamped(weapon, weaponAttrs, AosWeaponAttribute.HitLeechHits, 16, 50);
                        TryAddClamped(weapon, attrs, AosAttribute.RegenHits, 2, 2);
                        break;
                    }
                case CraftResource.Frostwood:
                    {
                        TryAddClamped(weapon, attrs, AosAttribute.WeaponDamage, 12, 50);
                        break;
                    }
            }
        }

        private static void ApplyInherentWoodArmorBonuses(BaseArmor armor)
        {
            switch (armor.Resource)
            {
                case CraftResource.YewWood:
                    {
                        TrySetMax(armor, armor.Attributes, AosAttribute.RegenHits, 1);
                        break;
                    }
                case CraftResource.Bloodwood:
                    {
                        TrySetMax(armor, armor.Attributes, AosAttribute.RegenHits, 2);
                        break;
                    }
            }
        }

        private static void ApplyRunicWeaponPolish(
            BaseWeapon weapon, AosAttributes attrs, int skillTier, int materialTier, int power
        )
        {
            if (skillTier < 3)
            {
                return;
            }

            var speedFloor = Math.Clamp((10 + skillTier * 2 + materialTier * 2) / 5 * 5, 15, 25);
            var hitChanceFloor = Math.Clamp(5 + skillTier + materialTier, 8, 13);

            // Polish existing runic rolls without adding extra slots.
            PolishIfPresent(attrs, AosAttribute.WeaponSpeed, speedFloor);
            PolishIfPresent(attrs, AosAttribute.AttackChance, hitChanceFloor);

            if (skillTier >= 4 && materialTier >= 3)
            {
                SetMax(attrs, AosAttribute.WeaponDamage, Math.Clamp(42 + materialTier * 2, 42, 50));
            }

            // Fill up to the 5-property ceiling only when runic rolled fewer than 5 properties.
            TrySetMaxRunic(weapon, attrs, AosAttribute.WeaponSpeed, speedFloor);
            TrySetMaxRunic(weapon, attrs, AosAttribute.AttackChance, hitChanceFloor);

            if (weapon is BaseRanged ranged)
            {
                if (ranged is not BaseThrown && CanAddProperty(weapon))
                {
                    ranged.Balanced = true;
                }

                var velocity = Math.Clamp(15 + power * 2, 18, 30);
                if ((ranged.Velocity > 0 || CanAddProperty(weapon)) && ranged.Velocity < velocity)
                {
                    ranged.Velocity = velocity;
                }
            }
        }

        private static void ApplyPrimaryWeaponProc(
            BaseWeapon weapon, AosWeaponAttributes weaponAttrs, CraftResource resource, int intensity
        )
        {
            switch (resource)
            {
                case CraftResource.Bloodwood:
                case CraftResource.ShadowIron:
                case CraftResource.ExcelsiorShadowIron:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitLeechHits, intensity);
                        return;
                    }
                case CraftResource.Gold:
                case CraftResource.Electrum:
                case CraftResource.Heartwood:
                case CraftResource.ExcelsiorGold:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitLeechMana, intensity);
                        return;
                    }
                case CraftResource.AshWood:
                case CraftResource.BambooWood:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitLeechStam, intensity);
                        return;
                    }
                case CraftResource.Valorite:
                case CraftResource.Platinum:
                case CraftResource.ExcelsiorValorite:
                    {
                        TrySetMax(
                            weapon,
                            weaponAttrs,
                            Utility.RandomBool() ? AosWeaponAttribute.HitLeechHits : AosWeaponAttribute.HitLeechMana,
                            intensity
                        );
                        return;
                    }
            }

            switch (Utility.Random(4))
            {
                case 0:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitLeechHits, intensity);
                        break;
                    }
                case 1:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitLeechMana, intensity);
                        break;
                    }
                case 2:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitLeechStam, intensity);
                        break;
                    }
                default:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitLowerDefend, intensity);
                        break;
                    }
            }
        }

        private static void ApplyElementalWeaponProc(
            BaseWeapon weapon, AosWeaponAttributes weaponAttrs, CraftResource resource, int intensity
        )
        {
            switch (resource)
            {
                case CraftResource.Bronze:
                case CraftResource.Blaze:
                case CraftResource.Bloodwood:
                case CraftResource.PurpleHeartWood:
                case CraftResource.ExcelsiorBronze:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitFireArea, intensity);
                        return;
                    }
                case CraftResource.ShadowIron:
                case CraftResource.Ice:
                case CraftResource.Frostwood:
                case CraftResource.EbonyWood:
                case CraftResource.ExcelsiorShadowIron:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitColdArea, intensity);
                        return;
                    }
                case CraftResource.Verite:
                case CraftResource.Toxic:
                case CraftResource.Redwood:
                case CraftResource.ExcelsiorVerite:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitPoisonArea, intensity);
                        return;
                    }
                case CraftResource.Copper:
                case CraftResource.Agapite:
                case CraftResource.Electrum:
                case CraftResource.BambooWood:
                case CraftResource.ExcelsiorCopper:
                case CraftResource.ExcelsiorAgapite:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitEnergyArea, intensity);
                        return;
                    }
            }

            switch (Utility.Random(4))
            {
                case 0:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitPhysicalArea, intensity);
                        break;
                    }
                case 1:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitLightning, intensity);
                        break;
                    }
                case 2:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitFireball, intensity);
                        break;
                    }
                default:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitLowerDefend, intensity);
                        break;
                    }
            }
        }

        private static void ApplyMasterworkWeaponSignature(
            BaseWeapon weapon, AosWeaponAttributes weaponAttrs, int power
        )
        {
            var debuffChance = Math.Clamp(10 + power * 3, 20, 34);
            var splintering = Math.Clamp(5 + power * 2, 15, 20);

            if (weapon is BaseRanged)
            {
                switch (Utility.Random(4))
                {
                    case 0:
                        {
                            TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.SplinteringWeapon, splintering);
                            break;
                        }
                    case 1:
                        {
                            TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitCurse, debuffChance);
                            break;
                        }
                    case 2:
                        {
                            TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitFatigue, debuffChance);
                            break;
                        }
                    default:
                        {
                            TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitManaDrain, debuffChance);
                            break;
                        }
                }

                return;
            }

            if (power >= 9 && Utility.RandomDouble() < 0.35)
            {
                TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.Bane, 1);
                return;
            }

            switch (Utility.Random(6))
            {
                case 0:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.SplinteringWeapon, splintering);
                        break;
                    }
                case 1:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.BattleLust, 1);
                        break;
                    }
                case 2:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitCurse, debuffChance);
                        break;
                    }
                case 3:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitFatigue, debuffChance);
                        break;
                    }
                case 4:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.HitManaDrain, debuffChance);
                        break;
                    }
                default:
                    {
                        TrySetMax(weapon, weaponAttrs, AosWeaponAttribute.BloodDrinker, 1);
                        break;
                    }
            }
        }

        private static void ApplyShieldBonuses(
            BaseShield shield, int skillTier, int materialTier, int power, bool isRunic
        )
        {
            var attrs = shield.Attributes;
            var armorAttrs = shield.ArmorAttributes;

            if (Core.ML)
            {
                var resistPoints = Math.Clamp(6 + power, 6, 12);
                shield.DistributeBonuses(resistPoints);
            }

            var dciFloor = Math.Clamp(7 + skillTier + materialTier, 8, 14);
            var reflectFloor = Math.Clamp(6 + skillTier + materialTier, 8, 12);

            if (isRunic)
            {
                PolishIfPresent(attrs, AosAttribute.DefendChance, dciFloor);
                PolishIfPresent(attrs, AosAttribute.ReflectPhysical, reflectFloor);
                TrySetMaxRunic(shield, attrs, AosAttribute.DefendChance, dciFloor);
                TrySetMaxRunic(shield, attrs, AosAttribute.ReflectPhysical, reflectFloor);
                return;
            }

            TrySetMax(shield, attrs, AosAttribute.DefendChance, dciFloor);
            TrySetMax(shield, attrs, AosAttribute.ReflectPhysical, reflectFloor);

            if (skillTier < 3)
            {
                return;
            }

            var resourceType = CraftResources.GetType(shield.Resource);
            var isWooden = resourceType == CraftResourceType.Wood ||
                           resourceType == CraftResourceType.None && shield.MaterialType == ArmorMaterialType.Wood;

            if (isWooden)
            {
                if (CanAddProperty(shield))
                {
                    ApplySpellChanneling(attrs);
                }

                if (power >= 5)
                {
                    TrySetMax(shield, armorAttrs, AosArmorAttribute.SoulCharge, Math.Clamp(8 + power * 2, 10, 20));
                }

                return;
            }

            if (power >= 7)
            {
                TrySetMax(shield, armorAttrs, AosArmorAttribute.SoulCharge, Math.Clamp(8 + power * 2, 15, 20));
                TrySetMax(shield, armorAttrs, AosArmorAttribute.ReactiveParalyze, 1);
            }
            else if (power >= 5)
            {
                TrySetMax(shield, armorAttrs, AosArmorAttribute.SoulCharge, Math.Clamp(8 + power * 2, 10, 15));
                if (Utility.RandomBool() && CanAddProperty(shield))
                {
                    ApplySpellChanneling(attrs);
                }
            }
            else if (Utility.RandomBool())
            {
                if (CanAddProperty(shield))
                {
                    ApplySpellChanneling(attrs);
                }
            }
            else
            {
                TrySetMax(shield, armorAttrs, AosArmorAttribute.SoulCharge, Math.Clamp(8 + power, 10, 15));
            }
        }

        private static void ApplyRunicArmorPolish(
            BaseArmor armor, AosAttributes attrs, bool isMedFriendly, int skillTier, int materialTier
        )
        {
            if (skillTier < 3)
            {
                return;
            }

            if (isMedFriendly)
            {
                var lmcFloor = Math.Clamp(3 + skillTier / 2 + materialTier / 2, 4, 7);
                var lrcFloor = Math.Clamp(8 + skillTier * 2 + materialTier, 10, 18);
                PolishIfPresent(attrs, AosAttribute.LowerManaCost, lmcFloor);
                PolishIfPresent(attrs, AosAttribute.LowerRegCost, lrcFloor);
                TrySetMaxRunic(armor, attrs, AosAttribute.LowerManaCost, lmcFloor);
                TrySetMaxRunic(armor, attrs, AosAttribute.LowerRegCost, lrcFloor);

                if (skillTier >= 4 && materialTier >= 3)
                {
                    TrySetMaxRunic(armor, attrs, AosAttribute.CastingFocus, Math.Clamp(skillTier - 1, 2, 4));
                }
            }
            else
            {
                var lmcFloor = Math.Clamp(3 + skillTier / 2 + materialTier / 2, 4, 6);
                var hitsFloor = Math.Clamp(2 + skillTier / 2 + materialTier / 3, 3, 5);
                PolishIfPresent(attrs, AosAttribute.LowerManaCost, lmcFloor);
                PolishIfPresent(attrs, AosAttribute.BonusHits, hitsFloor);
                TrySetMaxRunic(armor, attrs, AosAttribute.LowerManaCost, lmcFloor);
                TrySetMaxRunic(armor, attrs, AosAttribute.BonusHits, hitsFloor);

                if (skillTier >= 4 && materialTier >= 3)
                {
                    TrySetMaxRunic(armor, attrs, AosAttribute.DamageEater, Math.Clamp(skillTier - 1, 2, 4));
                }
            }
        }

        private static void ApplyRunicHatPolish(BaseClothing hat, AosAttributes attrs, int skillTier)
        {
            var lrcFloor = Math.Clamp(8 + skillTier * 2, 10, 16);
            PolishIfPresent(attrs, AosAttribute.LowerRegCost, lrcFloor);
            TrySetMaxRunic(hat, attrs, AosAttribute.LowerRegCost, lrcFloor);

            if (skillTier >= 3)
            {
                var lmcFloor = Math.Clamp(2 + skillTier, 5, 7);
                PolishIfPresent(attrs, AosAttribute.LowerManaCost, lmcFloor);
                TrySetMaxRunic(hat, attrs, AosAttribute.LowerManaCost, lmcFloor);
                TrySetMaxRunic(hat, attrs, AosAttribute.CastingFocus, Math.Clamp(skillTier - 1, 2, 4));
            }
        }

        private static void ApplyRunicJewelryPolish(
            BaseJewel jewel, AosAttributes attrs, AosElementAttributes resists, int skillTier, int materialTier
        )
        {
            if (skillTier < 3)
            {
                return;
            }

            var weaponDamage = Math.Clamp(10 + skillTier * 3 + materialTier, 16, 24);
            var hitOrDefChance = Math.Clamp(5 + skillTier * 2 + materialTier / 2, 10, 14);
            var spellDamage = Math.Clamp(2 + skillTier * 2 + materialTier / 2, 8, 12);
            var lmc = Math.Clamp(3 + skillTier + materialTier / 2, 5, 8);
            var lrc = Math.Clamp(8 + skillTier * 2 + materialTier, 12, 20);
            var statBonus = Math.Clamp(2 + skillTier + materialTier / 2, 4, 8);

            // Polish existing runic attributes first without consuming property slots.
            PolishIfPresent(attrs, AosAttribute.WeaponDamage, weaponDamage);
            PolishIfPresent(attrs, AosAttribute.AttackChance, hitOrDefChance);
            PolishIfPresent(attrs, AosAttribute.DefendChance, hitOrDefChance);
            PolishIfPresent(attrs, AosAttribute.SpellDamage, spellDamage);
            PolishIfPresent(attrs, AosAttribute.LowerManaCost, lmc);
            PolishIfPresent(attrs, AosAttribute.LowerRegCost, lrc);
            PolishIfPresent(attrs, AosAttribute.BonusStr, statBonus);
            PolishIfPresent(attrs, AosAttribute.BonusDex, statBonus);
            PolishIfPresent(attrs, AosAttribute.BonusInt, statBonus);

            // Add at most one gem-themed signature polish if the runic roll left an open slot.
            if (!CanAddProperty(jewel))
            {
                return;
            }

            switch (jewel.GemType)
            {
                case GemType.Diamond:
                    {
                        TrySetMaxRunic(jewel, attrs, AosAttribute.AttackChance, hitOrDefChance);
                        break;
                    }
                case GemType.Ruby:
                    {
                        TrySetMaxRunic(jewel, attrs, AosAttribute.WeaponDamage, weaponDamage);
                        break;
                    }
                case GemType.Sapphire:
                case GemType.StarSapphire:
                    {
                        TrySetMaxRunic(jewel, attrs, AosAttribute.SpellDamage, spellDamage);
                        break;
                    }
                case GemType.Emerald:
                    {
                        TrySetMaxRunic(jewel, attrs, AosAttribute.LowerManaCost, lmc);
                        break;
                    }
                case GemType.Amethyst:
                    {
                        TrySetMaxRunic(jewel, attrs, AosAttribute.CastingFocus, Math.Clamp(skillTier - 1, 2, 4));
                        break;
                    }
                case GemType.Citrine:
                case GemType.Amber:
                    {
                        TrySetMaxRunic(jewel, attrs, AosAttribute.Luck, Math.Clamp(30 + skillTier * 12, 60, 95));
                        break;
                    }
                case GemType.Tourmaline:
                    {
                        TrySetMaxRunic(jewel, attrs, AosAttribute.DefendChance, hitOrDefChance);
                        break;
                    }
                default:
                    {
                        TrySetMaxRunic(jewel, resists, GetRandomElementAttribute(), Math.Clamp(4 + skillTier * 2, 8, 12));
                        break;
                    }
            }
        }

        private static void ApplyMaterialEater(
            BaseArmor armor, AosAttributes attrs, CraftResource resource, int power, bool hasMageArmor
        )
        {
            var elementalEater = Math.Clamp(3 + power, 5, 10);
            var damageEater = Math.Clamp(2 + power / 3, 3, hasMageArmor ? 3 : 4);

            switch (resource)
            {
                case CraftResource.Bronze:
                case CraftResource.Blaze:
                case CraftResource.RedScales:
                case CraftResource.Bloodwood:
                case CraftResource.ExcelsiorBronze:
                    {
                        TrySetMax(armor, attrs, AosAttribute.FireEater, elementalEater);
                        return;
                    }
                case CraftResource.ShadowIron:
                case CraftResource.Ice:
                case CraftResource.WhiteScales:
                case CraftResource.Frostwood:
                case CraftResource.ExcelsiorShadowIron:
                    {
                        TrySetMax(armor, attrs, AosAttribute.ColdEater, elementalEater);
                        return;
                    }
                case CraftResource.Verite:
                case CraftResource.Toxic:
                case CraftResource.GreenScales:
                case CraftResource.ExcelsiorVerite:
                    {
                        TrySetMax(armor, attrs, AosAttribute.PoisonEater, elementalEater);
                        return;
                    }
                case CraftResource.Copper:
                case CraftResource.Agapite:
                case CraftResource.Electrum:
                case CraftResource.BlueScales:
                case CraftResource.ExcelsiorCopper:
                case CraftResource.ExcelsiorAgapite:
                    {
                        TrySetMax(armor, attrs, AosAttribute.EnergyEater, elementalEater);
                        return;
                    }
                case CraftResource.DullCopper:
                case CraftResource.BlackScales:
                case CraftResource.ExcelsiorDullCopper:
                    {
                        TrySetMax(armor, attrs, AosAttribute.KineticEater, elementalEater);
                        return;
                    }
                default:
                    {
                        TrySetMax(armor, attrs, AosAttribute.DamageEater, damageEater);
                        return;
                    }
            }
        }

        private static void ApplyMaterialResistBonus(BaseArmor armor, int amount)
        {
            switch (armor.Resource)
            {
                case CraftResource.Bronze:
                case CraftResource.Blaze:
                case CraftResource.BlazeLeather:
                case CraftResource.DaemonicLeather:
                case CraftResource.RedScales:
                case CraftResource.Bloodwood:
                case CraftResource.ExcelsiorBronze:
                    {
                        armor.FireBonus += amount;
                        break;
                    }
                case CraftResource.ShadowIron:
                case CraftResource.Ice:
                case CraftResource.PolarLeather:
                case CraftResource.FrostLeather:
                case CraftResource.WhiteScales:
                case CraftResource.Frostwood:
                case CraftResource.ExcelsiorShadowIron:
                    {
                        armor.ColdBonus += amount;
                        break;
                    }
                case CraftResource.Verite:
                case CraftResource.Toxic:
                case CraftResource.GreenScales:
                case CraftResource.Heartwood:
                case CraftResource.ExcelsiorVerite:
                    {
                        armor.PoisonBonus += amount;
                        break;
                    }
                case CraftResource.Copper:
                case CraftResource.Agapite:
                case CraftResource.Electrum:
                case CraftResource.BarbedLeather:
                case CraftResource.EtherealLeather:
                case CraftResource.BlueScales:
                case CraftResource.AshWood:
                case CraftResource.ExcelsiorCopper:
                case CraftResource.ExcelsiorAgapite:
                case CraftResource.ExcelsiorBarbedLeather:
                    {
                        armor.EnergyBonus += amount;
                        break;
                    }
                default:
                    {
                        armor.DistributeBonuses(amount);
                        break;
                    }
            }
        }

        private static void ApplyClothingResonance(
            BaseClothing clothing, AosArmorAttributes clothingAttrs, CraftResource resource, int amount
        )
        {
            switch (resource)
            {
                case CraftResource.BlazeLeather:
                case CraftResource.DaemonicLeather:
                    {
                        TrySetMax(clothing, clothingAttrs, AosArmorAttribute.FireResonance, amount);
                        return;
                    }
                case CraftResource.PolarLeather:
                case CraftResource.FrostLeather:
                    {
                        TrySetMax(clothing, clothingAttrs, AosArmorAttribute.ColdResonance, amount);
                        return;
                    }
                case CraftResource.BarbedLeather:
                case CraftResource.EtherealLeather:
                case CraftResource.ExcelsiorBarbedLeather:
                    {
                        TrySetMax(clothing, clothingAttrs, AosArmorAttribute.EnergyResonance, amount);
                        return;
                    }
            }

            switch (Utility.Random(3))
            {
                case 0:
                    {
                        TrySetMax(clothing, clothingAttrs, AosArmorAttribute.FireResonance, amount);
                        break;
                    }
                case 1:
                    {
                        TrySetMax(clothing, clothingAttrs, AosArmorAttribute.ColdResonance, amount);
                        break;
                    }
                default:
                    {
                        TrySetMax(clothing, clothingAttrs, AosArmorAttribute.EnergyResonance, amount);
                        break;
                    }
            }
        }

        private static void ApplySpellChanneling(AosAttributes attrs)
        {
            if (attrs.SpellChanneling == 0)
            {
                attrs.SpellChanneling = 1;
                attrs.CastSpeed -= 1;
            }
        }

        private static bool CanAddProperty(Item item) =>
            ImbuingCatalog.CountProperties(item) < ImbuingCatalog.MaxProperties;

        private static void TryAddSpellbookAttr(Spellbook book, AosAttributes attrs, AosAttribute attr, int value)
        {
            if (attrs[attr] >= value)
            {
                return;
            }

            if (attrs[attr] == 0 && !CanAddProperty(book))
            {
                return;
            }

            var mod = ImbuingCatalog.FindMod(attr);
            if (mod != null)
            {
                var currentWeight = ImbuingCatalog.ComputeTotalWeight(book);
                var currentModWeight = attrs[attr] > 0 ? mod.ComputeWeight(book, attrs[attr]) : 0;
                var nextModWeight = mod.ComputeWeight(book, value);
                if (currentWeight - currentModWeight + nextModWeight > MaxMasterworkWeight)
                {
                    return;
                }
            }

            attrs[attr] = value;
        }

        private static bool IsMedFriendlyArmor(BaseArmor armor) =>
            armor.MeditationAllowance == ArmorMeditationAllowance.All ||
            armor.MaterialType is ArmorMaterialType.Leather or ArmorMaterialType.Cloth or
                ArmorMaterialType.Spined or ArmorMaterialType.Horned or ArmorMaterialType.Barbed;

        private static int ComputeSwingSpeedBonus(int skillTier, int materialTier, int power)
        {
            if (power >= 7)
            {
                return 25;
            }

            if (power >= 5 || skillTier >= 3 && materialTier >= 1)
            {
                return 20;
            }

            if (skillTier >= 2 || power >= 3)
            {
                return 15;
            }

            return 10;
        }

        private static double GetWeaponCraftSkill(Mobile crafter, CraftSystem craftSystem)
        {
            if (craftSystem != null)
            {
                return crafter.Skills[craftSystem.MainSkill].Value;
            }

            return Math.Max(
                crafter.Skills.Blacksmith.Value,
                Math.Max(crafter.Skills.Fletching.Value, crafter.Skills.Carpentry.Value)
            );
        }

        private static double GetArmorCraftSkill(Mobile crafter, CraftSystem craftSystem)
        {
            if (craftSystem != null)
            {
                return crafter.Skills[craftSystem.MainSkill].Value;
            }

            return Math.Max(
                crafter.Skills.Blacksmith.Value,
                Math.Max(crafter.Skills.Tailoring.Value, crafter.Skills.Carpentry.Value)
            );
        }

        private static AosElementAttribute GetRandomElementAttribute() =>
            Utility.Random(5) switch
            {
                0 => AosElementAttribute.Physical,
                1 => AosElementAttribute.Fire,
                2 => AosElementAttribute.Cold,
                3 => AosElementAttribute.Poison,
                _ => AosElementAttribute.Energy
            };

        private static void PolishIfPresent(AosAttributes attrs, AosAttribute attr, int floor)
        {
            if (attrs[attr] > 0 && attrs[attr] < floor)
            {
                attrs[attr] = floor;
            }
        }

        private static void TryAddClamped(Item item, AosAttributes attrs, AosAttribute attr, int bonus, int cap)
        {
            var current = attrs[attr];
            if (current <= 0 && !CanAddProperty(item))
            {
                return;
            }

            if (current < cap)
            {
                attrs[attr] = Math.Min(cap, current + bonus);
            }
        }

        private static void TryAddClamped(Item item, AosWeaponAttributes attrs, AosWeaponAttribute attr, int bonus, int cap)
        {
            var current = attrs[attr];
            if (current <= 0 && !CanAddProperty(item))
            {
                return;
            }

            if (current < cap)
            {
                attrs[attr] = Math.Min(cap, current + bonus);
            }
        }

        private static void TrySetMax(Item item, AosAttributes attrs, AosAttribute attr, int value)
        {
            var current = attrs[attr];
            if (current >= value)
            {
                return;
            }

            if (current <= 0 && !CanAddProperty(item))
            {
                return;
            }

            var fitted = FitMasterworkValue(item, ImbuingCatalog.FindMod(attr), current, value);
            if (fitted > current)
            {
                attrs[attr] = fitted;
            }
        }

        private static void TrySetMax(Item item, AosWeaponAttributes attrs, AosWeaponAttribute attr, int value)
        {
            var current = attrs[attr];
            if (current >= value)
            {
                return;
            }

            if (current <= 0 && !CanAddProperty(item))
            {
                return;
            }

            var fitted = FitMasterworkValue(item, ImbuingCatalog.FindMod(attr), current, value);
            if (fitted > current)
            {
                attrs[attr] = fitted;
            }
        }

        private static void TrySetMax(Item item, AosArmorAttributes attrs, AosArmorAttribute attr, int value)
        {
            var current = attrs[attr];
            if (current >= value)
            {
                return;
            }

            if (current <= 0 && !CanAddProperty(item))
            {
                return;
            }

            var fitted = FitMasterworkValue(item, ImbuingCatalog.FindMod(attr), current, value);
            if (fitted > current)
            {
                attrs[attr] = fitted;
            }
        }

        private static void TrySetMax(Item item, AosElementAttributes attrs, AosElementAttribute attr, int value)
        {
            var current = attrs[attr];
            if (current >= value)
            {
                return;
            }

            if (current <= 0 && !CanAddProperty(item))
            {
                return;
            }

            var resistType = attr switch
            {
                AosElementAttribute.Physical => ResistanceType.Physical,
                AosElementAttribute.Fire     => ResistanceType.Fire,
                AosElementAttribute.Cold     => ResistanceType.Cold,
                AosElementAttribute.Poison   => ResistanceType.Poison,
                _                            => ResistanceType.Energy
            };

            var fitted = FitMasterworkValue(item, ImbuingCatalog.FindResistanceMod(resistType), current, value);
            if (fitted > current)
            {
                attrs[attr] = fitted;
            }
        }

        private static void TrySetMaxRunic(Item item, AosAttributes attrs, AosAttribute attr, int value)
        {
            var current = attrs[attr];
            if (current >= value)
            {
                return;
            }

            if (current <= 0 && !CanAddProperty(item))
            {
                return;
            }

            attrs[attr] = value;
        }

        private static void TrySetMaxRunic(Item item, AosElementAttributes attrs, AosElementAttribute attr, int value)
        {
            var current = attrs[attr];
            if (current >= value)
            {
                return;
            }

            if (current <= 0 && !CanAddProperty(item))
            {
                return;
            }

            attrs[attr] = value;
        }

        private static void TrySetRangedVelocity(BaseRanged ranged, int targetVelocity, int maxWeight)
        {
            var current = ranged.Velocity;
            if (current >= targetVelocity)
            {
                return;
            }

            if (current <= 0 && !CanAddProperty(ranged))
            {
                return;
            }

            var fitted = FitMasterworkValue(ranged, ImbuingCatalog.FindRangedVelocityMod(), current, targetVelocity, maxWeight);
            if (fitted > current)
            {
                ranged.Velocity = fitted;
            }
        }

        private static int GetMasterworkWeightCap(Item item) =>
            Core.ML && item is BaseRanged { Resource: CraftResource.OakWood or CraftResource.AshWood or CraftResource.YewWood or CraftResource.Heartwood or CraftResource.Bloodwood or CraftResource.Frostwood }
                ? MaxMasterworkWeight - 35
                : MaxMasterworkWeight;

        private static int FitMasterworkValue(Item item, ImbuingMod mod, int current, int target, int? customCap = null)
        {
            if (mod == null)
            {
                return target;
            }

            var cap = customCap ?? GetMasterworkWeightCap(item);
            if (ImbuingCatalog.ComputeProjectedWeight(item, mod, target) <= cap)
            {
                return target;
            }

            if (mod.IsBoolean)
            {
                return current;
            }

            var step = Math.Max(1, mod.Step);
            var minAllowed = Math.Max(current + 1, mod.MinValue);

            for (var candidate = target - step; candidate >= minAllowed; candidate -= step)
            {
                if (ImbuingCatalog.ComputeProjectedWeight(item, mod, candidate) <= cap)
                {
                    return candidate;
                }
            }

            if (minAllowed < target && ImbuingCatalog.ComputeProjectedWeight(item, mod, minAllowed) <= cap)
            {
                return minAllowed;
            }

            return current;
        }

        private static void SetMax(AosAttributes attrs, AosAttribute attr, int value)
        {
            if (attrs[attr] < value)
            {
                attrs[attr] = value;
            }
        }
    }
}
