using System;
using System.Collections;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(1, false)]
public abstract partial class BaseRunicTool : BaseTool
{
    private const int MaxProperties = 48;

    private static bool m_IsRunicTool;
    private static int m_LuckChance;

    private static readonly SkillName[] m_PossibleBonusSkills =
    {
        SkillName.Swords,
        SkillName.Fencing,
        SkillName.Macing,
        SkillName.Archery,
        SkillName.Wrestling,
        SkillName.Parry,
        SkillName.Tactics,
        SkillName.Anatomy,
        SkillName.Healing,
        SkillName.Magery,
        SkillName.Meditation,
        SkillName.EvalInt,
        SkillName.MagicResist,
        SkillName.AnimalTaming,
        SkillName.AnimalLore,
        SkillName.Veterinary,
        SkillName.Musicianship,
        SkillName.Provocation,
        SkillName.Discordance,
        SkillName.Peacemaking,
        SkillName.Chivalry,
        SkillName.Focus,
        SkillName.Necromancy,
        SkillName.Stealing,
        SkillName.Stealth,
        SkillName.SpiritSpeak,
        SkillName.ArmsLore,
        SkillName.Bushido,
        SkillName.Ninjitsu,
        SkillName.Spellweaving,
        SkillName.Mysticism,
        SkillName.Throwing,
        SkillName.Imbuing
    };

    private static readonly SkillName[] m_PossibleSpellbookSkills =
    {
        SkillName.Magery,
        SkillName.Meditation,
        SkillName.EvalInt,
        SkillName.MagicResist
    };

    private static readonly SkillName[] _mysticSpellbookSkills =
    {
        SkillName.Mysticism,
        SkillName.Focus,
        SkillName.Imbuing,
        SkillName.Meditation,
        SkillName.MagicResist
    };

    private static readonly SkillName[] _necroSpellbookSkills =
    {
        SkillName.Necromancy,
        SkillName.SpiritSpeak,
        SkillName.Meditation,
        SkillName.MagicResist
    };

    private static readonly SkillName[] _chivalrySpellbookSkills =
    {
        SkillName.Chivalry,
        SkillName.Focus,
        SkillName.Parry,
        SkillName.MagicResist
    };

    private static readonly SkillName[] _spellweavingSpellbookSkills =
    {
        SkillName.Spellweaving,
        SkillName.Meditation,
        SkillName.EvalInt,
        SkillName.MagicResist
    };

    private static readonly SkillName[] _bushidoSpellbookSkills =
    {
        SkillName.Bushido,
        SkillName.Parry,
        SkillName.Tactics,
        SkillName.Focus
    };

    private static readonly SkillName[] _ninjitsuSpellbookSkills =
    {
        SkillName.Ninjitsu,
        SkillName.Stealth,
        SkillName.Hiding,
        SkillName.Focus
    };

    private static readonly BitArray m_Props = new(MaxProperties);
    private static readonly int[] m_Possible = new int[MaxProperties];

    public BaseRunicTool(CraftResource resource, int itemID) : base(itemID) => _resource = resource;

    public BaseRunicTool(CraftResource resource, int uses, int itemID) : base(uses, itemID) => _resource = resource;

    [SerializableField(0, fieldChanged: nameof(OnResourceChanged))]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [InvalidateProperties]
    private CraftResource _resource;

    private void OnResourceChanged(CraftResource oldValue, CraftResource newValue)
    {
        Hue = CraftResources.GetHue(_resource);
    }

    private void Deserialize(IGenericReader reader, int version)
    {
        _resource = (CraftResource)reader.ReadInt();
    }

    public static int Scale(int min, int max, int low, int high, bool isRunicTool, int luckChance)
    {
        if (low >= high)
        {
            return low;
        }

        var effectiveMax = Math.Clamp(max, 0, 100);
        var effectiveMin = Math.Clamp(min, 0, effectiveMax);

        double t;

        if (isRunicTool)
        {
            // Upper-weighted distribution so runic crafting stays consistently above equivalent loot ranges.
            t = Math.Pow(Utility.RandomDouble(), 0.72);
        }
        else
        {
            // Smooth right-skewed curve inside [min, max] with asymptotic Luck uplift (no max-roll spike).
            t = Math.Pow(Utility.RandomDouble(), 1.35);

            if (luckChance > 0)
            {
                var luckShift = 0.22 * Math.Clamp(luckChance / 7000.0, 0.0, 1.0);
                t += (1.0 - t) * luckShift;
            }
        }

        var percent = (effectiveMin + t * (effectiveMax - effectiveMin)) / 100.0;
        var scaled = low + (int)Math.Floor(percent * (high - low + 1));

        return Math.Clamp(scaled, low, high);
    }

    private static int Scale(int min, int max, int low, int high) =>
        Scale(min, max, low, high, m_IsRunicTool, m_LuckChance);

    private static void ApplyRandomElementalEater(AosAttributes attrs, int min, int max, int high)
    {
        var eater = Utility.Random(5) switch
        {
            0 => AosAttribute.KineticEater,
            1 => AosAttribute.FireEater,
            2 => AosAttribute.ColdEater,
            3 => AosAttribute.PoisonEater,
            _ => AosAttribute.EnergyEater
        };

        ApplyAttribute(attrs, min, max, eater, 1, high);
    }

    private static void ApplyRandomElementalResonance(AosArmorAttributes attrs, int min, int max)
    {
        var resonance = Utility.Random(5) switch
        {
            0 => AosArmorAttribute.KineticResonance,
            1 => AosArmorAttribute.FireResonance,
            2 => AosArmorAttribute.ColdResonance,
            3 => AosArmorAttribute.PoisonResonance,
            _ => AosArmorAttribute.EnergyResonance
        };

        ApplyAttribute(attrs, min, max, resonance, 5, 20, 5);
    }

    private static void ApplyAttribute(
        AosAttributes attrs, int min, int max, AosAttribute attr, int low, int high,
        int scale = 1
    )
    {
        if (attr == AosAttribute.CastSpeed)
        {
            attrs[attr] += Scale(min, max, low / scale, high / scale) * scale;
        }
        else
        {
            attrs[attr] = Scale(min, max, low / scale, high / scale) * scale;
        }

        if (attr == AosAttribute.SpellChanneling)
        {
            attrs[AosAttribute.CastSpeed] -= 1;
        }
    }

    private static void ApplyAttribute(
        AosArmorAttributes attrs, int min, int max, AosArmorAttribute attr, int low,
        int high
    )
    {
        attrs[attr] = Scale(min, max, low, high);
    }

    private static void ApplyAttribute(
        AosArmorAttributes attrs, int min, int max, AosArmorAttribute attr, int low,
        int high, int scale
    )
    {
        attrs[attr] = Scale(min, max, low / scale, high / scale) * scale;
    }

    private static void ApplyAttribute(
        AosWeaponAttributes attrs, int min, int max, AosWeaponAttribute attr, int low,
        int high
    )
    {
        attrs[attr] = Scale(min, max, low, high);
    }

    private static void ApplyAttribute(
        AosWeaponAttributes attrs, int min, int max, AosWeaponAttribute attr, int low,
        int high, int scale
    )
    {
        attrs[attr] = Scale(min, max, low / scale, high / scale) * scale;
    }

    private static void ApplyAttribute(
        AosElementAttributes attrs, int min, int max, AosElementAttribute attr, int low,
        int high
    )
    {
        attrs[attr] = Scale(min, max, low, high);
    }

    private static void ApplyAttribute(
        AosElementAttributes attrs, int min, int max, AosElementAttribute attr, int low,
        int high, int scale
    )
    {
        attrs[attr] = Scale(min, max, low / scale, high / scale) * scale;
    }

    private static void ApplyAdditiveAttribute(
        AosElementAttributes attrs, int min, int max, AosElementAttribute attr, int low,
        int high
    )
    {
        attrs[attr] += Scale(min, max, low, high);
    }

    private static void ApplySkillBonus(AosSkillBonuses attrs, int min, int max, int index, int low, int high)
    {
        var isSpellBook = attrs.Owner is Spellbook;
        var source = attrs.Owner switch
        {
            MysticSpellbook      => _mysticSpellbookSkills,
            NecromancerSpellbook => _necroSpellbookSkills,
            BookOfChivalry       => _chivalrySpellbookSkills,
            SpellweavingBook     => _spellweavingSpellbookSkills,
            BookOfBushido        => _bushidoSpellbookSkills,
            BookOfNinjitsu       => _ninjitsuSpellbookSkills,
            Spellbook            => m_PossibleSpellbookSkills,
            _                    => m_PossibleBonusSkills
        };

        var count = source.Length;

        if (!isSpellBook)
        {
            if (!Core.SE)
            {
                count -= 6;
            }
            else if (!Core.ML)
            {
                count -= 4;
            }
            else if (!Core.SA)
            {
                count -= 3;
            }
        }

        Span<SkillName> pool = stackalloc SkillName[count];
        source.AsSpan(0, count).CopyTo(pool);

        var remaining = count;
        SkillName sk;
        bool found;

        do
        {
            found = false;
            var pick = Utility.Random(remaining);
            sk = pool[pick];
            pool[pick] = pool[--remaining];

            for (var i = 0; !found && i < 5; ++i)
            {
                found = attrs.GetValues(i, out var check, out _) && check == sk;
            }
        } while (found && remaining > 0);

        if (found)
        {
            return;
        }

        attrs.SetValues(index, sk, Scale(min, max, low, high));
    }

    private static void ApplyResistance(BaseArmor ar, int min, int max, ResistanceType res, int low, int high)
    {
        switch (res)
        {
            case ResistanceType.Physical:
                {
                    ar.PhysicalBonus += Scale(min, max, low, high);
                    break;
                }
            case ResistanceType.Fire:
                {
                    ar.FireBonus += Scale(min, max, low, high);
                    break;
                }
            case ResistanceType.Cold:
                {
                    ar.ColdBonus += Scale(min, max, low, high);
                    break;
                }
            case ResistanceType.Poison:
                {
                    ar.PoisonBonus += Scale(min, max, low, high);
                    break;
                }
            case ResistanceType.Energy:
                {
                    ar.EnergyBonus += Scale(min, max, low, high);
                    break;
                }
        }
    }

    public static int GetUniqueRandom(int count)
    {
        if (count <= 0)
        {
            return -1;
        }

        if (count > MaxProperties)
        {
            count = MaxProperties;
        }

        var avail = 0;

        for (var i = 0; i < count; ++i)
        {
            if (!m_Props[i])
            {
                m_Possible[avail++] = i;
            }
        }

        if (avail == 0)
        {
            return -1;
        }

        var v = m_Possible[Utility.Random(avail)];

        m_Props.Set(v, true);

        return v;
    }

    private static int GetWeightedUniqueRandom(ReadOnlySpan<int> weights)
    {
        var totalWeight = 0;

        for (var i = 0; i < weights.Length; ++i)
        {
            if (!m_Props[i] && weights[i] > 0)
            {
                totalWeight += weights[i];
            }
        }

        if (totalWeight <= 0)
        {
            return -1;
        }

        var roll = Utility.Random(totalWeight);

        for (var i = 0; i < weights.Length; ++i)
        {
            if (m_Props[i] || weights[i] <= 0)
            {
                continue;
            }

            if (roll < weights[i])
            {
                m_Props.Set(i, true);
                return i;
            }

            roll -= weights[i];
        }

        return -1;
    }

    public void ApplyAttributesTo(BaseWeapon weapon)
    {
        var resInfo = CraftResources.GetInfo(_resource);

        var attrs = resInfo?.AttributeInfo;

        if (attrs == null)
        {
            return;
        }

        var attributeCount = Utility.RandomMinMax(attrs.RunicMinAttributes, attrs.RunicMaxAttributes);
        var min = attrs.RunicMinIntensity;
        var max = attrs.RunicMaxIntensity;

        ApplyAttributesTo(weapon, true, 0, attributeCount, min, max);
    }

    public static void ApplyAttributesTo(BaseWeapon weapon, int attributeCount, int min, int max)
    {
        ApplyAttributesTo(weapon, false, 0, attributeCount, min, max);
    }

    public static void ApplyAttributesTo(
        BaseWeapon weapon, bool isRunicTool, int luckChance, int attributeCount,
        int min, int max
    )
    {
        m_IsRunicTool = isRunicTool;
        m_LuckChance = luckChance;

        var primary = weapon.Attributes;
        var secondary = weapon.WeaponAttributes;
        var apex = max > 100;
        var highTier = max >= 75;
        var midTier = max >= 50;
        var fillerWeight = apex ? 0 : highTier ? 1 : 6;
        var procMax = apex ? 60 : 50;
        var resistMax = apex ? 18 : 15;

        m_Props.SetAll(false);

        if (weapon is BaseRanged)
        {
            m_Props.Set(2, true); // ranged weapons cannot be ubws or mageweapon
        }

        Span<int> weights =
        [
            10,                                                                // 0: HitArea
            10,                                                                // 1: HitSpell
            8,                                                                 // 2: UseBestSkill / MageWeapon
            14,                                                                // 3: WeaponDamage
            10,                                                                // 4: DefendChance
            7,                                                                 // 5: CastSpeed
            12,                                                                // 6: AttackChance
            8,                                                                 // 7: Luck
            13,                                                                // 8: WeaponSpeed
            7,                                                                 // 9: SpellChanneling
            7,                                                                 // 10: HitDispel
            12,                                                                // 11: HitLeechHits
            9,                                                                 // 12: HitLowerAttack
            11,                                                                // 13: HitLowerDefend
            12,                                                                // 14: HitLeechMana
            11,                                                                // 15: HitLeechStam
            fillerWeight,                                                      // 16: LowerStatReq
            6,                                                                 // 17: ResistPhysicalBonus
            6,                                                                 // 18: ResistFireBonus
            6,                                                                 // 19: ResistColdBonus
            6,                                                                 // 20: ResistPoisonBonus
            6,                                                                 // 21: ResistEnergyBonus
            fillerWeight,                                                      // 22: DurabilityBonus
            9,                                                                 // 23: Slayer
            7,                                                                 // 24: ElementalDamages
            Core.ML && midTier && weapon is BaseRanged and not BaseThrown ? 12 : 0, // 25: Balanced
            Core.ML && midTier && weapon is BaseRanged ? 14 : 0,               // 26: Velocity
            Core.SA && midTier ? 8 : 0,                                        // 27: HitCurse
            Core.SA && midTier ? 8 : 0,                                        // 28: HitFatigue
            Core.SA && midTier ? 8 : 0,                                        // 29: HitManaDrain
            Core.SA && highTier ? 9 : 0,                                       // 30: SplinteringWeapon
            Core.SA && highTier ? 8 : 0,                                       // 31: BattleLust
            Core.SA && highTier ? 8 : 0,                                       // 32: BloodDrinker
            Core.SA && highTier ? (apex ? 8 : 4) : 0                           // 33: Bane
        ];

        for (var i = 0; i < attributeCount; ++i)
        {
            var random = GetWeightedUniqueRandom(weights);

            if (random == -1)
            {
                break;
            }

            switch (random)
            {
                case 0:
                    {
                        switch (Utility.Random(5))
                        {
                            case 0:
                                {
                                    ApplyAttribute(secondary, min, max, AosWeaponAttribute.HitPhysicalArea, 2, procMax, 2);
                                    break;
                                }
                            case 1:
                                {
                                    ApplyAttribute(secondary, min, max, AosWeaponAttribute.HitFireArea, 2, procMax, 2);
                                    break;
                                }
                            case 2:
                                {
                                    ApplyAttribute(secondary, min, max, AosWeaponAttribute.HitColdArea, 2, procMax, 2);
                                    break;
                                }
                            case 3:
                                {
                                    ApplyAttribute(secondary, min, max, AosWeaponAttribute.HitPoisonArea, 2, procMax, 2);
                                    break;
                                }
                            case 4:
                                {
                                    ApplyAttribute(secondary, min, max, AosWeaponAttribute.HitEnergyArea, 2, procMax, 2);
                                    break;
                                }
                        }

                        break;
                    }
                case 1:
                    {
                        switch (Utility.Random(4))
                        {
                            case 0:
                                {
                                    ApplyAttribute(secondary, min, max, AosWeaponAttribute.HitMagicArrow, 2, procMax, 2);
                                    break;
                                }
                            case 1:
                                {
                                    ApplyAttribute(secondary, min, max, AosWeaponAttribute.HitHarm, 2, procMax, 2);
                                    break;
                                }
                            case 2:
                                {
                                    ApplyAttribute(secondary, min, max, AosWeaponAttribute.HitFireball, 2, procMax, 2);
                                    break;
                                }
                            case 3:
                                {
                                    ApplyAttribute(secondary, min, max, AosWeaponAttribute.HitLightning, 2, procMax, 2);
                                    break;
                                }
                        }

                        break;
                    }
                case 2:
                    {
                        switch (Utility.Random(2))
                        {
                            case 0:
                                {
                                    ApplyAttribute(secondary, min, max, AosWeaponAttribute.UseBestSkill, 1, 1);
                                    break;
                                }
                            case 1:
                                {
                                    ApplyAttribute(secondary, min, max, AosWeaponAttribute.MageWeapon, 1, 10);
                                    break;
                                }
                        }

                        break;
                    }
                case 3:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.WeaponDamage, 1, apex ? 60 : 50);
                        break;
                    }
                case 4:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.DefendChance, 1, apex ? 20 : 15);
                        break;
                    }
                case 5:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.CastSpeed, 1, 1);
                        break;
                    }
                case 6:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.AttackChance, 1, apex ? 20 : 15);
                        break;
                    }
                case 7:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.Luck, 1, apex ? 150 : 100);
                        break;
                    }
                case 8:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.WeaponSpeed, 5, apex ? 40 : 30, 5);
                        break;
                    }
                case 9:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.SpellChanneling, 1, 1);
                        break;
                    }
                case 10:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.HitDispel, 2, procMax, 2);
                        break;
                    }
                case 11:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.HitLeechHits, 2, procMax, 2);
                        break;
                    }
                case 12:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.HitLowerAttack, 2, procMax, 2);
                        break;
                    }
                case 13:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.HitLowerDefend, 2, procMax, 2);
                        break;
                    }
                case 14:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.HitLeechMana, 2, procMax, 2);
                        break;
                    }
                case 15:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.HitLeechStam, 2, procMax, 2);
                        break;
                    }
                case 16:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.LowerStatReq, 10, 100, 10);
                        break;
                    }
                case 17:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.ResistPhysicalBonus, 1, resistMax);
                        break;
                    }
                case 18:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.ResistFireBonus, 1, resistMax);
                        break;
                    }
                case 19:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.ResistColdBonus, 1, resistMax);
                        break;
                    }
                case 20:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.ResistPoisonBonus, 1, resistMax);
                        break;
                    }
                case 21:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.ResistEnergyBonus, 1, resistMax);
                        break;
                    }
                case 22:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.DurabilityBonus, 10, 100, 10);
                        break;
                    }
                case 23:
                    {
                        weapon.Slayer = GetRandomSlayer();
                        break;
                    }
                case 24:
                    {
                        GetElementalDamages(weapon);
                        break;
                    }
                case 25:
                    {
                        if (weapon is BaseRanged balancedRanged)
                        {
                            balancedRanged.Balanced = true;
                        }

                        break;
                    }
                case 26:
                    {
                        if (weapon is BaseRanged velocityRanged)
                        {
                            velocityRanged.Velocity = Scale(min, max, 5, apex ? 30 : 25) * 2;
                        }

                        break;
                    }
                case 27:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.HitCurse, 2, procMax, 2);
                        break;
                    }
                case 28:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.HitFatigue, 2, procMax, 2);
                        break;
                    }
                case 29:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.HitManaDrain, 2, procMax, 2);
                        break;
                    }
                case 30:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.SplinteringWeapon, 10, 30, 5);
                        break;
                    }
                case 31:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.BattleLust, 1, 1);
                        break;
                    }
                case 32:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.BloodDrinker, 1, 1);
                        break;
                    }
                case 33:
                    {
                        ApplyAttribute(secondary, min, max, AosWeaponAttribute.Bane, 1, 1);
                        break;
                    }
            }
        }
    }

    public static void GetElementalDamages(BaseWeapon weapon)
    {
        GetElementalDamages(weapon, true);
    }

    public static void GetElementalDamages(BaseWeapon weapon, bool randomizeOrder)
    {
        weapon.GetDamageTypes(null, out var phys, out _, out _, out _, out _, out _, out _);

        var totalDamage = phys;

        AosElementAttribute[] attrs =
        {
            AosElementAttribute.Cold,
            AosElementAttribute.Energy,
            AosElementAttribute.Fire,
            AosElementAttribute.Poison
        };

        if (randomizeOrder)
        {
            for (var i = 0; i < attrs.Length; i++)
            {
                var temp = attrs[i];
                var rand = Utility.Random(attrs.Length);

                attrs[i] = attrs[rand];
                attrs[rand] = temp;
            }
        }

        /*
          totalDamage = AssignElementalDamage( weapon, AosElementAttribute.Cold, totalDamage );
          totalDamage = AssignElementalDamage( weapon, AosElementAttribute.Energy, totalDamage );
          totalDamage = AssignElementalDamage( weapon, AosElementAttribute.Fire, totalDamage );
          totalDamage = AssignElementalDamage( weapon, AosElementAttribute.Poison, totalDamage );

          weapon.AosElementDamages[AosElementAttribute.Physical] = 100 - totalDamage;
         * */

        for (var i = 0; i < attrs.Length; i++)
        {
            totalDamage = AssignElementalDamage(weapon, attrs[i], totalDamage);
        }

        // Order is Cold, Energy, Fire, Poison -> Physical left
        // Cannot be looped, AoselementAttribute is 'out of order'

        weapon.Hue = weapon.GetElementalDamageHue();
    }

    private static int AssignElementalDamage(BaseWeapon weapon, AosElementAttribute attr, int totalDamage)
    {
        if (totalDamage <= 0)
        {
            return 0;
        }

        var random = Utility.Random(totalDamage / 10 + 1) * 10;
        weapon.AosElementDamages[attr] = random;

        return totalDamage - random;
    }

    public static SlayerName GetRandomSlayer()
    {
        // TODO: Check random algorithm on OSI

        var groups = SlayerGroup.Groups;

        if (groups.Length == 0)
        {
            return SlayerName.None;
        }

        // 10% chance of a super - skip fey
        if (Utility.RandomDouble() < 0.10)
        {
            return groups[Utility.Random(groups.Length - 1)].Super.Name;
        }

        // Minor slayer - skip fey and undead
        return groups[Utility.Random(groups.Length - 2)].Entries.RandomElement().Name;
    }

    public void ApplyAttributesTo(BaseArmor armor)
    {
        var resInfo = CraftResources.GetInfo(_resource);

        var attrs = resInfo?.AttributeInfo;

        if (attrs == null)
        {
            return;
        }

        var attributeCount = Utility.RandomMinMax(attrs.RunicMinAttributes, attrs.RunicMaxAttributes);
        var min = attrs.RunicMinIntensity;
        var max = attrs.RunicMaxIntensity;

        ApplyAttributesTo(armor, true, 0, attributeCount, min, max);
    }

    public static void ApplyAttributesTo(BaseArmor armor, int attributeCount, int min, int max)
    {
        ApplyAttributesTo(armor, false, 0, attributeCount, min, max);
    }

    public static void ApplyAttributesTo(
        BaseArmor armor, bool isRunicTool, int luckChance, int attributeCount, int min,
        int max
    )
    {
        m_IsRunicTool = isRunicTool;
        m_LuckChance = luckChance;

        var primary = armor.Attributes;
        var secondary = armor.ArmorAttributes;
        var apex = max > 100;
        var highTier = max >= 75;
        var midTier = max >= 50;
        var fillerWeight = apex ? 0 : highTier ? 1 : 5;
        var selfRepairWeight = apex ? 0 : highTier ? 2 : 6;
        var resistMax = apex ? 18 : 15;

        m_Props.SetAll(false);

        if (armor is BaseShield)
        {
            Span<int> shieldWeights =
            [
                10,                             // 0: SpellChanneling
                14,                             // 1: DefendChance
                10,                             // 2: ReflectPhysical / AttackChance
                9,                              // 3: CastSpeed
                fillerWeight,                   // 4: LowerStatReq
                selfRepairWeight,               // 5: SelfRepair
                fillerWeight,                   // 6: DurabilityBonus
                Core.SA && midTier ? 10 : 0,    // 7: SoulCharge
                Core.SA && highTier ? 9 : 0,    // 8: ReactiveParalyze
                Core.SA && midTier ? 8 : 0,     // 9: DamageEater
                Core.SA && midTier ? 9 : 0,     // 10: Elemental Eater
                Core.SA && midTier ? 7 : 0      // 11: CastingFocus
            ];

            for (var i = 0; i < attributeCount; ++i)
            {
                var random = GetWeightedUniqueRandom(shieldWeights);

                if (random == -1)
                {
                    break;
                }

                switch (random)
                {
                    case 0:
                        {
                            ApplyAttribute(primary, min, max, AosAttribute.SpellChanneling, 1, 1);
                            break;
                        }
                    case 1:
                        {
                            ApplyAttribute(primary, min, max, AosAttribute.DefendChance, 1, apex ? 20 : 15);
                            break;
                        }
                    case 2:
                        {
                            if (Core.ML)
                            {
                                ApplyAttribute(primary, min, max, AosAttribute.ReflectPhysical, 1, apex ? 20 : 15);
                            }
                            else
                            {
                                ApplyAttribute(primary, min, max, AosAttribute.AttackChance, 1, apex ? 20 : 15);
                            }

                            break;
                        }
                    case 3:
                        {
                            ApplyAttribute(primary, min, max, AosAttribute.CastSpeed, 1, 1);
                            break;
                        }
                    case 4:
                        {
                            ApplyAttribute(secondary, min, max, AosArmorAttribute.LowerStatReq, 10, 100, 10);
                            break;
                        }
                    case 5:
                        {
                            ApplyAttribute(secondary, min, max, AosArmorAttribute.SelfRepair, 1, 5);
                            break;
                        }
                    case 6:
                        {
                            ApplyAttribute(secondary, min, max, AosArmorAttribute.DurabilityBonus, 10, 100, 10);
                            break;
                        }
                    case 7:
                        {
                            ApplyAttribute(secondary, min, max, AosArmorAttribute.SoulCharge, 5, apex ? 30 : 25, 5);
                            break;
                        }
                    case 8:
                        {
                            ApplyAttribute(secondary, min, max, AosArmorAttribute.ReactiveParalyze, 1, 1);
                            break;
                        }
                    case 9:
                        {
                            ApplyAttribute(primary, min, max, AosAttribute.DamageEater, 1, apex ? 8 : 6);
                            break;
                        }
                    case 10:
                        {
                            ApplyRandomElementalEater(primary, min, max, apex ? 18 : 15);
                            break;
                        }
                    case 11:
                        {
                            ApplyAttribute(primary, min, max, AosAttribute.CastingFocus, 1, apex ? 8 : 6);
                            break;
                        }
                }
            }

            return;
        }

        var isLeather = CraftResources.GetType(armor.Resource) == CraftResourceType.Leather;
        var canMageArmor = armor.MeditationAllowance != ArmorMeditationAllowance.All;
        var canNightSight = armor.RequiredRaces != Race.AllowElvesOnly;
        var nightSightWeight = canNightSight ? (apex ? 0 : highTier ? 1 : 4) : 0;

        Span<int> armorWeights =
        [
            isLeather ? 0 : fillerWeight,   // 0: LowerStatReq
            selfRepairWeight,               // 1: SelfRepair
            isLeather ? 0 : fillerWeight,   // 2: DurabilityBonus
            canMageArmor ? 9 : 0,           // 3: MageArmor
            11,                             // 4: RegenHits
            10,                             // 5: RegenStam
            11,                             // 6: RegenMana
            nightSightWeight,               // 7: NightSight
            11,                             // 8: BonusHits
            10,                             // 9: BonusStam
            11,                             // 10: BonusMana
            13,                             // 11: LowerManaCost
            12,                             // 12: LowerRegCost
            9,                              // 13: Luck
            7,                              // 14: ReflectPhysical
            10,                             // 15: Physical Resist
            10,                             // 16: Fire Resist
            10,                             // 17: Cold Resist
            10,                             // 18: Poison Resist
            10,                             // 19: Energy Resist
            Core.SA && midTier ? 9 : 0,     // 20: CastingFocus
            Core.SA && highTier ? 8 : 0,    // 21: DamageEater
            Core.SA && midTier ? 10 : 0,    // 22: Elemental Eater
            Core.SA && midTier ? 8 : 0,     // 23: SoulCharge
            Core.SA && highTier ? 6 : 0,    // 24: ReactiveParalyze
            Core.SA && highTier ? 7 : 0,    // 25: ManaPhase
            Core.SA && midTier ? 8 : 0      // 26: Elemental Resonance
        ];

        for (var i = 0; i < attributeCount; ++i)
        {
            var random = GetWeightedUniqueRandom(armorWeights);

            if (random == -1)
            {
                break;
            }

            switch (random)
            {
                case 0:
                    {
                        ApplyAttribute(secondary, min, max, AosArmorAttribute.LowerStatReq, 10, 100, 10);
                        break;
                    }
                case 1:
                    {
                        ApplyAttribute(secondary, min, max, AosArmorAttribute.SelfRepair, 1, 5);
                        break;
                    }
                case 2:
                    {
                        ApplyAttribute(secondary, min, max, AosArmorAttribute.DurabilityBonus, 10, 100, 10);
                        break;
                    }
                case 3:
                    {
                        ApplyAttribute(secondary, min, max, AosArmorAttribute.MageArmor, 1, 1);
                        break;
                    }
                case 4:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.RegenHits, 1, apex ? 4 : 2);
                        break;
                    }
                case 5:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.RegenStam, 1, apex ? 4 : 3);
                        break;
                    }
                case 6:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.RegenMana, 1, apex ? 4 : 2);
                        break;
                    }
                case 7:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.NightSight, 1, 1);
                        break;
                    }
                case 8:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.BonusHits, 1, apex ? 10 : 5);
                        break;
                    }
                case 9:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.BonusStam, 1, apex ? 10 : 8);
                        break;
                    }
                case 10:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.BonusMana, 1, apex ? 10 : 8);
                        break;
                    }
                case 11:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.LowerManaCost, 1, apex ? 10 : 8);
                        break;
                    }
                case 12:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.LowerRegCost, 1, apex ? 25 : 20);
                        break;
                    }
                case 13:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.Luck, 1, apex ? 150 : 100);
                        break;
                    }
                case 14:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.ReflectPhysical, 1, apex ? 20 : 15);
                        break;
                    }
                case 15:
                    {
                        ApplyResistance(armor, min, max, ResistanceType.Physical, 1, resistMax);
                        break;
                    }
                case 16:
                    {
                        ApplyResistance(armor, min, max, ResistanceType.Fire, 1, resistMax);
                        break;
                    }
                case 17:
                    {
                        ApplyResistance(armor, min, max, ResistanceType.Cold, 1, resistMax);
                        break;
                    }
                case 18:
                    {
                        ApplyResistance(armor, min, max, ResistanceType.Poison, 1, resistMax);
                        break;
                    }
                case 19:
                    {
                        ApplyResistance(armor, min, max, ResistanceType.Energy, 1, resistMax);
                        break;
                    }
                case 20:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.CastingFocus, 1, apex ? 8 : 6);
                        break;
                    }
                case 21:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.DamageEater, 1, apex ? 8 : 6);
                        break;
                    }
                case 22:
                    {
                        ApplyRandomElementalEater(primary, min, max, apex ? 18 : 15);
                        break;
                    }
                case 23:
                    {
                        ApplyAttribute(secondary, min, max, AosArmorAttribute.SoulCharge, 5, apex ? 30 : 25, 5);
                        break;
                    }
                case 24:
                    {
                        ApplyAttribute(secondary, min, max, AosArmorAttribute.ReactiveParalyze, 1, 1);
                        break;
                    }
                case 25:
                    {
                        ApplyAttribute(secondary, min, max, AosArmorAttribute.ManaPhase, 1, 1);
                        break;
                    }
                case 26:
                    {
                        ApplyRandomElementalResonance(secondary, min, max);
                        break;
                    }
            }
        }
    }

    public static void ApplyAttributesTo(BaseHat hat, int attributeCount, int min, int max)
    {
        ApplyAttributesTo(hat, false, 0, attributeCount, min, max);
    }

    public static void ApplyAttributesTo(
        BaseHat hat, bool isRunicTool, int luckChance, int attributeCount, int min,
        int max
    )
    {
        m_IsRunicTool = isRunicTool;
        m_LuckChance = luckChance;

        var primary = hat.Attributes;
        var secondary = hat.ClothingAttributes;
        var resists = hat.Resistances;
        var apex = max > 100;
        var highTier = max >= 75;
        var midTier = max >= 50;
        var fillerWeight = apex ? 0 : highTier ? 1 : 5;
        var selfRepairWeight = apex ? 0 : highTier ? 2 : 6;
        var nightSightWeight = apex ? 0 : highTier ? 1 : 4;
        var resistMax = apex ? 18 : 15;

        m_Props.SetAll(false);

        Span<int> weights =
        [
            7,                              // 0: ReflectPhysical
            11,                             // 1: RegenHits
            10,                             // 2: RegenStam
            11,                             // 3: RegenMana
            nightSightWeight,               // 4: NightSight
            11,                             // 5: BonusHits
            10,                             // 6: BonusStam
            11,                             // 7: BonusMana
            13,                             // 8: LowerManaCost
            12,                             // 9: LowerRegCost
            9,                              // 10: Luck
            fillerWeight,                   // 11: LowerStatReq
            selfRepairWeight,               // 12: SelfRepair
            fillerWeight,                   // 13: DurabilityBonus
            10,                             // 14: Physical Resist
            10,                             // 15: Fire Resist
            10,                             // 16: Cold Resist
            10,                             // 17: Poison Resist
            10,                             // 18: Energy Resist
            Core.SA && midTier ? 10 : 0,    // 19: CastingFocus
            Core.SA && highTier ? 8 : 0,    // 20: DamageEater
            Core.SA && midTier ? 10 : 0,    // 21: Elemental Eater
            Core.SA && midTier ? 8 : 0,     // 22: SoulCharge
            Core.SA && highTier ? 9 : 0,    // 23: ManaPhase
            Core.SA && midTier ? 9 : 0      // 24: Elemental Resonance
        ];

        for (var i = 0; i < attributeCount; ++i)
        {
            var random = GetWeightedUniqueRandom(weights);

            if (random == -1)
            {
                break;
            }

            switch (random)
            {
                case 0:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.ReflectPhysical, 1, apex ? 20 : 15);
                        break;
                    }
                case 1:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.RegenHits, 1, apex ? 4 : 2);
                        break;
                    }
                case 2:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.RegenStam, 1, apex ? 4 : 3);
                        break;
                    }
                case 3:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.RegenMana, 1, apex ? 4 : 2);
                        break;
                    }
                case 4:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.NightSight, 1, 1);
                        break;
                    }
                case 5:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.BonusHits, 1, apex ? 10 : 5);
                        break;
                    }
                case 6:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.BonusStam, 1, apex ? 10 : 8);
                        break;
                    }
                case 7:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.BonusMana, 1, apex ? 10 : 8);
                        break;
                    }
                case 8:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.LowerManaCost, 1, apex ? 10 : 8);
                        break;
                    }
                case 9:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.LowerRegCost, 1, apex ? 25 : 20);
                        break;
                    }
                case 10:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.Luck, 1, apex ? 150 : 100);
                        break;
                    }
                case 11:
                    {
                        ApplyAttribute(secondary, min, max, AosArmorAttribute.LowerStatReq, 10, 100, 10);
                        break;
                    }
                case 12:
                    {
                        ApplyAttribute(secondary, min, max, AosArmorAttribute.SelfRepair, 1, 5);
                        break;
                    }
                case 13:
                    {
                        ApplyAttribute(secondary, min, max, AosArmorAttribute.DurabilityBonus, 10, 100, 10);
                        break;
                    }
                case 14:
                    {
                        ApplyAdditiveAttribute(resists, min, max, AosElementAttribute.Physical, 1, resistMax);
                        break;
                    }
                case 15:
                    {
                        ApplyAdditiveAttribute(resists, min, max, AosElementAttribute.Fire, 1, resistMax);
                        break;
                    }
                case 16:
                    {
                        ApplyAdditiveAttribute(resists, min, max, AosElementAttribute.Cold, 1, resistMax);
                        break;
                    }
                case 17:
                    {
                        ApplyAdditiveAttribute(resists, min, max, AosElementAttribute.Poison, 1, resistMax);
                        break;
                    }
                case 18:
                    {
                        ApplyAdditiveAttribute(resists, min, max, AosElementAttribute.Energy, 1, resistMax);
                        break;
                    }
                case 19:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.CastingFocus, 1, apex ? 8 : 6);
                        break;
                    }
                case 20:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.DamageEater, 1, apex ? 8 : 6);
                        break;
                    }
                case 21:
                    {
                        ApplyRandomElementalEater(primary, min, max, apex ? 18 : 15);
                        break;
                    }
                case 22:
                    {
                        ApplyAttribute(secondary, min, max, AosArmorAttribute.SoulCharge, 5, apex ? 30 : 25, 5);
                        break;
                    }
                case 23:
                    {
                        ApplyAttribute(secondary, min, max, AosArmorAttribute.ManaPhase, 1, 1);
                        break;
                    }
                case 24:
                    {
                        ApplyRandomElementalResonance(secondary, min, max);
                        break;
                    }
            }
        }
    }

    public static void ApplyAttributesTo(BaseJewel jewelry, int attributeCount, int min, int max)
    {
        ApplyAttributesTo(jewelry, false, 0, attributeCount, min, max);
    }

    public static void ApplyAttributesTo(
        BaseJewel jewelry, bool isRunicTool, int luckChance, int attributeCount,
        int min, int max
    )
    {
        m_IsRunicTool = isRunicTool;
        m_LuckChance = luckChance;

        var primary = jewelry.Attributes;
        var resists = jewelry.Resistances;
        var skills = jewelry.SkillBonuses;
        var apex = max > 100;
        var highTier = max >= 75;
        var midTier = max >= 50;
        var nightSightWeight = apex ? 0 : highTier ? 1 : 4;
        var resistMax = apex ? 18 : 15;
        var skillMax = apex ? 18 : 15;

        m_Props.SetAll(false);

        Span<int> weights =
        [
            8,                              // 0: Physical Resist
            8,                              // 1: Fire Resist
            8,                              // 2: Cold Resist
            8,                              // 3: Poison Resist
            8,                              // 4: Energy Resist
            12,                             // 5: WeaponDamage
            12,                             // 6: DefendChance
            12,                             // 7: AttackChance
            9,                              // 8: BonusStr
            9,                              // 9: BonusDex
            9,                              // 10: BonusInt
            7,                              // 11: EnhancePotions
            11,                             // 12: CastSpeed
            11,                             // 13: CastRecovery
            11,                             // 14: LowerManaCost
            11,                             // 15: LowerRegCost
            9,                              // 16: Luck
            12,                             // 17: SpellDamage
            nightSightWeight,               // 18: NightSight
            8,                              // 19: Skill 0
            8,                              // 20: Skill 1
            8,                              // 21: Skill 2
            8,                              // 22: Skill 3
            8,                              // 23: Skill 4
            Core.SA && midTier ? 9 : 0,     // 24: CastingFocus
            Core.SA && highTier ? 8 : 0     // 25: DamageEater
        ];

        for (var i = 0; i < attributeCount; ++i)
        {
            var random = GetWeightedUniqueRandom(weights);

            if (random == -1)
            {
                break;
            }

            switch (random)
            {
                case 0:
                    {
                        ApplyAttribute(resists, min, max, AosElementAttribute.Physical, 1, resistMax);
                        break;
                    }
                case 1:
                    {
                        ApplyAttribute(resists, min, max, AosElementAttribute.Fire, 1, resistMax);
                        break;
                    }
                case 2:
                    {
                        ApplyAttribute(resists, min, max, AosElementAttribute.Cold, 1, resistMax);
                        break;
                    }
                case 3:
                    {
                        ApplyAttribute(resists, min, max, AosElementAttribute.Poison, 1, resistMax);
                        break;
                    }
                case 4:
                    {
                        ApplyAttribute(resists, min, max, AosElementAttribute.Energy, 1, resistMax);
                        break;
                    }
                case 5:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.WeaponDamage, 1, apex ? 30 : 25);
                        break;
                    }
                case 6:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.DefendChance, 1, apex ? 20 : 15);
                        break;
                    }
                case 7:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.AttackChance, 1, apex ? 20 : 15);
                        break;
                    }
                case 8:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.BonusStr, 1, apex ? 10 : 8);
                        break;
                    }
                case 9:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.BonusDex, 1, apex ? 10 : 8);
                        break;
                    }
                case 10:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.BonusInt, 1, apex ? 10 : 8);
                        break;
                    }
                case 11:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.EnhancePotions, 5, apex ? 30 : 25, 5);
                        break;
                    }
                case 12:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.CastSpeed, 1, 1);
                        break;
                    }
                case 13:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.CastRecovery, 1, 3);
                        break;
                    }
                case 14:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.LowerManaCost, 1, apex ? 10 : 8);
                        break;
                    }
                case 15:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.LowerRegCost, 1, apex ? 25 : 20);
                        break;
                    }
                case 16:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.Luck, 1, apex ? 150 : 100);
                        break;
                    }
                case 17:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.SpellDamage, 1, apex ? 18 : 12);
                        break;
                    }
                case 18:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.NightSight, 1, 1);
                        break;
                    }
                case 19:
                    {
                        ApplySkillBonus(skills, min, max, 0, 1, skillMax);
                        break;
                    }
                case 20:
                    {
                        ApplySkillBonus(skills, min, max, 1, 1, skillMax);
                        break;
                    }
                case 21:
                    {
                        ApplySkillBonus(skills, min, max, 2, 1, skillMax);
                        break;
                    }
                case 22:
                    {
                        ApplySkillBonus(skills, min, max, 3, 1, skillMax);
                        break;
                    }
                case 23:
                    {
                        ApplySkillBonus(skills, min, max, 4, 1, skillMax);
                        break;
                    }
                case 24:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.CastingFocus, 1, apex ? 5 : 4);
                        break;
                    }
                case 25:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.DamageEater, 1, apex ? 5 : 4);
                        break;
                    }
            }
        }
    }

    public static void ApplyAttributesTo(Spellbook spellbook, int attributeCount, int min, int max)
    {
        ApplyAttributesTo(spellbook, false, 0, attributeCount, min, max);
    }

    public static void ApplyAttributesTo(
        Spellbook spellbook, bool isRunicTool, int luckChance, int attributeCount,
        int min, int max
    )
    {
        m_IsRunicTool = isRunicTool;
        m_LuckChance = luckChance;

        var primary = spellbook.Attributes;
        var skills = spellbook.SkillBonuses;
        var apex = max > 100;
        var midTier = max >= 50;
        var skillMax = apex ? 18 : 15;

        m_Props.SetAll(false);

        Span<int> weights =
        [
            14,                             // 0: BonusInt
            10,                             // 1: BonusMana
            11,                             // 2: CastSpeed
            11,                             // 3: CastRecovery
            14,                             // 4: SpellDamage
            8,                              // 5: Skill 0
            8,                              // 6: Skill 1
            8,                              // 7: Skill 2
            8,                              // 8: Skill 3
            12,                             // 9: LowerRegCost
            12,                             // 10: LowerManaCost
            12,                             // 11: RegenMana
            10,                             // 12: Slayer
            Core.SA && midTier ? 11 : 0     // 13: CastingFocus
        ];

        for (var i = 0; i < attributeCount; ++i)
        {
            var random = GetWeightedUniqueRandom(weights);

            if (random == -1)
            {
                break;
            }

            switch (random)
            {
                case 0:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.BonusInt, 1, apex ? 10 : 8);
                        break;
                    }
                case 1:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.BonusMana, 1, apex ? 10 : 8);
                        break;
                    }
                case 2:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.CastSpeed, 1, 1);
                        break;
                    }
                case 3:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.CastRecovery, 1, 3);
                        break;
                    }
                case 4:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.SpellDamage, 1, apex ? 18 : 12);
                        break;
                    }
                case 5:
                    {
                        ApplySkillBonus(skills, min, max, 0, 1, skillMax);
                        break;
                    }
                case 6:
                    {
                        ApplySkillBonus(skills, min, max, 1, 1, skillMax);
                        break;
                    }
                case 7:
                    {
                        ApplySkillBonus(skills, min, max, 2, 1, skillMax);
                        break;
                    }
                case 8:
                    {
                        ApplySkillBonus(skills, min, max, 3, 1, skillMax);
                        break;
                    }
                case 9:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.LowerRegCost, 1, apex ? 25 : 20);
                        break;
                    }
                case 10:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.LowerManaCost, 1, apex ? 10 : 8);
                        break;
                    }
                case 11:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.RegenMana, 1, apex ? 4 : 2);
                        break;
                    }
                case 12:
                    {
                        spellbook.Slayer = GetRandomSlayer();
                        break;
                    }
                case 13:
                    {
                        ApplyAttribute(primary, min, max, AosAttribute.CastingFocus, 1, apex ? 8 : 6);
                        break;
                    }
            }
        }
    }
}
