using System;
using Server.Items;

namespace Server.Mobiles;

public partial class BaseCreature
{
    internal const int DiversityBonusExperience = 8;
    private const int DiversityFamilyLimit = 3;
    private static readonly TimeSpan _diversityWindow = TimeSpan.FromMinutes(30);
    private string[] _recentTrainingFamilies;
    private int _recentTrainingFamilyCount;
    private DateTime _trainingDiversityExpires;

    // Slayer groups keep variants of one family from counting as different opponents.
    internal static string OpponentTrainingFamily(BaseCreature creature)
    {
        foreach (var group in SlayerGroup.Groups)
        {
            if (group.Super.Slays(creature))
            {
                return group.Super.Name.ToString();
            }
        }

        var species = creature.GetType();
        return species.FullName ?? species.Name;
    }

    internal int TakePetDiversityBonus(BaseCreature defeated) => TakePetDiversityBonus(defeated, Core.Now);

    internal int TakePetDiversityBonus(BaseCreature defeated, DateTime now)
    {
        if (!CanProgressAsPet || PetExperienceForNextLevel == 0 ||
            defeated is not { Controlled: false, Summoned: false, IsInvulnerable: false, Player: false, Master: null } ||
            defeated is BaseVendor || defeated.HitsMax < Math.Max(50, HitsMax / 2))
        {
            return 0;
        }

        if (_trainingDiversityExpires <= now)
        {
            _recentTrainingFamilyCount = 0;
            _trainingDiversityExpires = now + _diversityWindow;
        }

        if (_recentTrainingFamilyCount >= DiversityFamilyLimit)
        {
            return 0;
        }

        var family = OpponentTrainingFamily(defeated);
        _recentTrainingFamilies ??= new string[DiversityFamilyLimit];
        for (var i = 0; i < _recentTrainingFamilyCount; i++)
        {
            if (_recentTrainingFamilies[i] == family)
            {
                return 0;
            }
        }

        _recentTrainingFamilies[_recentTrainingFamilyCount++] = family;
        return DiversityBonusExperience;
    }
}
