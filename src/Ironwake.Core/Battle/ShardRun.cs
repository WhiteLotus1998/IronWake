namespace Ironwake.Core;

/// <summary>
/// A map's shard race (issue 1386, the secret path; Lotus 2026-10-08, shaped in Table round 487): the inner tile
/// <paramref name="Inner"/> a beaten boss runs back to with the shard, and the phases of his side,
/// <paramref name="Phases"/>, he holds it before he swallows. Read from the <c>shard_race:</c> header.
/// </summary>
public sealed record ShardRace(Coord Inner, int Phases)
{
    /// <summary>The most phases a race may give: past this it is no race.</summary>
    public const int MaxPhases = 9;
}

/// <summary>
/// Stage 1 fell on a shard race (issue 1386): <paramref name="UnitId"/> ran from <paramref name="From"/> to
/// <paramref name="To"/>, the inner tile or the nearest free one to it, the shard in hand, and swallows in
/// <paramref name="Phases"/> of his side's phases unless it is taken.
/// </summary>
public sealed record ShardRaceBegan(string UnitId, Coord From, Coord To, int Phases) : GameEvent;

/// <summary>At his side's phase start the race ticked (issue 1386): <paramref name="UnitId"/> swallows in <paramref name="Left"/> phases, or now at 0.</summary>
public sealed record ShardCountdown(string UnitId, int Left) : GameEvent;

/// <summary>
/// <paramref name="UnitId"/> took the shard from <paramref name="BossId"/> on <paramref name="At"/> and broke it
/// (issue 1386): he leaves the field alive, in the coma, which is not a kill.
/// </summary>
public sealed record ShardBroken(string UnitId, string BossId, Coord At) : GameEvent;

/// <summary>
/// The secret path's race at the keep (issue 1386; DESIGN.md section 8). On a map with a <see cref="ShardRace"/>, the
/// hit, strike, cast, blow or landing that takes a boss's first bar to 0 (every one of them reaches
/// <see cref="Swallow.Take"/>) does not swallow: he runs to the race's inner tile on 1 HP with the shard
/// (<see cref="Begin"/>). While he runs (<see cref="Running"/>) he cannot be attacked, plans and strikes nothing,
/// begins each of his side's phases moved and acted, and a stray hit that would take him to 0 leaves him on 1. At
/// each of his side's phase starts the race ticks; when it reaches 0 he swallows there, the normal stage 2
/// (<see cref="AtPhaseStart"/>). A player unit orthogonally beside him, not yet acted, may take the shard as its
/// action (<see cref="TakeShard"/>): it is broken, and he leaves the board alive, in the coma
/// (<see cref="BattleState.Coma"/>), which is not a kill, so a Defeat Boss map is won. No Canto follows. While
/// he runs, the map's turn limit ends nothing (<see cref="BattleState.Racing"/>). The race's resistance is the map's
/// <see cref="RaceTrigger"/> events, fired as he runs and at each tick (<see cref="MapEvents.AfterRace"/>). Everything
/// is board state, so Recall restores it.
/// </summary>
public static class ShardRun
{
    /// <summary>Whether <paramref name="unit"/> is running with the shard.</summary>
    public static bool Running(BattleUnit unit) => unit.ShardIn is > 0;

    /// <summary>Whether the fall of <paramref name="unit"/>'s first bar starts the race on <paramref name="state"/>: the map has one and he has not run.</summary>
    public static bool Starts(BattleState state, BattleUnit unit) =>
        state.Map.ShardRace is not null && unit is { Kin: not null, Swallowed: false, ShardIn: null };

    /// <summary>
    /// <paramref name="unitId"/>, at 0 HP on <paramref name="state"/>, runs: to the race's inner tile, or the free tile
    /// nearest it he can stand on (then lowest y, then lowest x), on 1 HP, moved and acted, holding.
    /// </summary>
    public static BattleState Begin(BattleState state, GameContent content, string unitId, List<GameEvent> events)
    {
        var unit = state.Find(unitId)!;
        var race = state.Map.ShardRace!;
        var to = RunTo(state, content, unit, race.Inner);
        events.Add(new ShardRaceBegan(unitId, unit.At, to, race.Phases));
        state = state.WithUnit(unit with { At = to, Hp = 1, ShardIn = race.Phases, Moved = true, Acted = true, Behavior = Behavior.Hold, Canto = null });
        return MapEvents.AfterRace(state, content, 0, events);
    }

    private static Coord RunTo(BattleState state, GameContent content, BattleUnit unit, Coord inner)
    {
        var movement = content.Class(unit.Unit.ClassId).Movement;
        Coord? best = null;
        for (var y = 0; y < state.Map.Height; y++)
        {
            for (var x = 0; x < state.Map.Width; x++)
            {
                var at = new Coord(x, y);
                if (!state.Map.TerrainAt(at, content).IsPassable(movement) || (state.UnitAt(at) is { } other && other.Id != unit.Id))
                {
                    continue;
                }

                if (best is not { } b || at.DistanceTo(inner) < b.DistanceTo(inner))
                {
                    best = at;
                }
            }
        }

        return best ?? unit.At;
    }

    /// <summary>
    /// The race at <paramref name="side"/>'s phase start (issue 1386): each runner of that side counts a phase down.
    /// Above 0 he spends the phase holding the shard, moved and acted; at 0 he swallows on his tile
    /// (<see cref="Swallow.Take"/>), and spends that phase swallowing.
    /// </summary>
    public static BattleState AtPhaseStart(BattleState state, GameContent content, Side side, List<GameEvent> events)
    {
        foreach (var runner in state.Units.Where(u => Running(u) && u.Side == side).ToList())
        {
            var left = runner.ShardIn!.Value - 1;
            events.Add(new ShardCountdown(runner.Id, left));
            state = state.WithUnit(runner with { ShardIn = left, Hp = left == 0 ? 0 : runner.Hp, Moved = true, Acted = true });
            if (left == 0)
            {
                state = Swallow.Take(state, content, runner.Id, events);
            }
            else
            {
                state = MapEvents.AfterRace(state, content, state.Map.ShardRace!.Phases - left, events);
            }
        }

        return state;
    }

    /// <summary>Why <paramref name="unit"/> cannot attack <paramref name="target"/> because the target runs with the shard, or null.</summary>
    public static string? AttackRefusal(BattleUnit unit, BattleUnit target) =>
        Running(target) ? $"{unit.Id} cannot attack {target.Id}: beaten, holding the shard; take it from a tile beside (take {unit.Id} {target.Id})" : null;

    /// <summary>
    /// Why <paramref name="unit"/> cannot take the shard from <paramref name="bossId"/>, in the order the rules are
    /// checked, or null when it can. Whether the unit may act at all is the caller's check.
    /// </summary>
    public static string? Refusal(BattleState state, BattleUnit unit, string bossId)
    {
        if (state.Find(bossId) is not { } boss || !Running(boss))
        {
            return $"{unit.Id} cannot take a shard from {bossId}: only a beaten boss running with the shard holds one";
        }

        if (unit.Side != Side.Player)
        {
            return $"{unit.Id} cannot take the shard: only the company takes it";
        }

        if (unit.At.DistanceTo(boss.At) != 1)
        {
            return $"{unit.Id} cannot take the shard from {boss.Id}: the taker stands on a tile beside {boss.Id}";
        }

        return null;
    }

    /// <summary>
    /// <paramref name="unit"/> takes the shard from <paramref name="bossId"/> and breaks it (issue 1386): he leaves the
    /// board alive and joins <see cref="BattleState.Coma"/>; the taker is done for the phase.
    /// </summary>
    public static BattleState TakeShard(BattleState state, BattleUnit unit, string bossId, List<GameEvent> events)
    {
        var boss = state.Find(bossId)!;
        events.Add(new ShardBroken(unit.Id, boss.Id, boss.At));
        var next = state.WithUnit(unit with { Moved = true, Acted = true, Canto = null }).WithoutUnit(boss.Id);
        return next with { Coma = next.Coma.Add(boss.Id) };
    }

    /// <summary>
    /// The line the board, <c>threat</c> and the status print while <paramref name="unit"/> runs:
    /// <c>Hask holds the shard: swallows in 3 (a unit beside takes it as its action: take &lt;unit&gt; hask_warden)</c>; null otherwise.
    /// </summary>
    public static string? Line(BattleUnit unit, UnitNames names) =>
        Running(unit) ? $"{names[unit.Id]} holds the shard: swallows in {unit.ShardIn} (a unit beside takes it as its action: take <unit> {unit.Id})" : null;
}
