namespace Ironwake.Core;

/// <summary>
/// What a player or an AI may ask of the battle (DESIGN.md sections 2 and 7). Commands
/// are data: a replay is a seed and a list of them, and <see cref="Resolver.Apply"/> is
/// the only thing that reads them. The hierarchy is closed; a command the resolver does
/// not know is rejected, never thrown at.
/// </summary>
public abstract record Command;

/// <summary>
/// Move a unit to a tile in its reach (section 4). Once per phase, before it acts. With
/// <paramref name="Via"/> the unit walks its cheapest route to that tile, then its cheapest from
/// there to <paramref name="To"/>, the two together within its Mov (13.25, issue 782): the
/// player's way to pick which planks a walk spends.
/// </summary>
public sealed record Move(string UnitId, Coord To, Coord? Via = null) : Command;

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
/// within its range. Ends the unit's action. <paramref name="Art"/> names a heal art the unit
/// knows (issue 635, <see cref="HealArtEffect"/>), declared with a healing spell before it is cast.
/// </summary>
public sealed record UseItem(string UnitId, int Slot, string? TargetId = null, string? Art = null) : Command;

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
/// Open the chest at <paramref name="At"/> (issue 649): a player unit on the chest's tile or
/// orthogonally beside it opens it as its action, in place of Attack, Item or Wait, after its
/// Move or without one, unless an enemy stands on the chest's tile. What fits goes to its pack in
/// the chest's order and the rest to the wagon (issue 679). No Canto follows. The enemy never
/// opens a chest.
/// </summary>
public sealed record Open(string UnitId, Coord At) : Command;

/// <summary>
/// Drop the rock (DESIGN.md 13.26, experiment): a player unit standing on a ledge whose drop
/// events have not fired brings the rock down as its action, in place of Attack, Item or Wait,
/// after its Move or without one. Every event on the ledge fires (<see cref="Rockfall"/>). No
/// Canto follows. The enemy never drops.
/// </summary>
public sealed record Drop(string UnitId) : Command;

/// <summary>
/// Shove an orthogonally adjacent ally (DESIGN.md 13.12, experiment): on a <c>shove: on</c> map,
/// a player unit pushes an ally one tile directly away from itself, as its action in place of
/// Attack, Item or Wait, after its Move or without one. The tile beyond must be on the map,
/// passable for the pushed unit and empty. An enemy is never pushed (issue 355). The pushed ally
/// fires the enter events of the tile it lands on. No Canto follows. The AI never shoves.
/// </summary>
public sealed record Shove(string UnitId, string TargetId) : Command;

/// <summary>
/// Dash to <paramref name="To"/> (DESIGN.md 13.27, experiment): on a <c>dash: on</c> map, a player
/// unit that has neither moved nor acted moves up to <see cref="Winded.ExtraMov"/> tiles past its
/// Move, as its Move and its action both. It is winded until its side's next phase begins: every
/// strike against it is at <see cref="Winded.Hit"/> more hit (<see cref="Winded"/>). No Canto
/// follows, so it neither strikes nor exits that phase. The AI never dashes.
/// </summary>
public sealed record Dash(string UnitId, Coord To) : Command;

/// <summary>
/// Carry an ally on a grown drake (issue 805, experiment): on a <c>carry:</c> map, a rider whose drake
/// is Grown or more, unmoved and not acted, lifts the orthogonally adjacent ally <paramref name="AllyId"/>,
/// flies to <paramref name="To"/> as a Move of its own, and sets the ally down on <paramref name="SetDown"/>,
/// beside it (<see cref="DrakeCarry"/>). The rider's whole turn; no Canto follows. The AI never carries.
/// </summary>
public sealed record Carry(string UnitId, string AllyId, Coord To, Coord SetDown) : Command;

/// <summary>
/// Breathe rime (issue 805, experiment): on a <c>breath:</c> map, a rider whose drake is Unbroken, once a
/// map, as its action, breathes a line of three tiles out through <paramref name="Toward"/>, the
/// orthogonally adjacent tile, chilling everyone on it and freezing Water to Rime ice (<see cref="Rime"/>).
/// The AI never breathes.
/// </summary>
public sealed record Breathe(string UnitId, Coord Toward) : Command;

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

/// <summary>
/// Talk to the claimant who came back as a foe (issue 633, <see cref="Returned"/>): the pick or the
/// captain, orthogonally beside them, takes this as its action, in place of Attack, Item or Wait,
/// after its Move or without one. The pick's talk turns them, the captain's spares them; either way
/// they leave the board. No Canto follows. The enemy never talks.
/// </summary>
public sealed record Talk(string UnitId, string TargetId) : Command;

/// <summary>End the unit's action without attacking.</summary>
public sealed record Wait(string UnitId) : Command;

/// <summary>End the current side's phase. After the enemy phase the turn counter increments.</summary>
public sealed record EndPhase : Command;

/// <summary>Rewind to a prior state in this map's history, spending one Recall charge (section 7).</summary>
public sealed record Recall(int ToIndex) : Command;

/// <summary>
/// Take back a move (issue 676, DESIGN.md section 7): a player unit that has moved and not
/// acted returns to the tile it began the phase on, unmoved, when its Move was the last command
/// and changed nothing but its tile (<see cref="TakeBack"/>). No charge and no history entry:
/// the state the move left comes back whole, with the history it had.
/// </summary>
public sealed record Undo(string UnitId) : Command;

/// <summary>
/// Commander's Word (DESIGN.md 13.2, issue 85): the captain calls one order a map as his action,
/// after his Move or without one, reaching the allies within <see cref="Orders.Radius"/> of him.
/// Open on a map with <c>orders: on</c> and on every campaign map from the second
/// (<see cref="BattleState.OrdersOpen"/>).
/// </summary>
public sealed record Order(OrderKind Kind) : Command;

/// <summary>
/// The move a <see cref="OrderKind.FallBack"/> order owes an ally who had already acted (issue 85):
/// up to <see cref="Orders.FallBackMov"/> movement from where it stands, refused if the move would
/// wake a sleeping group. Not a second action; the unit's own tile declines it.
/// </summary>
public sealed record FallBack(string UnitId, Coord To) : Command;

/// <summary>
/// The line strike (issue 1384, <see cref="LineStrike"/>): a unit holding <see cref="LineStrikeEffect"/> strikes, as its
/// action after a move or without one, every unit of another side on the line out through <paramref name="Toward"/>,
/// the tile orthogonally beside it. No Canto follows. Only the enemy holds one; the enemy planner issues it.
/// </summary>
public sealed record StrikeLine(string UnitId, Coord Toward) : Command;
