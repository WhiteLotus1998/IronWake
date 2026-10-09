namespace Ironwake.Core;

/// <summary>
/// Where the shard under the hill is (issue 1386 slice 3a): carried by <paramref name="Bearer"/>, lying on
/// <paramref name="Lies"/>, or <paramref name="Broken"/>. Exactly one holds.
/// </summary>
public sealed record ShardHold(string? Bearer, Coord? Lies, bool Broken)
{
    /// <summary>The shard carried by <paramref name="bearer"/>.</summary>
    public static ShardHold Carried(string bearer) => new(bearer, null, false);

    /// <summary>The shard lying on <paramref name="at"/>.</summary>
    public static ShardHold Lying(Coord at) => new(null, at, false);

    /// <summary>The shard broken.</summary>
    public static ShardHold Gone { get; } = new(null, null, true);
}

/// <summary>The shard's bearer <paramref name="UnitId"/> left the board on <paramref name="At"/>, and the shard lies there (issue 1386 slice 3a).</summary>
public sealed record ShardDropped(string UnitId, Coord At) : GameEvent;

/// <summary>At the enemy phase start the sworn <paramref name="UnitId"/> walked from <paramref name="From"/> onto <paramref name="At"/> and picked the shard up (issue 1386 slice 3a).</summary>
public sealed record ShardPicked(string UnitId, Coord From, Coord At) : GameEvent;

/// <summary>At the enemy phase start the Kin re-took the fallen sworn <paramref name="UnitId"/> through the whole shard: it stands on <paramref name="At"/> on <paramref name="Hp"/> (issue 1386 slice 3a).</summary>
public sealed record SwornRetaken(string UnitId, Coord At, int Hp) : GameEvent;

/// <summary><paramref name="UnitId"/> took the shard lying on <paramref name="At"/> and broke it (issue 1386 slice 3a): the Kin re-takes no one now.</summary>
public sealed record KinShardBroken(string UnitId, Coord At) : GameEvent;

/// <summary>
/// The shard under the hill (issue 1386 slice 3a; STORY's Under the Hill, Fight; Lotus 2026-10-08, Chat's round 487).
/// A map's <c>kin_shard: x,y</c> header names the enemy placement that carries it. While it is whole, carried or lying,
/// the Kin re-takes one sworn each enemy phase start: the oldest enemy body on the map that was neither a boss nor a
/// Hollow stands again where it fell, or on the free tile nearest it, on full HP under its own id, moved and acted, so it
/// acts from the next enemy phase (<see cref="AtPhaseStart"/>). The bearer leaving the board by any way drops the shard on
/// the tile he fell on (<see cref="After"/>). A company unit on that tile or orthogonally beside it, not yet acted, takes
/// it and breaks it as its action (<c>take &lt;unit&gt; shard</c>, <see cref="TakeShard"/> naming <see cref="Ground"/>),
/// after its Move or without one, no Canto after; broken, it re-takes no one. A shard still lying at an enemy phase start
/// goes to the nearest sworn, not a boss, whose Move reaches its tile (then the lowest id): he walks onto it and spends
/// that phase holding it, moved and acted. None reaches it: it lies another phase. Everything is board state, so Recall
/// restores it.
/// </summary>
public static class KinShard
{
    /// <summary>The word a take names in place of a boss to take the shard off the ground: <c>take &lt;unit&gt; shard</c>.</summary>
    public const string Ground = "shard";

    /// <summary>Where the shard is on <paramref name="state"/>, or null on a map without one.</summary>
    public static ShardHold? Of(BattleState state)
    {
        if (state.Map.KinShard is not { } placed)
        {
            return null;
        }

        if (state.Shard is { } shard)
        {
            return shard;
        }

        var bearer = state.UnitsOf(Side.Enemy).FirstOrDefault(u =>
            u.PlacementIndex >= 0 && u.PlacementIndex < state.Map.Placements.Count && state.Map.Placements[u.PlacementIndex].At == placed);
        return bearer is null ? null : ShardHold.Carried(bearer.Id);
    }

    /// <summary>Whether the shard on <paramref name="state"/> is whole: carried or lying.</summary>
    public static bool Whole(BattleState state) => Of(state) is { Broken: false };

    /// <summary>The canonical word for <paramref name="shard"/>: <c>carried &lt;id&gt;</c>, <c>lies x,y</c> or <c>broken</c>.</summary>
    public static string Word(ShardHold shard) =>
        shard.Broken ? "broken" : shard.Lies is { } at ? $"lies {at}" : $"carried {shard.Bearer}";

    /// <summary>
    /// After every accepted command: when the shard's bearer on <paramref name="before"/> has left the board on
    /// <paramref name="after"/>, the shard lies on the tile he fell on (<see cref="ShardDropped"/>).
    /// </summary>
    public static BattleState After(BattleState before, BattleState after, List<GameEvent> events)
    {
        if (Of(before) is not { Bearer: { } bearer } || after.Find(bearer) is not null || before.Find(bearer) is not { } carrier)
        {
            return after;
        }

        var at = events.OfType<UnitDied>().LastOrDefault(d => d.UnitId == bearer)?.At ?? carrier.At;
        events.Add(new ShardDropped(bearer, at));
        return after with { Shard = ShardHold.Lying(at) };
    }

    /// <summary>
    /// The shard at <paramref name="side"/>'s phase start: on an enemy phase, a lying shard goes to the nearest sworn
    /// that reaches it (<see cref="ShardPicked"/>), then, while it is whole, the Kin re-takes the oldest sworn body
    /// (<see cref="SwornRetaken"/>).
    /// </summary>
    public static BattleState AtPhaseStart(BattleState state, GameContent content, Side side, List<GameEvent> events)
    {
        if (side != Side.Enemy || Of(state) is not { Broken: false } shard)
        {
            return state;
        }

        if (shard.Lies is { } lies)
        {
            var picker = state.UnitsOf(Side.Enemy)
                .Where(u => Sworn(u) && (state.UnitAt(lies) is not { } standing || standing.Id == u.Id) && state.ReachOf(u, content).Destinations.Contains(lies))
                .OrderBy(u => u.At.DistanceTo(lies))
                .ThenBy(u => u.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (picker is not null)
            {
                events.Add(new ShardPicked(picker.Id, picker.At, lies));
                state = state.WithUnit(picker with { At = lies, Moved = true, Acted = true, Canto = null }) with { Shard = ShardHold.Carried(picker.Id) };
            }
        }

        var body = state.Bodies.FirstOrDefault(b => b.Side == Side.Enemy && Sworn(b) && state.Find(b.Id) is null);
        if (body is null || RiseOn(state, content, body) is not { } at)
        {
            return state;
        }

        var hp = body.MaxHp(content);
        var bodies = state.Bodies.ToList();
        bodies.Remove(body);
        var risen = new BattleUnit(body.Unit, Side.Enemy, at, hp, Moved: true, Acted: true, body.Group, body.Behavior, PlacementIndex: body.PlacementIndex);
        events.Add(new SwornRetaken(body.Id, at, hp));
        return (state with { Bodies = ValueList<BattleUnit>.From(bodies) }).WithRisen(risen);
    }

    private static bool Sworn(BattleUnit unit) => !unit.IsBoss && unit.Hollow is null;

    private static Coord? RiseOn(BattleState state, GameContent content, BattleUnit body)
    {
        var movement = content.Class(body.Unit.ClassId).Movement;
        Coord? best = null;
        for (var y = 0; y < state.Map.Height; y++)
        {
            for (var x = 0; x < state.Map.Width; x++)
            {
                var at = new Coord(x, y);
                if (!state.Map.TerrainAt(at, content).IsPassable(movement) || state.UnitAt(at) is not null)
                {
                    continue;
                }

                if (best is not { } b || at.DistanceTo(body.At) < b.DistanceTo(body.At))
                {
                    best = at;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Why <paramref name="unit"/> cannot take the shard off the ground, in the order the rules are checked, or null
    /// when it can. Whether the unit may act at all is the caller's check.
    /// </summary>
    public static string? Refusal(BattleState state, BattleUnit unit)
    {
        if (Of(state) is not { Lies: { } at })
        {
            return Of(state) is { Bearer: { } bearer }
                ? $"{unit.Id} cannot take the shard: {bearer} carries it; it falls where he falls"
                : $"{unit.Id} cannot take a shard: none lies on this field";
        }

        if (unit.Side != Side.Player)
        {
            return $"{unit.Id} cannot take the shard: only the company takes it";
        }

        if (unit.At.DistanceTo(at) > 1)
        {
            return $"{unit.Id} cannot take the shard on {at}: the taker stands on it or on a tile beside it";
        }

        return null;
    }

    /// <summary><paramref name="unit"/> takes the lying shard and breaks it: the taker is done for the phase.</summary>
    public static BattleState Take(BattleState state, BattleUnit unit, List<GameEvent> events)
    {
        events.Add(new KinShardBroken(unit.Id, Of(state)!.Lies!.Value));
        return state.WithUnit(unit with { Moved = true, Acted = true, Canto = null }) with { Shard = ShardHold.Gone };
    }

    /// <summary>The line the board and <c>threat</c> print about the shard, or null on a map without one.</summary>
    public static string? Line(BattleState state, UnitNames names) => Of(state) switch
    {
        null => null,
        { Broken: true } => "The shard is broken: the Kin re-takes no one",
        { Lies: { } at } => $"The shard lies on {at}: take it from on or beside it (take <unit> {Ground}); at the enemy phase the nearest sworn who reaches it picks it up",
        { Bearer: { } bearer } => $"{names[bearer]} carries the shard: while it is whole the Kin re-takes one fallen sworn each enemy phase; it drops where he falls",
        _ => null,
    };
}
