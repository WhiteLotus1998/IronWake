# 0356 — The finale company kits within its ranks

Date: 2026-10-08. Issue #1395 (`bug`), slice 2, layer 3; Design Table rounds 494 to 497. This implements what the Table agreed. It amends 0293's finale company, after 0353 (Kitted), 0354 (the scythe plans first) and 0355 (earned ranks).

## Decision

- **`FinaleCompanies.Stocked`** gives each story member, after Kitted and the earned ranks, the best weapon the keep map's own stock (`campaign.json`, the `ironwake_keep` entry) sells in each type the member already carries, when the member can wield it and it outranks every weapon of that type in the pack. Best is highest rank, then highest price. It goes in front of the first carried weapon of its type. A full pack drops its last stack, as Kitted does. A type the member carries none of gains nothing, so the stock never opens a new type: buying is an upgrade, not a respec.
- Under the shipped ranks (0355) that is **Bolt for Pell** (reason D) and **a Steel Lance for Rook** (lance D). Maud's faith D already carries Radiance, and the keep sells no faith tome above it within D. Everyone else is at E.
- **Quest weapons stay out** under the majority rule (0353): the heuristic campaign pays no quest by the keep.
- Hires are unchanged. `play --company` fields the same roster.

## Read

`--finale content/keep/ironwake_keep.map`, L8, 200 seeds, full / depleted / floor, in `docs/measurements/keep-1395-kit.txt`:

- Before: 157 / 101 / 0. Kit within rank: **148 / 114 / 0**.
- Bolt alone gives 149 / 114. The Steel Lance alone gives 159 / 101. The depleted company fields Pell but not Rook, so its +13 is all Bolt's. In the full company Bolt costs about 8 and the lance adds about 2.
- Placing each stocked weapon behind its type instead of in front reads the same (148 / 114). The planner ranges over every weapon carried, so the slot doesn't matter.
- Depleted still fails gate 1 (114, against the pre-storm 117 and the 120 bar). Full passes.

## What this closes

Slice 2 of #1395 is built: Kitted, earned ranks, kit within rank. Per round 497, the stand-in Hask's read, **148 / 114 / 0**, is the new baseline, not a regression. #1397 and the stage-1 lever compare against it.

## Open, for the Table

- Bolt lifts the depleted company and costs the full one about 8 wins. Reads in this slice moved by a few wins between variants that should match, so the full company's -8 may be partly noise. Why Bolt costs the full company anything was not traced in this slice. If the Table wants it traced, it's a player question (how the Sim picks a tome), not a kit one.
