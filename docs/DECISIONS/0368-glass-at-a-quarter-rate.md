# 0368 — Glass is repaired at a quarter rate (slice 4 of #1403)

Date: 2026-10-09. Issue #1403. Amends DECISIONS/0360 (half rate) on Lotus's ruling of 2026-10-08, relayed on the Table (#1368, 22:09Z, "two calls", item 1).

## Ruling

Glass is repaired at a quarter of the per-use rate, not half. His reason is the economy: obsidian arrives at map 7, and maps 7 to 10 pay 1,400 to 2,000. At half rate (896 a full repair, 112 a swing against steel's 30) one obsidian user ate about half a map's pay and two ate all of it. A quarter makes one user a felt choice every camp and two a luxury. "Glass is never Refined" (0154) stands as written.

## Built

- `CampaignRules.RepairPricePerUse` prices glass at `max(1, price / durability / 4)`: 1800 / 8 / 4 = 56 a use, a full repair of 8 for 448 against 1800 for another. Other weapons are unchanged (225 a use for a non-glass copy).
- The camp's repair offer and `CampaignRecord.Repair` read the same function, so both follow.
- DESIGN 4's ladder paragraph carries the new rate.

## Open

- The campaign play reading whether 8 uses last a map is still owed (0360's Open, uses before price).
