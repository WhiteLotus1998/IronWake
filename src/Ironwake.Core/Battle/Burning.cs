namespace Ironwake.Core;

/// <summary>
/// A school's burn rider (issue 1243, DECISIONS/0297; fire's, from <c>rules.json</c>). A hit from a
/// tome that names its school's <see cref="RiderKind.Burn"/> rider (<see cref="GameContent.RiderOf"/>)
/// on a unit that survives the combat leaves it burning: at the start of each of its own side's next
/// <see cref="SchoolRider.Phases"/> phases it loses <see cref="SchoolRider.Amount"/> HP, never below 1.
/// <list type="bullet">
/// <item>A miss does nothing, and a kill leaves no one to burn.</item>
/// <item>It does not stack: a second burn refreshes the count and takes no more a tick.</item>
/// <item>Tile fire (DESIGN.md 13.15) is untouched and does not stack with it either: a burning unit
/// on a burning tile loses the larger of the two, as one <see cref="UnitBurned"/>.</item>
/// </list>
/// Everything is board state (<see cref="BattleUnit.Burn"/>, <see cref="BattleUnit.BurnPhases"/>), so
/// Recall restores it with the board.
/// </summary>
public static class Burning
{
    /// <summary>
    /// After a combat or a strike: each unit in <paramref name="strikes"/> still standing that a hit
    /// from a burning school's tome landed on is set burning (<see cref="UnitIgnited"/>), its count
    /// refreshed. <paramref name="aWeapon"/> is what <paramref name="aId"/> struck with and
    /// <paramref name="bWeapon"/> what <paramref name="bId"/> did; a null weapon never burns.
    /// </summary>
    public static BattleState AfterCombat(BattleState state, GameContent content, string aId, Weapon? aWeapon, string bId, Weapon? bWeapon, ValueList<StrikeEvent> strikes, List<GameEvent> events)
    {
        foreach (var (strikerId, weapon, targetId) in new[] { (aId, aWeapon, bId), (bId, bWeapon, aId) })
        {
            if (Rider(content, weapon) is not { } rider || !strikes.Any(s => s.AttackerId == strikerId && s.TargetId == targetId && s.Hit))
            {
                continue;
            }

            if (state.Find(targetId) is { } target)
            {
                events.Add(new UnitIgnited(target.Id, strikerId, rider.Amount, rider.Phases));
                state = state.WithUnit(target with { Burn = rider.Amount, BurnPhases = rider.Phases });
            }
        }

        return state;
    }

    /// <summary>The burn rider <paramref name="weapon"/> carries, or null when it carries none.</summary>
    public static SchoolRider? Rider(GameContent content, Weapon? weapon) =>
        content.RiderOf(weapon) is { Kind: RiderKind.Burn } rider ? rider : null;

    /// <summary>What <paramref name="unit"/>'s burn takes at its side's next phase start: <see cref="BattleUnit.Burn"/> while the count runs, else 0.</summary>
    public static int Due(BattleUnit unit) => unit.BurnPhases > 0 ? unit.Burn : 0;

    /// <summary><paramref name="unit"/> with its burn's count turned once, as its side's phase begins: cleared when the count runs out.</summary>
    public static BattleUnit Ticked(BattleUnit unit) =>
        unit.BurnPhases > 1 ? unit with { BurnPhases = unit.BurnPhases - 1 } : unit with { Burn = 0, BurnPhases = 0 };

    /// <summary>The forecast's words for a side whose weapon burns, after its strike columns: <c> burns 2 for two phases</c>; empty when it does not.</summary>
    public static string ForecastText(GameContent content, Weapon? weapon) =>
        Rider(content, weapon) is { } rider ? $" burns {rider.Amount} for {SchoolRider.PhasesText(rider.Phases)}" : "";

    /// <summary>The unit card's line for a burning unit: <c>burning: 2 for two more phases</c>; null when it is not burning.</summary>
    public static string? CardLine(BattleUnit unit) =>
        Due(unit) > 0 ? $"burning: {unit.Burn} for {More(unit.BurnPhases)}" : null;

    /// <summary>
    /// What a burn rider is worth to a planner striking <paramref name="target"/> (issue 1243): the HP
    /// its ticks would take, never below 1 and only the phases a refresh adds to a burn already
    /// running, from the HP <paramref name="hpAfter"/> the strikes are expected to leave, weighted
    /// by <paramref name="landed"/>, the chance some strike hits. 0 when the weapon carries no burn.
    /// </summary>
    public static double Expected(GameContent content, Weapon? weapon, BattleUnit target, double hpAfter, double landed)
    {
        if (Rider(content, weapon) is not { } rider)
        {
            return 0;
        }

        var added = rider.Amount * rider.Phases - Due(target) * target.BurnPhases;
        return Math.Max(0, Math.Min(added, hpAfter - 1)) * landed;
    }

    private static string More(int phases) => SchoolRider.PhasesText(phases).Replace(" phase", " more phase");
}
