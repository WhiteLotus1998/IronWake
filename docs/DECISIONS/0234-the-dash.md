# 0234 — The dash (13.27), spiked behind `dash: on`

Date: 2026-10-04. Issue #950. Provisional; an experiment, samples only.

## Context

The chain Builder found no `ready` issue it could build: #804, #805, #806, #807 and #872 each wait on a play, #634 or #535's clips, and were relabeled `blocked` with what each waits on. All 26 experiments in DESIGN 13 have been tried, so the run proposed a new one. Brace (13.14) prices standing still. Nothing prices running, though every clock map the Table has called a wall (the Long Count, the Counting House, the Oath Stone's long road) was lost to Move.

## Decision

- **`dash <unit> <x,y>`** on a `dash: on` map: a player unit that has neither moved, acted nor been shoved moves within its Move plus 2 (`Winded.ExtraMov`), as its Move and its action both. No strike, item, exit or Canto follows. Map events, watches, the messenger and planks read the path as they read a Move's.
- **Winded:** until its side's next phase begins, every strike against it is at +15 hit (`Winded.Hit`). It sits in `Brace.StrikeHit` beside the pin and the brace, so the forecast, `threat`, the planner and the resolver read one number, and they add.
- **On screen:** the board prints the rule; the unit list marks `winded`; the event line reads `<name> dashed and is winded: struck at +15 Acc until the player phase`. `threat <unit> from <x,y>` accepts a tile only a dash reaches and prices it with the unit winded, under `<tile> is a dash away: priced winded, struck at +15 Acc`.
- **Protocol:** the `dash` command, the `unitWinded` event, the unit's `winded`. Additions only, so the version stays.
- **Blind spots, on purpose:** the enemy never dashes; `Resolver.Legal`, the planner and the Sim never offer one; the escape `count:` line counts Moves.
- **Cost (0099):** the action, and the +15 on the coming enemy phase.
- **Kill criterion:** killed if, in both partners' plays, no dash is taken over a legal Move, or every dash taken is free (no winded unit is struck or could have been). Kept on its sample if a journal names a tile only the dash reached and the +15 was weighed for it.
- **Sample:** `docs/samples/brackwater_cut_dash.map`, the shipped Brackwater (dusk 5) with the header.

## Code's warm play (seed 950)

Won on turn 6 with all five out, no Recall. Six dashes. The three on turn 1 were free (the chase was out of reach). Pell's on turn 3 (13,2 to 16,0) took her off the chase's road: the rider stopped at 15,3. Dunstan's on turn 4 (17,3 to 19,6) was priced against walking: walking to 18,4 or 18,5 read `If all land: 17 against 17`, while the corner exit read 6 from one strike tile at 94 to 96 percent. He was struck at 94 for 5. Pell's on turn 5 (18,2 to 19,7, at 5 HP) was the run's best turn: 19,4 read 22 against 5, and the corner was a dash away with the shieldbearer in the dark beside it. It swung at 93 and missed. Tension 8, choice 7, surprise 6, warm. Both halves of the keep clause are met on one warm play; Chat's cold play decides.

## Open

Whether `count:` should name the turns a dash saves; whether a winded unit should also lose its counter (one number is the lean); Chat's cold play.

## Amendment (2026-10-04, issue 952, rounds 324 and 325)

- **Move +2 is movement, not tiles.** The rule was already built that way (`DashReachOf` is the Move plus `Winded.ExtraMov` through the terrain field); the legend, the CLI help, the doc comments and DESIGN said "2 tiles past its Move". They now say Move +2, terrain costing the extra, and `ADashIsMeasuredInMovementSoForestCostsOnTheExtra` pins it.
- **The kill criterion counts.** Each journal tallies its dashes taken out of reach (free) and in reach (priced). Code's 950 play: 4 free (turn 1's three, Pell's turn 3), 2 priced (Dunstan's turn 4, Pell's turn 5). If both plays take more free than priced, the next lever is the **borrowed step** (a dashing unit moves at Move -2 on its next turn): total distance stays the same, so the dash can never be a clock cheat, and what survives is reaching contact a turn early, where the +15 bites, and the last turn's sprint. Not a bigger hit number: it cannot price a tile no enemy reaches. Named ahead so it is not argued again; not built.
- **Words:** DIALOGUE's "a sleeper in the open can be dashed past" now reads "slipped past", so it does not read as a rule about the command.
