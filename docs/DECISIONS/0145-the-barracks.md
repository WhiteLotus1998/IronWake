# 0145 — The barracks as built: rooms, hires and the card a hire joins with

Date: 2026-10-01. Issue #690. The rule is the Table's (Lotus's batch, item 3; round 213). The shapes and the derivations below are the Builder's and are provisional.

## Decided (the Table, restated)

- The barracks is a keep room, 500 from the one purse, opened after the raid; +2 beds and a list of four hires (a cadet, a pikeman, a bowman, an adept). One upgrade, 300: +1 bed and two more hires.
- A hire costs 300, joins at the base class at the living company's average level less 2 (floor 1), with the class's starting weapon and nothing else; growths are the cast class's less 10 per stat, floor 5. No region, supports, signature, quests or epilogue card; one ending line, `<name> served at the keep.`

## Decided (the Builder, provisional)

- **Two rooms, not a levelled room.** `barracks` (500, +2 beds, `after: ironwake_raid`) and `barracks_wing` (300, +1 bed, `requires: barracks`). A room's new `requires` names a room that must be built first; its `hires` lists the hires it opens. `keep.hirePrice` and `keep.hires` hold the price and the six soldiers.
- **The card is derived, not written.** A hire's growths are the mean of the class's cast members other than the captain (rounded down), less 10, floor 5; their level-1 stats are that mean of the cast's stats. Each level past 1 adds the hire's growth as an average, rounded down, so the card on the menu is exactly the card that joins and no roll is spent. The issue's fallback, "the class table's growths", does not exist (classes carry growth modifiers only), so the loader instead refuses a hire whose class no cast member but the captain holds.
- **Items are listed per hire** (`items`, rank E weapons the class uses, or items), since "the class's iron weapon" has no meaning for the adept (Cinder) or the chaplain (Salve).
- **One check on joining.** `hire <id>` goes through `CampaignRecord.Room` like an arrival: `company full (12): <name> will not join` when the cap binds, `no bed free: ...` when the beds do. A fallen hire stays fallen and holds the bed; a hire is never hired twice.
- **The upgrade's two:** Tamsin Hale, chaplain (she); Wat Dunmore, outrider (he).
- **Beds.** The keep still starts at 12 (0138, 0144), so the shipped maximum is 12 + 4 + 3 = 19, not the issue's 14; it falls to 14 when the starting beds become 7 with the levy roster.
- **Not built here:** the Godot client has `CampaignClient.Hire` and prints the list on the Keep panel, but no button; the Sim never hires.

## Kill / revisit

Kept if in either chair's campaign play a hire is fielded on the keep or refused for a wall, and the journal names the trade. Killed, or turned into a free garrison, if no play ever buys the room.
