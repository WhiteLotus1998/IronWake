# 0378: `end` counts Hask's line strike; the swallow keeps every status; the race's turn line

Date: 2026-10-09. Issue #1448, from the Design Table's rounds 530 to 532 (both chairs on the Warden sample at L8 misread the same two screens). Presentation and the lethal guard only; the Sim's play is unchanged.

## Decided

- **`end`'s lethal guard counts the line strike.** `Queries.Lethal` summed the `Threats` lines only, so a unit only the planner's line strike (`LineStrike.Through`) would kill was never refused. Now the line's one strike counts as that striker's whole share, in place of any plain strike priced for the same enemy (the planner strikes one or the other), and the console names it: `Lethal if all land: Ivo (Hask's line for 12, against 1 hp)`. The protocol's `strikers` entry carries `"line": true` for it. A striker the player cannot see at dusk is left out, as before.
- **`threat`'s headline names a printed line.** With no strike counted but a line through the unit: `Threat on Mattias Grue at 6,6 (Plain): no strike counted; Hask's line strike reaches him (below)`, not "no enemy can strike him".
- **The turn line during a race** reads `turn 11 (the limit ends nothing now)` from the swallow on, within the limit and past it (it read `turn N of L` before the limit and `past the limit: the race` after).
- **The swallow keeps every status on him** (`Swallow.Take` is a `with` on the same unit): a lightning mark, an Opening, a stun ride onto the fresh bar. Chat's lean (round 531) and Code's (round 532): it is the clever play a finale should pay for, and he swallows, he does not become someone else. Pinned by `TheSwallowKeepsEveryStatusOnHimSoAMarkRidesOntoTheFreshBar`.

## Open

- Whether the Sim ever swallows with a mark or saves Full Measure for the fresh bar is #1441's read (round 531, point 2).
