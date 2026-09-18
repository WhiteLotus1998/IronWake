# 0015 — Outcome is computed, Escape has exit tiles, protect is a header, and a decided battle refuses everything but Recall

Date: 2026-09-18. Ruled by: Code, while building issue 7. Provisional where marked.

## The outcome is a property of the board, never stored
`BattleState.Outcome` is computed on every read from the units, the map, and the turn. Storing it would give the record a field that can disagree with the board, and a Recall would have to restore it. The order of reads is fixed (captain dead, protected recruit dead, win condition, turn limit) so that a captain who dies on the enemy phase that would also have ended a Survive map loses it, and a kill that routs the map is read before the limit that would have lost it.

## Escape needs tiles the format did not have
Section 10 had no way to say where an Escape map's exits are. The `exit:` header lists them as `x,y` tiles separated by spaces. Escape is read as every living player unit standing on an exit tile, so the parser requires at least as many exits as the map has player slots: otherwise the condition cannot be satisfied and the map is unwinnable by construction. Exits on a map whose win is not Escape are refused, since a tile with no meaning is a lie waiting for a reader. Provisional: a later map may want units to leave the board on exiting (so the exits do not need to hold the whole party at once); that is a change to the read of the condition, not to the header.

## The protect target is a header naming a slot
`protect: <id>` names a `recruit:<id>` slot on the same map, checked at parse time. A bare recruit slot cannot be protected, since the roster decides who stands there. The loss is read as that unit not being on the board.

## A decided battle refuses everything but Recall
Once `Outcome` is won or lost, `Resolver.Apply` rejects every command with `BattleOver` and the reason, except Recall. A loss is what Recall exists for (section 7), and the win case costs nothing to allow. `Resolver.Legal` returns nothing for a decided battle, so the random player stops by itself.

## Healing is floored and capped, and reported as the gain
`max HP * percent / 100` with integer division, then capped at max HP, applied to the units of the side whose phase is beginning, inside `EndPhase`. The event carries the amount actually gained; a unit at full HP emits no event, so an event stream with a `UnitHealed` in it always means HP moved.

## `Resolver.Legal` lives in Core
The enumerator of acceptable commands is what the random player of gates 2 and 8 draws from, and the enemy AI (issue 10) will need the same reach and range questions. It lives beside `Apply` so the two cannot drift: a test applies every command it yields.

## The smoke roster is synthetic until issue 13
`--smoke` needs player units and there is no cast. The Sim carries five cadets with iron swords, ids `captain`, `wren`, `recruit-2..4`, and prints that it did. When issue 13 lands, the Sim reads the roster from content and this paragraph is struck.
