namespace Ironwake.Core;

/// <summary>
/// A scripted happening declared in a map file's <c>events:</c> block (DESIGN.md section
/// 10, issue 32): a trigger and one action, fired at most once per battle. The name is
/// unique on the map and is what the <see cref="MapEventFired"/> event and the state's
/// fired list carry, so a transcript explains itself.
/// </summary>
public sealed record MapEvent(string Name, MapEventTrigger Trigger, MapEventAction Action);

/// <summary>When a map event fires.</summary>
public abstract record MapEventTrigger;

/// <summary>At the start of <paramref name="Phase"/> on turn <paramref name="Turn"/>, after healing terrain has healed. Never turn 1's player phase: that board is the placements.</summary>
public sealed record TurnTrigger(int Turn, Side Phase) : MapEventTrigger;

/// <summary>
/// When a player unit ends a Move on any of the tiles (issue 370). Passing through, or
/// starting there, does not fire it. The event still fires once, so a second listed tile
/// entered later does nothing. The tiles are in file order, at least one, none twice.
/// </summary>
public sealed record EnterTrigger(ValueList<Coord> Tiles) : MapEventTrigger
{
    /// <summary>An enter trigger on one tile.</summary>
    public EnterTrigger(Coord at)
        : this(ValueList<Coord>.Of(at))
    {
    }
}

/// <summary>
/// When the map's messenger reaches its road and leaves the board (DESIGN.md 13.24, experiment).
/// Every event with this trigger fires then, in file order; a map without a <c>messenger:</c>
/// header may not use it.
/// </summary>
public sealed record MessengerTrigger : MapEventTrigger;

/// <summary>
/// The <c>messenger:</c> header (DESIGN.md 13.24): the enemy placed at <paramref name="From"/>
/// is the messenger, and <paramref name="Road"/> is the edge tile it runs for.
/// </summary>
public sealed record MessengerRoute(Coord From, Coord Road);

/// <summary>How the messenger left the board (issue 675): <paramref name="Escaped"/> by the road at <paramref name="At"/>, else fallen there.</summary>
public sealed record MessengerFate(Coord At, bool Escaped);

/// <summary>What a map event does.</summary>
public abstract record MapEventAction;

/// <summary>The tile becomes another terrain: a gate opens, a bridge falls, a fort is raised.</summary>
public sealed record ChangeTerrain(Coord At, string TerrainId) : MapEventAction;

/// <summary>An enemy arrives on an edge tile from a template, with a group and a behavior, never a boss.</summary>
public sealed record SpawnEnemy(EnemyPlacement Placement) : MapEventAction;

/// <summary>A named flag is set on the battle, for a win condition to read later.</summary>
public sealed record SetFlag(string Flag) : MapEventAction;

/// <summary>
/// The <c>freed:</c> header (issue 750): the enemy placed at <paramref name="Bound"/> is bound to
/// the boss of group <paramref name="BossGroup"/>, placed or spawned, and is freed when he falls.
/// </summary>
public sealed record FreedBond(Coord Bound, string BossGroup);

/// <summary>How the bound enemy of a <c>freed:</c> header left the board (issue 750).</summary>
public enum BondFate
{
    /// <summary>Its boss fell while it stood: it left the board, not a kill.</summary>
    Freed,

    /// <summary>It was killed (or otherwise removed) before its boss fell.</summary>
    Fell,
}
