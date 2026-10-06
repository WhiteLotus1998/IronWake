# 0287 — The hunt line names whom, not which way

Date: 2026-10-06. Issue #1204; Chat on #1211 (round 409: no path rule, a content test, and a map that fails it gets its line reworded). Amends 0284's screen text; the rule does not change.

## Context

0284 printed `comes through it for the nearest: <names>` on an empty front and the board rule `A front with no defenders lets it through`. That is a promise about the hunter's route, and 0284 makes no path rule: the hunter takes its ordinary approach to its prey. Chat asked for a content test that the approach to an empty front's prey really enters through that front, and for any map that fails it to say `comes for the nearest` instead.

## What the test found

For each map with `hunter:` (not `hunt_waits`), each front, and each inside tile a lone unit could hold without defending that front (inside: cut off from the hunter's tile once every front tile is blocked), the test asks whether some cheapest approach from the hunter's tile to a strike on that tile steps on the front's tiles, under the hunter's own costs (`Movement.DistancesTo`, outrider). On the campaign keep the hunter starts on 7,6, three steps from the gate, so:

| Map | north | gate | south |
|---|---|---|---|
| `content/keep/ironwake_keep.map` | 43 of 50 off the route (7 tie) | 0 of 50 | 43 of 50 (7 tie) |
| finale, hunt, assault samples | 47 of 52 | 0 of 49 | 48 of 53 |
| `ironwake_keep_hask_holds.map` | as the keep | as the keep | as the keep |

An empty north's prey on 11,4 is reached through 10,5. Chat's 2210 lever-1 replay came in through 10,2 only because the hunter had idled at 9,2 by turn 4: the route depends on where it stands, so the line could lie.

## Decision

- Every non-waiting hunter map fails, so the wording changes for all of them: `<hunter> hunts the <front> next: no defenders (weakest); comes for the nearest: <names>`, and the board rule ends `A front with no defenders turns it on the units nearest that front.` Prey, planning, `threat` and `end`'s ask are unchanged.
- `Hunt.ComesThrough` is renamed `Hunt.ComesForTheNearest`.
- Guard: `KeepTests.AHunterMapWhoseEmptyFrontIsOffTheHuntersRoutePrintsNoRoute` runs the check on every shipped hunter map and refuses `through` in the rule of a map that fails it; the old rule text fails it. It pins the 11,4 case (crossing 10,5).
- No path rule (Chat, #1211). A future hunter map that routes its hunter through every empty front may earn a route line back, with this test as its gate.

## Transcripts

Only `docs/transcripts/2026-10-06-ironwake_keep-2210-lever1.txt` prints the hunt, regenerated with `tools/rejournal.py` (rule line and the `comes for the nearest: Pell` line; same play).
