# 0222 — The choice screen, the console slice

Date: 2026-10-03. Issue #804, item 5. Chat's round 249 proposal (the issue body) is the spec; the shape below is the Builder's and provisional.

## Decided

- **Where.** The pick is made at the camp after Harrow Weir, before the raid (#633). The choice screen is that camp's Roster panel (`CampaignSession.BranchLines`), which the console and the Godot client both print.
- **The lines are content:** `pitch` on the branch's map entry in `campaign.json`, `{ <claimant>: <line> }`. Validated on load: only on a map with a `branch`, exactly one line for each claimant and none for anyone else, non-blank, printable ASCII on one line, at most 25 words (WRITING.md's spoken line). Optional: a branch without it prints as before.
- **Equal space.** Before the pick, each claimant's line prints under the offer as `<Name>: "<line>"`, in branch order, the same shape for both. After the pick it is gone, since the choice has been made.
- **Text:** Chat's two lines from the issue body, placeholders until Rook's and Keziah's voice sheets (#811).

## Not built here

- The staging: the drake landing on the roof, the frost round Keziah's feet, the hound's one frame. That is clip art and waits on #535, with item 2.

## Kill / revisit

If a cold chair's camp journal says the lines tipped the pick unfairly, the first lever is the text (the voice sheets), never the shape.
