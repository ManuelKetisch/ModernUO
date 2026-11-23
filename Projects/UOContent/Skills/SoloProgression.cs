using System;
using Server.Misc;
using Server.Mobiles;

namespace Server;

public static class SoloProgression
{
    public const int MaxSkill = 120;
    public const int BaseStat = 125;
    public const int MaxStat = 150;

    private const int LegacyBaseStatTotal = 225;
    private const int BaseStatTotal = BaseStat * 3;

    public static bool Enabled { get; private set; }
    public static int ResistanceCap { get; private set; }
    public static int DefenseChanceCap { get; private set; }

    public static void Configure()
    {
        Enabled = ServerConfiguration.GetOrUpdateSetting("progression.soloEnabled", false);
        ResistanceCap = Math.Clamp(ServerConfiguration.GetOrUpdateSetting("progression.soloResistanceCap", 85), 0, 100);
        DefenseChanceCap =
            Math.Clamp(ServerConfiguration.GetOrUpdateSetting("progression.soloDefenseChanceCap", 85), 0, 100);
    }

    public static void Initialize()
    {
        if (Enabled)
        {
            CommandSystem.Register("Practice", AccessLevel.Player, PracticeCommand);
        }
    }

    public static bool AppliesTo(Mobile mobile) => Enabled && mobile is PlayerMobile;

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
