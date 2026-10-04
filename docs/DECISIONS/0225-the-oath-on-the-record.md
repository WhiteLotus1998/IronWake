# 0225 — The Oath Stone's pins: Joab, the envoy keeps his fort, the counter feed in `threat`, `KeziahOath` (#635 slice 16)

Date: 2026-10-04. Built by the chain Builder. Restates what the Table agreed in rounds 303 to 306 on #875 (Chat 303 and 305, Code 304 and 306); nothing here is a new lean except where marked.

## What is built

- **Joab.** The bound man is his own template in `units/enemies.json` (`joab`, `named`, the Marauder's numbers and steel axe, a `description`), so the board and the bond line name him: `Joab is bound to Sworn Captain: freed when Sworn Captain falls`. He stays `hold` (round 304: a woken Guard turns Aggressive).
- **The intro line** is the quest card's first line, printed immediately before the board: "Keziah knows the man on the door. Joab kept the lamps at her mother's shrine; he is sworn now." Placeholder for Lotus. Code's call: a map header for one line would be new format surface for a board only the camp plays.
- **The reach rule (round 304)** is a test on the shipped map: a path from Keziah's slot through the breach to a tile that strikes the envoy, never entering Joab's reach for any weapon he carries.
- **The envoy keeps his fort** (round 305's first lever, ahead of the camp): his `B` line is `behavior:boss`, so he never moves and strikes from 12,3.
- **`threat` prints a counter feed, on every map.** Under a strike line, when the unit answering carries an unwoken hungering weapon equipped and its counter kills the striker if every strike lands (`CombatForecast.CounterIsLethal`), one row: `Counter kill: Keziah +10 HP, to max 26 (Kinsbane feeds, fed 11)` (`Kinsbane.CounterFeedLine`). Console only; the protocol's threat shape is unchanged.
- **`KeziahOath: fed | spared | refused | other` (round 305).** The board keeps `BondKilledBy` (`BondKill`: the killer, and whether the kill fed, which is the killer standing with a hungering weapon equipped, the condition a kill feeds on) for a combat that kills the bound enemy, strike or counter; a Recall restores it with the board. `AfterQuest` on a won side map whose member is the hungering weapon's bearer, on a board with a `freed:` bond, sets `CampaignRecord.KeziahOath` (`Oath.Of`): freed is `spared`; a kill that fed is `fed`; the member's kill that did not feed is `refused`; anything else is `other`. Saved as `keziahOath`, written only when set, any other word refused. A lost side map writes nothing. Nothing reads it until #634.

## Played

Code's warm replay of seed 1133 on the new board (same save, ally Ottilie): lost on turn 6, one Recall, 8/7/5. The oath came up: on turn 3 Keziah stood two tiles from Joab at 16 HP, the forecast said his counter killed her, and I went round. The camp fed her once; two 74% misses on the soldier ended it. Joab never struck. Transcript `docs/transcripts/2026-10-04-the_oath_stone-1133-fort.txt`; the slice 15 play stays as history.

## Next lever, if a cold chair asks for one

The camp further from the start (0224's first lever, now second). The drain against a four-Move bearer makes the long road three turns of hunger before the first meal; a cold chair decides whether that is the price or a wall.
