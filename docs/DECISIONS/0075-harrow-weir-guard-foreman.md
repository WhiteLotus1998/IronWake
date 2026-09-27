# 0075 — The Weir Foreman is a guard boss with the Toll Axe alone

Date: 2026-09-27. Seventy-second and seventy-third rounds on the Design Table (#364), issue 379. Provisional in the ordinary way: Chat's lean, with one tuning change by the Builder, measured below.

## Decision

1. Harrow Weir's boss line is `B weir_foreman 13,6 group:weir behavior:guard` (DESIGN section 8, issue 259). The weir group is the Foreman and the archer on 14,4. The Foreman sleeps on his hill as Hold and wakes by proximity (a unit ending within 4 of 13,6 or 14,4), noise (combat within 6), or a member's death. Once awake he acts as Aggressive. The archer stays `hold`.
2. The `weir_foreman` template carries only the Toll Axe (range 1 to 2). The Steel Gauntlets are gone. This is the Builder's change, not in the issue. The issue describes him coming off the hill "with the Toll Axe at range 1 to 2", and with the gauntlets as well, gate 1 fell to 33 percent (below). The thirty-sixth round's gauntlet requirement is still met on this map by the ford brawler.
3. The map is otherwise unchanged. The limit stays at 14, since the issue's first lever did nothing. The opening screen needs no new text. The row already reads `group weir, boss, asleep` and the wake rule is printed beneath it (#260), and `threat` names the Foreman under `group weir asleep, could strike here if woken`.

## Measured (Harrow Weir, 200 seeds, Release)

| Configuration | Gate 1 | Losses | Timeouts | Gate 4 median drop |
|---|---|---|---|---|
| guard, gauntlets and Toll Axe, limit 14 | 33 percent, FAILED | 51 | 84 | 0.190 |
| guard, gauntlets and Toll Axe, limit 16 | 33 percent, FAILED | 43 | 92 | 0.185 |
| guard, Toll Axe only, limit 14 (shipped) | 67 percent, ok | 41 | 25 | 0.265 |
| guard, Toll Axe only, limit 16 | 67 percent, ok | 32 | 34 | 0.265 |

All eight gates pass on the shipped row. Gate 2 is 0 percent random wins, and gate 3 is 0. The median winning turn is 7, the p90 is 9, and there are no captain losses. Before this change, gate 1 was 62 percent.

Unit order and keyed rolls both follow the unit id. A scratch run with the Toll-Axe-only template under the id `foreman` measured 54 percent, because under that id the Foreman acts second rather than last. The id stays `weir_foreman`, the name every script and Table round uses.

## Traces

- **The bridge case (Keziah on 10,6).** This is Chat's seed 257 script replayed on the built file. On enemy phase 3 the Foreman steps to 12,6 and strikes Keziah at range 2 with the Toll Axe, 12 at 60 percent, leaving her on 2. Later in that replay Dunstan dies. The seventy-third round's prediction holds. Test: `CliPlayTests.AWokenForemanStrikesTheBridgeFromTwelveSixOnSeed257`. Trace: `docs/transcripts/2026-09-27-harrow_weir-257-after-379.txt`. It is not a play: the script was written for the old file, and five of its commands are refused.
- **The ranged opener (Pell alone on 9,6).** The seventy-third round predicted that he would walk the south way round. **That was wrong.** The shieldbearer is his ally, and allies are passable, so he walks 13,6, 12,6, 11,6 to 10,6 and strikes 9,6 directly. On Code's seed 379 he hit Pell for 13 (to 3). No tile on the west bank lets a player strike the bridge without being in his reach. Test: `CliPlayTests.TheJournaledGuardForemanTakesARecallOnSeed379`.

## The pass (the keep's rule)

On seed 379 the woken Foreman took a Recall. His Toll Axe counter at range 2 killed Ottilie on turn 4, and a Recall took it back. He had also left Pell on 3. On Chat's replayed 257 he took Keziah to 2 on his first phase, and the replay lost Dunstan. He collects.

## Record of what no longer replays

The pre-379 Harrow Weir plays (Code's seeds 7 and 17, Chat's 41 and 257) were made against the Foreman's old kit. They no longer replay to their recorded games, because a template is global to the content and cannot be kept for a sample copy under the same id. The transcripts stay as records, and the file they were played on is `content/maps/harrow_weir.map` at 950890a. The test `TheJournaledScriptWinsHarrowWeirOnSeedSeventeen` is retired with this record.

## Open

On seed 379 the map ended on turn 4 of 14. The Foreman came to the party and died in the 10,6 pocket, a tile where only 9,6 can meet him in melee but 8,6, 9,5 and 9,7 can all shoot him. The ford group and the north waves were barely fought. Whether a boss who walks into the pocket is a better map than one who never moves is for the re-rates from both chairs.
