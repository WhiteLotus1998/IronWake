namespace Ironwake.Core;

/// <summary>
/// The <c>seen_far:</c> header (issue 973, rounds 334 to 337; DECISIONS/0240): one unit a sleeping
/// Guard member hears <paramref name="Extra"/> tiles farther than the wake rule's radius while that
/// unit stands on the board as a player unit. Noise and death wakes are untouched, and an enemy-side
/// unit of the same id reads nothing. A campaign seats the unit when it is with the company and
/// refuses to bench it (<see cref="CampaignRecord.Bench"/>), so the sighting cannot be dodged by
/// leaving it home. <see cref="WakeCheck"/> is its one reader for the radius.
/// With <paramref name="Group"/> set (issue 1044, rounds 368 and 369), the header also names a Guard
/// group that sees the unit coming: as <paramref name="Turn"/>'s enemy phase begins, with the unit on
/// the board as a player unit, the group wakes if it still sleeps (<see cref="AtPhaseStart"/>), and the
/// board prints the turn until then (<see cref="ComingLine"/>). The sighting is the board's, not the
/// party's route, so it never fixes a <c>route_drift:</c> route (<see cref="Routes"/>).
/// </summary>
public sealed record SeenFar(string UnitId, int Extra, string? Group = null, int Turn = 0)
{
    /// <summary>The fewest tiles the header may add.</summary>
    public const int MinExtra = 1;

    /// <summary>The most tiles the header may add.</summary>
    public const int MaxExtra = 4;

    /// <summary>The tiles added to the wake radius around <paramref name="unit"/>: <see cref="Extra"/> for this unit on the player side, else 0.</summary>
    public int For(BattleUnit unit) => unit.Side == Side.Player && unit.Id == UnitId ? Extra : 0;

    /// <summary>The header's value as <see cref="MapDefinition"/> files write it: <c>rook 2</c>.</summary>
    public override string ToString() => Group is null ? $"{UnitId} {Extra}" : $"{UnitId} {Extra}; {Group} turn {Turn}";

    /// <summary>
    /// The line under the wake legend while <paramref name="name"/> is seen far:
    /// <c>seen far: Rook wakes a sleeper within 6 (seen for miles)</c>.
    /// </summary>
    public string Line(string name, GameContent content) =>
        $"seen far: {name} wakes a sleeper within {content.WakeRadius + Extra} (seen for miles)";

    /// <summary>The player unit this header reads on <paramref name="state"/>'s board, or null when it is not there.</summary>
    public BattleUnit? Seen(BattleState state) => state.UnitsOf(Side.Player).FirstOrDefault(u => For(u) > 0);

    /// <summary>
    /// As a phase begins: on <see cref="Turn"/>'s enemy phase, with <see cref="Group"/> set, the unit
    /// on the board and the group asleep with a member left, wakes the group with one
    /// <see cref="GroupWoke"/> of cause <see cref="WakeCause.Sighted"/>, the seen unit as its caller.
    /// Unchanged on any other phase or map.
    /// </summary>
    public static BattleState AtPhaseStart(BattleState state, List<GameEvent> events)
    {
        if (state.Map.SeenFar is not { Group: { } group } far || state.Phase != Side.Enemy || state.Turn != far.Turn
            || far.Seen(state) is not { } seen || state.IsAwake(group) || state.UnitsOf(Side.Enemy).All(u => u.Group != group))
        {
            return state;
        }

        events.Add(new GroupWoke(group, WakeCause.Sighted, CalledBy: seen.Id));
        return state.Wake(group);
    }

    /// <summary>
    /// The line the board prints under the wake legend while the sighting is still to come:
    /// <c>seen coming: the south group wakes on turn 3's enemy phase, Rook seen from afar</c>; null
    /// once the turn has passed, when the group is awake or gone, or when the unit is not on the board.
    /// </summary>
    public static string? ComingLine(BattleState state)
    {
        if (state.Map.SeenFar is not { Group: { } group } far || state.Turn > far.Turn || far.Seen(state) is not { } seen
            || state.IsAwake(group) || state.UnitsOf(Side.Enemy).All(u => u.Group != group))
        {
            return null;
        }

        return $"seen coming: {UnitNames.Group(group)} wakes on turn {far.Turn}'s enemy phase, {seen.Unit.Name} seen from afar";
    }
}
