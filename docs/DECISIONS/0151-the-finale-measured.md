# 0151 — The finale measured: full, depleted and floor

Date: 2026-10-02. Issue #692 slice 5 (Chat's round 214: "measured full, depleted and floor, with its own time and length lines"; Code's round 215: the floor is decided by a cold chair, not the Sim). The three cases and the two lines are the Table's; the companies' exact make-up, the level and the verdicts are the Builder's, and all are provisional.

## Decided (the Table, restated)

- The finale is measured with three companies: full (12), depleted (the captain, 5 story members, hires to fill), floor (the captain, 3 story members, every hire).
- The full keep's AI-vs-AI stays under one second. The median turn count is reported; over 16, the lever is one fewer wave, never a smaller map.
- Enemy numbers never scale to the company; if the floor loses every time, the lever is wave size.

## Decided (the Builder, provisional)

- **A Sim mode of its own, not a `--full` row:** `ironwake-sim --finale <map|file> [--seeds N] [--level N] [--scheme one|two]`. `--full` reads gates 1 to 8 on the cast; the finale needs three rosters on one `deploy: all` board, so it refuses any other board (`finale: <map> is not 'deploy: all'; ...`).
- **The companies** (`FinaleRun.Roster`): story members are the cast in cast order, captain first, each raised to the level by average growth in its own class (`Unit.ScaledTo`, as `play --level`); hires are the keep's hires in its order, joining at the level less `Barracks.LevelsBelow` by `Barracks.Recruit`, as the barracks prints them. Full is the 10 of the cast and the first 2 hires (12); depleted is 6 story and 6 hires (12); floor is 4 story and all 6 hires (10). The first members in cast order are the ones kept, so a depleted run is not a chosen best five.
- **The level is an input, default 8** (`FinaleRun.DefaultLevel`, the same 8 `FinaleStrengthTests` holds the bosses to). 0150 said the measured finale level replaces it; that level needs all ten maps to exist, so it is measured with the campaign rank report (#702), not here.
- **The verdicts.** Full and depleted are held to gate 1 (60 percent). Every company is held to time (5 AI-vs-AI games, slowest under 1000 ms) and length (median turns played over every game, a timeout counted at the limit, at most 16). The floor's wins are printed as data with `a cold chair's play decides`, as round 215 said. The mode exits non-zero on any failed verdict; it is not in CI, like `--full`.

## Found (Sim, `docs/samples/ironwake_keep_finale.map`, 200 seeds, two-roll average, level 8)

The sample is the pair board (`ironwake_keep_pair.map`) with `deploy: all` and twelve start tiles.

Full output: `docs/measurements/ironwake_keep_finale-692.txt`.

- **Full** (12): 93/200 won (47 percent), losses 97 timeout and 10 captain. Gate 1 fails.
- **Depleted** (12): 79/200 (40 percent), 116 timeout, 5 captain. Gate 1 fails.
- **Floor** (10): 0/200, 131 timeout, 69 captain. Data.
- **Length:** median 11 turns in all three, the limit; **time:** slowest 159 ms (full). Both lines pass.
- **Reading.** Most losses are timeouts: the lord arrives on turn 6 at the far edge as a `boss`, waits there, and the heuristic does not reach and kill him by turn 11. The stand-in is a measurement fixture, not a tuned map, and its failing gate 1 is the expected first reading. The levers, one at a time and measured here: the arriving boss advancing (a `B` line takes only `boss` or `guard` today, so that is a rule, asked on the PR), a longer clock, or the assault on an earlier turn. Depleted sits 7 points under full, so the hires carry a depleted company close to a full one, which is what the barracks is for.

## Kill / revisit

- If a cold chair wins the floor and the heuristic never does, the floor stays data. If a cold chair loses the floor too, the lever is wave size (the Table's), measured here first.
- When the keep is authored for real (after #656), it is measured here; the stand-in's numbers are not its.
