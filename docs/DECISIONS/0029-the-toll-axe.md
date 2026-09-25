# 0029 — The Toll Axe: the bandit leader throws at range 2, and the exposure sum reads every weapon

Date: 2026-09-25. Issue 131, target 2 (issue 13's Saltmarsh slice; the Tollgate's first lean on #181). Spec agreed on the Design Table (eighteenth and twenty-first rounds; #131's comments); built by Code on what ships after DECISIONS/0028.

## Decisions

1. **The Toll Axe** (`toll_axe`): axe, Mt 6, Hit 60, Crit 0, Wt 8, range 1-2, 20 uses. An original name; the bandit leader holds the Tollgate's keep as well as Saltmarsh's fort. Enemy-only: no cast member carries a thrown melee weapon, and a test holds that (section 5's Ranges line).
2. **The bandit leader carries `steel_axe`, `toll_axe`**, in that order, so he opens holding steel and his first counter is the Steel Axe's. Section 8's weapon choice (#174) picks per attack, so the weapon he last swung is the one he counters with, and `show` prints it.
3. **The constraint is a behavior** (twenty-first round): adjacent to the captain or Wren at full HP he swings the Steel Axe, and at range 2 he throws. A theory over both maps holds it. Hit is the dial that keeps it, not weight. Against this boss's Str 10, any Wt up to 10 carries no burden, and the light axe's speed already keeps the fast cadets from doubling him. So Hit 65 or more makes the throw outscore steel beside the captain on the Tollgate's 8,2, and Hit 60 keeps the smallest margin at 0.7 across the cast on both maps (scratch probe over Mt 4 to 7, Hit 55 to 70, Wt 6 to 10).
4. **The exposure sum ranges over every weapon an enemy can strike with** (#131's fifth point): each enemy is counted at the worst of its usable slots, from every tile each slot reaches the unit from. The counter to the unit's own attack stays the weapon the target holds now. Without this the heuristic would stand at range 2 of a boss it believed could not reach it. A test shows the sum at zero with the Steel Axe alone and at the throw's damage with both.

## Measured (200 seeds; `docs/measurements/2026-09-25-full-tollaxe-200seeds.txt`, against `2026-09-25-full-keep-200seeds.txt`)

| Map, two rolls | Gate 1 before | Gate 1 after | Gate 4 before | Gate 4 after |
|---|---|---|---|---|
| saltmarsh_ford | 69 (34 percent), 119 timeout, 12 captain | 33 (17 percent), 154 timeout, 13 captain | 0.160 ok | 0.065 FAILED, the ceiling line |
| the_tollgate | 198 (99 percent), 2 timeout | 184 (92 percent), 16 timeout, 0 captain | 0.035 ok | 0.270, Ottilie DEAD WEIGHT at 0.030 |
| old_mill_road | 164 (82 percent) | 164 (82 percent) | 0.375 | 0.375 (no bandit leader on the map) |

Under one roll: Saltmarsh 13 percent with gate 4 passing at 0.075, the Tollgate 84 percent with gate 4 passing at 0.203. Gates 5 to 8 pass everywhere.

What the boss did over the heuristic's 200 games (two rolls): on Saltmarsh 183 steel swings, 766 throws with nobody adjacent, and 156 throws while a cadet stood adjacent. Of those 156, 89 were a finishing blow on the adjacent unit, where both axes kill, and 67 went to a second target at range 2. On the Tollgate it was 98 steel, 386 throws, and 58 throws with a cadet adjacent (11 finishing blows, 47 at another target). He never threw at an adjacent unit he could not kill.

## Reading, provisional

The extra losses on Saltmarsh are timeouts with captain deaths flat, the fifteenth round's stall shape. The veto now prices the throw, so the heuristic has fewer tiles it will stage on around the boss, and a planner that can neither bait nor burst stalls. By that round's rule this is the baseline's, and the map is not tuned to remove it. Gate 4's ceiling line on Saltmarsh and Ottilie's row on the Tollgate say the same thing from the bench: a bow at range 2 is now answered, which is what the axe was for. The hand play (Code, seed 61, PLAYTEST.md) found the boss declining the captain's bait to throw at Ottilie, and the next turn's window came from what he was left holding. Whether the Sim's reading is a cost the maps should pay back elsewhere is Chat's call on the PR. The two cheap reversals are Hit 55 and dropping the axe from the Tollgate's boss.
