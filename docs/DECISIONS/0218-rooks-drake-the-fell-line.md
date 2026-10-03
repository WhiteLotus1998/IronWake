# 0218 — The fell line: a rider's death takes the drake off the field (#805, slice 4)

Date: 2026-10-03. Issue #805, item 3. This builds STORY draft 6's line ("If she falls, the drake leaves the field, and one ending line says it was seen over the fells. Nobody owns it after that.") as a record fact and a printed line. The ending's text waits on #811 and #634.

## Decided

- **The return half was already built.** `CampaignRecord.Returning` brings a passed rider back at Grown at least (slice 1, 0213).
- **`CampaignRecord.DrakeFlew`** is the stage the drake had reached when its rider fell for good. That covers a fall on a main map (`AfterBattle`), on a side map (`AfterQuest`), and the returned claimant killed on the field (`AfterReturn`, fate `fell`). A spared or turned claimant keeps the drake. With permadeath off, the rider comes back Wounded with the drake, so the fact is never set. Once set, it holds for the rest of the campaign, as `freedUnitFell` does. The save writes `drakeFlew` (the stage word) only when it is set, and any other word is refused naming the field.
- **Printed.** The fall line for a unit that took the field on a drake reads `Rook falls at 4,5; her drake leaves the field`. `UnitNames.Rides` tracks this from the board, so no protocol event was added: a renderer already has the drake on the unit. The camp's Fallen list reads `Rook (fell on <board>; the drake flew, grown)`.
- **Nothing reads the fact yet.** The ending line that it was seen over the fells is scene text under WRITING.md (#811). It belongs to #634's endings, which name `drakeFlew` among their conditions.

## Not decided

- Whether the line differs by stage. An Unbroken drake that flies is a bigger loss than a half-grown one. The record keeps the stage so the beat sheet can choose.
