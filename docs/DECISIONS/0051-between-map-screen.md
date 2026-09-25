# 0051 — The between-map screen: the campaign record, purse, shop, repair, seal and deployment

Date: 2026-09-25. Issue 74 (DESIGN section 9), built by Code. The review is on the issue. The shape below is implementation. The numbers are content and provisional: they are Code's lean, put to the Table with the PR, and any round may move them.

## Decisions

1. **The campaign is content.** `content/campaign.json` holds `startingPurse`, `certificationPrice` and `maps` in play order. Each map entry names the map, the `reward` a win pays, and the `stock` sold on the screen before it. Stock is fixed and unlimited, so it is a plan and never a roll. There is a screen before map 1 too, so deployment is a choice from the start. Content without the file plays single maps as before.
2. **Prices live on the item.** `price` is optional on weapons and items. A stock entry without one is refused by the loader, and a weapon without one is never sold and cannot be repaired (the Toll weapons). Repair costs `max(1, price / durability)` for each missing use, and restores the weapon to full whether it is worn or broken. A spell is refused, since its uses refresh every map.
3. **The seal is a price, not an item.** Certification goes through issue 72's `Certifications.Check`, and the seal is paid from the purse at `certificationPrice`. An item in a five-slot inventory would have been a slot tax for no decision. No class carries requirements yet, since the ladder is the Table's, so any unit may certify into any other class.
4. **The bench is a deployment choice.** A benched unit leaves the roster passed to `BattleState.From`, so the next recruit in roster order fills its bare slot. Gate 4's bench leaves the placement empty and stays as it was. The captain, the map's `protect:` recruit and a recruit the map places by name cannot be benched, and each refusal says which.
5. **Permadeath carries.** After a won map, a deployed unit missing from the board has fallen and leaves the roster for good. A named slot whose recruit has fallen stays empty. A lost map ends the campaign: Recall is the answer to a loss, and a decided battle refuses everything but Recall.
6. **Survivors come back as the battle left them.** That covers level, EXP, ranks, mastery points and weapon uses. Spells are refreshed. **A `supplies` cap is what a unit brings into the battle; the rest stays in the wagon.** The uses the cap held back are returned afterwards. Without this, Old Mill Road's `supplies: 1` would have cut every dressing to one use for the whole campaign.
7. **Each map plays on the campaign seed plus its index.** Combat keys name no map, so a single seed would give the same strike on the same turn the same roll on every map, which is a pattern a player could learn. Map 1 plays on the campaign seed itself, so `play <map1> --seed N` reproduces its rolls.
8. **The record is a protocol shape** (`docs/PROTOCOL.md`, Campaign record). A campaign is a file, and so is a bug report. The console is `ironwake campaign`: the screen's commands, then `march`, then every `play` command, and `leave` once the battle is decided.

## Numbers (provisional)

Iron tier 400 (sword, lance, axe, bow, gauntlets), Hatchet 300, steel tier 900, Ridgeblade 1200, Longpike 1100, Cinder 500, Gust 600, Bolt 900, Salve 500, Beacon 800, Radiance 700, Field Dressing 150. Starting purse 500. Rewards 600, 800 and 1000 for maps 1 to 3. A seal costs 500. The stock grows by map: iron and dressings before map 1, then the axe, fists, hatchet, Cinder and Salve, then steel swords and lances and Gust.

## Played

Code, by hand, seed 139 (PLAYTEST.md, `docs/transcripts/2026-09-25-campaign-139.txt`). This was two maps through the screen, with a repair, a purchase, a certification, a refused bench and a bench that moved Pell into Ottilie's slot. Wren mastered Cadet on Saltmarsh Ford's turn 6 on points carried from Old Mill Road. It is the first play in which a mastery was earned in the ordinary course of the game.

## Not decided

- Every number above.
- Whether a certification trial (0049) stands in for the seal, sits beside it, or grants the class free.
- Whether the purse should ever be tight. On seed 139 it ended at 1120 with nothing worth buying, so a shop needs something to want before its prices mean anything.
