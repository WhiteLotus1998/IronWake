# 0194 — The meeting: side characters met by choice at a camp, Ansgar on the field (#633 slice 3)

Date: 2026-10-03. Built by the chain Builder. The shape is the Table's (DESIGN 14: side characters met by choice, at most one meeting a map, gated by beds, the bed count printed where they are met; STORY draft 6: Ansgar met on map 9). Where it happens is Code's provisional lean (CLAUDE.md, "take it to the Table, then proceed with a lean"). Chat argues on the PR.

## What is built

- **`campaign.json`**: a map's optional `meets` names one or two side characters, each off the roster until that map, like a joiner. The field carries `["ansgar"]`. The loader refuses more than two, an id not in the cast, the captain, and an id another map already names (`meets`, with file, entry and field).
- **`meet <unit>`** at the camp before that map (by id or name, any case) takes one of them on. He joins like a joiner, at no less than the living company's median level. The meeting is final and one a map. It is refused with `no bed free: <name> will not join` (or `company full (12): ...`) when the camp's arrivals have used the room. Not meeting is just marching: the march is never refused for it. Whoever is not met never joins.
- **The record and the save** carry `met` (cast ids in the order met). A saved id that no map's `meets` offers is refused. `campaign --from <map>` past a meeting counts it as made, so the keep still opens with Ansgar.
- **Named slots.** A map slot naming a side character not with the company (before the meeting, or never met) is a bare slot, as slice 1 did for the claimants. Sallow Grange named Ansgar at 0,10; in the campaign that slot is now filled in roster order.
- **On screen.** The camp's roster panel prints `Met on the road: Ansgar (Outrider, level N) (meet <unit>; one, final).` and the bed rule with the beds as they will stand after the camp's arrivals, the same line the return prints. After the meeting it reads `Met: Ansgar, with the company.` With no bed free it prints the refusal in place of the command. The client offers a `meet Ansgar` row (CampActions).
- **The Sim** meets whoever a camp offers while a bed is free (`SimPick.Made`, `CampaignScript`). Ansgar is now off the Sim's roster on maps 1 to 8, so the record-driven runs (`--levels`, `--heirloom`, `--supports`, the full-campaign script) shift. The full-campaign parity script is regenerated at variant 13; variant 42 now loses on the keep. `--curve` and `--full` build from the files and do not move.

## The lean: at the camp, not on the board

STORY calls Ansgar's arrival a "meeting card", and the camp is where cards and beds already live. A meeting at the camp also lands before the field's fight, so the bed it takes is a bed the turned claimant may then not have. That is DESIGN 14's cost ("wanting them back means leaving a bed empty and turning down a side character"). A meeting on the board (Ansgar running from Hask's men, rescued or not) is the next lever if the chairs find the camp flat.

## Played

Code's warm hand play, seed 634, Rook picked, the company at level 5 (`--from the_field --pick rook --level 5`). Stopped at turn 4 with the pickets cleared, one Recall. The field has one bare slot, so Ansgar and Rook compete for it: I benched Wren and Dunstan and fielded Ansgar, so Rook could not turn Keziah and the captain spared her on turn 2. The enemy went for the newcomer. Ansgar died on enemy phase 2 at 8,12, where `threat` had printed `28 against 22`; the Recall put him behind the captain. PLAYTEST has the entry; the transcript is `docs/transcripts/2026-10-03-the_field-634.txt`.

## Not in this slice

- Today's beds (12, the cast plus one spare) never bind at the field: 9 of 12 are taken with the pick, so Ansgar and a turned claimant both fit. The cost bites only with the levy roster's seven beds (0138), or after bunks are skipped and members fall. Measuring it waits for that roster.
- Pell, Dunstan and Brannock are still on the roster from map 1. Their meetings (STORY: Dunstan on map 8) are the levy roster's work.
- Ansgar's meeting card and his line to Hask wait on WRITING.md (#811).
