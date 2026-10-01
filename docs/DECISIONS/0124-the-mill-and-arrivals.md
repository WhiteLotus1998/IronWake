# 0124 — Arrivals, and The Mill as map 2

Date: 2026-10-01. Issue #632, slice 1. The Builder building what the Table agreed (rounds 186 to 190, 0121, DESIGN 14): one arrival per map, Maud rescued on map 2 under `protect`. The shapes below are Code's leans; Chat can argue them on the PR.

## Arrivals

- A `campaign.json` map takes an optional `arrives`: cast ids who join on that map. Such a unit is off the roster until its map, so the camp screen before it neither lists nor kits it; `Begin` adds it to the battle (`CampaignRecord.Present`, in cast order), and `AfterBattle` keeps it if it stands or records it fallen like anyone deployed. `StartAt` puts every earlier arrival on the roster. The record's shape does not change: who has arrived follows from the map index.
- The loader refuses an id outside the cast, the captain (there from map 1), and a unit arriving twice, naming file, map and field. That each arrival is placed by name on its map is a test over the shipped campaign, since the loader does not read maps.
- A recruit no map names stays on the roster from map 1. This slice names only Maud, so Wren, Teodor and Ottilie keep their measured parties until their maps' slices (one retune per PR).

## The Mill, not Old Mill Road redrawn

- Old Mill Road's file is a fixture for about 50 test references, a dozen `--strict` transcript replays and DESIGN 8's and 10's worked examples. Redrawing it in place would break all of them for no player gain. So map 2 is a new board, `content/maps/the_mill.map`, and Old Mill Road leaves the campaign but stays in `content/maps` as a standalone board for `play`, the Sim and the fixtures. #160 (its Fun Gate failure) is moot for the campaign.
- The board, 12x10 of rout, `turn_limit: 12`, `recall: 3`, `protect: maud`: a stream down x 6 with a road bridge on row 3; the captain at 1,8; Maud on the fort at 8,5, the miller's house. A road pair (brigand, archer, aggressive) comes from the south-east and reaches her on enemy phase 2; the mill pair (soldier on the hill at 10,1, archer at 11,1, guard) sleeps 6 and 7 from the fort, so a fight on the fort is noise that wakes it. The decision is the fort: safe tiles that wake the mill if she fights from them, or the open beside the captain.
- Levers measured before play (200 seeds): with a mill bandit as a third mill member, gate 1 12 percent (52 captain, 43 protected); without it 67 percent (133/200, 66 protected, 1 timeout), shipped; the mill soldier on 10,0 instead of the hill 52 percent; an acolyte for the mill archer 6 percent (the heuristic walks Maud into the soldier). Gate 4 has no recruit to judge (the captain and Maud are never benched).
- Cards in the captain's voice continue Starting Alone's: before (the keeper's story, her line on his list, the rules in brackets: lost if she falls, Radiance at range 1 or 2, the Salve, the wake noise) and after (her prayer, two names in one place).

## Not in this slice

Commander's Word arriving with Maud is #85. Wren to Saltmarsh, Teodor and the Tollgate's new four, Ottilie and Harrow Weir are the next slices of #632.

## Tests

Campaign scripts journaled before the story order replay on content with Old Mill Road back in The Mill's place and nobody arriving (`Fixture.WithoutStartingAlone`).
