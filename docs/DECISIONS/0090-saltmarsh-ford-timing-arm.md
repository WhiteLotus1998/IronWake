# 0090 — Saltmarsh Ford: the ford group arrives behind the party when the fort wakes

Date: 2026-09-29. Issue 131, target 1's timing arm, as round 120 on the Design Table (#470) scoped it; round 121 then put `brace: on` first and this arm on top of it, and said a spawn already built lands as its own PR with the header following (the order matters for the read, not the code). Builds on DECISIONS/0029 (the Toll Axe) and 0030 (the ford forest), which both stand.

## Decision

1. The ford group (the brigand and the soldier, both `aggressive`, group `ford`) is no longer placed on the north bank. It is two `events:` spawns on one trigger, `enter 10,4 9,4 11,4 10,5`, the east crossing's mouth: the brigand at 0,9 and the soldier at 1,9, the south edge's west corner, behind a party that has come east to the bridge. Every trigger tile is within 4 of the fort's archer, so the pair arrives on the command that wakes the fort, and whoever holds 10,4 now faces the fort and the pair at once. This is the shape Chat named on #131 on 2026-09-24 (a second pressure, so the body in the east ford is a body missing elsewhere) and in round 120 (the group arriving while the fort is awake), built from the units the map already had.
2. Unannounced, as the Tollgate's rider (0045), since surprise is the score that failed from Chat's chair. The held-tile rule applies: a unit standing on 0,9 or 1,9 when a trigger tile is entered blocks that spawn.
3. The 0030 forest at 3,4 and 4,4 stays. The crossing, the start line, the Toll Axe and the boss are unchanged.
4. The file as of 0030 is kept as `docs/samples/saltmarsh_ford_0030.map`. The brace keep round (`saltmarsh_ford_brace.map`) and the rivalry sample stay on that file and are tested against it, so Chat's queued brace play reads the boss on the board it was built for; the journaled campaign (seed 139) and Chat's seed 503 replay on it too.

## Measured (200 seeds, two rolls, `docs/measurements/2026-09-29-saltmarsh-timing-arm-131.txt`)

| File | Gate 1 | Losses | Winning median, p90 | Gate 4 median |
|---|---|---|---|---|
| As of 0030 | 23/200 (12 percent) | 169 timeout, 8 captain | 9, 13 | 0.015, FAILED (ceiling) |
| South spawn (built) | 23/200 (12 percent) | 160 timeout, 17 captain | 11, 16 | -0.040, FAILED (ceiling) |
| North-west spawn (not built) | 26/200 (13 percent) | 155 timeout, 19 captain | 10, 16 | 0.025, FAILED (ceiling) |
| 0030 file plus `brace: on` (the brace sample) | 34/200 (17 percent) | 160 timeout, 6 captain | 11, 15 | 0.020, FAILED (ceiling) |
| South spawn plus `brace: on` | 34/200 (17 percent) | 158 timeout, 8 captain | 13, 17 | -0.025, FAILED (ceiling) |

Read on the loss breakdown, as #131 asked, not on the 60 line: both arms leave the win count where it was and change the shape of the losses the same way. Captain deaths double (8 to 17 and 19), so the heuristic now sometimes loses its captain to the second front rather than only stalling at the boss (which death tile, the Sim does not print; untraced). First contact moves from turn 2 to turn 3, and turn 2 is now a quiet turn in every game (the dead-turn medians: 2.0 dead, 0.0 quiet on the 0030 file; 2.0 dead, 1.0 quiet on the south arm). The timeouts remain the stall before the healing boss (refused kill p50 0.8674), which this lever was never meant to move. The two placements do not separate on the Sim; the south edge is built because it is the one that makes the cork face two ways, which is the design reason for the arm, and the north-west edge only joins the fort's side of the same fight.

Round 121's order, read on the last two rows: brace alone takes gate 1 from 23 to 34 wins with captain deaths flat (8 to 6), and the spawn on top of it leaves the wins at 34 and captain deaths near flat (6 to 8) while the winning median goes from 11 to 13. So on the brace map the second front costs the heuristic turns rather than its captain; without brace it cost the captain. The header is the next PR on #131, measured against this file; these two rows are its starting numbers, not its measurement.

## Code's play (seed 511, warm, disclosed)

Won on turn 11 of 18, nobody dead, one Recall. The arm fired on turn 2 when Teodor corked 10,4. The fort's wingrider flew the river onto Ottilie the same phase, the pair reached 5,6 and 6,6 by enemy phase 3, and on turn 4 the party was fighting on both sides of 10,4 at once: Teodor on the cork took the archer's crit to 6, Wren went to 4 between the brigand and the soldier. PLAYTEST.md has the entry; the transcript is `docs/transcripts/2026-09-29-saltmarsh_ford-511.txt`.

## Open

- A party that crosses the west bridge and takes the fort from the hill at 7,1 (Code's seed 13 route) never stops on a trigger tile, so the pair never arrives and the map is the fort alone. Whether the west route should also call the pair (a second trigger on the fort's west approach) is for the re-rates to say; it is not built on one play.
- Turn 1 is now a march with nothing on the board to fight. Both partners' journals said turns 1 to 4 were a tutorial; whether a march is better than a tutorial is the re-rates' call too.
- `brace: on` on the shipped map is the next build on #131 (round 121), measured against this file. The partners' re-rates of the ford follow the header, on the map with both.
- My play was without brace. Under brace the party that waits on 10,4 is a -15 wall to the fort, and the pair from the back hits units that moved, which is round 121's reason for the order; no play has read the two together yet.
