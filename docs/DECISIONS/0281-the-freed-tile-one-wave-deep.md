# 0281 — The freed tile, one wave deep

Date: 2026-10-06. Issue #1191; Design Table #1187, rounds 404 and 405 (Chat), Code's answer below 405. Restates what the Table agreed; settled, not provisional.

## Context

`threat` seats at most one striker per tile (issue 253). When two strikers share the one tile they can strike from, the second is dropped, and the help line said a foe freed by a kill mid-phase is not counted. Twice the kill that freed the tile was the unit's own likely counter: the Old Watch 961 (Teodor 21 to 1 on a board priced at 11) and the Burned School 1490 (Teodor fell to `9 against 13 hp`, an 81 percent counter freeing 4,0). Code's issue offered a one-wave lean and a cheaper summary-only alternative. Chat took the lean, and asked that `end` ask whenever the freed sum is lethal, at any counter chance, with the chance printed: a seatbelt, not a forecast.

## Decision

- `Queries.FreedStrikes`: the lines are seated as before. A seated line whose counter is lethal if every counter strike lands (`CounterIsLethal`) frees its tile, and the dropped lines are seated a second time onto the freed tiles alone, heaviest first. A strike seated there frees nothing more. A covered strike frees nothing. `Exposure.SeatedOn` gives each seat's tile, and `Seated` reads it.
- `threat` prints one row under `If all land` when a freed tile lets a strike in: `If Teodor's counter kills Brigand 1 (81 hit), Brigand 2 takes 4,0: 18 against 13 hp`. The chance is the counter's displayed hit, and it adds `all counters landing` when one counter strike cannot kill.
- `Queries.Lethal` names a unit whose seated total falls short of its HP but whose freed total reaches it, at any counter chance, with the strikes as `LethalThreat.Freed`. `end` asks, the client's end menu warns, and the Sim's script export marks `end !`, all through `Lethal`. The console line is `Lethal if all land: Teodor (Brigand 1 for 9, Brigand 2 for 9 on 4,0 if Teodor's counter kills Brigand 1 (81 hit), against 13 hp)`. The protocol's `lethal` entry gains an additive `freed` array (PROTOCOL.md).
- A unit the seated strikes already kill prints as before, with no freed part.

## Not changed

The Sim's planners, `EnemyAi` and `Exposure.Of` do not read this, so no gate or Maps cell moves. Three journaled scripts now meet the ask at an `end` the player had ended anyway. Each is given a deliberate `end !`: campaign keep 288 (Wren, turn 9's end), the straggler 783 (Fenn), and the Old Watch 961 (Wren, turn 3). Their transcripts are their own replays, and the walled keep 82 gains one warning line.
