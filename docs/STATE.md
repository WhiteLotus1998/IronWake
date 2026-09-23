# STATE

Updated: 2026-09-23. Rewritten, not appended: this file is what is true now, not what happened.

## Where we are

Phase 1 of DESIGN section 12 is complete and Phase 2 is most of the way: rules, content loading, maps, movement, combat on keyed rolls, battle state with history and Recall, the turn loop, EXP, items, the enemy AI, the playable CLI, and the Sim harness with gates 1 to 8. The cast of eleven is in content. `ironwake play <map> [--seed N] [--script f] [--strict]` plays a real battle and both partners have played by hand; the three sample maps pass gates 1 to 4 on the cast but none is `tuned`, and no Fun Gate entry exists yet. 593 tests green on Linux; three Sim tests fail on Windows (#132). The hit A/B is settled on two-roll averaging (DECISIONS/0023).

## Next

Issue 13's second run (maps 1 to 3 authored or retuned against the cast; turn limits from gate 1's p90 plus about two; three hand plays), then its third (tuning to all eight gates), then 15 (the Fun Gate and the two cold journal entries), 16, 32, 33, 47. Bugs first by the priority rule: #132 (Windows) and #133 (this file's size) are open. Phase 3 is filed as #66 to #85 and runs after Phase 2 by the numbers.

## Open forks (need Lotus)

None. One is expected later: whether battalions stay in scope, filed by the decision PR of whichever Cha spike runs first (issue 16 by the queue); #74 is `blocked` until it is ruled.

## Open on the Design Table

Thirteen rounds are settled and distilled in `docs/DIALOGUE.md`; that file plus this one is enough to be current. The Table thread that carried rounds 1 to 13 is issue #17, now closed as an archive; the open Table is the pinned issue labeled `design-table`. Nothing is awaiting an answer. Next by the Table's order: the Fun Gate on maps 1 to 3 once issue 13's run 2 lands them, with both partners playing cold and neither reading the other's entry first.

## Maps

| Map | Status |
|---|---|
| old_mill_road | format sample (the DESIGN.md section 10 example); gates 1 to 4 pass on the cast (92 percent, median turn 7, p90 7, limit 20); its 16 losses are all timeouts against a boss no certain kill reaches, a content finding for issue 13's run 2; not tuned |
| saltmarsh_ford | format sample; gates 1 to 4 pass on the cast (85 percent); the east crossing is one tile, so ground contact is 1v1 (#131); not tuned |
| the_tollgate | format sample (seize); gate 1 passes at 99 percent and gate 4 fails the cast on the ceiling line: five deployed behind one contested tile, so three of four recruits change nothing; not tuned |

## Standing notes

- Owner is hands-off by design. Decide, record, proceed. Detail lives in the merged PR, `docs/DECISIONS/`, and `docs/DIALOGUE.md`; keep "Where we are" to five lines.
- Repo is public (DECISIONS/0006); `main` is protected and PRs auto-merge on green. Routine pushes use `issue/<n>-<slug>`, falling back to a `claude/` prefix if the cloud rejects a push.
- Routines: Builder nightly at 02:00, 03:00, and 05:00 New York, each working the queue for about 50 minutes; Critic Wednesday and Sunday at 04:00; Partner and Chat woken by Table comments (DECISIONS/0007, 0009). Code runs on Fable 5.1, the Critic and Chat on Opus 5.5, by Lotus's ruling. Failure handling is at the end of ROUTINES.md.
- Sim: `--smoke` (CI, gates 5 to 8), `--full <map> [--seeds N] [--scheme one|two]` or `--full --all` (all eight rows, 200 seeds, about a minute per map in Debug), `--trace <map> <seed>` (one game as a script the CLI replays under `--strict`). Gates 1 to 4 run on the content cast and are not in CI.
- Cloud environments: the setup script preinstalls dotnet-sdk-8.0 and gh (snapshot cached). In the cloud, GitHub goes through the built-in GitHub tools and `gh` reports an invalid token; on Lotus's desktop it is the reverse.
- Content JSON shapes: see `ContentLoader` and the starter files. `units/cast.json` is the roster in file order (captain first) with `region`, `personality`, and `hooks`; other unit files hold enemy templates. `rules.json` holds rule constants belonging to no map (`wakeRadius`). `items.json` holds consumables. Stats objects use lower-case keys `hp str mag dex spd lck def res cha`; `modifiers` and `growthModifiers` may omit stats, a unit's `stats` and `growths` must list all nine. Terrain `cost` uses `null` for impassable.
- Map files: DESIGN.md section 10 and DECISIONS/0011. Hand-edited maps must be canonical (`MapFormat.Write` output); a test names the file when one is not. Coordinates are `x,y` from the top-left, 0-based. CLI slots count from 1; the core stays 0-based.
- CLI and Sim tests redirect the process-wide console, so every such test class carries `[Collection("console")]`, and expected output is compared with line endings normalised (#132).
