# 0336: On her join map the pick takes the first seat a bench frees; `bench` names who sits

Date: 2026-10-08. Issue #1357. Design Table #1334, rounds 457 (Code), 458 (Chat) and 459 (Code). Amends 0278.

## Context

The picked claimant sits last in the roster's fill order, so on the raid `pick keziah` then `bench dunstan` seated Maud, and fielding the pick took a second bench. The raid's warm plays 633 and 1570 went without her for that reason; 2055 benched twice (#1356).

## Decided

- **On the map the pick joins, the pick takes the place in the fill order of the first unit benched, in bench order, who stands before her** (`CampaignRecord.SeatOrder`). She takes that unit's tile and nobody else's tile moves. A second bench seats in roster order. From the next map on she is on the roster and roster order governs.
- The rule reads the bench, not when it was made: a bench made before the pick gives its seat to the pick once she is picked. The Sim's script writer seats its deploy list again after its pick, so its benches still field the units it names.
- **`bench <unit>` names who takes the seat on every map**: `Dunstan is benched from Raid on Ironwake; Keziah takes the seat`, or `; nobody takes the seat` when the company is short.
- Scripts edited deliberately to keep the same play: the 2055 raid drops `bench maud`; the 3971 cold chair adds `bench rook` after `bench wren`, so Maud still takes Wren's seat. The Sim-written `tests/parity/campaign/psalter-art-644.script` is rewritten from the Sim (one more bench at the raid; Rook fights it). The 633 raid transcript is historical and not replayed; under this rule its tiles would differ.

## Amends 0278

Warm plays under 0278 use the floor the campaign seats: the levy floor N less 3 before map N, the pick at her join level, not `--level 4`, unless the map's own row in STATE names a level (Chat, round 458; taken in 459). The raid read 9/7/6 at L1 and 6/6/4 twice at `--level 4`: a lab company over the floor drains a map's tension. The raid stays shut to rework until a read at its floor (levy L3, the pick at L4) repeats the dead-van pattern; if one does, Chat's 301 lever (the van a column west) is first.

## Kill criterion

If a player who picks the claimant on her join map still needs two benches to field her, or a bench's line names a unit who does not then deploy, the rule is wrong and goes back to the Table.
