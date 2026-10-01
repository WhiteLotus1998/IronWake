# 0144 — The company cap and the deploy header

Date: 2026-10-01. Issue #689. The rule is the Table's (Lotus's batch, item 1; rounds 213 and 214, his finale ruling). The shapes below are the Builder's and are provisional.

## Decided (the Table)

- **The company holds at most 12 living members**, the captain and eleven. The fallen keep their beds (0010) and never count toward the cap.
- **A map fields up to 6.** The keep's finale fields the whole living company, set by a header, not by special code.
- **The bunk room adds 2 beds** (was 1), 400, at most two.

## Decided (the Builder, provisional)

- **One limit on joining.** The arrivals a map seats are the fewer of the free beds and the places left under 12 (`CampaignRecord.Room`). A turned-away arrival prints `company full (12): <name> will not join` when the cap is the tighter limit (ties go to the cap), else `no bed free: ...`. #690's hires go through the same check.
- **`deploy: N | all`.** N is 1 to 12, default 6, and must cover the map's `P` lines; a map with more than 6 `P` lines needs the header. `all` needs at least 12 `P` lines, fills bare slots from the living roster in order, leaves the rest empty, ignores and refuses the bench. The loader's errors name the file, the header line and `deploy`.
- **The camp's lines.** `Roster: company 10/12`; `Deploys to <map>: <names> (deploy 4 of 11)`, counted against the living members present for that map (arrivals included); on `all`, `Deploys to <map>: the whole company fights: 11; <names>`. The bench stays the pick.
- **The starting beds stay 12, not the issue's 7.** The loader holds the keep's beds to at least the cast, so every scripted arrival has a bed, and the campaign still opens on the pre-levy cast. Seven lands with the levy roster, as 0138 says.
- **Not built here:** choosing which start tile each unit takes on `deploy: all`. No map has a pre-battle placement step; #692 adds one with the keep's big board. No content map carries `deploy: all` yet.

## Kill / revisit

13.20's kill clause still reads the raid-screen purse: if the +2 bunk room lets both plays afford every room and wall, the bunk room goes back to +1 and the barracks (#690) carries the beds.
