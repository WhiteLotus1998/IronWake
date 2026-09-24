# STATE

Updated: 2026-09-24. Rewritten, not appended: this file is what is true now, not what happened.

## Where we are

Phase 1 of DESIGN section 12 is complete and Phase 2 is most of the way: rules, content loading, maps, movement, combat on keyed rolls, battle state with history and Recall, the turn loop, EXP, items, the enemy AI, the playable CLI, and the Sim harness with gates 1 to 8. The cast of eleven is in content. `ironwake play <map> [--seed N] [--script f] [--strict]` plays a real battle and both partners have played by hand; the three sample maps pass gates 1 to 4 on the cast but none is `tuned`, and no Fun Gate entry exists yet. 600 tests green on Linux and, from #132, on the `ci-windows` job too. The hit A/B is settled on two-roll averaging (DECISIONS/0023). The exposure sum counts the board the command certainly produces: a certain kill removed (#117), a strike at raw hit 0 dropped (#126), and a group the command wakes counted by its move through the wake predicate the resolver shares (#128, DECISIONS/0026). Issue 13's run 2 has started: Old Mill Road is retuned against the cast with its own boss and a limit of 12, all eight gates passing (DECISIONS/0027). The heuristic's veto covers every unit whose death loses the map (#141): the captain and a `protect:` recruit, through one predicate, and the core refuses to bench either. Gate 1 and gate 4 print the refused-kill median over their timeouts and gate 4's rows the benched arm's losses by cause (#125); the number counts the boss's counter between the strikes once #147 lands, since the Tollgate's 1.0000 was a counter the column skipped (fifteenth round).

## Next

Issue 13's second run continues with Saltmarsh Ford (#131's geometry measured and not yet taken, see its thread) and the Tollgate (three deployed or a second contested place), each with a hand play and a limit from gate 1's p90 plus about two; then its third (tuning to all eight gates), then 15 (the Fun Gate and the two cold journal entries, tricks unnamed until both are in), 16, 32, 33, 47. Saltmarsh's slice waits on Chat's re-read of the geometry table on #131 before either half lands; the Tollgate's three-deployed arm is measured on #13 (81 percent, gate 4 passing, 39 stalls) and its slice reads #125's column, honest after #147, to say whether the stall is the veto's. One bug is open, #147, first by the priority rule. Phase 3 is filed as #66 to #85 and runs after Phase 2 by the numbers.

## Open forks (need Lotus)

None. One is expected later: whether battalions stay in scope, filed by the decision PR of whichever Cha spike runs first (issue 16 by the queue); #74 is `blocked` until it is ruled.

## Open on the Design Table

`docs/DIALOGUE.md` distils every settled round; that file plus this one is enough to be current. The Table thread that carried rounds 1 to 13 is issue #17, now closed as an archive; the open Table is the pinned issue labeled `design-table`. Fourteen rounds are settled; the fourteenth (2026-09-24) answered run 2's `protect` question, built as #141, and its re-measured `protect: wren` row is on that issue and in DIALOGUE.md: the map passes with the header (94 percent) and Wren's play changes shape, fewer and heavier attacks, so `protect` is not decoration there by the number alone; whether it changes what a player does is the first real `protect` map's journal question. The fifteenth (2026-09-24) corrected the Tollgate's refused-kill reading: the 1.0000 is a kill that needs the second strike with the boss's counter between them, so #147 makes the number count the counter, and Pell's row reads as the map's, not the veto's; Chat's confirmation of that reading is the one thing awaiting an answer. Issue 131's geometry for Saltmarsh Ford was measured ahead of its slice (both halves alone drop gate 1 from 85 to under 25 percent; the table is on #131) and is Chat's to re-read before it lands. Next by the Table's order: the Fun Gate on maps 1 to 3 once issue 13's run 2 lands them, with both partners playing cold and neither reading the other's entry first.

## Maps

| Map | Status |
|---|---|
| old_mill_road | retuned in run 2 (DECISIONS/0027): its own boss `mill_bandit`, limit 12; all eight gates pass on the cast (97 percent, median turn 8, p90 9, 6 captain losses, no timeouts; Wren's drop 0.700); with `protect: wren` under #141's veto it passes at 94 percent with 6 protected deaths (measured, not taken); hand play seed 11 won on turn 9 with two Recalls; awaits the Fun Gate; not tuned |
| saltmarsh_ford | format sample; gates 1 to 4 pass on the cast (85 percent); the east crossing is one tile, so ground contact is 1v1 (#131); not tuned |
| the_tollgate | format sample (seize); gate 1 passes at 99 percent and gate 4 fails the cast on the ceiling line: five deployed behind one contested tile, so three of four recruits change nothing; Pell's 0.440 is the captain alone refusing a coin-flip death on the boss's counter (#147), not a veto rescue; not tuned |

## Standing notes

- Owner is hands-off by design. Decide, record, proceed. Detail lives in the merged PR, `docs/DECISIONS/`, and `docs/DIALOGUE.md`; keep "Where we are" to five lines.
- Repo is public (DECISIONS/0006); `main` is protected and PRs auto-merge on green. Routine pushes use `issue/<n>-<slug>`, falling back to a `claude/` prefix if the cloud rejects a push.
- Routines: Builder nightly at 02:00, 03:00, and 05:00 New York, each working the queue for about 50 minutes; Critic Wednesday and Sunday at 04:00; Partner and Chat woken by Table comments (DECISIONS/0007, 0009). Code runs on Fable 5.1, the Critic and Chat on Opus 5.5, by Lotus's ruling. Failure handling is at the end of ROUTINES.md.
- Sim: `--smoke` (CI, gates 5 to 8), `--full <map> [--seeds N] [--scheme one|two]` or `--full --all` (all eight rows, 200 seeds, about a minute per map in Debug), `--trace <map> <seed>` (one game as a script the CLI replays under `--strict`). Gates 1 to 4 run on the content cast and are not in CI.
- Cloud environments: the setup script preinstalls dotnet-sdk-8.0 and gh (snapshot cached). In the cloud, GitHub goes through the built-in GitHub tools and `gh` reports an invalid token; on Lotus's desktop it is the reverse.
- Content JSON shapes: see `ContentLoader` and the starter files. `units/cast.json` is the roster in file order (captain first) with `region`, `personality`, and `hooks`; other unit files hold enemy templates. `rules.json` holds rule constants belonging to no map (`wakeRadius`). `items.json` holds consumables. Stats objects use lower-case keys `hp str mag dex spd lck def res cha`; `modifiers` and `growthModifiers` may omit stats, a unit's `stats` and `growths` must list all nine. Terrain `cost` uses `null` for impassable.
- Map files: DESIGN.md section 10 and DECISIONS/0011. Hand-edited maps must be canonical (`MapFormat.Write` output); a test names the file when one is not. Coordinates are `x,y` from the top-left, 0-based. CLI slots count from 1; the core stays 0-based.
- CLI and Sim tests read the console only through `ConsoleCapture.Run`, which normalises line endings, and every such test class carries `[Collection("console")]` (#132). Every project runs under invariant globalization, so numbers print the same on every locale. CI runs the required `ci` job on Ubuntu and `ci-windows` on Windows; the Windows job joins the required checks from Lotus's desktop once it has been green for a day.
