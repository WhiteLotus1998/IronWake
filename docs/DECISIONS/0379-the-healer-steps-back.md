# 0379: the Sim's unarmed healer heals from a safe tile and steps back from an exposed one

Date: 2026-10-09. Issue #1441, step 2's healer half, from the Design Table's rounds 533 and 534 (Chat named both halves; Code read the guard's gap from the source). A planner fix, not depleted's lever. Scoped as 0377 scoped the walk: a unit with a heal it can wield and no weapon it can strike with.

## Decided

- **The heal tile** (`Players.cs`, `Heal`): such a healer ranks the tiles it may heal from by no-crit exposure first, then the old ranking (the fewest enemies able to end beside the tile), then row order, and never heals from a tile whose no-crit sum reaches her HP. With only lethal tiles reaching the patient she heals no one and falls through to the walk. Before, a unit the veto does not cover ranked by the count of enemies alone and had no lethal check, so she healed from a tile one strong enemy could kill her on. An armed healer's heal and the captain's are unchanged.
- **The step back** (`StepBack`): when no exposure-0 tile closes on the ally she walks to and she stands where some enemy's no-crit strike reaches her, she moves to the exposure-0 tile nearest that ally (steps left, then cost, then row order). Already at exposure 0, or with no exposure-0 tile in reach, she waits as before. No exposed fallback (0377's screen).
- **On the Warden sample** (`docs/measurements/keep-1441-healer.txt`): wins 91 / 84 / 0 to **93 / 91 / 0**. Her depleted stage-1 falls 83 to 38 (after a heal 40 to 4, after a wait 42 to 34). The step back reads as a walk in the existing buckets; a bucket of its own was not built.
- **The re-runs (#971):** `--finale content/keep/ironwake_keep.map` 149 / 142 / 0 to 149 / 135 / 0, `finale: ok` (7 of 200, inside round 526's band; the keep is not tuned). `--full --all`: every gate 1 cell unchanged but the field, 104 to 103 of 200 (inside the band); every gate 4 verdict unchanged.
- **The parity script:** at variant 44 the rewritten `full-campaign-644.script` lost the field (map 9); every variant 44 to 53 did. Variant 6 wins it, so the committed script's variant is 6 (`ScriptVariant`); the art script and the side-map reads keep 44, which still win.

## Open, for the Table

- Depleted is 91 against 120. Wave entry is the stage-1 lever, screened next (round 533), then the captain's stage-2 read for full.
- The 34 falls after a wait: a wait now stands at exposure 0 or where none is in reach. Whether they are crits, late movers or boards with no safe tile is not traced.
