using System;
using Server.Engines.CannedEvil;
using Server.Engines.Quests;
using Server.Items;
using Server.Regions;

namespace Server.Mobiles;

public static class Paragon
{
    public const double ChestChance = 0.10;               // Chance that a paragon will carry a paragon chest
    public const double ChocolateIngredientChance = 0.20; // Chance that a paragon will drop a chocolatiering ingredient

    public static Map[] Maps =
    {
        Map.Ilshenar,
        Map.Trammel,
        Map.Felucca,
        Map.Malas,
        Map.Tokuno,
        Map.TerMur
    };

    private static readonly TimeSpan FastRegenRate = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan CPUSaverRate = TimeSpan.FromSeconds(2);

    public static Type[] Artifacts =
    {
        typeof(GoldBricks), typeof(PhillipsWoodenSteed),
        typeof(AlchemistsBauble), typeof(ArcticDeathDealer),
        typeof(BlazeOfDeath), typeof(BowOfTheJukaKing),
        typeof(BurglarsBandana), typeof(CavortingClub),
        typeof(EnchantedTitanLegBone), typeof(GwennosHarp),
        typeof(IolosLute), typeof(LunaLance),
        typeof(NightsKiss), typeof(NoxRangersHeavyCrossbow),
        typeof(OrcishVisage), typeof(PolarBearMask),
        typeof(ShieldOfInvulnerability), typeof(StaffOfPower),
        typeof(VioletCourage), typeof(HeartOfTheLion),
        typeof(WrathOfTheDryad), typeof(PixieSwatter),
        typeof(GlovesOfThePugilist)
    };

    public static int Hue = 0x501; // Paragon hue

    // Buffs
    public const double HitsBuff = 5.0;
    public const double StrBuff = 1.05;
    public const double IntBuff = 1.20;
    public const double DexBuff = 1.20;
    public const double SkillsBuff = 1.20;
    public const double SpeedBuff = 1.20;
    public const double FameBuff = 1.40;
    public const double KarmaBuff = 1.40;
    public const int DamageBuff = 5;

    public static void Convert(BaseCreature bc)
    {
        if (bc.IsParagon)
        {
            return;
        }

        bc.Hue = Hue;

        if (bc.HitsMaxSeed >= 0)
        {
            bc.HitsMaxSeed = (int)(bc.HitsMaxSeed * HitsBuff);
        }

        bc.RawStr = (int)(bc.RawStr * StrBuff);
        bc.RawInt = (int)(bc.RawInt * IntBuff);
        bc.RawDex = (int)(bc.RawDex * DexBuff);

        bc.Hits = bc.HitsMax;
        bc.Mana = bc.ManaMax;
        bc.Stam = bc.StamMax;

        for (var i = 0; i < bc.Skills.Length; i++)
        {
            var skill = bc.Skills[i];

            if (skill.Base > 0.0)
            {
                skill.Base *= SkillsBuff;
            }
        }

        bc.PassiveSpeed /= SpeedBuff;
        bc.ActiveSpeed /= SpeedBuff;
        bc.ScaleMoveSpeed(1.0 / SpeedBuff);
        bc.CurrentSpeed = bc.PassiveSpeed;

        bc.DamageMin += DamageBuff;
        bc.DamageMax += DamageBuff;

        if (bc.Fame > 0)
        {
            bc.Fame = (int)(bc.Fame * FameBuff);
        }

        if (bc.Fame > 32000)
        {
            bc.Fame = 32000;
        }

        // TODO: Mana regeneration rate = Sqrt( buffedFame ) / 4

        if (bc.Karma != 0)
        {
            bc.Karma = (int)(bc.Karma * KarmaBuff);

            if (bc.Karma.Abs() > 32000)
            {
                bc.Karma = 32000 * Math.Sign(bc.Karma);
            }
        }

        new ParagonStamRegen(bc).Start();
    }

    public static void UnConvert(BaseCreature bc)
    {
        if (!bc.IsParagon)
        {
            return;
        }

        bc.Hue = 0;

        if (bc.HitsMaxSeed >= 0)
        {
            bc.HitsMaxSeed = (int)(bc.HitsMaxSeed / HitsBuff);
        }

        bc.RawStr = (int)(bc.RawStr / StrBuff);
        bc.RawInt = (int)(bc.RawInt / IntBuff);
        bc.RawDex = (int)(bc.RawDex / DexBuff);

        bc.Hits = bc.HitsMax;
        bc.Mana = bc.ManaMax;
        bc.Stam = bc.StamMax;

        for (var i = 0; i < bc.Skills.Length; i++)
        {
            var skill = bc.Skills[i];

            if (skill.Base > 0.0)
            {
                skill.Base /= SkillsBuff;
            }
        }

        bc.PassiveSpeed *= SpeedBuff;
        bc.ActiveSpeed *= SpeedBuff;
        bc.ScaleMoveSpeed(SpeedBuff);
        bc.SnapSpeedsToTable(); // an ulp of scaling drift must not read as hand-tuned
        bc.CurrentSpeed = bc.PassiveSpeed;

        bc.DamageMin -= DamageBuff;
        bc.DamageMax -= DamageBuff;

        if (bc.Fame > 0)
        {
            bc.Fame = (int)(bc.Fame / FameBuff);
        }

        if (bc.Karma != 0)
        {
            bc.Karma = (int)(bc.Karma / KarmaBuff);
        }
    }

    public static bool CheckConvert(BaseCreature bc) => CheckConvert(bc, bc.Location, bc.Map);

    public static bool CheckConvert(BaseCreature bc, Point3D location, Map m)
    {
        if (!Core.AOS)
        {
            return false;
        }

        if (Array.IndexOf(Maps, m) == -1)
        {
            return false;
        }

        if (bc.Controlled || bc.Summoned || bc.Blessed || bc.IsInvulnerable || bc.NoKillAwards ||
            bc.GivesMLMinorArtifact || bc.IsParagon ||
            bc is BaseChampion or Harrower or BaseVendor or BaseEscortable or Clone or BaseHire or BaseShieldGuard
                or BaseMount or BaseQuester)
        {
            return false;
        }

        // Keep peaceful wilderness critters and neutral mounts out of Paragon rolls on non-Ilshenar hunting facets.
        if (m != Map.Ilshenar && bc.FightMode is FightMode.None or FightMode.Aggressor)
        {
            return false;
        }

        if (m != null)
        {
            var region = Region.Find(location, m);

            if (region != null)
            {
                if (region.IsPartOf<ChampionSpawnRegion>() ||
                    region.IsPartOf<TownRegion>() ||
                    region.IsPartOf<ApprenticeRegion>() ||
                    region.IsPartOf<HouseRegion>() ||
                    region.IsPartOf("Haven Island") ||
                    region.IsPartOf("New Haven") ||
                    region.IsPartOf("Haven") ||
                    region.IsPartOf("Ocllo"))
                {
                    return false;
                }

                if (region.GetRegion<GuardedRegion>() is { } guarded && !guarded.IsDisabled())
                {
                    return false;
                }
            }
        }

        var fame = bc.Fame;

        if (fame > 32000)
        {
            fame = 32000;
        }

        var chance = 1 / Math.Round(20.0 - fame / 3200.0);

        return chance > Utility.RandomDouble();
    }

    public static double GetArtifactChance(Mobile m, BaseCreature bc)
    {
        if (!Core.AOS || m == null || bc == null)
        {
            return 0.0;
        }

        double fame = bc.Fame;

        if (fame > 32000)
        {
            fame = 32000;
        }

        var clampedLuck = Math.Max(0, m.Luck);
        var luckFactor = Math.Max(0.25, (100.0 - Math.Sqrt(clampedLuck)) / 100.0);
        var baseDenominator = Math.Max(
            10.0,
            100.0 * (0.83 - Math.Round(Math.Log(Math.Round(fame / 6000.0, 3) + 0.001, 10), 3))
        );
        var chance = 1.0 / (baseDenominator * luckFactor);

        if (SoloProgression.Enabled)
        {
            var soloLuckScale = 1.0 + Math.Min(1.0, Math.Sqrt(clampedLuck) / 60.0);
            chance = Math.Min(0.50, chance * 3.5 * soloLuckScale);
        }

        return chance;
    }

    public static bool CheckArtifactChance(Mobile m, BaseCreature bc) =>
        GetArtifactChance(m, bc) > Utility.RandomDouble();

    public static void GiveArtifactTo(Mobile m)
    {
        var item = Artifacts.RandomElement().CreateInstance<Item>();

        if (m.AddToBackpack(item))
        {
            m.SendMessage("As a reward for slaying the mighty paragon, an artifact has been placed in your backpack.");
        }
        else
        {
            m.SendMessage(
                "As your backpack is full, your reward for destroying the legendary paragon has been placed at your feet."
            );
        }
    }

    private class ParagonStamRegen : Timer
    {
        private readonly BaseCreature m_Owner;

        public ParagonStamRegen(Mobile m) : base(FastRegenRate, FastRegenRate) => m_Owner = m as BaseCreature;

        protected override void OnTick()
        {
            if (!m_Owner.Deleted && m_Owner.IsParagon && m_Owner.Map != Map.Internal)
            {
                m_Owner.Stam++;

                Delay = Interval = m_Owner.Stam < m_Owner.StamMax * .75 ? FastRegenRate : CPUSaverRate;
            }
            else
            {
                Stop();
            }
        }
    }
}
