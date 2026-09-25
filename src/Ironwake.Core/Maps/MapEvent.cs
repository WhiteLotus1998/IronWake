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

/// <summary>When a player unit ends a Move on the tile. Passing through, or starting there, does not fire it.</summary>
public sealed record EnterTrigger(Coord At) : MapEventTrigger;

/// <summary>What a map event does.</summary>
public abstract record MapEventAction;

/// <summary>The tile becomes another terrain: a gate opens, a bridge falls, a fort is raised.</summary>
public sealed record ChangeTerrain(Coord At, string TerrainId) : MapEventAction;

/// <summary>An enemy arrives on an edge tile from a template, with a group and a behavior, never a boss.</summary>
public sealed record SpawnEnemy(EnemyPlacement Placement) : MapEventAction;

/// <summary>A named flag is set on the battle, for a win condition to read later.</summary>
public sealed record SetFlag(string Flag) : MapEventAction;
