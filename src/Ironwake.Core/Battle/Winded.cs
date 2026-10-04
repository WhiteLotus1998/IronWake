namespace Ironwake.Core;

/// <summary>
/// The dash (DESIGN.md 13.27, experiment), behind a map's <c>dash: on</c> header. Brace prices
/// standing still; the dash prices running. A player unit that has neither moved nor acted may
/// move up to <see cref="ExtraMov"/> tiles past its Move (<see cref="Dash"/>), and that is its
/// Move and its action both: no strike, no item, no exit, no Canto. Until its side's next phase
/// begins it is winded, and every strike against it is at <see cref="Hit"/> more hit. The winded
/// mark sits in the striker's hit slot beside the pin and the brace (<see cref="Brace.StrikeHit"/>),
/// so every forecast, <c>threat</c>, the planner's score and the resolver read one number. The
/// planner, <see cref="Resolver.Legal"/> and the Sim never dash.
/// </summary>
public static class Winded
{
    /// <summary>The tiles a dash adds to the unit's Move (DESIGN.md 13.27, provisional).</summary>
    public const int ExtraMov = 2;

    /// <summary>The hit a strike against a winded unit gains (DESIGN.md 13.27, provisional).</summary>
    public const int Hit = 15;

    /// <summary>The line a <c>dash: on</c> map prints under its board, so the rule is on screen.</summary>
    public const string Legend = "dash: a unit that has not moved or acted may move 2 tiles past its Move, as its whole turn; it is struck at +15 Acc until its side's next phase";

    /// <summary>The hit modifier a strike against <paramref name="target"/> carries: plus <see cref="Hit"/> when it is winded, else 0.</summary>
    public static int HitAgainst(BattleUnit? target) =>
        target is { Winded: true } ? Hit : 0;

    /// <summary>
    /// Whether <paramref name="unit"/> could dash to <paramref name="at"/> now: it may dash
    /// (<see cref="Refusal"/>), has not acted, and the dash's reach lets it end there.
    /// </summary>
    public static bool CanDashTo(BattleState state, GameContent content, BattleUnit unit, Coord at) =>
        state.Phase == Side.Player && !unit.Acted && Refusal(state, unit) is null && state.DashReachOf(unit, content).CanEnd(at);

    /// <summary>
    /// Why <paramref name="unit"/> may not dash now, or null when it may: the map must carry
    /// <c>dash: on</c>, the unit must be a player unit, and it must not have moved or been shoved
    /// this phase (acting is refused before this is asked).
    /// </summary>
    public static string? Refusal(BattleState state, BattleUnit unit) =>
        !state.Map.DashEnabled ? "this map has no dash (dash: on)"
        : unit.Side != Side.Player ? "only a player unit dashes"
        : unit.Moved || unit.Shoved ? "it has already moved this phase; a dash is the whole turn"
        : null;
}
