using System;
using Server.Items;
using Server.Mobiles;

namespace Server
{
    public class LootPack
    {
        public static readonly LootPackItem[] Gold = [new LootPackItem(typeof(Gold), 1)];

        public static readonly LootPackItem[] Instruments = [new LootPackItem(typeof(BaseInstrument), 1)];

        public static readonly LootPackItem[] LowScrollItems = [new LootPackItem(typeof(ClumsyScroll), 1)];

        public static readonly LootPackItem[] MedScrollItems = [new LootPackItem(typeof(ArchCureScroll), 1)];

        public static readonly LootPackItem[] HighScrollItems = [new LootPackItem(typeof(SummonAirElementalScroll), 1)];

        public static readonly LootPackItem[] GemItems = [new LootPackItem(typeof(Amber), 1)];

        public static readonly LootPackItem[] PotionItems =
        [
            new LootPackItem(typeof(AgilityPotion), 1),
            new LootPackItem(typeof(StrengthPotion), 1),
            new LootPackItem(typeof(RefreshPotion), 1),
            new LootPackItem(typeof(LesserCurePotion), 1),
            new LootPackItem(typeof(LesserHealPotion), 1),
            new LootPackItem(typeof(LesserPoisonPotion), 1)
        ];

        public static readonly LootPackItem[] OldMagicItems =
        [
            new LootPackItem(typeof(BaseJewel), 1),
            new LootPackItem(typeof(BaseArmor), 4),
            new LootPackItem(typeof(BaseWeapon), 3),
            new LootPackItem(typeof(BaseRanged), 1),
            new LootPackItem(typeof(BaseShield), 1)
        ];

        public static readonly LootPack LowScrolls = new(
            [new LootPackEntry(false, LowScrollItems, 100.00, 1)]
        );

        public static readonly LootPack MedScrolls = new(
            [new LootPackEntry(false, MedScrollItems, 100.00, 1)]
        );

        public static readonly LootPack HighScrolls = new(
            [new LootPackEntry(false, HighScrollItems, 100.00, 1)]
        );

        public static readonly LootPack Gems = new(
            [new LootPackEntry(false, GemItems, 100.00, 1)]
        );

        public static readonly LootPack Potions = new(
            [new LootPackEntry(false, PotionItems, 100.00, 1)]
        );

        private readonly LootPackEntry[] m_Entries;

        public LootPack(LootPackEntry[] entries) => m_Entries = entries;

        public static int GetLuckChance(Mobile killer, Mobile victim)
        {
            if (!Core.AOS)
            {
                return 0;
            }

            var luck = killer.Luck;

            if (victim != null && killer is PlayerMobile pmKiller && pmKiller.SentHonorContext != null &&
                pmKiller.SentHonorContext.Target == victim)
            {
                luck += pmKiller.SentHonorContext.PerfectionLuckBonus;
            }

            if (luck < 0)
            {
                return 0;
            }

            if (!Core.SE && luck > 1200)
            {
                luck = 1200;
            }

            return (int)(Math.Pow(luck, 1 / 1.8) * 100);
        }

        public static int GetLuckChanceForKiller(Mobile dead)
        {
            var list = BaseCreature.GetLootingRights(dead.DamageEntries, dead.HitsMax);

            DamageStore highest = null;

            for (var i = 0; i < list.Count; ++i)
            {
                var ds = list[i];

                if (ds.m_HasRight && (highest == null || ds.m_Damage > highest.m_Damage))
                {
                    highest = ds;
                }
            }

            if (highest == null)
            {
                return 0;
            }

            return GetLuckChance(highest.m_Mobile, dead);
        }

        public static bool CheckLuck(int chance) => chance > Utility.Random(10000);

        public void Generate(Mobile from, Container cont, bool spawning, int luckChance)
        {
            if (cont == null)
            {
                return;
            }

            for (var i = 0; i < m_Entries.Length; ++i)
            {
                var entry = m_Entries[i];

                var shouldAdd = entry.Chance > Utility.Random(10000);

                if (!shouldAdd && Core.AOS && CheckLuck(luckChance))
                {
                    shouldAdd = entry.Chance > Utility.Random(10000);
                }

                if (!shouldAdd)
                {
                    continue;
                }

                var item = entry.Construct(from, luckChance, spawning);

                if (item != null)
                {
                    if (!item.Stackable || !cont.TryDropItem(from, item, false))
                    {
                        cont.DropItem(item);
                    }
                }
            }
        }

        public static readonly LootPackItem[] AosMagicItemsRichType1 =
        [
            new LootPackItem(typeof(BaseWeapon), 211),
            new LootPackItem(typeof(BaseRanged), 53),
            new LootPackItem(typeof(BaseArmor), 303),
            new LootPackItem(typeof(BaseShield), 39),
            new LootPackItem(typeof(BaseJewel), 158),
            new LootPackItem(typeof(Spellbook), 32),
            new LootPackItem(typeof(RandomTalisman), 18)
        ];

        public static readonly LootPack MlRich = new(
            [
                new LootPackEntry(true, Gold, 100.00, "4d50+450"),
                new LootPackEntry(false, AosMagicItemsRichType1, 100.00, 1, 4, 20, 75),
                new LootPackEntry(false, AosMagicItemsRichType1, 90.00, 1, 4, 25, 80),
                new LootPackEntry(false, AosMagicItemsRichType2, 75.00, 1, 5, 30, 85),
                new LootPackEntry(false, Instruments, 1.50, 1)
            ]
        );

        public static readonly LootPackItem[] AosMagicItemsPoor =
        [
            new LootPackItem(typeof(BaseWeapon), 3),
            new LootPackItem(typeof(BaseRanged), 1),
            new LootPackItem(typeof(BaseArmor), 4),
            new LootPackItem(typeof(BaseShield), 1),
            new LootPackItem(typeof(BaseJewel), 2)
        ];

        public static readonly LootPackItem[] AosMagicItemsMeagerType1 =
        [
            new LootPackItem(typeof(BaseWeapon), 56),
            new LootPackItem(typeof(BaseRanged), 14),
            new LootPackItem(typeof(BaseArmor), 81),
            new LootPackItem(typeof(BaseShield), 11),
            new LootPackItem(typeof(BaseJewel), 42)
        ];

        public static readonly LootPackItem[] AosMagicItemsMeagerType2 =
        [
            new LootPackItem(typeof(BaseWeapon), 28),
            new LootPackItem(typeof(BaseRanged), 7),
            new LootPackItem(typeof(BaseArmor), 40),
            new LootPackItem(typeof(BaseShield), 5),
            new LootPackItem(typeof(BaseJewel), 21)
        ];

        public static readonly LootPackItem[] AosMagicItemsAverageType1 =
        [
            new LootPackItem(typeof(BaseWeapon), 90),
            new LootPackItem(typeof(BaseRanged), 23),
            new LootPackItem(typeof(BaseArmor), 130),
            new LootPackItem(typeof(BaseShield), 17),
            new LootPackItem(typeof(BaseJewel), 68)
        ];

        public static readonly LootPackItem[] AosMagicItemsAverageType2 =
        [
            new LootPackItem(typeof(BaseWeapon), 54),
            new LootPackItem(typeof(BaseRanged), 13),
            new LootPackItem(typeof(BaseArmor), 77),
            new LootPackItem(typeof(BaseShield), 10),
            new LootPackItem(typeof(BaseJewel), 40)
        ];

        public static readonly LootPackItem[] AosMagicItemsRichType2 =
        [
            new LootPackItem(typeof(BaseWeapon), 170),
            new LootPackItem(typeof(BaseRanged), 43),
            new LootPackItem(typeof(BaseArmor), 245),
            new LootPackItem(typeof(BaseShield), 32),
            new LootPackItem(typeof(BaseJewel), 128),
            new LootPackItem(typeof(Spellbook), 26),
            new LootPackItem(typeof(RandomTalisman), 16)
        ];

        public static readonly LootPackItem[] AosMagicItemsFilthyRichType1 =
        [
            new LootPackItem(typeof(BaseWeapon), 219),
            new LootPackItem(typeof(BaseRanged), 55),
            new LootPackItem(typeof(BaseArmor), 315),
            new LootPackItem(typeof(BaseShield), 41),
            new LootPackItem(typeof(BaseJewel), 164),
            new LootPackItem(typeof(Spellbook), 38),
            new LootPackItem(typeof(RandomTalisman), 24)
        ];

        public static readonly LootPackItem[] AosMagicItemsFilthyRichType2 =
        [
            new LootPackItem(typeof(BaseWeapon), 239),
            new LootPackItem(typeof(BaseRanged), 60),
            new LootPackItem(typeof(BaseArmor), 343),
            new LootPackItem(typeof(BaseShield), 90),
            new LootPackItem(typeof(BaseJewel), 45),
            new LootPackItem(typeof(Spellbook), 38),
            new LootPackItem(typeof(RandomTalisman), 24)
        ];

        public static readonly LootPackItem[] AosMagicItemsUltraRich =
        [
            new LootPackItem(typeof(BaseWeapon), 276),
            new LootPackItem(typeof(BaseRanged), 69),
            new LootPackItem(typeof(BaseArmor), 397),
            new LootPackItem(typeof(BaseShield), 52),
            new LootPackItem(typeof(BaseJewel), 207),
            new LootPackItem(typeof(Spellbook), 50),
            new LootPackItem(typeof(RandomTalisman), 35)
        ];

        public static readonly LootPack SePoor = new(
            [
                new LootPackEntry(true, Gold, 100.00, "2d10+20"),
                new LootPackEntry(false, AosMagicItemsPoor, 8.00, 1, 2, 5, 45),
                new LootPackEntry(false, Instruments, 0.05, 1)
            ]
        );

        public static readonly LootPack SeMeager = new(
            [
                new LootPackEntry(true, Gold, 100.00, "4d10+40"),
                new LootPackEntry(false, AosMagicItemsMeagerType1, 35.00, 1, 2, 10, 50),
                new LootPackEntry(false, AosMagicItemsMeagerType2, 20.00, 1, 3, 15, 60),
                new LootPackEntry(false, Instruments, 0.20, 1)
            ]
        );

        public static readonly LootPack SeAverage = new(
            [
                new LootPackEntry(true, Gold, 100.00, "8d10+100"),
                new LootPackEntry(false, AosMagicItemsAverageType1, 50.00, 1, 3, 15, 60),
                new LootPackEntry(false, AosMagicItemsAverageType1, 45.00, 1, 3, 20, 65),
                new LootPackEntry(false, AosMagicItemsAverageType2, 30.00, 1, 4, 20, 70),
                new LootPackEntry(false, Instruments, 0.50, 1)
            ]
        );

        public static readonly LootPack SeRich = new(
            [
                new LootPackEntry(true, Gold, 100.00, "15d10+225"),
                new LootPackEntry(false, AosMagicItemsRichType1, 85.00, 1, 3, 20, 70),
                new LootPackEntry(false, AosMagicItemsRichType1, 85.00, 1, 4, 20, 75),
                new LootPackEntry(false, AosMagicItemsRichType2, 75.00, 1, 4, 25, 80),
                new LootPackEntry(false, Instruments, 1.50, 1)
            ]
        );

        public static readonly LootPack SeFilthyRich = new(
            [
                new LootPackEntry(true, Gold, 100.00, "3d100+400"),
                new LootPackEntry(false, AosMagicItemsFilthyRichType1, 90.00, 1, 4, 25, 80),
                new LootPackEntry(false, AosMagicItemsFilthyRichType1, 90.00, 1, 4, 30, 85),
                new LootPackEntry(false, AosMagicItemsFilthyRichType2, 85.00, 1, 5, 35, 85),
                new LootPackEntry(false, Instruments, 2.50, 1)
            ]
        );

        public static readonly LootPack SeUltraRich = new(
            [
                new LootPackEntry(true, Gold, 100.00, "6d100+600"),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 40, 95),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 40, 95),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 40, 95),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 40, 95),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 40, 95),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 45, 100, 0.04),
                new LootPackEntry(false, Instruments, 2.50, 1)
            ]
        );

        public static readonly LootPack SeSuperBoss = new(
            [
                new LootPackEntry(true, Gold, 100.00, "10d100+800"),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 45, 95),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 45, 95),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 45, 95),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 45, 95),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 50, 100),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 50, 100),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 50, 100),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 50, 100),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 60, 100, 0.08),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 60, 100, 0.08),
                new LootPackEntry(false, Instruments, 2.50, 1)
            ]
        );

        public static readonly LootPack AosPoor = new(
            [
                new LootPackEntry(true, Gold, 100.00, "1d10+10"),
                new LootPackEntry(false, AosMagicItemsPoor, 0.02, 1, 5, 0, 90),
                new LootPackEntry(false, Instruments, 0.02, 1)
            ]
        );

        public static readonly LootPack AosMeager = new(
            [
                new LootPackEntry(true, Gold, 100.00, "3d10+20"),
                new LootPackEntry(false, AosMagicItemsMeagerType1, 1.00, 1, 2, 0, 10),
                new LootPackEntry(false, AosMagicItemsMeagerType2, 0.20, 1, 5, 0, 90),
                new LootPackEntry(false, Instruments, 0.10, 1)
            ]
        );

        public static readonly LootPack AosAverage = new(
            [
                new LootPackEntry(true, Gold, 100.00, "5d10+50"),
                new LootPackEntry(false, AosMagicItemsAverageType1, 5.00, 1, 4, 0, 20),
                new LootPackEntry(false, AosMagicItemsAverageType1, 2.00, 1, 3, 0, 50),
                new LootPackEntry(false, AosMagicItemsAverageType2, 0.50, 1, 5, 0, 90),
                new LootPackEntry(false, Instruments, 0.40, 1)
            ]
        );

        public static readonly LootPack AosRich = new(
            [
                new LootPackEntry(true, Gold, 100.00, "10d10+150"),
                new LootPackEntry(false, AosMagicItemsRichType1, 20.00, 1, 4, 0, 40),
                new LootPackEntry(false, AosMagicItemsRichType1, 10.00, 1, 5, 0, 60),
                new LootPackEntry(false, AosMagicItemsRichType2, 1.00, 1, 5, 0, 90),
                new LootPackEntry(false, Instruments, 1.00, 1)
            ]
        );

        public static readonly LootPack AosFilthyRich = new(
            [
                new LootPackEntry(true, Gold, 100.00, "2d100+200"),
                new LootPackEntry(false, AosMagicItemsFilthyRichType1, 33.00, 1, 4, 0, 50),
                new LootPackEntry(false, AosMagicItemsFilthyRichType1, 33.00, 1, 4, 0, 60),
                new LootPackEntry(false, AosMagicItemsFilthyRichType2, 20.00, 1, 5, 0, 75),
                new LootPackEntry(false, AosMagicItemsFilthyRichType2, 5.00, 1, 5, 0, 100),
                new LootPackEntry(false, Instruments, 2.00, 1)
            ]
        );

        public static readonly LootPack AosUltraRich = new(
            [
                new LootPackEntry(true, Gold, 100.00, "5d100+500"),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 25, 100),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 25, 100),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 25, 100),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 25, 100),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 25, 100),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 35, 100),
                new LootPackEntry(false, Instruments, 2.00, 1)
            ]
        );

        public static readonly LootPack AosSuperBoss = new(
            [
                new LootPackEntry(true, Gold, 100.00, "5d100+500"),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 25, 100),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 25, 100),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 25, 100),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 25, 100),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 33, 100),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 33, 100),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 33, 100),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 33, 100),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 50, 100),
                new LootPackEntry(false, AosMagicItemsUltraRich, 100.00, 1, 5, 50, 100),
                new LootPackEntry(false, Instruments, 2.00, 1)
            ]
        );

        public static readonly LootPack OldPoor = new(
            [
                new LootPackEntry(true, Gold, 100.00, "1d25"),
                new LootPackEntry(false, Instruments, 0.02, 1)
            ]
        );

        public static readonly LootPack OldMeager = new(
            [
                new LootPackEntry(true, Gold, 100.00, "5d10+25"),
                new LootPackEntry(false, Instruments, 0.10, 1),
                new LootPackEntry(false, OldMagicItems, 1.00, 1, 1, 0, 60),
                new LootPackEntry(false, OldMagicItems, 0.20, 1, 1, 10, 70)
            ]
        );

        public static readonly LootPack OldAverage = new(
            [
                new LootPackEntry(true, Gold, 100.00, "10d10+50"),
                new LootPackEntry(false, Instruments, 0.40, 1),
                new LootPackEntry(false, OldMagicItems, 5.00, 1, 1, 20, 80),
                new LootPackEntry(false, OldMagicItems, 2.00, 1, 1, 30, 90),
                new LootPackEntry(false, OldMagicItems, 0.50, 1, 1, 40, 100)
            ]
        );

        public static readonly LootPack OldRich = new(
            [
                new LootPackEntry(true, Gold, 100.00, "10d10+250"),
                new LootPackEntry(false, Instruments, 1.00, 1),
                new LootPackEntry(false, OldMagicItems, 20.00, 1, 1, 60, 100),
                new LootPackEntry(false, OldMagicItems, 10.00, 1, 1, 65, 100),
                new LootPackEntry(false, OldMagicItems, 1.00, 1, 1, 70, 100)
            ]
        );

        public static readonly LootPack OldFilthyRich = new(
            [
                new LootPackEntry(true, Gold, 100.00, "2d125+400"),
                new LootPackEntry(false, Instruments, 2.00, 1),
                new LootPackEntry(false, OldMagicItems, 33.00, 1, 1, 50, 100),
                new LootPackEntry(false, OldMagicItems, 33.00, 1, 1, 60, 100),
                new LootPackEntry(false, OldMagicItems, 20.00, 1, 1, 70, 100),
                new LootPackEntry(false, OldMagicItems, 5.00, 1, 1, 80, 100)
            ]
        );

        public static readonly LootPack OldUltraRich = new(
            [
                new LootPackEntry(true, Gold, 100.00, "5d100+500"),
                new LootPackEntry(false, Instruments, 2.00, 1),
                new LootPackEntry(false, OldMagicItems, 100.00, 1, 1, 40, 100),
                new LootPackEntry(false, OldMagicItems, 100.00, 1, 1, 40, 100),
                new LootPackEntry(false, OldMagicItems, 100.00, 1, 1, 50, 100),
                new LootPackEntry(false, OldMagicItems, 100.00, 1, 1, 50, 100),
                new LootPackEntry(false, OldMagicItems, 100.00, 1, 1, 60, 100),
                new LootPackEntry(false, OldMagicItems, 100.00, 1, 1, 60, 100)
            ]
        );

        public static readonly LootPack OldSuperBoss = new(
            [
                new LootPackEntry(true, Gold, 100.00, "5d100+500"),
                new LootPackEntry(false, Instruments, 2.00, 1),
                new LootPackEntry(false, OldMagicItems, 100.00, 1, 1, 40, 100),
                new LootPackEntry(false, OldMagicItems, 100.00, 1, 1, 40, 100),
                new LootPackEntry(false, OldMagicItems, 100.00, 1, 1, 40, 100),
                new LootPackEntry(false, OldMagicItems, 100.00, 1, 1, 50, 100),
                new LootPackEntry(false, OldMagicItems, 100.00, 1, 1, 50, 100),
                new LootPackEntry(false, OldMagicItems, 100.00, 1, 1, 50, 100),
                new LootPackEntry(false, OldMagicItems, 100.00, 1, 1, 60, 100),
                new LootPackEntry(false, OldMagicItems, 100.00, 1, 1, 60, 100),
                new LootPackEntry(false, OldMagicItems, 100.00, 1, 1, 60, 100),
                new LootPackEntry(false, OldMagicItems, 100.00, 1, 1, 70, 100)
            ]
        );

        public static LootPack Poor => Core.SE ? SePoor :
            Core.AOS ? AosPoor : OldPoor;

        public static LootPack Meager => Core.SE ? SeMeager :
            Core.AOS ? AosMeager : OldMeager;

        public static LootPack Average => Core.SE ? SeAverage :
            Core.AOS ? AosAverage : OldAverage;

        public static LootPack Rich => Core.SE ? SeRich :
            Core.AOS ? AosRich : OldRich;

        public static LootPack FilthyRich => Core.SE ? SeFilthyRich :
            Core.AOS ? AosFilthyRich : OldFilthyRich;

        public static LootPack UltraRich => Core.SE ? SeUltraRich :
            Core.AOS ? AosUltraRich : OldUltraRich;

        public static LootPack SuperBoss => Core.SE ? SeSuperBoss :
            Core.AOS ? AosSuperBoss : OldSuperBoss;

        /*
        // TODO: Uncomment once added Legacy
        public static readonly LootPackItem[] ParrotItem = new LootPackItem[]
          {
            new LootPackItem( typeof( ParrotItem ), 1 )
          };

        public static readonly LootPack Parrot = new LootPack( new LootPackEntry[]
          {
            new LootPackEntry( false, ParrotItem, 10.00, 1 )
          } );
        */
    }

    public class LootPackEntry
    {
        private readonly bool m_AtSpawnTime;

        public LootPackEntry(bool atSpawnTime, LootPackItem[] items, double chance, string quantity) : this(
            atSpawnTime,
            items,
            chance,
            new LootPackDice(quantity)
        )
        {
        }

        public LootPackEntry(bool atSpawnTime, LootPackItem[] items, double chance, int quantity) : this(
            atSpawnTime,
            items,
            chance,
            new LootPackDice(0, 0, quantity)
        )
        {
        }

        public LootPackEntry(
            bool atSpawnTime, LootPackItem[] items, double chance, string quantity, int maxProps,
            int minIntensity, int maxIntensity
        ) : this(
            atSpawnTime,
            items,
            chance,
            new LootPackDice(quantity),
            maxProps,
            minIntensity,
            maxIntensity
        )
        {
        }

        public LootPackEntry(
            bool atSpawnTime, LootPackItem[] items, double chance, int quantity, int maxProps,
            int minIntensity, int maxIntensity
        ) : this(
            atSpawnTime,
            items,
            chance,
            new LootPackDice(0, 0, quantity),
            maxProps,
            minIntensity,
            maxIntensity
        )
        {
        }

        public LootPackEntry(
            bool atSpawnTime, LootPackItem[] items, double chance, int quantity, int maxProps,
            int minIntensity, int maxIntensity, double apexChance
        ) : this(
            atSpawnTime,
            items,
            chance,
            new LootPackDice(0, 0, quantity),
            maxProps,
            minIntensity,
            maxIntensity,
            apexChance
        )
        {
        }

        public LootPackEntry(
            bool atSpawnTime, LootPackItem[] items, double chance, LootPackDice quantity, int maxProps = 0,
            int minIntensity = 0, int maxIntensity = 0, double apexChance = 0.0
        )
        {
            m_AtSpawnTime = atSpawnTime;
            Items = items;
            Chance = (int)(100 * chance);
            Quantity = quantity;
            MaxProps = maxProps;
            MinIntensity = minIntensity;
            MaxIntensity = maxIntensity;
            ApexChance = apexChance;
        }

        public int Chance { get; set; }

        public LootPackDice Quantity { get; set; }

        public int MaxProps { get; set; }

        public int MinIntensity { get; set; }

        public int MaxIntensity { get; set; }

        public double ApexChance { get; set; }

        public LootPackItem[] Items { get; set; }

        private static bool IsInTokuno(Mobile m)
        {
            if (m.Region.IsPartOf("Fan Dancer's Dojo"))
            {
                return true;
            }

            if (m.Region.IsPartOf("Yomotsu Mines"))
            {
                return true;
            }

            return m.Map == Map.Tokuno;
        }

        private static bool IsMondain(Mobile m) => MondainsLegacy.IsMLRegion(m.Region);

        // Stygian Abyss loot flavor: creatures in Ter Mur draw from the SA gear pools.
        private static bool IsStygian(Mobile m) => m.Map == Map.TerMur;

        public Item Construct(Mobile from, int luckChance, bool spawning)
        {
            if (m_AtSpawnTime != spawning)
            {
                return null;
            }

            var totalChance = 0;

            for (var i = 0; i < Items.Length; ++i)
            {
                totalChance += Items[i].Chance;
            }

            var rnd = Utility.Random(totalChance);

            for (var i = 0; i < Items.Length; ++i)
            {
                var item = Items[i];

                if (rnd < item.Chance)
                {
                    return Mutate(from, luckChance, item.Construct(IsInTokuno(from), IsMondain(from), IsStygian(from)));
                }

                rnd -= item.Chance;
            }

            return null;
        }

        private int GetRandomOldBonus()
        {
            var rnd = Utility.RandomMinMax(MinIntensity, MaxIntensity);

            if (rnd < 50)
            {
                return 1;
            }

            rnd -= 50;

            if (rnd < 25)
            {
                return 2;
            }

            rnd -= 25;

            if (rnd < 14)
            {
                return 3;
            }

            rnd -= 14;

            if (rnd < 8)
            {
                return 4;
            }

            return 5;
        }

        public Item Mutate(Mobile from, int luckChance, Item item)
        {
            if (item == null)
            {
                return null;
            }

            if (item is BaseWeapon && Utility.Random(100) < 1)
            {
                item.Delete();
                return new FireHorn();
            }

            if (item is BaseWeapon or BaseArmor or BaseJewel or BaseHat or Spellbook)
            {
                if (Core.AOS)
                {
                    var bonusProps = GetBonusProperties();
                    var min = MinIntensity;
                    var max = MaxIntensity;

                    if (bonusProps < MaxProps && LootPack.CheckLuck(luckChance))
                    {
                        ++bonusProps;
                    }

                    var props = 1 + bonusProps;

                    // Make sure normal drops never exceed MaxProps.
                    if (props > MaxProps)
                    {
                        props = MaxProps;
                    }

                    if (ApexChance > 0.0)
                    {
                        var effectiveApexChance = ApexChance * (1.0 + Math.Clamp(luckChance / 5000.0, 0.0, 1.5));

                        if (Utility.RandomDouble() < effectiveApexChance)
                        {
                            props = ApexChance >= 0.08 ? Utility.RandomMinMax(5, 6) : Math.Max(MaxProps, 5);
                            min = 80;
                            max = 120;
                        }
                    }

                    if (item is BaseWeapon weapon)
                    {
                        BaseRunicTool.ApplyAttributesTo(weapon, false, luckChance, props, min, max);
                    }
                    else if (item is BaseArmor armor)
                    {
                        BaseRunicTool.ApplyAttributesTo(armor, false, luckChance, props, min, max);
                    }
                    else if (item is BaseJewel jewel)
                    {
                        BaseRunicTool.ApplyAttributesTo(jewel, false, luckChance, props, min, max);
                    }
                    else if (item is BaseHat hat)
                    {
                        BaseRunicTool.ApplyAttributesTo(hat, false, luckChance, props, min, max);
                    }
                    else if (item is Spellbook spellbook)
                    {
                        spellbook.LootType = LootType.Regular;

                        if (spellbook.Content == 0)
                        {
                            ulong bits = 0;
                            var spellCount = Utility.RandomMinMax(4, 16);

                            for (var i = 0; i < spellCount; ++i)
                            {
                                bits |= 1UL << Utility.Random(64);
                            }

                            spellbook.Content = bits;
                        }

                        BaseRunicTool.ApplyAttributesTo(spellbook, false, luckChance, props, min, max);
                    }
                }
                else // not aos
                {
                    if (item is BaseWeapon weapon)
                    {
                        if (Utility.Random(100) < 80)
                        {
                            weapon.AccuracyLevel = (WeaponAccuracyLevel)GetRandomOldBonus();
                        }

                        if (Utility.Random(100) < 60)
                        {
                            weapon.DamageLevel = (WeaponDamageLevel)GetRandomOldBonus();
                        }

                        if (Utility.Random(100) < 40)
                        {
                            weapon.DurabilityLevel = (WeaponDurabilityLevel)GetRandomOldBonus();
                        }

                        if (Utility.Random(100) < 5)
                        {
                            weapon.Slayer = SlayerName.Silver;
                        }
                        else if (from != null && weapon.AccuracyLevel == 0 && weapon.DamageLevel == 0 &&
                            weapon.DurabilityLevel == 0 && weapon.Slayer == SlayerName.None && Utility.Random(100) < 5)
                        {
                            weapon.Slayer = SlayerGroup.GetLootSlayerType(from.GetType());
                        }
                    }
                    else if (item is BaseArmor armor)
                    {
                        if (Utility.Random(100) < 80)
                        {
                            armor.ProtectionLevel = (ArmorProtectionLevel)GetRandomOldBonus();
                        }

                        if (Utility.Random(100) < 40)
                        {
                            armor.Durability = (ArmorDurabilityLevel)GetRandomOldBonus();
                        }
                    }
                }
            }
            else if (item is BaseInstrument instr)
            {
                SlayerName slayer;

                if (Core.AOS)
                {
                    slayer = BaseRunicTool.GetRandomSlayer();
                }
                else
                {
                    slayer = SlayerGroup.GetLootSlayerType(from.GetType());
                }

                if (slayer == SlayerName.None)
                {
                    instr.Delete();
                    return null;
                }

                instr.Quality = InstrumentQuality.Regular;
                instr.Slayer = slayer;
            }

            if (item.Stackable)
            {
                item.Amount = Quantity.Roll(); // We set the amount, even if it can be negative so we get an error log

                if (item.Amount <= 0)
                {
                    item.Delete();
                    return null;
                }
            }

            return item;
        }

        public int GetBonusProperties()
        {
            if (MaxProps <= 1)
            {
                return 0;
            }

            int p0 = 0, p1 = 0, p2 = 0, p3 = 0, p4 = 0, p5 = 0;

            switch (MaxProps)
            {
                case 2:
                    {
                        p0 = 4;
                        p1 = 6;
                        break;
                    }
                case 3:
                    {
                        p0 = 5;
                        p1 = 8;
                        p2 = 7;
                        break;
                    }
                case 4:
                    {
                        p0 = 5;
                        p1 = 10;
                        p2 = 12;
                        p3 = 8;
                        break;
                    }
                case 5:
                    {
                        p0 = 8;
                        p1 = 18;
                        p2 = 28;
                        p3 = 28;
                        p4 = 18;
                        break;
                    }
                default:
                    {
                        p0 = 5;
                        p1 = 12;
                        p2 = 23;
                        p3 = 25;
                        p4 = 20;
                        p5 = 15;
                        break;
                    }
            }

            var pc = p0 + p1 + p2 + p3 + p4 + p5;

            var rnd = Utility.Random(pc);

            if (rnd < p5)
            {
                return 5;
            }

            rnd -= p5;

            if (rnd < p4)
            {
                return 4;
            }

            rnd -= p4;

            if (rnd < p3)
            {
                return 3;
            }

            rnd -= p3;

            if (rnd < p2)
            {
                return 2;
            }

            return rnd - p2 < p1 ? 1 : 0;
        }
    }

    public class LootPackItem
    {
        private static readonly Type[] m_BlankTypes = [typeof(BlankScroll)];

        private static readonly Type[][] m_NecroTypes =
        [
            // Low
            [
                typeof(AnimateDeadScroll), typeof(BloodOathScroll), typeof(CorpseSkinScroll), typeof(CurseWeaponScroll),
                typeof(EvilOmenScroll), typeof(HorrificBeastScroll), typeof(MindRotScroll), typeof(PainSpikeScroll),
                typeof(SummonFamiliarScroll), typeof(WraithFormScroll)
            ],
            // Mid
            [typeof(LichFormScroll), typeof(PoisonStrikeScroll), typeof(StrangleScroll), typeof(WitherScroll)],
            // High
            Core.SE
                ? [typeof(VengefulSpiritScroll), typeof(VampiricEmbraceScroll), typeof(ExorcismScroll)]
                : [typeof(VengefulSpiritScroll), typeof(VampiricEmbraceScroll)]
        ];

        public LootPackItem(Type type, int chance)
        {
            Type = type;
            Chance = chance;
        }

        public Type Type { get; set; }

        public int Chance { get; set; }

        public static Item RandomScroll(int index, int minCircle, int maxCircle)
        {
            --minCircle;
            --maxCircle;

            var scrollCount = (maxCircle - minCircle + 1) * 8;

            if (index == 0)
            {
                scrollCount += m_BlankTypes.Length;
            }

            if (Core.AOS)
            {
                scrollCount += m_NecroTypes[index].Length;
            }

            var rnd = Utility.Random(scrollCount);

            if (index == 0 && rnd < m_BlankTypes.Length)
            {
                return Loot.Construct(m_BlankTypes);
            }

            if (index == 0)
            {
                rnd -= m_BlankTypes.Length;
            }

            if (Core.AOS && rnd < m_NecroTypes[index].Length)
            {
                return Loot.Construct(m_NecroTypes[index]);
            }

            return Loot.RandomScroll(minCircle * 8, maxCircle * 8 + 7, SpellbookType.Regular);
        }

        public Item Construct(bool inTokuno, bool isMondain, bool isStygian)
        {
            try
            {
                if (Type == typeof(BaseRanged))
                {
                    return Loot.RandomRangedWeapon(inTokuno, isMondain, isStygian);
                }

                if (Type == typeof(BaseWeapon))
                {
                    return Loot.RandomWeapon(inTokuno, isMondain, isStygian);
                }

                if (Type == typeof(BaseArmor))
                {
                    return Loot.RandomArmorOrHat(inTokuno, isMondain);
                }

                if (Type == typeof(BaseShield))
                {
                    return Loot.RandomShield();
                }

                if (Type == typeof(BaseJewel))
                {
                    return Core.AOS ? Loot.RandomJewelry() : Loot.RandomArmorOrShieldOrWeapon();
                }

                if (Type == typeof(Spellbook))
                {
                    return Core.AOS ? new Spellbook() : Loot.RandomArmorOrShieldOrWeapon();
                }

                if (Type == typeof(RandomTalisman) || Type == typeof(BaseTalisman))
                {
                    return Core.ML ? new RandomTalisman() : Loot.RandomArmorOrShieldOrWeapon();
                }

                if (Type == typeof(BaseInstrument))
                {
                    return Loot.RandomInstrument();
                }

                if (Type == typeof(Amber)) // gem
                {
                    return Loot.RandomGem();
                }

                if (Type == typeof(ClumsyScroll)) // low scroll
                {
                    return RandomScroll(0, 1, 3);
                }

                if (Type == typeof(ArchCureScroll)) // med scroll
                {
                    return RandomScroll(1, 4, 7);
                }

                if (Type == typeof(SummonAirElementalScroll)) // high scroll
                {
                    return RandomScroll(2, 8, 8);
                }

                return Type.CreateInstance<Item>();
            }
            catch
            {
                // ignored
            }

            return null;
        }
    }

    public class LootPackDice
    {
        public LootPackDice(string str)
        {
            var start = 0;
            var index = str.IndexOf('d', start);

            if (index < start)
            {
                return;
            }

            Count = Utility.ToInt32(str.AsSpan(start, index));

            start = index + 1;
            index = str.IndexOf('+', start);

            var negative = index < start;

            if (negative)
            {
                index = str.IndexOf('-', start);
            }

            if (index < start)
            {
                index = str.Length;
            }

            Sides = Utility.ToInt32(str.AsSpan(start, index - start));

            if (index == str.Length)
            {
                return;
            }

            start = index + 1;
            index = str.Length;

            Bonus = Utility.ToInt32(str.AsSpan(start, index - start));

            if (negative)
            {
                Bonus *= -1;
            }
        }

        public LootPackDice(int count, int sides, int bonus)
        {
            Count = count;
            Sides = sides;
            Bonus = bonus;
        }

        public int Count { get; set; }

        public int Sides { get; set; }

        public int Bonus { get; set; }

        public int Roll()
        {
            var v = Bonus;

            for (var i = 0; i < Count; ++i)
            {
                v += Utility.Random(1, Sides);
            }

            return v;
        }
    }
}
