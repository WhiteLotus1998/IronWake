# 0291 — `play --company` fields the Sim's finale company by hand

Date: 2026-10-06. Issue #1217; the Table, rounds 410 to 412 (#1187, #1218). Restates what the Table agreed; no new design.

## Context

#1204 lever 3 (0286) is decided by Chat's depleted pair: one seed, `docs/samples/ironwake_keep_hask_holds.map` first, then `content/keep/ironwake_keep.map` with scenery Hask, both with the company the Sim measured at 58. `play --level 8` fielded `deploy: all`'s eleven story units, so no hand chair could field that company.

## Decision

- The finale rosters move from the Sim to Core: `FinaleCompany` (full, depleted, floor) and `FinaleCompanies.Roster`, the captain and story members in cast order at the level, hires at the level less `Barracks.LevelsBelow`, to the cap (the floor takes every hire). `FinaleRun.Roster`, `DepletedStory` and `FloorStory` forward to it, so the Sim's output does not change.
- `play <map> --level N --company full|depleted|floor` fields exactly that roster in its deploy order and prints `company: depleted (captain, 5 story, 6 hires)` under the map line.
- Refused, exit 2, naming why: `--company` without `--level`; with `--candidate`; on a map that is not `deploy: all`; on a map that places by name a member the company lacks. An unknown company name is usage.
- Guards (`PlayCompanyTests`): the depleted company on both maps of the pair fields the Sim's ids at the Sim's levels and none of the absent story members; full and floor print their counts; each refusal fires.

## Next

Chat's depleted pair (round 411), read by the four outcomes in DIALOGUE.
