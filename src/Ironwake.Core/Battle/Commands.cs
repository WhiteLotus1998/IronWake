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

/// <summary>
/// Attack an enemy within the unit's weapon range. Ends the unit's action.
/// <paramref name="Slot"/> names the inventory slot of the weapon to strike with; it
/// moves to the front of the inventory, so the counter on the enemy phase uses the same
/// weapon. Without it the equipped weapon strikes. Choosing costs nothing extra (section 7).
/// <paramref name="Art"/> names a combat art the unit knows, declared before the roll
/// (issue 68): the weapon strikes as the art makes it and spends the art's extra uses,
/// hit or miss. Only an attack declares one, so a counter never does.
/// </summary>
public sealed record Attack(string UnitId, string TargetId, int? Slot = null, string? Art = null) : Command;

/// <summary>
/// Use the item in an inventory slot (section 7's Item action): a consumable heals its
/// user and needs no target; a healing spell heals the ally named by <paramref name="TargetId"/>
/// within its range. Ends the unit's action.
/// </summary>
public sealed record UseItem(string UnitId, int Slot, string? TargetId = null) : Command;

/// <summary>
/// An enemy below 30 percent HP falls back to a healing tile it can reach this phase
/// and ends its action without attacking (DESIGN.md 13.10, issue 33). Only on a map with
/// the <c>retreat: on</c> header, only for a unit whose effective behavior is Aggressive,
/// and never twice in a battle. The AI issues it; the player never does.
/// </summary>
public sealed record Retreat(string UnitId, Coord To) : Command;

/// <summary>End the unit's action without attacking.</summary>
public sealed record Wait(string UnitId) : Command;

/// <summary>End the current side's phase. After the enemy phase the turn counter increments.</summary>
public sealed record EndPhase : Command;

/// <summary>Rewind to a prior state in this map's history, spending one Recall charge (section 7).</summary>
public sealed record Recall(int ToIndex) : Command;
