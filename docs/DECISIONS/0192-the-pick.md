# 0192 — The branch's pick: two claimants at the raid's camp, `pick <unit>`, final (#633 slice 1)

Date: 2026-10-03. Built by the chain Builder. The branch's shape is the Table's (rounds 186 to 188, amended by 192 to 194; DESIGN 14). This record restates it and records the implementation choices, which are Code's to make (CLAUDE.md). Chat argues on the PR.

## What is built

- **`campaign.json`**: a map's optional `branch` names exactly two claimants. The raid carries `["keziah", "rook"]` in place of `joins: ["rook"]`. Both are off the roster until the raid's camp. `ArrivalIndex` counts a claimant like an arrival, so Keziah is no longer on the roster from map 1. The loader refuses a branch that is not two ids, an id outside the cast, the captain, an id named on two maps or twice, and a second map with a branch, naming file, entry and field.
- **The record's `Pick`**, null until the camp's `pick <unit>` (by id or name, any case). The pick joins like a joiner, at no less than the living median (0175). The passed claimant (`Passed`) never joins. The pick is final: a second pick is refused. The save writes `pick` once made and refuses an id no branch offers.
- **The march is refused until the seat is filled**: `the seat is not filled; pick keziah or rook first`. A camp-screen decision cannot be skipped, so this is a refusal and not a default.
- **The camp prints the branch** in the Roster panel: both claimants with their classes and the command, `The one passed on rides home.`, and the bed rule the return is set up under (`They come back before the keep. Turned, they join only if a bed is free, and a death never frees one.`, DESIGN 14). After the pick, `The seat: <pick>. <passed> rode home.` The client draws both rows as camp actions until one is taken.
- **A slot naming an absent claimant is a bare slot** (DESIGN 14: "a unit who may be absent holds a roster slot, not a named slot"). Harrow Weir and the field place Keziah by name. In the campaign, her slot is filled in roster order when she is not with the company, before the branch or after she was passed on. A claimant who fell keeps the fallen's empty named slot (section 9). The standalone maps are unchanged.
- **The Sim picks Rook** (`SimPick`), the claimant every measurement before this was taken with. The parity script's writer does the same and writes `pick rook`.

## What moved with it

- Real-content tests that counted Keziah on the starting roster read one fewer (beds 8 held, not 9).
- `full-campaign-631.script` is regenerated. Harrow Weir's opening now fills Keziah's slot from the roster (Wren), and the script still wins all ten maps.
- The test fixtures' content copies put the branch back as `joins: ["rook"]` (`Fixture.WithoutTheField`). Every journaled campaign transcript predates the branch, so they replay unchanged.

## Not in this slice

- The passed claimant's return on the field (#633 slice 2): an enemy at the pick's level, `talk`, the bed check, the record's fate.
- The pick's place in the fill order. A joiner takes a bare slot in roster order, and the claimants are last in the cast, so the pick sits out the raid unless the player benches someone. Rook joining at the raid's camp did the same (`JoinTests`). The arrival table's party of six (DESIGN 14) waits on #632's later slices, which take the side characters off the starting roster.
