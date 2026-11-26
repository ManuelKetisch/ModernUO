using System;
using System.Runtime.CompilerServices;
using Server.Misc;
using Server.Mobiles;

namespace Server;

public static class SoloProgression
{
    public const int MaxSkill = 120;
    public const int BaseStat = 125;
    public const int MaxStat = 150;
    public const int DefaultAttackChanceCap = 60;
    public const int PvMMissStreakThreshold = 2;
    public const double PvMMissStreakBonusStep = 0.15;
    public const double PvMMissStreakMaxChance = 0.98;

    private const int LegacyBaseStatTotal = 225;
    private const int BaseStatTotal = BaseStat * 3;

    private static readonly ConditionalWeakTable<Mobile, PvMMissStreakState> _pvmMissStreaks = new();

    public static bool Enabled { get; private set; }
    public static int ResistanceCap { get; private set; }
    public static int DefenseChanceCap { get; private set; }
    public static int AttackChanceCap { get; private set; } = DefaultAttackChanceCap;

    public static void Configure()
    {
        Enabled = ServerConfiguration.GetOrUpdateSetting("progression.soloEnabled", false);
        ResistanceCap = Math.Clamp(ServerConfiguration.GetOrUpdateSetting("progression.soloResistanceCap", 85), 0, 100);
        DefenseChanceCap =
            Math.Clamp(ServerConfiguration.GetOrUpdateSetting("progression.soloDefenseChanceCap", 85), 0, 100);
        AttackChanceCap =
            Math.Clamp(
                ServerConfiguration.GetOrUpdateSetting("progression.soloAttackChanceCap", DefaultAttackChanceCap),
                0,
                100
            );
    }

    public static void Initialize()
    {
        if (Enabled)
        {
            CommandSystem.Register("Practice", AccessLevel.Player, PracticeCommand);
        }
    }

    public static bool AppliesTo(Mobile mobile) => Enabled && mobile is PlayerMobile;

    public static int GetAttackChanceCap(Mobile attacker) =>
        AppliesTo(attacker) ? AttackChanceCap : 45;

    public static double GetPvMMissStreakBonus(Mobile attacker, Mobile defender)
    {
        if (!AppliesTo(attacker) || defender is not BaseCreature)
        {
            return 0.0;
        }

        if (!_pvmMissStreaks.TryGetValue(attacker, out var state))
        {
            return 0.0;
        }

        if (!state.LastTarget.TryGetTarget(out var lastTarget) || !ReferenceEquals(lastTarget, defender))
        {
            state.LastTarget.SetTarget(defender);
            state.ConsecutiveMisses = 0;
            return 0.0;
        }

        if (state.ConsecutiveMisses < PvMMissStreakThreshold)
        {
            return 0.0;
        }

        return (state.ConsecutiveMisses - (PvMMissStreakThreshold - 1)) * PvMMissStreakBonusStep;
    }

    public static void RecordPvMAttackResult(Mobile attacker, Mobile defender, bool hit)
    {
        if (!AppliesTo(attacker))
        {
            return;
        }

        if (defender is not BaseCreature)
        {
            if (_pvmMissStreaks.TryGetValue(attacker, out var existing))
            {
                existing.LastTarget.SetTarget(null);
                existing.ConsecutiveMisses = 0;
            }

            return;
        }

        var state = _pvmMissStreaks.GetOrCreateValue(attacker);

        if (hit)
        {
            state.LastTarget.SetTarget(defender);
            state.ConsecutiveMisses = 0;
            return;
        }

        if (!state.LastTarget.TryGetTarget(out var lastTarget) || !ReferenceEquals(lastTarget, defender))
        {
            state.LastTarget.SetTarget(defender);
            state.ConsecutiveMisses = 1;
            return;
        }

        if (state.ConsecutiveMisses < 10)
        {
            state.ConsecutiveMisses++;
        }
    }

    public static void ResetPvMMissStreak(Mobile attacker)
    {
        if (attacker != null && _pvmMissStreaks.TryGetValue(attacker, out var state))
        {
            state.LastTarget.SetTarget(null);
            state.ConsecutiveMisses = 0;
        }
    }

    private sealed class PvMMissStreakState
    {
        public readonly WeakReference<Mobile> LastTarget = new(null);
        public int ConsecutiveMisses;
    }

    public static void Apply(PlayerMobile player)
    {
        if (!Enabled)
        {
            return;
        }

        player.SkillsCap = player.Skills.Length * (MaxSkill * 10);
        player.StatCap = GetStatLimit(player) * 3;
    }

    public static int GetStatLimit(PlayerMobile player)
    {
        // The saved combined cap represents three identical training ceilings.
        if (player.StatCap >= BaseStatTotal)
        {
            return Math.Clamp(player.StatCap / 3, BaseStat, MaxStat);
        }

        var veteranBonus = player.HasStatReward ? 5 : 0;
        return BaseStat + GetStatScrollBonus(player.StatCap - veteranBonus);
    }

    public static int GetStatScrollBonus(int legacyCap) =>
        Math.Clamp(legacyCap - LegacyBaseStatTotal, 0, MaxStat - BaseStat);

    private static void PracticeCommand(CommandEventArgs args)
    {
        if (args.Mobile is not PlayerMobile { Alive: true, NetState: { } session } player)
        {
            return;
        }

        var skill = FindSkill(player.Skills, args.ArgString.Trim());
        if (skill == null)
        {
            player.SendMessage("Enter a skill name, for example '[Practice Imbuing'.");
            return;
        }

        if (!CanPractice(skill))
        {
            player.SendMessage("Set the skill to Raise and make sure it is below its cap.");
            return;
        }

        if (!player.BeginAction(typeof(SoloProgression)))
        {
            player.SendMessage("You are already practicing a skill.");
            return;
        }

        var map = player.Map;
        var location = player.Location;
        player.SendMessage($"Stay here for 30 seconds to practice {skill.Info.Name}.");
        Timer.DelayCall(
            TimeSpan.FromSeconds(30),
            () =>
            {
                player.EndAction(typeof(SoloProgression));
                if (player.Deleted || player.NetState != session)
                {
                    return;
                }

                if (!player.Alive || player.Map != map || player.Location != location || !CanPractice(skill))
                {
                    player.SendMessage("Practice interrupted.");
                    return;
                }

                SkillCheck.Gain(player, skill);
                player.SendMessage($"You practiced {skill.Info.Name}. Your base skill is {skill.Base:F1}.");
            }
        );
    }

    private static bool CanPractice(Skill skill) =>
        skill.Lock == SkillLock.Up && skill.Base < Math.Min(skill.Cap, MaxSkill);

    private static Skill FindSkill(Skills skills, string name)
    {
        if (Enum.TryParse<SkillName>(name, true, out var id))
        {
            return skills[id];
        }

        for (var index = 0; index < skills.Length; index++)
        {
            var skill = skills[index];
            if (string.Equals(skill.Info.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return skill;
            }
        }

        return null;
    }
}
