namespace Ironwake.Core;

/// <summary>
/// What a player or an AI may ask of the battle (DESIGN.md sections 2 and 7). Commands
/// are data: a replay is a seed and a list of them, and <see cref="Resolver.Apply"/> is
/// the only thing that reads them. The hierarchy is closed; a command the resolver does
/// not know is rejected, never thrown at.
/// </summary>
public abstract record Command;

/// <summary>Move a unit to a tile in its reach (section 4). Once per phase, before it acts.</summary>
public sealed record Move(string UnitId, Coord To) : Command;

/// <summary>Attack an enemy within the unit's weapon range. Ends the unit's action.</summary>
public sealed record Attack(string UnitId, string TargetId) : Command;

/// <summary>Use an inventory item. Refused until issue 9; it exists so a script can be refused out loud.</summary>
public sealed record UseItem(string UnitId, int Slot) : Command;

/// <summary>End the unit's action without attacking.</summary>
public sealed record Wait(string UnitId) : Command;

/// <summary>End the current side's phase. After the enemy phase the turn counter increments.</summary>
public sealed record EndPhase : Command;

/// <summary>Rewind to a prior state in this map's history, spending one Recall charge (section 7).</summary>
public sealed record Recall(int ToIndex) : Command;
