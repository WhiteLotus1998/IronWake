namespace Ironwake.Core;

/// <summary>
/// The Sky Captain's drake frost (issue 1127, DECISIONS/0262; Lotus's design, Chat's numbers, Table #1112):
/// Rook's alone, since it is her drake's, and instead of <see cref="Stoop"/>.
/// <list type="bullet">
/// <item>It fires when a rider holding <see cref="DrakeFrostEffect"/> ends a Move it flew, at least one tile and
/// not grounded, with at least one enemy orthogonally beside the tile it landed on. Standing still, a carry, a
/// shove, a Canto, a dash or a Fall back never fires it, and a landing beside no enemy never spends it.</item>
/// <item>Order: the move, then the frost, then the rider's action, so it may attack a unit it just frosted.</item>
/// <item>Each enemy on those four tiles takes <see cref="DrakeFrostEffect.Damage"/>, never below 1 (the frost
/// never kills, as burn and the rock do not), and is held: Mov at most <see cref="HoldMov"/> and no Canto
/// through its side's next phase, on the chill's clock (<see cref="BattleUnit.Frosted"/>). Allies are untouched.
/// A boss takes the damage and is never held. A unit already held is not held again, and the hold does not stack
/// with a chill: Mov 1 is Mov 1.</item>
/// <item>Then it rests: fired on turn N, it is ready again on turn N + Rest + 1 (<see cref="BattleUnit.FrostTurn"/>).</item>
/// </list>
/// Everything is board state, so Recall restores it with the board, and <c>threat</c>, the planners and the Sim
/// read the hold through <see cref="BattleState.ReachOf"/>.
/// </summary>
public static class DrakeFrost
{
    /// <summary>The most Mov a held unit has.</summary>
    public const int HoldMov = 1;

    /// <summary><paramref name="mov"/> as a hold leaves it for <paramref name="unit"/>: at most <see cref="HoldMov"/> while held.</summary>
    public static int Mov(int mov, BattleUnit unit) => unit.Frosted > 0 ? Math.Min(mov, HoldMov) : mov;

    /// <summary>The frost <paramref name="unit"/> carries, or null: the effect needs a drake (<see cref="AbilityRules.DrakeFrost"/>).</summary>
    public static DrakeFrostEffect? Of(GameContent content, BattleUnit unit) =>
        AbilityRules.DrakeFrost(content.AbilitiesOf(unit.Unit), unit.Unit);

    /// <summary>The first turn <paramref name="unit"/>'s frost is ready again after it fired, or null when it has not fired.</summary>
    public static int? ReadyTurn(GameContent content, BattleUnit unit) =>
        unit.FrostTurn is { } fired && Of(content, unit) is { } frost ? fired + frost.Rest + 1 : null;

    /// <summary>Whether <paramref name="unit"/> carries the frost and it is not resting on <paramref name="state"/>'s turn.</summary>
    public static bool Ready(BattleState state, GameContent content, BattleUnit unit) =>
        Of(content, unit) is not null && (ReadyTurn(content, unit) is not { } ready || state.Turn >= ready);

    /// <summary>The enemies of <paramref name="rider"/> orthogonally beside <paramref name="at"/>, in board order; <paramref name="rider"/> itself is never one.</summary>
    public static IReadOnlyList<BattleUnit> Targets(BattleState state, BattleUnit rider, Coord at) =>
        state.Units.Where(u => u.Side != rider.Side && u.At.DistanceTo(at) == 1).ToList();

    /// <summary>
    /// Whether a Move of <paramref name="rider"/>, standing where it took off, to <paramref name="to"/> would fire
    /// the frost: the frost ready, the rider flying (<see cref="Grounding.MovementOf"/>), the move at least one tile,
    /// and an enemy beside <paramref name="to"/>.
    /// </summary>
    public static bool WouldFire(BattleState state, GameContent content, BattleUnit rider, Coord to) =>
        to != rider.At
        && Ready(state, content, rider)
        && Grounding.MovementOf(rider, content) == MovementType.Flying
        && Targets(state, rider, to).Count > 0;

    /// <summary>
    /// After <paramref name="before"/>'s Move to <paramref name="to"/> is on the board: when it fires, each enemy
    /// beside the landing is struck and, a boss aside and unless already held, held (<see cref="UnitFrosted"/>), and the
    /// rider's frost starts its rest. <paramref name="before"/> is the rider as it stood before the move.
    /// </summary>
    public static BattleState AfterMove(BattleState state, GameContent content, BattleUnit before, Coord to, List<GameEvent> events)
    {
        if (!WouldFire(state, content, before, to) || state.Find(before.Id) is null)
        {
            return state;
        }

        state = Strike(state, content, before, to, events);
        return state.WithUnit(state.Find(before.Id)! with { FrostTurn = state.Turn });
    }

    /// <summary>
    /// The frost's strike on the enemies beside <paramref name="to"/>, as <paramref name="rider"/>'s landing there makes it,
    /// with no check that it fires and no rest started: what <see cref="AfterMove"/> does, and the board <c>threat from</c>
    /// prices the coming enemy phase on.
    /// </summary>
    public static BattleState Strike(BattleState state, GameContent content, BattleUnit rider, Coord to, List<GameEvent> events)
    {
        if (Of(content, rider) is not { } frost)
        {
            return state;
        }

        foreach (var target in Targets(state, rider, to))
        {
            var hp = Math.Max(1, target.Hp - frost.Damage);
            var held = !target.IsBoss && target.Frosted == 0;
            events.Add(new UnitFrosted(target.Id, rider.Id, target.Hp - hp, hp, held, target.IsBoss, target.Side, target.Side == state.Phase));
            state = state.WithUnit(target with { Hp = hp, Frosted = held ? 1 : target.Frosted });
        }

        return state;
    }

    /// <summary>
    /// The rider's card line (issue 1127): <c>Frost: ready</c> or <c>Frost: resting (ready turn 5)</c>, with what it
    /// does; null for a unit without the frost.
    /// </summary>
    public static string? CardLine(BattleState state, GameContent content, BattleUnit unit)
    {
        if (Of(content, unit) is not { } frost)
        {
            return null;
        }

        var status = Ready(state, content, unit) ? "ready" : $"resting (ready turn {ReadyTurn(content, unit)})";
        return $"Frost: {status}; on a flown landing beside enemies, each takes {frost.Damage} and is held to {HoldMov} tile next phase (bosses: no hold)";
    }

    /// <summary>The card line for a held unit: <c>frosted: Mov 1, no Canto, until enemy phase ends</c>; null when it is not held.</summary>
    public static string? HeldLine(BattleState state, BattleUnit unit) =>
        unit.Frosted > 0 ? $"frosted: Mov {HoldMov}, no Canto, until {Frost.Until(unit.Side, unit.Frosted == 1 && unit.Side == state.Phase)}" : null;

    /// <summary>
    /// What a Move of <paramref name="rider"/> to <paramref name="to"/> would frost, as the <c>move</c> preview and
    /// <c>threat from</c> print it: <c>Frost: 3 enemies, 1 damage, held to 1 tile (Hask: no hold, boss)</c>;
    /// null when it would not fire. <paramref name="names"/> names units by id.
    /// </summary>
    public static string? Preview(BattleState state, GameContent content, BattleUnit rider, Coord to, UnitNames names)
    {
        if (rider.Moved || !WouldFire(state, content, rider, to) || Of(content, rider) is not { } frost)
        {
            return null;
        }

        var targets = Targets(state, rider, to);
        var count = targets.Count == 1 ? "1 enemy" : $"{targets.Count} enemies";
        var notes = targets.Where(t => t.IsBoss || t.Frosted > 0)
            .Select(t => $"{names[t.Id]}: no hold, {(t.IsBoss ? "boss" : "already held")}")
            .ToList();
        var floored = targets.Where(t => t.Hp <= frost.Damage).Select(t => $"{names[t.Id]} stays at 1").ToList();
        var tail = string.Join("; ", notes.Concat(floored));
        return $"Frost: {count}, {frost.Damage} damage, held to {HoldMov} tile" + (tail.Length > 0 ? $" ({tail})" : "");
    }
}
