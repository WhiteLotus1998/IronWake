# 0031 — Old Mill Road: the mill bandit wakes with the mill and walks

Date: 2026-09-25. Issue 160 (issue 13's Old Mill Road slice). Spec from both Fun Gate entries and Code's arm-4 re-rate (6/5/6, seed 53): the boss is one legal line, played against a man who never leaves his tile. Built by Code on what ships after DECISIONS/0028 to 0030; Chat argues on the PR if it disagrees.

## Decision

The mill bandit's line changes from `B mill_bandit 10,1 group:mill behavior:boss` to `E mill_bandit 10,1 group:mill behavior:guard`. Nothing else on the map moves: grid, limit 12, the road brigand, the soldier and archer, the template's numbers (DECISIONS/0027) are unchanged.

Old Mill Road is a rout map, so it needs no `B` line; the boss flag only made him hold his tile. As an ordinary guard he sleeps with the mill, wakes by section 8's rule with the soldier and archer, and then plays Aggressive. The map's last fight is now where you receive three enemies at once and what you do with a recruit the planner will hunt: the fort at 6,2 is the obvious place to stand, and at avoid 15 (DECISIONS/0028) it is a good tile, not a free one. The "open for 10, trade 9 for 10, 4 against 4" script, which both entries named the map's one line, needs a stationary target and no longer exists.

## Measured (200 seeds, `docs/measurements/2026-09-25-full-millwakes-200seeds.txt`)

| Old Mill Road | Gate 1 | Losses | Gate 4 (Wren) |
|---|---|---|---|
| Before, two rolls | 164 (82 percent), median 9, p90 11 | 26 timeout, 10 captain | 0.375 |
| Mill bandit an `E` guard (shipped), two rolls | 155 (78 percent), median 8, p90 10 | 4 timeout, 41 captain | 0.500 |
| The same, one roll | 124 (62 percent), p90 11 | 16 timeout, 60 captain | 0.450 |

All eight gates pass under two rolls. The losses turn from stalls into captain deaths: the heuristic no longer freezes in front of a Hold boss; it loses to a threat that comes to it, which is what a first map should teach. Wren's contribution rises (0.375 to 0.500), since she is now a target the mill chooses and a body the captain needs.

Also measured and not taken, all two rolls: the road brigand as a sleeping guard (identical to shipped: the heuristic wakes it at once); a second archer asleep beside the brigand (62 percent, 65 timeouts); the mill archer moved to an asleep road group (87 percent, gate 4 0.375), hand-played for three turns and dropped because the brigand still dies to the captain's counter double at 90 and the archer's blind spot is a free kill, the same sum a turn later; the brigand aggressive from 11,5 (57 percent, gate 4 fails at 0.045).

## The hand play

Seed 73, won on turn 7 of 12 with one Recall (Wren died once, on turn 6, and the Recall to state 33 took it back). PLAYTEST.md has the entry; the transcript is `docs/transcripts/2026-09-25-old_mill_road-73.txt` and a test replays its script under `--strict`.

## Kept, not decided here

- Turn limit 12: gate 1's p90 is 10 and the hand line 7 with a Recall; honest line plus about two, rounded to the old limit. The Fun Gate reads it.
- The opening (the brigand walking into the captain's counter) is unchanged; the variants above did not fix it. It is the next target if the re-rate still fails on turns 1 to 3.
- The seed 53 script is no longer the replayed one: it was played against a boss that holds and loses now that he walks. Its `.txt` stays as the record of that build.

## Not decided

The map is not `tuned`. It needs Chat's cold re-rate on this version.
