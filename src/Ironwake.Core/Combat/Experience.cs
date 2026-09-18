namespace Ironwake.Core;

/// <summary>
/// DESIGN.md section 6: what one combat is worth to a unit, computed once from its best
/// outcome in that combat. A strike that landed earns the strike formula; a kill adds the
/// kill formula; a boss kill adds a flat bonus; nothing landed earns nothing. Level
/// difference is the enemy's level minus the unit's, so fighting up pays more.
/// </summary>
public static class Experience
{
    public const int LevelUpAt = 100;
    public const int BossKillBonus = 20;
    public const int HealExp = 11;
    public const int HealBelowHalfBonus = 5;

    /// <summary>EXP for a combat in which the unit landed a strike or not, killed or not, against a boss or not.</summary>
    public static int ForCombat(int unitLevel, int enemyLevel, bool landed, bool killed, bool boss)
    {
        if (!landed)
        {
            return 0;
        }

        var diff = enemyLevel - unitLevel;
        var exp = Math.Clamp(10 + 2 * diff, 1, 30);
        if (killed)
        {
            exp += Math.Clamp(20 + 3 * diff, 5, 70);
            if (boss)
            {
                exp += BossKillBonus;
            }
        }

        return exp;
    }

    /// <summary>EXP for a heal (section 5): flat, plus a bonus when the target was below half its HP. Issue 9 adds the caller.</summary>
    public static int ForHeal(bool targetBelowHalf) => HealExp + (targetBelowHalf ? HealBelowHalfBonus : 0);
}

/// <summary>One level gained: the new level and which stats rose (each 0 or 1).</summary>
public sealed record LevelUp(int NewLevel, Stats Gains);

/// <summary>The unit after gaining EXP and every level it crossed on the way, in order.</summary>
public sealed record ExpResult(Unit Unit, ValueList<LevelUp> LevelUps);
