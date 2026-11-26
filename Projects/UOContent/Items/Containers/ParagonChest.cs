using System;
using ModernUO.Serialization;

namespace Server.Items;

[Flippable]
[SerializationGenerator(0, false)]
public partial class ParagonChest : LockableContainer
{
    private static readonly int[] _itemIDs =
    {
        0x9AB, 0xE40, 0xE41, 0xE7C
    };

    private static readonly int[] _hues =
    {
        0x0, 0x455, 0x47E, 0x89F, 0x8A5, 0x8AB,
        0x966, 0x96D, 0x972, 0x973, 0x979
    };

    [InternString]
    [SerializableField(0, "private", "private")]
    private string _name;

    [Constructible]
    public ParagonChest(string name, int level) : this(name, level, 0)
    {
    }

    public ParagonChest(string name, int level, int luckChance) : base(_itemIDs.RandomElement())
    {
        _name = name;
        Hue = _hues.RandomElement();
        Fill(level, luckChance);
    }

    public override void OnSingleClick(Mobile from)
    {
        base.OnSingleClick(from);
        LabelTo(from, 1063449, _name);
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add(1063449, _name);
    }

    private static void GetRandomAOSStats(int level, bool isApex, out int attributeCount, out int min, out int max)
    {
        if (isApex)
        {
            attributeCount = 5;
            min = 80;
            max = 120;
            return;
        }

        var tier = Math.Clamp(level, 1, 6);
        var rnd = Utility.Random(100);

        switch (tier)
        {
            case 1:
                {
                    if (rnd < 15)
                    {
                        attributeCount = Utility.RandomMinMax(3, 4);
                        min = 25;
                        max = 65;
                    }
                    else if (rnd < 50)
                    {
                        attributeCount = Utility.RandomMinMax(2, 3);
                        min = 15;
                        max = 55;
                    }
                    else
                    {
                        attributeCount = Utility.RandomMinMax(1, 2);
                        min = 10;
                        max = 45;
                    }

                    break;
                }
            case 2:
                {
                    if (rnd < 20)
                    {
                        attributeCount = Utility.RandomMinMax(3, 4);
                        min = 30;
                        max = 75;
                    }
                    else if (rnd < 60)
                    {
                        attributeCount = Utility.RandomMinMax(2, 4);
                        min = 20;
                        max = 65;
                    }
                    else
                    {
                        attributeCount = Utility.RandomMinMax(2, 3);
                        min = 15;
                        max = 55;
                    }

                    break;
                }
            case 3:
                {
                    if (rnd < 25)
                    {
                        attributeCount = Utility.RandomMinMax(4, 5);
                        min = 35;
                        max = 85;
                    }
                    else if (rnd < 65)
                    {
                        attributeCount = Utility.RandomMinMax(3, 4);
                        min = 25;
                        max = 75;
                    }
                    else
                    {
                        attributeCount = Utility.RandomMinMax(2, 3);
                        min = 20;
                        max = 65;
                    }

                    break;
                }
            case 4:
                {
                    if (rnd < 30)
                    {
                        attributeCount = Utility.RandomMinMax(4, 5);
                        min = 45;
                        max = 95;
                    }
                    else if (rnd < 70)
                    {
                        attributeCount = Utility.RandomMinMax(3, 5);
                        min = 35;
                        max = 85;
                    }
                    else
                    {
                        attributeCount = Utility.RandomMinMax(3, 4);
                        min = 25;
                        max = 75;
                    }

                    break;
                }
            case 5:
                {
                    if (rnd < 40)
                    {
                        attributeCount = Utility.RandomMinMax(4, 5);
                        min = 55;
                        max = 100;
                    }
                    else if (rnd < 80)
                    {
                        attributeCount = Utility.RandomMinMax(4, 5);
                        min = 45;
                        max = 95;
                    }
                    else
                    {
                        attributeCount = Utility.RandomMinMax(3, 4);
                        min = 35;
                        max = 85;
                    }

                    break;
                }
            default:
                {
                    if (rnd < 50)
                    {
                        attributeCount = 5;
                        min = 60;
                        max = 100;
                    }
                    else
                    {
                        attributeCount = Utility.RandomMinMax(4, 5);
                        min = 50;
                        max = 100;
                    }

                    break;
                }
        }
    }

    public void Flip()
    {
        ItemID = ItemID switch
        {
            0x9AB => 0xE7C,
            0xE7C => 0x9AB,
            0xE40 => 0xE41,
            0xE41 => 0xE40,
            _     => ItemID
        };
    }

    private void Fill(int level, int luckChance)
    {
        var effectiveLevel = Math.Clamp(level, 1, 6);

        TrapType = TrapType.ExplosionTrap;
        TrapPower = effectiveLevel * 25;
        TrapLevel = effectiveLevel;
        Locked = true;

        RequiredSkill = effectiveLevel switch
        {
            1   => 36,
            2   => 76,
            3   => 84,
            4   => 92,
            >= 5 => 100,
            _   => 36
        };

        LockLevel = RequiredSkill - 10;
        MaxLockLevel = RequiredSkill + 40;

        DropItem(new Gold(effectiveLevel * 200));

        for (var i = 0; i < effectiveLevel; ++i)
        {
            DropItem(Loot.RandomScroll(0, 63, SpellbookType.Regular));
        }

        var itemCount = effectiveLevel * 2;
        var apexItemIndex = -1;

        // Cap Apex Legendary drops to at most one item per high-level paragon chest.
        if (Core.AOS && effectiveLevel >= 5 && itemCount > 0)
        {
            var baseApexChance = effectiveLevel == 5 ? 0.20 : 0.35;
            var luckBoost = 1.0 + Math.Clamp(luckChance / 7000.0, 0.0, 0.5);

            if (Utility.RandomDouble() < Math.Min(0.65, baseApexChance * luckBoost))
            {
                apexItemIndex = Utility.Random(itemCount);
            }
        }

        for (var i = 0; i < itemCount; ++i)
        {
            var item = Core.AOS ? Loot.RandomArmorOrShieldOrWeaponOrJewelry() : Loot.RandomArmorOrShieldOrWeapon();
            var isApex = i == apexItemIndex;

            if (item is BaseWeapon weapon)
            {
                if (Core.AOS)
                {
                    GetRandomAOSStats(effectiveLevel, isApex, out var attributeCount, out var min, out var max);
                    BaseRunicTool.ApplyAttributesTo(weapon, false, luckChance, attributeCount, min, max);
                }
                else
                {
                    weapon.DamageLevel = (WeaponDamageLevel)Utility.Random(6);
                    weapon.AccuracyLevel = (WeaponAccuracyLevel)Utility.Random(6);
                    weapon.DurabilityLevel = (WeaponDurabilityLevel)Utility.Random(6);
                }

                DropItem(weapon);
            }
            else if (item is BaseArmor armor)
            {
                if (Core.AOS)
                {
                    GetRandomAOSStats(effectiveLevel, isApex, out var attributeCount, out var min, out var max);
                    BaseRunicTool.ApplyAttributesTo(armor, false, luckChance, attributeCount, min, max);
                }
                else
                {
                    armor.ProtectionLevel = (ArmorProtectionLevel)Utility.Random(6);
                    armor.Durability = (ArmorDurabilityLevel)Utility.Random(6);
                }

                DropItem(armor);
            }
            else if (item is BaseHat hat)
            {
                if (Core.AOS)
                {
                    GetRandomAOSStats(effectiveLevel, isApex, out var attributeCount, out var min, out var max);
                    BaseRunicTool.ApplyAttributesTo(hat, false, luckChance, attributeCount, min, max);
                }

                DropItem(hat);
            }
            else if (item is BaseJewel jewel)
            {
                GetRandomAOSStats(effectiveLevel, isApex, out var attributeCount, out var min, out var max);
                BaseRunicTool.ApplyAttributesTo(jewel, false, luckChance, attributeCount, min, max);

                DropItem(jewel);
            }
        }

        for (var i = 0; i < effectiveLevel; i++)
        {
            var item = Loot.RandomPossibleReagent();
            item.Amount = Utility.RandomMinMax(40, 60);
            DropItem(item);
        }

        for (var i = 0; i < effectiveLevel; i++)
        {
            var item = Loot.RandomGem();
            DropItem(item);
        }

        DropItem(new TreasureMap(Math.Min(6, effectiveLevel + 1), Utility.RandomBool() ? Map.Felucca : Map.Trammel));
    }
}
