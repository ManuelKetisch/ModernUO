using System;

namespace Server.Mobiles;

internal static class PetExperiencePolicy
{
    internal static int AwardForDamage(
        int opponentMaxHits, int previouslyCreditedDamage, int actualDamage, out int creditedDamage
    )
    {
        if (opponentMaxHits <= 0)
        {
            creditedDamage = 0;
            return 0;
        }

        var before = Math.Clamp(previouslyCreditedDamage, 0, opponentMaxHits);
        creditedDamage = before + Math.Clamp(actualDamage, 0, opponentMaxHits - before);

        // A creature's full health budget yields the same XP regardless of hit size or count.
        var budget = Math.Clamp(12 + opponentMaxHits / 4, 12, 120);
        return (int)((long)creditedDamage * budget / opponentMaxHits - (long)before * budget / opponentMaxHits);
    }
}
