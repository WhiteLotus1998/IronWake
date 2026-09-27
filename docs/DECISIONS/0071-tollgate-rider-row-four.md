# 0071 — The Tollgate: the rider spawns at 13,4 (amends 0045)

Date: 2026-09-27. Issue 351, from Chat's cold Tollgate play (seed 97, sixty-second round on #322). Amends DECISIONS/0045 item 2; items 1, 3, 4 and 5 stand.

## Decision

The spawn event reads `riders turn 4 enemy spawn rider 13,4 group:flank behavior:aggressive`. Row 4 is open plain from x=13 to x=7, so the outrider's Mov 6 reaches 7,4 on the phase it arrives, beside the door's ranged tile 6,4 and the door approach at 7,5 and 8,4. From 13,5 the same walk costs 7, one tile short. The turn stays 4 and the rider stays unannounced. Holding 13,4 as enemy phase 4 opens still spends the event.

The fallback in the issue (13,5 on turn 3) was not used: every gate passes on 13,4.

## Why

0045 placed the rider to land beside the woods fight. That happens on a slow clear. On Code's seed 163 the woods fight was still open on turn 4, and the rider hit Pell at 8,5. On a fast clear it lands after the fight, one tile short of the door. On Chat's seed 97 the woods pair were dead by the end of turn 3, the rider stopped on 8,4 next to a 4 hp Pell on 6,4, and it never swung. A fast clear is what a good player does, so the file should answer it.

## Measured (200 seeds, two rolls, `docs/measurements/2026-09-27-full-tollgate-rider-row4-200seeds.txt`)

| The Tollgate | Gate 1 | Losses | Gate 4 |
|---|---|---|---|
| 0045, rider at 13,5 | 159 (80 percent), median 8, p90 9 | 41 timeout, 0 captain | ok, 0.310 |
| Rider at 13,4 (shipped) | 147 (73 percent), median 8, p90 9 | 53 timeout, 0 captain | ok, 0.260: Pell 0.445, Teodor 0.260, Wren 0.195 |

All eight gates pass.

## The hand play

Code's seed 181 (not cold, since Code built it) won on turn 7 with no Recall and nobody hurt, 6/6/6. The woods took until turn 4 because three coins missed. The rider came onto 8,5 with nobody it could strike, because the party stood in the woods' forest. Even so, it cost the door a turn: turn 5 went to the captain and Teodor killing the rider, and the warden fell on the counter to Pell that phase. With the rider alive, `threat pell from 6,4` read 19 against 16 hp. That is the door the issue wanted. PLAYTEST.md has the entry.

## Records moved

The journaled seed-163 line takes a different path on 13,4. It is kept replayable on `docs/samples/the_tollgate_row5.map`, which is the 0045 map, and the four fixtures that replay it load that file. Chat's seed 97 script replays there too.

## Not decided

- The Fun Gate entries for the Tollgate are reset. The next plays are cold re-rates by both partners on 13,4.
