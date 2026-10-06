namespace Ironwake.Core;

/// <summary>
/// The bell (DESIGN.md 13.30, experiment): a map's <c>bell:</c> header puts an alarm bell on a tile
/// with a radius. A player unit standing on the bell rings it as its action, once a battle. Every
/// enemy within the radius by Manhattan distance that is not a boss and not the messenger answers:
/// its group wakes, and on the next enemy phase it marches its full Move toward the bell and strikes
/// no one (<see cref="Answering"/>). When that phase ends it is roused (<see cref="Roused"/>) and
/// fights as an aggressive unit from wherever it stopped, for the rest of the battle. The cost is the
/// ringer's action and the tile it stands on, which the answering enemies are walking to.
/// </summary>
public static class Bell
{
    /// <summary>The <see cref="BattleUnit.Rung"/> of an enemy answering the bell this coming enemy phase.</summary>
    public const int Answering = 1;

    /// <summary>The <see cref="BattleUnit.Rung"/> of an enemy that answered and now fights as aggressive.</summary>
    public const int Roused = 2;

    /// <summary>
    /// Why <paramref name="unit"/> cannot ring the bell, or null when it can. Whether the unit may
    /// act at all is the caller's check.
    /// </summary>
    public static string? Refusal(BattleState state, BattleUnit unit)
    {
        if (unit.Side != Side.Player)
        {
            return $"{unit.Id} cannot ring the bell: only player units ring it";
        }

        if (state.Map.Bell is not { } bell)
        {
            return $"{unit.Id} cannot ring the bell: this map has no bell";
        }

        if (unit.At != bell.At)
        {
            return $"{unit.Id} cannot ring the bell: the bell is on {bell.At}, not {unit.At}";
        }

        if (state.BellRung)
        {
            return $"{unit.Id} cannot ring the bell: it has already been rung";
        }

        return null;
    }

    /// <summary>The enemies that would answer the bell now, in unit order: within its radius, neither a boss nor the messenger.</summary>
    public static IReadOnlyList<BattleUnit> Answerers(BattleState state)
    {
        if (state.Map.Bell is not { } bell)
        {
            return Array.Empty<BattleUnit>();
        }

        return state.UnitsOf(Side.Enemy)
            .Where(u => !u.IsBoss && !Messenger.Is(state, u) && u.At.DistanceTo(bell.At) <= bell.Radius)
            .ToList();
    }

    /// <summary>
    /// Rings the bell from <paramref name="ringer"/>'s tile: the ringer is spent, every answerer is
    /// marked <see cref="Answering"/> and its group wakes, and <see cref="BellRang"/> names them.
    /// </summary>
    public static BattleState Ring(BattleState state, BattleUnit ringer, List<GameEvent> events)
    {
        var answerers = Answerers(state);
        var next = state.WithUnit(ringer with { Moved = true, Acted = true, Canto = null }) with { BellRung = true };
        foreach (var answerer in answerers)
        {
            next = next.WithUnit(answerer with { Rung = Answering });
            if (answerer.Group is { } group)
            {
                next = next.Wake(group);
            }
        }

        events.Add(new BellRang(ringer.Id, ringer.At, ValueList<string>.From(answerers.Select(a => a.Id))));
        return next;
    }

    /// <summary>When an enemy phase ends, every answering enemy is roused.</summary>
    public static BattleState AtPhaseEnd(BattleState state, Side ended) =>
        ended != Side.Enemy || !state.Units.Any(u => u.Rung == Answering)
            ? state
            : state with { Units = ValueList<BattleUnit>.From(state.Units.Select(u => u.Rung == Answering ? u with { Rung = Roused } : u)) };

    /// <summary>
    /// The board's line for the bell, or null on a map without one:
    /// <c>bell (ring, an action, once): 12,7; every enemy but a boss within 8 wakes and marches to it next enemy phase, striking no one</c>,
    /// and once rung, the enemies still answering.
    /// </summary>
    public static string? Line(BattleState state)
    {
        if (state.Map.Bell is not { } bell)
        {
            return null;
        }

        if (!state.BellRung)
        {
            return $"bell (ring, an action, once): {bell.At}; every enemy but a boss within {bell.Radius} wakes and marches to it next enemy phase, striking no one";
        }

        var answering = state.Units.Where(u => u.Rung == Answering).Select(u => u.Id).ToList();
        return answering.Count == 0
            ? $"bell on {bell.At}: rung"
            : $"bell on {bell.At}: rung; answering this enemy phase, striking no one: {string.Join(' ', answering)}";
    }
}
