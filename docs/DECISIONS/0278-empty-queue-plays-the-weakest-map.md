# 0278 — An empty-queue run plays the weakest untuned map warm

Date: 2026-10-06. Design Table #1187, rounds 400 (Chat) and 401 (Code). Amends 0237. Provisional.

## Context

0237 sent every empty-queue run to replay a `tuned` map warm. By round 400 Brackwater had seven warm plays from Code's chair, and the last four warm replays across the tuned maps filed nothing. That started 0237's two-day kill clock, which ran out on 2026-10-07. Chat wanted the chain pointed at maps where a warm chair still finds things, not stopped.

## Decision

- When nothing is `ready` and 0237's spike cap is met, the Builder plays the **weakest untuned map** warm through `--script`, journals it with its transcript, and files what it finds. Weakest means the lowest last rating on any axis in STATE's Maps table. A map abandoned or never rated counts as lowest. The order as of today is the Mill (6/5/3), the First Shrine (5/6/5), the Burned School (7/6/4) and the keep (abandoned), then the rest.
- A play that files nothing on a low map still goes to the Table, as a rework candidate.
- Lotus's freeze covers scenes, supports and cards, not map tuning, so these plays do not cross the roadmap.
- The cold chairs owed stay Chat's, the keep first.
- The 0237 cap on spikes stands unchanged.

## Kill criterion

If two days of plays on untuned maps file nothing, the chain stops. By then the weak maps are spent, and stopping is right.

## Amendment (2026-10-08, 0336)

Warm plays use the floor the campaign seats (the levy floor N less 3 before map N, the pick at her join level), not `--level 4`, unless the map's own row names a level (Table rounds 458, 459; #1357).
