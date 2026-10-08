namespace Ironwake.Core;

/// <summary>
/// A first strike that resolved (issue 1359): the turn and phase it was rolled on, who struck whom,
/// the raw hit chance it rolled against, and whether it hit. Its key is the first strike's
/// <see cref="RollKey.Combat"/> tuple, so the same striker on the same target on the same turn and
/// phase draws the same roll again.
/// </summary>
public sealed record SeenStrike(int Turn, Side Phase, string StrikerId, string TargetId, int HitChance, bool Hit);

/// <summary>
/// What a Recall has shown (issue 1359, Table rounds 461 and 462). Rolls are keyed by the turn
/// (DESIGN.md section 7), so a first strike that resolved in a line a Recall discarded rolls the
/// same if the same striker takes it at the same target on the same turn and phase. The battle keeps
/// every such strike, from every Recall, in <see cref="BattleState.Seen"/>; the forecast and
/// <c>threat</c> print it. A seen roll is fixed but its hit chance is not, so a seen strike speaks
/// only when its outcome still follows: a miss at or under the chance it missed at, a hit at or
/// over the chance it hit at. Both roll schemes land more often as the chance rises, so this is never wrong.
/// Only first strikes are kept: a double's second strike or a counter's second round waits on what
/// came before it, which a changed board can change.
/// </summary>
public static class SeenRolls
{
    /// <summary>
    /// The first strike of each side in a resolved combat, as <see cref="SeenStrike"/>s, the attacker's
    /// first. <paramref name="forecast"/> is the forecast the resolver rolled against.
    /// </summary>
    public static IEnumerable<SeenStrike> FirstStrikes(CombatResult result, CombatForecast forecast, CombatContext context, string attackerId)
    {
        var seen = new HashSet<string>();
        foreach (var strike in result.Strikes)
        {
            if (!seen.Add(strike.AttackerId))
            {
                continue;
            }

            var side = strike.AttackerId == attackerId ? forecast.Attacker : forecast.Defender;
            yield return new SeenStrike(context.Turn, context.Phase, strike.AttackerId, strike.TargetId, side.HitChance, strike.Hit);
        }
    }

    /// <summary>
    /// The state a Recall lands on, carrying what it showed: every strike already seen, plus each first
    /// strike that resolved in the discarded line (<see cref="BattleState.Struck"/> past the target's own).
    /// </summary>
    public static BattleState Carry(BattleState from, BattleState restored)
    {
        var discarded = from.Struck.Skip(restored.Struck.Count);
        var seen = ValueList<SeenStrike>.From(from.Seen.Concat(discarded).Distinct());
        return restored with { Seen = seen };
    }

    /// <summary>
    /// Whether a Recall has shown this first strike: true if it hits, false if it misses, null when no
    /// discarded line rolled it, or when its hit chance has moved past the one it was seen at in the
    /// direction that could change the outcome.
    /// </summary>
    public static bool? Outcome(BattleState state, int turn, Side phase, string strikerId, string targetId, int hitChance)
    {
        foreach (var s in state.Seen)
        {
            if (s.Turn != turn || s.Phase != phase || s.StrikerId != strikerId || s.TargetId != targetId)
            {
                continue;
            }

            if (s.Hit && hitChance >= s.HitChance)
            {
                return true;
            }

            if (!s.Hit && hitChance <= s.HitChance)
            {
                return false;
            }
        }

        return null;
    }

    /// <summary>
    /// The console's line for a combat a Recall has partly shown, <c>Seen before the recall: Keziah misses, Brigand hits</c>,
    /// the striker's first strike then the counter's, each only where <see cref="Outcome"/> knows it. Null when it knows neither.
    /// </summary>
    public static string? Line(BattleState state, int turn, Side phase, string strikerId, string strikerName, int strikerHit, string targetId, string targetName, int? counterHit)
    {
        var parts = new List<string>();
        if (Outcome(state, turn, phase, strikerId, targetId, strikerHit) is { } first)
        {
            parts.Add($"{strikerName} {(first ? "hits" : "misses")}");
        }

        if (counterHit is { } chance && Outcome(state, turn, phase, targetId, strikerId, chance) is { } counter)
        {
            parts.Add($"{targetName} {(counter ? "hits" : "misses")}");
        }

        return parts.Count == 0 ? null : "Seen before the recall: " + string.Join(", ", parts);
    }
}
