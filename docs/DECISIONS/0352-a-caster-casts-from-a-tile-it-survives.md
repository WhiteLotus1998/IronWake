# 0352 — A caster casts from a tile it survives

Date: 2026-10-08. Issue #1395 (`bug`), slice 1; Design Table rounds 493 to 497. Implementation, a planner fault. Amends 0349's "Who casts".

## What moved the keep

`--finale content/keep/ironwake_keep.map`, L8, 200 seeds (full / depleted). Measurements in `docs/measurements/keep-1395.txt`.

- **The real baseline was 169 / 117, not 167 / 136.** 2bad0a5 and 6dadf03 both read 169 / 117, which matches 0293. The depleted 117 was already a FAILED read that the hand pair overrode. `keep-1384.txt`'s "main" row was a copy of `keep-1204.txt`.
- **#1393 (content) cost 21 / 28.** Pell is the only lightning caster at the keep. Spark Storm takes Gust's slot, so she goes from 14 single-target uses to 8.
- **#1394 (the cast) cost the full company 40 more** and gave the depleted company 12 (no-cast 148 / 89, main 108 / 101).
- **The cause was the cast's tile.** `HeuristicPlayer.Cast` refused tiles on exposure only for a unit whose death loses the map. A cast takes no counter, so its price never reads the tile it stands on, and the storm walked Pell into the crowd it struck. On seeds 1 to 60 she died in 42 games casting and in 20 not casting.

## Decision

- **The Sim's player casts only from a tile whose no-crit exposure stays under the caster's HP, for every caster.** That is the veto's rule for a strike. The attack path is unchanged. Spark Storm's signed numbers (0347) are unchanged.
- **The re-run rule (#971) also covers the keep.** A PR that changes the Sim's player or the enemy's casts re-runs `--finale content/keep/ironwake_keep.map` beside `--full --all`.
- The enemy planner is unchanged: it still casts under the boss veto alone, and no shipped enemy carries an area tome.

## Read

- **The keep:** 153 / 101 / 0. Full passes gate 1. Depleted is 16 under its pre-storm 117 and 12 over no-cast. The rest is Pell's lost Gust uses, a content cost, not the planner.
- **`--full --all`** (main, then this change; gate 1 wins, gate 4 median drop). No gate newly fails. Two gate-4 failures from 0349 pass again.

| Map | Gate 1 | Gate 4 |
|---|---|---|
| the_tollgate | 167 to 190 | 0.315 to 0.280 |
| brackwater_cut | 135 to 133 | 0.255 to 0.235 |
| harrow_weir | 170 to 132 | 0.225 to 0.230 |
| sallow_grange | 171 to 162 | fails on Wren, now passes (0.385) |
| the_field | 93 to 104 | **0.065 fails, now 0.260 ok** |

  The other maps are unchanged.

## Not decided here

- **The finale company's kit: #1395 slice 2.** The Table agreed on it in rounds 494 to 497, and the work is recorded on the issue. Three layers go onto `FinaleCompanies.Roster`, in order, each with its own `--finale` read:
  1. `CampaignRecord.Kitted`.
  2. Median campaign-earned ranks.
  3. Keep stock within those ranks, plus each quest pay that most runs earn.

  The fielded company today is at rank E in everything but Maud's faith D. Bolt (rank D) is out of Pell's reach for that reason: the read with Bolt added is identical to main's 108. After slice 2, the stand-in Hask's read becomes the new baseline.
