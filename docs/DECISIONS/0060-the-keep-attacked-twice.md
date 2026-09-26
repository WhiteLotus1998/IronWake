# 0060 — The keep attacked twice: the raid, the menu after it, the record's keep

Date: 2026-09-26. Issue 288, built from the forty-fourth round on the Design Table (#265) and DECISIONS/0059 decisions 7 and 8. Everything below is implementation or tuning the issue left to the Builder, and it is provisional in the ordinary way.

## Decisions

1. **The raid goes after map 4 (Harrow Weir), as the issue leaned.** By then the purse has steel, seals and trials to weigh an edit against. The campaign is now eight entries: the four maps, the raid, Sallow Grange, Brackwater Cut, and the keep.
2. **The keep is the campaign's last map now, and #83 builds on it.** Acceptance asks for a finale fought on the edited keep, and the keep map is the only finale there is. Map 7 (#81) slots in before it when it exists.
3. **The raid is a Rout, not a Survive.** As a Survive on the same grid, the random player won 89 percent (gate 2 failed): a party that stands still outlives a small force. A Rout with a limit of 7 makes the player break the raid, and the random player wins 0 percent. Because Rout is won the moment no enemy is alive, every raid wave lands by enemy phase 2, so no early win can skip a wave still to come; a test holds it.
4. **The raid is the finale's own keep.** It has the same grid tile for tile and the same player slots. Its van stands on three of the finale's van tiles, and its four waves spawn only on the finale's spawn tiles (0,3, 0,4, 0,7, 0,8), including a hexer from 0,8, the finale's ditch lesson. It brings 7 enemies against the finale's 16. Tests hold all of this.
5. **The record carries the edits bought, not the map text.** The issue said the record stores the edited keep written by `MapFormat.Write`. `Ironwake.Core` cannot call the writer, and a list of (edit, tile) pairs is canonical by construction. The finale's map is the content's keep with them made, in order, and a test holds that that map writes canonically and parses back equal. In the protocol, the campaign shape gains `keep`, an array of `{edit, at}`; a record written before it reads as nothing built, and an edit that is not on the menu is refused.
6. **The menu opens after the raid is won, while a map is left.** Before that, `keep` and `build` are refused, naming the raid. A campaign with a keep but no raid never opens it. The loader refuses a raid listed after its keep, or named as the keep.
7. **The placement line is derived, never authored.** It gives the terrain's name, the movement types that cannot stand on it, any avoid, def, res or heal it grants, and, when the tile lies in a gap that foot units can pass and that is closed on both sides by tiles they cannot enter, along its row or its column (the map's edge does not close a gap), how that gap narrows. A test changes the water terrain and watches the ditch line change.
8. **The raid pays 1000**, less than Harrow Weir's 1200. One wall is 400, so the camp after the raid can buy two edits, or one edit and a steel weapon.

## Measured

`ironwake-sim --full ironwake_raid` (200 seeds, two rolls): gate 1 94 percent, median 4, p90 6, limit 7, 9 timeouts, 4 captain; gate 2 0 percent; gate 3 0 killed; gate 4 ok, median drop 0.195; gates 5 to 8 ok. `--full` and `--trace` now find the keep and its raid under `content/keep` by id.

## Played

Code, seed 288, a two-map campaign of the raid and the keep (`docs/transcripts/2026-09-26-campaign-keep-288.*`, replayed by `TheJournaledKeepCampaignReplaysToItsTranscript`). The raid was won on turn 4 with one Recall. Dunstan and Teodor held the south breach and took the damage, so at the camp I bought the wall at 10,8. In the finale, Dunstan held 11,7 alone through the first four enemy phases, and on the last one the heavy wave that spawned in the south walked north instead. Nobody fell in either map.
