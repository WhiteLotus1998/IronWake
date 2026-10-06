# 0290 — The `holds:` line says a held member stays where it struck

Date: 2026-10-06. Issue #1216; the Table, round 410 (Chat on #1187, Code agreed). Amends 0279's screen text; the rule does not change.

## Context

0279's line, `holds: the mill group leaves 0,0 to 11,2 only to strike, then goes back`, read as if a held member were home again by the player phase. It is not: a member that struck from off its ground stays on its strike tile and walks back only in an enemy phase with no strike in reach. Chat's 2300 on `ironwake_keep_hask_holds.map` turned on exactly that (Hask struck from 3,5 on turn 9 and was still there on turn 10), and the Mill's 2250 showed it too (the archer stayed on 8,3).

## Decision

- The board line names the three facts: `holds: the mill group strikes out from 0,0 to 11,2, stays where it struck, and goes back when nothing is in reach`; a one-tile post prints one tile, `holds: the lord group strikes out from 0,6, ...`.
- `help` prints the same rule as a sentence among the map's rules: `The mill group holds 0,0 to 11,2. A member strikes out from it, stays where it struck, and goes back when nothing is in reach.`
- DESIGN 8's held-ground paragraph says the same.
- Guard: `HeldGroundTests.AHeldMemberStaysWhereItStruckIntoThePlayerPhase` plays a whole enemy phase and finds the member still on its strike tile, off its ground, when the player phase starts, so the line cannot drift from the rule.

## Transcripts

Regenerated with `tools/rejournal.py`, the `holds:` line alone changed, same plays: `docs/transcripts/2026-10-06-the_mill-1500.txt`, `-1510.txt`, `-632-south.txt`, `-632-campaign.txt`. Unreplayed transcripts that print the old line (Chat's 2250 and 2300) are left as played.
