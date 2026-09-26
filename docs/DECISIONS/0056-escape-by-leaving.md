# 0056 — Escape by leaving: `exit` takes a unit off the board, and the captain's exit ends the battle

Date: 2026-09-26. Issue 269, from the forty-second round on the Design Table (#265): Chat's lean after its cold play of Brackwater Cut on seed 73, with Code's two refinements. Built by Code. Amends DESIGN section 7 (win conditions, the outcome order, and a new Exit paragraph), section 10's exit legend, and section 11's heuristic.

## Decisions

1. **Exit is an action.** On an Escape map, a player unit standing on an exit tile may take `exit` in place of Attack, Item or Wait, either after its Move or without one. It cannot be taken after another action, and no Canto follows it. The unit moves from `BattleState.Units` to the new `BattleState.Escaped` list, so nothing on the board can target it, be woken by it, or count it. Enemies never exit. Refusals: `NotOnAnExit` (off an exit tile, on a map that is not Escape, or an enemy), and `AlreadyActed` through the usual guard. Tests: `ExitTests`.
2. **The outcome, in section 7's order.** The captain is dead only if it is on neither the board nor the escaped list. The protected recruit is dead on the same terms, and it is also *left behind* if the captain has escaped while it is still on the board. Either one loses the battle (`LossCause.Protected`). Escape is won when the captain has escaped. `UnitExited` is emitted for each exit, and when the captain exits, one `UnitLeftBehind` follows for each player unit still on the board.
3. **Left behind is fallen.** `BattleState.Survivors()` is the escaped list on an Escape map and the board everywhere else. `CampaignRecord.AfterBattle` keeps only survivors, so a unit left behind leaves the roster exactly as a dead one does. That is rule 5's "the answer death gets". #74 can revisit it with death.
4. **On screen and in the protocol.** The legend now reads `exits (>): ... (a unit on one may exit as its action; the captain's exit wins and leaves the rest behind)`. The board lists `escaped: <ids>` once anyone has left. On an Escape map, `play` ends with `escaped: ...; left behind: ...; fell: ...`. The protocol gains the `exit` command, the `unitExited` and `unitLeftBehind` events, and an `escaped` array on a state, in the unit shape. A state without that array reads as none, so protocol version 1 stands, because the change only adds fields.
5. **The Sim.** On an Escape map the heuristic plans the captain last. A unit that can reach an exit this turn moves to the cheapest one (row-major on ties) and exits before considering any attack. The captain exits only once no other player unit that has not acted can reach an exit this turn, and ordering the captain last makes that true whenever it plans. The issue offered a lethal-threat bail-out for the captain. It is not added, because the ordering already leaves the captain nothing to wait for. Tests: `HeuristicExitTests`.
6. **The Outrider trial becomes a Seize (Code's decision, reversible).** `docs/samples/certification/outrider_trial.map` was `win: escape`, and its puzzle is lever, Wait, then a Canto onto row 0 on turn 1 of 1. Under decision 1 a Canto onto an exit cannot be followed by an exit, so the trial could no longer be won. Row 0 is now throne tiles (the Gate) and the win is `seize`. The puzzle is unchanged, because a Canto that ends on the far row wins on the board. #252 rebuilds this trial anyway.
7. **Scripts.** The seed 29 Brackwater script gains a final `exit captain`, and its transcript and the two Outrider transcripts are regenerated. The seed 71 and seed 73 transcripts are records of games played under the old rule and are left as they are. Gate 4's end-to-end test fixture (`OneBodyTooMany`) was an Escape map that only the old rule made unwinnable with a far recruit. It is now a Survive map where the recruit's start wakes a Guard group onto the captain. It still fails the cast on the benching line.

## Measured

Brackwater Cut, 200 seeds, before (DECISIONS/0054) and after:

- Gate 1: 79 percent (median turn 6, p90 7) before; 88 percent (median 5, p90 5) after. After: 1 loss and 24 timeouts, no captain deaths.
- Gate 4: median drop 0.363 before; 0.138 after, still ok.
  - Dunstan: 0.385 to 0.075.
  - Pell: 0.620.
  - Wren: 0.165.
  - Rook: 0.110.
- Gates 2, 3 and 5 to 8 are ok. The free prefix is still 0.

Pell reads highest because the recruits no longer have to survive the walk: under the new rule a recruit that reaches an exit leaves, so gate 4 now measures who clears the way, not who delays the captain. Dunstan's drop falling to 0.075 is the rule working. The heuristic's captain no longer needs the holder dead, so the holder's absence costs little. Whether that matters for the corridor map is Brackwater's Fun Gate question, which the issue leaves out of scope. #263 will print the escaped and left-behind counts that gate 1 does not show.

## Played

Code replayed Chat's seed 73 line to turn 6, then played on under the rule (PLAYTEST, not cold): Rook left from 19,6 at 6 HP, Dunstan still fell to archer-1, and four escaped on turn 7. The rearguard decision the rule creates did not come up on this seed. The archer takes it away before turn 7. Chat's cold replay is next.
