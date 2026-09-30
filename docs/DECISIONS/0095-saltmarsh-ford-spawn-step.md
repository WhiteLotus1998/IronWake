# 0095 — Saltmarsh Ford: the pair's spawn stays at 0,9 and 1,9

Date: 2026-09-30. Issue 524, filed from Design Table rounds 135 and 136 (#503) on the lever Chat named after its cold play 571; the floor and its outcome were written into the issue before the Sim ran. Decided by the Builder under that rule; nothing here is a new call.

## The measurement

`--full` at 200 seeds, two-roll average, on main at 0f8ef2a (`docs/measurements/2026-09-30-saltmarsh-spawn-step-131.txt`):

| Pair spawns | Gate 1 | Timeouts | Captain deaths | Win median / p90 | Gate 4 median |
|---|---|---|---|---|---|
| 0,9 and 1,9 (shipped, 0093) | 47/200 | 129 | 24 | 11 / 16 | 0.055 |
| 4,9 and 5,9 (the lean) | 33/200 | 150 | 17 | 11 / 17 | 0.010 |
| 7,9 and 8,9 (the slope) | 31/200 | 156 | 13 | 11 / 15 | 0.010 |

The floor was gate 1 at 40/200 on the setting shipped. Neither holds it.

## Decision

The north cut stays as shipped. `content/` is unchanged; no sample file is cut and the 547 replay stays on the shipped map.

## What the numbers say

The closer pair costs the heuristic wins through timeouts, not deaths: captain deaths fall (24, 17, 13) while timeouts rise (129, 150, 156). The pair arriving sooner stalls the heuristic's assault on the fort rather than killing it. That is the Sim's policy, not a player's (the heuristic knows nothing of the call), so it says nothing about whether the pair still read late to a person; it says the map has no gate 1 room for a sooner pair while 129 of 200 games already time out.

## Not decided

Whether the pair read late remains open from both chairs (547, 571). The next lever, if one is wanted, has to buy gate 1 back first: the turn limit and the fort's heal were set aside in issue 524, and both stay a Table question.
