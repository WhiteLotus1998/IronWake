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

/// <summary>
/// Canto (issue 71): after its Attack, Item or Wait, a unit with Canto moves to a tile
/// within what its first move left of its Mov. Its own tile is a legal destination, so a
/// Canto declined is this command with the unit's own tile, and the transcript says so.
/// After it the unit is done for the phase.
/// </summary>
public sealed record Canto(string UnitId, Coord To) : Command;

/// <summary>
/// Leave the board through an exit (issue 269): on an Escape map, a unit standing on an
/// exit tile takes this as its action, in place of Attack, Item or Wait, after its Move or
/// without one. The unit is gone and safe for the rest of the battle, and no Canto follows.
/// The captain's exit ends the battle, and every player unit still on the board is left behind.
/// </summary>
public sealed record Exit(string UnitId) : Command;

/// <summary>
/// Recover the keepsake on the unit's tile (DESIGN.md 13.8, experiment): on a <c>keepsakes: on</c>
/// map, a player unit standing where an ally fell takes that ally's weapon as its action, in place
/// of Attack, Item or Wait, after its Move or without one. It needs a free inventory slot, and no
/// Canto follows.
/// </summary>
public sealed record Recover(string UnitId) : Command;

/// <summary>
/// Shove an orthogonally adjacent ally (DESIGN.md 13.12, experiment): on a <c>shove: on</c> map,
/// a player unit pushes an ally one tile directly away from itself, as its action in place of
/// Attack, Item or Wait, after its Move or without one. The tile beyond must be on the map,
/// passable for the pushed unit and empty. An enemy is never pushed (issue 355). The pushed ally
/// fires the enter events of the tile it lands on. No Canto follows. The AI never shoves.
/// </summary>
public sealed record Shove(string UnitId, string TargetId) : Command;

/// <summary>
/// Watch (DESIGN.md 13.17, experiment): on an <c>overwatch: on</c> map, a unit whose equipped
/// weapon reaches range 2 takes this as its action, in place of Attack, Item or Wait, after its
/// Move or without one, and watches the tiles two steps from it until its side's next phase
/// (<see cref="Overwatch"/>). No Canto follows. It is not a Wait, so it never braces.
/// </summary>
public sealed record Watch(string UnitId) : Command;

/// <summary>
/// Cover (DESIGN.md 13.19, experiment): on a <c>cover: on</c> map, a player unit orthogonally
/// beside an ally takes this as its action, in place of Attack, Item or Wait, after its Move or
/// without one. Until the ally's side's next phase, the first Attack aimed at the ally while the
/// two stand side by side swaps them and strikes the coverer (<see cref="CoverRule"/>).
/// No Canto follows. It is not a Wait, so it never braces.
/// </summary>
public sealed record Cover(string UnitId, string AllyId) : Command;

/// <summary>End the unit's action without attacking.</summary>
public sealed record Wait(string UnitId) : Command;

/// <summary>End the current side's phase. After the enemy phase the turn counter increments.</summary>
public sealed record EndPhase : Command;

/// <summary>Rewind to a prior state in this map's history, spending one Recall charge (section 7).</summary>
public sealed record Recall(int ToIndex) : Command;
