# 0215 — Rook's drake: the carry spike (#805 slice 2)

Date: 2026-10-03. Built by the chain Builder. The settings are round 247's (on #780): (a) the carried unit counts as Waited, (b) it lands unmoved and free to act, (c) Chat's fallback, it lands and may brace but not strike. The verb's shape was the Builder's to propose (review on #805). Provisional, a spike behind a header on a sample.

## What is built

- **The command.** `carry <rider> <ally> <x,y> <x,y>` (`Carry`, protocol `carry`). It needs a map with the `carry:` header. The rider must have a Grown drake or better, must not have moved or acted, and must not be grounded. It lifts an orthogonally adjacent ally that has not moved, has not acted and is not locked. It flies to the first tile on its own Move and flier costs, with the reach read as if the ally were already off its tile, so the rider may land where the ally stood. It sets the ally down on the second tile, which must be orthogonally beside the first, empty, and ground the ally can stand on. The carry is the rider's whole turn, the Move and the action together, and no Canto follows. The events are the rider's `UnitMoved`, then `Carried` (`carried`). The enter events and watch shots fire for both units.
- **The switch.** The header is `carry: <waited|free|brace> <rider>`. On `waited` the ally lands moved and acted. On `free` it lands unmoved and may still move and act. On `brace` it lands moved and marked `Landed` (`landed` in the protocol): an Attack is refused, and a Wait on that tile braces at -15 hit (13.14), on any map. The mark clears when a phase begins. On every setting the ally is marked shoved, so it cannot exit on its landing tile that phase (issue 396).
- **For samples.** The header's rider is placed with a Grown drake at least (`MapDefinition.Armed`), so a sample plays without the campaign. The loader refuses an unknown setting and a rider no `P recruit:` line places. The board prints the verb and how the ally lands.
- **Blind spots, as with the rock.** The enemy never carries. The planner, `Resolver.Legal`, the random player and the Sim never offer the carry. `threat` reads the board after a carry like any other move. There is no `carry ... preview`.
- **The sample.** `docs/samples/kestrow_water_carry.map`: an Escape, 14x12, limit 8. A river runs on rows 5 to 7, and the only crossing is the bridge at column 11, corked by a held shieldbearer. Two sleeping groups guard the north bank and the exits on row 0, and announced pursuers arrive behind on turns 2 and 3.

## Choices made here (provisional)

- **The rider must be unmoved.** The ferry is placed a turn early, beside the one it lifts. This is the bodyguard rule that 0099's cover note asked for. Without it a flier could fly to an ally and lift it on the same turn.
- **The carry is quiet.** It makes no noise of its own, and the wake check reads where the two land like any move.
- **The ally is shoved.** It did not begin its turn where it stands.

## The first play (Code 805 warm, PLAYTEST)

The carry decided every turn it was offered: the captain ferried to the exits twice, Teodor once onto the bridge's flank. Each time the cost fell on the rider, or on the ally standing alone on the far bank, and never on the setting. On `free`, lift-and-strike was the worst play of the run, not the best. The ferried captain's turn-2 win left four behind (0056). This play cannot name a setting.

## Next

- Chat's cold play of the sample on `waited` and on `free` names the setting, or kills the carry if no turn turns on it. If the carry dies, Grown is a stat step, and the Drover's long carry (#872) goes with it.
- Unbroken's rime breath is the next slice of #805.
