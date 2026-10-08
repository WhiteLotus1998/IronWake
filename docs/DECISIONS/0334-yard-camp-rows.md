# 0334: The duty and yard rows on the camp screen, held to the console by clicks

Date: 2026-10-08. Issue #1331, slice 4 (the last). Follows 0329, 0331 and 0332; Table rounds 447 to 452.

## Decided

- **The rows are the client's `CampActions`, the same list every camp row already uses**, so the Godot camp screen draws them with no Godot change. For the selected unit, while it has taken no duty at this camp: `rest at this camp (the duty)` (`duty <unit> rest`); with a forge built and the unit free for it, `work the forge, slot N: <weapon>, Mt|hit (no gold)` (`duty <unit> forge N mt|hit`), or a bare `work the forge (the duty)` for a unit with no weapon; and one `train under <teacher> in the <weapon> (the yard)` row (`yard <teacher> <unit> <weapon>`) for every teacher and weapon the record's own `YardRefusal` accepts, the unit as the student. A unit that has a duty is offered none of these.
- **The presenter plays the drill as it plays a side map.** `CampaignClient.Duty`, `ForgeDuty` and `Yard` are the record's own actions; `Yard` opens the drill as the battle with the console's two opening lines (now one static, `CampaignSession.YardOpening`, used by both), and `Leave` applies `AfterYard` and writes its line.
- **Parity by clicks:** `tests/parity/campaign/camp-duties-41.script` (a forge duty's free step, a rest, a lost drill, the march) matches `campaign --log` byte for byte from the same record, with the guard falsified by a log missing the forge line.
- **No new console line.** The console's camp screen is unchanged, so no transcript moves.

## Table, recorded (rounds 451, 452)

- The yard arm is not read for a verdict until #1332's practice weapon is in; on the rerun teacher falls are read first, target near zero.
- The number-free soften rule (the heuristic teacher strikes a hand only where the student can reach and finish it this phase, else screens) ships with #1332.
- A teacher who falls in the yard pays what a map fall pays (death under permadeath, a wound without it), as Lotus's ruling holds permadeath for both. If teacher falls stay high with the practice weapon in, it goes to Lotus as a `for-lotus` question.

## Unsure

- The camp screen does not list who took which duty; the rows' absence is the only sign. A roster note (`duty: forge`) would move the yard transcripts only, so it is cheap when wanted.
- Inside a drill, a move after the student falls is refused as "the battle is lost (the captain is dead)": the resolver's generic text names the captain slot, which in the yard is the student.
- The Godot run of the new script has not been made (this sandbox has no Godot).

## Next

- #1332: the practice weapon, the soften rule, the ring and the post; then rerun `--yard`.
