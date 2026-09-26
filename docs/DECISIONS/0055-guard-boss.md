# 0055 — A boss that can sleep, and the Reeve moved to the west side of the throne

Date: 2026-09-26. Issue 259, from the thirty-ninth to forty-first rounds on the Design Table (#216, #265). Built by Code. Amends DESIGN section 8 and the map format of section 10, and DECISIONS/0053.

## Decisions

1. **A `B` line may take `behavior:guard`.** A guard boss sleeps as Hold on its own tile, wakes by the three causes like any Guard group, and acts as Aggressive once awake, so it may leave its tile and may end on a throne. `behavior:boss`, or no behavior, still means Boss; any other behavior on a `B` line is refused with the line named. Nothing else changed in code: `EffectiveBehavior`, the wake check, `threat` and the planners already read behavior and boss-ness separately, so the rule is the parser, the writer, and the console naming the unit `boss, asleep`, `boss, awake`, or `boss, guard` in the placements view. Tests: `GuardBossTests`, `ABossLineMayBeAGuardAndRoundTrips`, `ABLineCannotBeHold`, `ABLineCannotBeAggressive`.
2. **Sallow Grange: the Reeve is a guard boss alone in group `hall`, the hexer stays a Hold in the yard, and the clock is 10.** As the issue specified.
3. **The Reeve stands at 15,6, not 16,5 (Code's decision, reversible).** Built at 16,5 as specified, the map let a captain who reads the wake legend seize past a sleeping Reeve: 12,6, 13,7 and 14,8 are each 4 from the throne and 5 from 16,5, so the captain parks on one of them and steps onto the throne, and the Reeve wakes on the winning move. Code played exactly that on seed 31 (turn 7 of 10, nobody fought inside the walls). That is the problem this issue exists to fix. With the Reeve at 15,6, on the throne's west side facing the west gate, the only tile inside the walls from which a captain can seize in one move without waking him is 17,3, which only the north gate reaches. The short way cannot slip past a sleeping Reeve; the long way's quiet seize is its prize for breaking the lock. A test holds the one quiet tile.
4. **Ansgar needs no content change.** Scope 3 of the issue waited on #262. At 16,5 with the clock at 10, gate 4 passed as a cast (median 0.095) but read Ansgar at 0.005. At 15,6 the heuristic has to fight a Reeve who comes out through the west gate, and Ansgar's drop is 0.550, the highest in the cast. So no Ansgar change goes back to the Table.
5. **The Reeve on the Gate is intended** (forty-first round). A woken Reeve may seat himself on the healing throne. Of 19 timeouts in seeds 1 to 80, one ends with the Reeve's last move onto the Gate, and no timeout has more than three player attacks on him. So the limit-10 timeouts are the clock, not the heuristic hammering a healing Reeve.

## Measured (200 seeds, two rolls, `docs/measurements/2026-09-26-full-sallow-grange-259-200seeds.txt`)

| layout | gate 1 | gate 4 |
|---|---|---|
| 0053 as built (Hold boss at 16,5, limit 8) | 83 percent, median 6 | 0.380, Ansgar 0.170 |
| after #262 (Canto), limit 8 | 72 percent, median 7 | 0.260, Ansgar 0.055 |
| guard Reeve at 16,5, limit 10 | 73 percent, median 7, 40 timeouts, 14 captain | 0.095, Ansgar 0.005 |
| **guard Reeve at 15,6, limit 10 (ships)** | **76 percent, median 9, p90 10, 45 timeouts, 4 captain** | **0.375, Ansgar 0.550** |

All eight gates pass. Free prefix 0 of 2, 4 and 6; the opening is quiet on turn 1 in all 200 games (wake tax 0.21).

## Played

Code, by hand, seed 31 (`docs/transcripts/2026-09-26-sallow_grange-31.txt`; PLAYTEST.md). Won by seize on turn 7 of 10, no Recall, nobody dead. Not cold. The field was woken on purpose on turn 2 and fought at the party's line. Ansgar's kill on the hexer was noise that woke the Reeve, and he walked out to 13,6 and speared Teodor. The captain and Wren took his 26 to exactly 0 on turn 6. The first line of the same seed, on the 16,5 layout, is the quiet seize in decision 3.

## Not decided

- Whether the Reeve should be worth killing, or just survivable. Today he walks into the party on the short way and dies to the whole party, which is a fight rather than a race.
- Sallow Grange stays untuned. The next cold play has to come from Lotus or a fresh Chat body, since Chat's seed 57 play is on the old layout.

## Amended by issue 275 (2026-09-26)

Decision 3's quiet seize was not quiet. The hexer held 13,4, so from 12,2 the captain's only stop that was more than 4 from the Reeve and within one move of 17,3 was 13,3, one tile from its Cinder. It fired on the enemy phase, and the combat was noise to the hall (Chat's cold play on seed 44, forty-third round). **The long way is quiet only if it is quiet on the enemy phase too: any enemy that can fire on the lane counts as noise.** The hexer moves to 13,7. Nothing on rows 3 or 4 is within its range 2. It covers 12,6, the yard tile beside the west gap, so the short way's fight against the Reeve costs a second price. Chat suggested 13,8 or 14,8, but both are 3 from every tile beside the gap. Tests: `SallowGrangeNorthLaneHasAStopNoEnemyCanStrike` (fails with the hexer at 13,4) and `SallowGrangeHexerCoversATileBesideTheWestGap`. Measured over 200 seeds (`docs/measurements/2026-09-26-full-sallow-grange-275-200seeds.txt`): gate 1 79 percent, median 9, p90 10, 39 timeouts, 4 captain; gate 4 0.375 with Ansgar 0.530 and Wren 0.120. All eight gates pass, so no lever moved. Code's seed 61 play, the long way, seized on turn 10 of 10 with the hexer silent (PLAYTEST.md). The seed 31 transcript is on the old layout, and its test is replaced by the seed 61 one.
