# 0028 — Issue 158 kept: burden against full Str, speed twice in avoid, the iron tier 15 lower, the fort at 15

Date: 2026-09-25. Issue 158, the keep. Agreed by both partners on the Design Table (nineteenth round for the arm, twenty-fourth for the keep and the fort); built by Code.

## The question

Section 5's burden (`Wt - Str / 5`) cost every Str 5 to 9 unit 4 to 7 speed against an iron weapon, so avoid on plain was 1 to 7 for everyone and raw hits sat at 93 to 100 both ways. The forecast was arithmetic, terrain barely registered, and doubling followed weapon weight. Four arms were measured over 200 seeds per map under both roll schemes (`docs/measurements/2026-09-25-hitband-terrain-200seeds.txt`); the Table chose arm 4 under two rolls as the provisional cell, and three hand plays on it followed (Code on Old Mill Road seed 53, 6/5/6; Chat on Saltmarsh Ford seeds 41 and 19, 7/6/7 and 8/6/7). All three voted keep.

## Decisions

1. **Section 5 changes.** `Burden = max(0, Wt - Str)` and `Avoid (phys) = 2 * AttackSpeed + Lck / 2 + TerrainAvoid`. Magic avoid, hit, crit, damage, and the doubling threshold are untouched.
2. **The iron tier's hit is 15 lower in content**: Iron Sword 75, Iron Lance 70, Iron Axe 65, Iron Bow 70. Steel and everything else are unchanged. A test pins the four numbers.
3. **The fort's avoid is 15, not 30**; its +2 Def, +2 Res and heal stay, and the throne keeps its 30 until a map puts a boss on it. Under the kept formulas a boss on a fort read 31 to 47 from every chair and the burst that kills him was about 11 percent without crits; at 15 the same four units read about 58 to 73 and a bait turn followed by a burst is a plan. Old Mill Road has the same hole from the other side: the enemy into the captain's fort at 6,2 read 14 and 18, so the mill's wake was free.
4. **The arms retire with the keep.** `CombatFormula`, `FormulaArm`, and the CLI's and the trace's `--formula` are removed: section 5 is one set of formulas again, and a transcript's header names only its scheme. `--scheme one|two` stays in both front ends, and the Sim's `--hitband` stays as a standing number (both sides' raw-hit histogram, per defender terrain, the doubling rate, gates 1 and 4) over what ships, under both schemes. Transcripts recorded with `--formula arm4` are the record of the build they were played on; the ones that replay under `--strict` today do so because the fort change did not touch them.
5. **The doubles are not the next dial.** None of the three entries missed them, and the ones that happened landed where speed says they should.

## What was measured on what ships (200 seeds, two rolls; `docs/measurements/2026-09-25-full-keep-200seeds.txt`)

| Map | Gate 1 | Losses | Gate 4 |
|---|---|---|---|
| old_mill_road | 164 (82 percent), median 9, p90 11, limit 12 | 26 timeout, 10 captain | 0.375 ok |
| saltmarsh_ford | 69 (34 percent), median 8, p90 14, limit 18 | 119 timeout, 12 captain | 0.160 ok |
| the_tollgate | 198 (99 percent), median 8, p90 9, limit 16 | 2 timeout | 0.035 ok |

These match the scratch measurement of the twenty-fourth round. Gate 1 fails Saltmarsh Ford: the rest of its collapse is the heuristic's stall in front of the healing boss (a per-unit planner with a vetoed captain can neither bait nor burst), which by the fifteenth round's rule is the baseline's and not the map's. Saltmarsh's content is #131; nothing here touches its geometry. Gates 5 to 8 pass on every map.

## Tests moved with the rule

Section 5's table-driven tests carry the new hand-computed numbers (Wren's attack speed 8, the brigand's 3, avoid 18 and 26, hit 72 and 55, displayed 85 and 60). Four fixtures in the Sim's gate tests leaned on raw hits near 100 and were re-seated rather than loosened past their point: the walled-off map's brigands are level 3 and the test reads 60 seeds; the escape map's far recruit falls in 2 of 20 baseline games, so its row reads -0.900; the Veto board's captain is given the speed to double the level-12 brigand; the counter-between-strikes bounds moved from 0.5 and 0.3 to 0.4 and 0.2. The issue 11 acceptance script is Code's seed 53 play, which still wins; the 2026-09-18 seed 7 script loses under these formulas and stays as the record of its build.

## Hand play on what ships

Code, Saltmarsh Ford seed 61, 6/7/6, rout on turn 11 of 18, no Recall, nobody dead (PLAYTEST.md, `docs/transcripts/2026-09-25-saltmarsh_ford-61.txt`). The fort at 15 read 73 for the captain and 70 for Wren; the bait turn Chat found on seed 19 worked on the first try, and the burst the turn after closed on a 70 that missed once.
