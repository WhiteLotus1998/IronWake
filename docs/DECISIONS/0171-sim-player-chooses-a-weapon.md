# 0171 — The Sim's player chooses a weapon per attack (#746)

Date: 2026-10-02. Issue #746, rescoped by the Table in round 230 (#731: Chat's argument, Code's agreement). A defect fix to the instrument, not a rule of the game. The shapes are the Builder's and provisional.

## Decided

- **The chooser.** `HeuristicPlayer.PlanUnit` scores every weapon the unit can strike with (`HeuristicPlayer.Arms`: each usable slot, the unit read with that slot in front, as `EnemyAi.Arms` reads an enemy) on every tile and target, through the same `EnemyAi.Score`, the veto's `Exposure.Of` with the slot, and the slot's own kill probability. The attack names the slot when it is not the equipped one, so the weapon chosen stays in front for the enemy phase's counter: a unit that ends its phase holding a bow cannot counter at range 1. Ties keep the earlier slot, so a unit with one weapon plans exactly as before. The enemy's `BestOption` is not shared whole: the player's veto, crit key and ledger refusal sit inside its loop, so the player shares the arms reading and the score instead.
- **The approach is unchanged.** A unit that cannot strike this phase still walks toward the nearest enemy by its equipped weapon's range. Choosing the approach weapon is a separate question and nothing has asked it.
- **The mix.** `GameResult.Weapons` counts, per player unit, attacks and counters by weapon type (`WeaponMix`, read from the board before each command). `--ladder` prints the captain's beside its action mix (`weapons [sword 32/1 bow 415/5]`, attacks/counters); `--levels` prints one line per class over the won battles.
- **Budget.** Per-slot scoring multiplies the attack loop by the slots a unit carries (one to three). Timed as one `--trace` (process start, content load and one heuristic game, seed 3, Release): Harrow Weir 0.76 s on main and 0.81 s here, Saltmarsh 0.64 and 0.59, Brackwater 0.82 and 0.84. Inside the one-second budget with the start-up counted, so the two-read fallback is not needed.
- **A transcript test.** The wildfire trace (issue 438) pinned seed 1 with Pell casting Cinder; the chooser now casts Gust (hit 100, crit 5, against Cinder's 90 and 0), and the score does not price ignition, so the heuristic never lights a tile. The test now traces the sample with Pell carrying Cinder alone (`Program.Trace` takes a content adjustment), and seed 1 reproduces the old story exactly.

## Measured

Gate 1 at 100 seeds, `--curve`, main (c8f5ab3) against this branch. Three of the cast carry a second weapon: Pell (Cinder, Gust), Ansgar (lance, sword) and Keziah (axe, gauntlets). The full rows are in `docs/measurements/curve-746.txt`.

| Map | Read | Before | After | Move |
|---|---|---|---|---|
| starting_alone | file | 0 | 0 | 0 (captain alone, one weapon) |
| the_mill | file | 59 | 59 | 0 |
| saltmarsh_ford | file | 25 | 25 | 0 |
| the_tollgate | file | 80 | 82 | +2 |
| harrow_weir | file | 63 | 70 | +7 (at the noise line) |
| ironwake_raid | file / campaign / party +4 | 95 / 29 / 89 | 95 / 37 / 88 | 0 / **+8** / -1 |
| sallow_grange | file / campaign / party +4 | 77 / 7 / 35 | 78 / 12 / 37 | +1 / +5 / +2 |
| brackwater_cut | file / campaign / party +5 | 68 / 3 / 72 | 60 / 4 / 70 | **-8** / +1 / -2 |
| ironwake_keep | file / campaign / party +6 | 77 / 0 / 28 | 77 / 4 / 34 | 0 / +4 / +6 |

Two reads move past about 7 points: the raid as the campaign fights it (+8) and Brackwater's file (-8). Both are one step past the noise at 100 seeds and want 200 before anyone reads them as real. Harrow Weir's file sits on the line. `--levels` (100 runs) reads as #738's 200 did: 59 of 100 runs lose map 1 (118 of 200 then), and the keep is won in 5 of 41 that reach it (10 of 82).

Nothing is tuned on this (round 230): both partners read the list first.

## The ladder after the fix

`--ladder --seeds 100`, full rows in `docs/measurements/ladder-746.txt`. Tier 1 gate 1 (cadet / marshal / ranger / vanguard):

| Map | Cadet | Marshal | Ranger | Vanguard | Spread |
|---|---|---|---|---|---|
| the_tollgate | 65 | 76 | 76 | 76 | 0 |
| the_mill | 39 | 66 | 73 | 83 | 17 |
| saltmarsh_ford | 27 | 61 | 69 | 24 | 45 |
| harrow_weir | 70 | 95 | 95 | 68 | 27 |
| sallow_grange | 40 | 40 | 41 | 38 | 3 |
| brackwater_cut | 25 | 81 | 71 | 62 | 19 |
| old_mill_road | 27 | 92 | 95 | 38 | 57 |
| starting_alone | 1 | 1 | 12 | 0 | 12 |

- **The kit fires.** The Ranger draws the bow (Tollgate 278 bow attacks to 264 sword; Saltmarsh 767 to 59), the Marshal casts (Tollgate 305 reason to 162 sword), and tier 2's Champion swings the axe (Harrow 104, Saltmarsh 118) and the lance where it pays (Harrow 133; Tollgate 3).
- **The Ranger is no longer under.** On the slice 6 read it was the low class on the Tollgate (57 against the cadet's 70); now it reads at or near the top of tier 1 on every map, so 0164's exemption is not invoked by it on any board. It still absorbs least (Tollgate 114 against the cadet's 269): it stands off with the bow and gives up the anvil, which is the variable Chat named, now visible.
- **The low class is now the Vanguard** where the board needs range or magic (Saltmarsh 24, Harrow 68, Old Mill Road 38), and the spread fails on five of eight maps from the top, not the bottom. No lever is proposed here; the Table reads it.

## Open

- Whether the score should price ignition (Cinder) and other side effects, for the player and the enemy alike. The enemy AI does not price it either.
- The ladder's tier 1 bar and 0164's exemption read on the fixed Sim; the lever, if any, is the Table's.
