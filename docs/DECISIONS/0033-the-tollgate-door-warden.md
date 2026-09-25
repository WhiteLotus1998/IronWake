# 0033 — The Tollgate: a door warden who throws, four deployed, limit 10

Date: 2026-09-25. Issue 181 (issue 13's Tollgate slice). Spec from both Fun Gate entries (Code 2/4/5 on seed 31, Chat 3/4/4 on seed 37; twenty-third round): 6,4 answers the keep for free, the boss is scenery, five deployed behind one contested tile fails gate 4, and the hand lines of 6 and 8 sit against a limit of 16. Built by Code on what ships after DECISIONS/0028 and 0029; Chat argues on the PR if it disagrees.

## Decisions

1. **The Toll Spear** (`toll_spear`): lance, Mt 5, Hit 65, Crit 0, Wt 7, range 1-2, 20 uses. Enemy-only, like the Toll Axe (section 5's Ranges line); a test holds that no cast member carries a thrown melee weapon.
2. **The Toll Warden** (`toll_warden`): the soldier's pikeman template with the Toll Spear as its only weapon. It replaces the soldier on the Tollgate's door tile, 6,2, still `group:keep behavior:hold`. With one weapon it always counters at 1 and at 2, so 6,4 stays the right tile and stops being a free one: a range-2 unit there trades with the warden, and a Hold unit's enemy phase throw at it is answered by the counter. The soldier template is unchanged on the other maps.
3. **A test states the rule for the whole keep:** every open tile outside the walls within 2 of a keep enemy is a tile that enemy's weapons reach. It fails at 6,4 with a range-1 spear and fails outright with the soldier back on the door.
4. **Four deployed:** the captain, `recruit:wren`, `recruit:teodor`, `recruit:pell`. Ottilie is no longer deployed here. The seventh round's authoring rule is one contested place per two deployed; the keep's door is one and the woods are half of another, and Ottilie's row was dead weight in both measured versions (0.030 before, 0.040 with the warden), since Pell's Cinder does the bow's job from 6,4 and counters at 1 as well.
5. **Turn limit 10** (from 16): gate 1's p90 is 9 and Code's hand line is 7 (Chat's was 6 on the old keep), hand line plus about two. Two rolls lose no win going from 16 to 10; one roll loses 7 of 136.

## Measured (200 seeds, `docs/measurements/2026-09-25-full-tollwarden-200seeds.txt`)

| The Tollgate, two rolls | Gate 1 | Losses | Gate 4 |
|---|---|---|---|
| As shipped (Toll Axe, five deployed, limit 16) | 184 (92 percent), median 8, p90 9 | 16 timeout, 0 captain | 0.270, FAILED: Ottilie dead weight 0.030 |
| The warden only | 178 (89 percent), median 8, p90 9 | 22 timeout | 0.217, FAILED: Ottilie 0.040 |
| The warden, four deployed, limit 10 (shipped) | 150 (75 percent), median 7, p90 9 | 50 timeout, 0 captain | ok, median 0.480: Pell 0.595, Teodor and Wren above half |
| The same, one roll | 129 (65 percent), p90 9 | 71 timeout | ok, 0.445 |

All eight gates pass under two rolls. The captain's enemy attacks absorbed rise from 39 to 264 over 200 games: the keep now hits back. The losses are timeouts with no captain deaths and a refused kill median of 0.9892, the fifteenth round's stall shape (the veto refusing a near-certain kill it prices as a risk), and by that round's rule the map is not tuned to remove it.

## The hand play

Seed 97, seize on turn 7 of 10, no Recall, no deaths; Wren ended at 3 of 20. PLAYTEST.md has the entry; the transcript is `docs/transcripts/2026-09-25-the_tollgate-97.txt` and a test replays its script under `--strict`.

## Kept, not decided here

- The woods group is untouched. On this play it woke by noise on turn 2 and walked onto plain to die on turn 3 with no counter taken, the same line as both Fun Gate entries. It is the map's next target.
- The boss's Toll Axe (DECISIONS/0029) is unchanged; on this play it reached 7,4 through the wall.

## Not decided

The map is not `tuned`. It needs Chat's cold re-rate on this version.
