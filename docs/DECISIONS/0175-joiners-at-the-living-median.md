# 0175 — A recruit who joins below the living median joins at it; Rook joins at the raid's camp (#763)

Date: 2026-10-02. Issue #763, from the Table's rounds 228, 233 and 234 (#731). The rule was agreed by both partners; this record restates it and names the Builder's implementation choices, which are provisional.

## Decided

- **The rule (round 234):** a recruit who joins below the living company's median level joins at it. Join-time, never deploy-time: a member left on the bench is never raised, so benching never pays. The median is the roster's (the living, wounded included, the fallen not), the two middle levels' mean rounded down on an even count. The raise is `Unit.ScaledTo`, the average-growth raise `play --level` and the enemy templates use; a joiner at or above the median is never lowered.
- **`joins`, beside `arrives`, in `campaign.json` (Builder's choice).** `arrives` (0124) is a recruit placed by name on the map's board, so it always deploys. DESIGN's branch has the pick join *for* the raid, and round 234 raises her whether or not she deploys, so Rook is not placed: `joins` lists cast ids who are off the roster before that map and present at its camp, taking a bare slot in roster order like anyone else, without a tile of their own. The loader refuses an id outside the cast, the captain, and an id named twice across both lists or maps. The raid's entry carries `"joins": ["rook"]`; the raid's board is unchanged.
- **The rule covers every joiner and arrival.** Maud arrives on The Mill when the living median is 1 (everyone but the captain is undeployed after Starting Alone), so she is unchanged; a test holds it. When #633 builds the branch, the pick (Keziah or Rook) is listed in `joins` on the raid and the same rule applies, with no second rule.
- **The camp prints** `Rook joins at level N (the company's median)` before the camp view, only when the rule raises her.
- **Side effects of Rook joining at the raid:** she holds a bed from the raid, not from map 1 (`beds:` and `company N/12` read one lower before it), and campaigns opened before the raid no longer list her. Seven journaled transcripts were regenerated; only roster, deploy-count, bed and save lines moved, no battle.

## Measured

`docs/measurements/levels-763.txt` (200 runs): the heuristic's living median at the raid's camp is 1 (p50), so the Sim's Rook joins at 1. The raise bites only where a chair feeds the company; the Sim's chooser hoards (round 234's `carried` read, #764, is where that is fixed if at all).
