# 0027 — Old Mill Road: the boss is beatable by one cadet, protect is not the fix, and the limit is 12

Date: 2026-09-24. Issue 13, run 2, first map. Decided by Code with the Sim and one hand play; Chat argues on the PR if it disagrees.

## The question

The eighth round left Old Mill Road two ways to stop stalling: "the boss is beatable by one cadet, or losing the second body loses the map then and there." Code leaned `protect: wren`. Both were measured before either was chosen.

## What was measured (200 seeds, gate 1, two-roll averaging unless said)

| Map | Wins | Losses | Wren's drop |
|---|---|---|---|
| As shipped (`bandit_leader`, limit 20) | 184 (92 percent), p90 7 | 16 timeouts, quiet tail 14.8 | 0.630 |
| `protect: wren`, limit 14 | 9 (4 percent) | 191 protected | 0.045 (the bench loses at once, since the protected recruit is absent) |
| `bandit_leader` with a hatchet (scratch, all maps) | 189 (94 percent), p90 10 | 1 timeout, 10 captain | 0.800 |
| `mill_bandit` (a level-2 reaver, 22 HP, Str 7, Def 3, hatchet), limit 12 | 194 (97 percent), p90 9 | 6 captain | 0.700 (0.360 under one roll) |

## Decisions

1. **`protect: wren` is not taken on this map.** The heuristic baseline takes no veto for recruits (eighth round), so under `protect` gate 1 measures the baseline's recklessness with Wren and nothing about the map: 191 of 200 games lose her, mostly on the boss turn, and the bench arm for the protected recruit is void. `protect` stays a rule (DECISIONS/0015) for a map whose geometry keeps the protected unit off the front; it cannot be measured on a two-cadet map. A Table question rides the PR: whether the baseline should grow a recruit veto only on `protect` maps, or `protect` maps should read gate 1 with protected losses excluded.
2. **Old Mill Road gets its own boss, `mill_bandit`, and `bandit_leader` is untouched**, so Saltmarsh Ford and the Tollgate, which carry the hit A/B, do not move. The captain hits him for 10 and he hits back for 9, so a captain at 19 or more can open alone, eat the counter and the enemy-phase swing, and leave him at 4 for Wren; the veto passes that board, which is why the stall is gone. The hatchet's 5 crit is the first crit on the campaign, at 2 to 3 percent, and it is the surprise this map gets.
3. **The bandit's 10 against Wren is exactly half her HP.** She cannot end a player phase beside him. Two Recalls found that in the seed 11 play (transcript in the PR), and it is the map's one hard lesson: the captain opens, the recruit closes. Kept on purpose; the Fun Gate says whether it reads as a lesson or a trick.
4. **Turn limit 12.** Gate 1's p90 is 9 under two rolls and 10 under one; the hand play won on 9 with two Recalls, and the rewritten seed 7 script wins on 12 after three turns of healing on the fort. Nine plus about two, rounded up to the fort line, so the first miss is a real loss and healing on the fort is still a legal plan. Provisional until the Fun Gate.
5. **The seed 7 journaled script is rewritten from turn 8** (docs/transcripts/README.md allows it: a replayed script is rewritten with its test); its 2026-09-18 transcript stays as the record of the old build.

## Not decided

The map is not `tuned`: all eight gates pass on it, but the Fun Gate (issue 15) has not been played on this version by either partner.
