namespace Ironwake.Core;

/// <summary>
/// The wind (DESIGN.md 13.28, experiment), behind a map's <c>wind:</c> header. The wake rule of
/// section 8 is a circle; the wind makes it a direction. A sleeping member downwind of a player
/// unit, or of a combat, hears it <see cref="Carry"/> tiles farther than the rule's radius, and one
/// upwind <see cref="Carry"/> tiles nearer; across the wind the radius is the rule's. Downwind
/// means the member lies inside the quarter the wind blows toward: its offset along the wind is
/// larger than its offset across it. The wind can turn on announced turns, and the board prints
/// the way it blows and the turns it will turn. Nothing else reads it: strikes, sight at dusk and
/// the enemy's own hearing stay as they are. <see cref="WakeCheck"/> is its one reader, so the
/// rules, <c>threat from</c> and the captain veto agree.
/// </summary>
public static class Wind
{
    /// <summary>The tiles the wind adds downwind and takes upwind (DESIGN.md 13.28, provisional).</summary>
    public const int Carry = 2;

    /// <summary>The header word for <paramref name="way"/>.</summary>
    public static string Word(WindDirection way) => way.ToString().ToLowerInvariant();

    /// <summary>The direction a header word names, or null.</summary>
    public static WindDirection? Parse(string word) => word switch
    {
        "north" => WindDirection.North,
        "east" => WindDirection.East,
        "south" => WindDirection.South,
        "west" => WindDirection.West,
        _ => null,
    };

    /// <summary>
    /// The change to a wake radius from <paramref name="source"/> (a player unit or a combat) to a
    /// sleeping member at <paramref name="member"/> while the wind blows <paramref name="way"/>:
    /// plus <see cref="Carry"/> downwind, minus <see cref="Carry"/> upwind, 0 across or on the same tile.
    /// </summary>
    public static int Shift(WindDirection way, Coord source, Coord member)
    {
        var (dx, dy) = (member.X - source.X, member.Y - source.Y);
        var (along, across) = way switch
        {
            WindDirection.East => (dx, dy),
            WindDirection.West => (-dx, dy),
            WindDirection.South => (dy, dx),
            _ => (-dy, dx),
        };

        return along > Math.Abs(across) ? Carry
            : -along > Math.Abs(across) ? -Carry
            : 0;
    }

    /// <summary>The radius <paramref name="radius"/> becomes from <paramref name="source"/> to <paramref name="member"/> on <paramref name="state"/>'s turn; unchanged on a map without wind.</summary>
    public static int Radius(BattleState state, int radius, Coord source, Coord member) =>
        state.Map.Wind is { } wind ? radius + Shift(wind.On(state.Turn), source, member) : radius;

    /// <summary>
    /// The line a <c>wind:</c> map prints under its wake legend on <paramref name="turn"/>: the way it
    /// blows, the radii it makes, and every turn still to come on which it turns. Null without wind.
    /// </summary>
    public static string? Line(MapDefinition map, GameContent content, int turn)
    {
        if (map.Wind is not { } wind)
        {
            return null;
        }

        var way = wind.On(turn);
        var near = Math.Max(0, content.WakeRadius - Carry);
        var far = content.WakeRadius + Carry;
        var line = $"wind: blowing {Word(way)}; a sleeper downwind of a unit hears it within {far} (a fight within {content.NoiseRadius + Carry}), upwind within {near} (a fight within {content.NoiseRadius - Carry})";
        var coming = wind.Shifts.Where(s => s.Turn > turn).Select(s => $"turns {Word(s.To)} on turn {s.Turn}").ToList();
        return coming.Count == 0 ? line : line + "; " + string.Join(", ", coming);
    }
    /// <summary>
    /// The wind's coming turn (issue 957): when the wind turns on the turn after
    /// <paramref name="state"/>'s, <see cref="WakeCheck.Run"/> on the board as it stands with that
    /// turn's wind, each group it wakes with the units <see cref="WakeCheck.Wakers"/> names, and each
    /// group a woken one calls. The turn lands as the player phase of that turn begins, so this is a
    /// forecast on the current board: what the enemy phase between kills or wakes can change it.
    /// Null on a map without wind, with no turn due next turn, or once the battle is over.
    /// </summary>
    public static WindTurnWarning? Coming(BattleState state, GameContent content)
    {
        if (state.Map.Wind is not { } wind || state.Outcome.IsOver || wind.Shifts.FirstOrDefault(s => s.Turn == state.Turn + 1) is not { Turn: > 0 } shift)
        {
            return null;
        }

        var turned = state with { Turn = shift.Turn };
        var woke = WakeCheck.Run(state, turned, content, Array.Empty<Noise>(), Array.Empty<string>());
        var wakes = woke.Where(w => w.Cause == WakeCause.Proximity)
            .Select(w => new WindWake(w.Group, ValueList<BattleUnit>.From(WakeCheck.Wakers(turned, content, w.Group))))
            .ToList();
        var calls = woke.Where(w => w.Cause == WakeCause.Call).ToList();
        return new WindTurnWarning(shift.Turn, shift.To, ValueList<WindWake>.From(wakes), ValueList<GroupWoke>.From(calls));
    }

    /// <summary>
    /// The line <c>end</c> prints beside the lethal and the board prints under the wind line
    /// (issue 957): <c>the wind turns east at turn 5: Wren at 2,4 wakes the field group</c>, or,
    /// when the turn wakes nobody and <paramref name="quiet"/> asks for it, <c>... wakes nobody where
    /// your units stand</c>. Null without a coming turn, or when it wakes nobody and
    /// <paramref name="quiet"/> is false.
    /// </summary>
    public static string? ComingLine(BattleState state, GameContent content, UnitNames names, bool quiet)
    {
        if (Coming(state, content) is not { } coming || (coming.Wakes.Count == 0 && !quiet))
        {
            return null;
        }

        var head = $"the wind turns {Word(coming.To)} at turn {coming.Turn}: ";
        if (coming.Wakes.Count == 0)
        {
            return head + "it wakes nobody where your units stand";
        }

        var wakes = coming.Wakes.Select(w =>
            $"{Listed(w.Wakers.Select(u => $"{names[u.Id]} at {u.At}").ToList())} {(w.Wakers.Count == 1 ? "wakes" : "wake")} {UnitNames.Group(w.Group)}");
        var calls = coming.Calls.Select(c => $"{UnitNames.Group(c.CalledBy!)} calls {UnitNames.Group(c.Group)}");
        return head + string.Join("; ", wakes.Concat(calls));
    }

    private static string Listed(IReadOnlyList<string> items) =>
        items.Count == 1 ? items[0] : string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1];

    /// <summary>
    /// The groups <paramref name="unit"/> stopping on <paramref name="tile"/> would wake once the wind
    /// turns next turn and does not wake now (issue 957): each sleeping group whose
    /// <see cref="WakeCheck.Wakers"/> under the coming wind include the unit on the tile, less the
    /// groups <see cref="Queries.StopWakes"/> names now. Null when <see cref="Coming"/> is, or when
    /// <see cref="Queries.StopWakes"/> is (not a player unit's standable tile on a player phase).
    /// The reverse, loud now and quiet then, is never named: a group woken now stays awake.
    /// </summary>
    public static (WindShift Shift, IReadOnlyList<string> Groups)? StopWakesOnTurn(BattleState state, GameContent content, BattleUnit unit, Coord tile)
    {
        if (Queries.StopWakes(state, content, unit, tile) is not { } now || state.Map.Wind is not { } wind || state.Outcome.IsOver
            || wind.Shifts.FirstOrDefault(s => s.Turn == state.Turn + 1) is not { Turn: > 0 } shift)
        {
            return null;
        }

        var turned = state.WithUnit(unit with { At = tile }) with { Turn = shift.Turn };
        var groups = state.Units
            .Where(u => u is { Behavior: Behavior.Guard, Group: { } g } && !state.IsAwake(g))
            .Select(u => u.Group!)
            .Distinct()
            .Where(g => now.All(w => w.Group != g) && WakeCheck.Wakers(turned, content, g).Any(p => p.Id == unit.Id))
            .OrderBy(g => g, StringComparer.Ordinal)
            .ToList();
        return (shift, groups);
    }
}

/// <summary>The wind's coming turn (issue 957): the turn it lands on, the way it will blow, the groups it wakes with their wakers, and the calls they make.</summary>
public sealed record WindTurnWarning(int Turn, WindDirection To, ValueList<WindWake> Wakes, ValueList<GroupWoke> Calls);

/// <summary>A sleeping group the wind's coming turn wakes, and the player units it will hear (issue 957).</summary>
public sealed record WindWake(string Group, ValueList<BattleUnit> Wakers);
