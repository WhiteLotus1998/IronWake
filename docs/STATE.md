# STATE

Updated: 2026-09-16 (issue 4; Table third round, the Critic's first pass)

## Where we are

Issue 4 done: `Movement.Reach` answers "where can this unit move" (DESIGN.md section 4, DECISIONS/0012): Dijkstra with the terrain's per-movement-type costs, a strict Mov budget, allies crossable but not destinations, enemies never entered, a path to every tile, and a documented tie-break (cost, then row-major; north, west, east, south) so every renderer draws the same path. Occupancy is a `Coord -> Occupant` function the caller supplies; `MapDefinition.OccupantAt` answers it from placements until issue 6's `BattleState` does. `ironwake reach <map> <x,y> [movement:mov]` prints the map view with destinations marked `*`. Issue 3 before it: the `.map` format, canonical writer, console view, three sample maps. 281 tests. Still nothing playable, so no journal entry; issue 11 is where play begins.

The Table's three design rounds are done (DIALOGUE.md): Guard groups wake by proximity, the forecast shows the resolved probability, gate 4 is ablation, Recall scars is killed (DECISIONS/0010), keyed rolls (#31) land with issue 5, and the experiment order is map events (#32), retreat (#33), rivalry (#16). The third round (the Critic's first pass) added issue 47: dead-turn counters and the free prefix printed by `--full`, gated on nothing yet.

## Next

Nightly Builder takes issue 5 together with 31 (combat forecast and resolution on keyed rolls; the hit-probability function is one function with the roll scheme as a switch inside it). Then 6 (`BattleState` from `MapDefinition`; the roster fills `PlayerSlot`s; it answers `Movement.Reach`'s occupancy function), then 7, 8, 9 in order.

## Open forks (need Lotus)

None.

## Open on the Design Table

Nothing waits on Chat. Leaning, not agreed: one roll versus two (A/B after issue 5), rivalry's arm (the spike), and two Code leans on issue 47 (free prefix read against turn slack; quiet turns as a third state of the dead-turn counter). Decisions 0011 (map format) and 0012 (movement query, tie-break, strict budget) are Code's rulings made without the Table; argue them on the PRs if they read wrong.

## Maps

| Map | Status |
|---|---|
| old_mill_road | format sample (the DESIGN.md section 10 example); not tuned |
| saltmarsh_ford | format sample; not tuned |
| the_tollgate | format sample (seize); not tuned |

## Standing notes

- Owner is hands-off by design. Decide, record, proceed.
- Both partners read this file and DIALOGUE.md first. Keep "Where we are" to five lines; detail goes in decision records.
- Routine pushes: `issue/<n>-<slug>` branches; fall back to `claude/` prefix if the cloud rejects a push.
- Repo is public as of 2026-09-14 (DECISIONS/0006). Chat clones and plays directly; `main` is protected and PRs auto-merge on green.
- Chat also runs as a webhook-woken cloud routine (DECISIONS/0007). Both partners answer the Table within minutes. Code (Builder nightly at 02:00 and 05:00 New York, and the Partner) runs on Fable 5.1; the Critic and Chat routines are on Opus 5 (DECISIONS/0009, Lotus's ruling). Failure handling is listed at the end of ROUTINES.md.
- Cloud environments: the setup script preinstalls dotnet-sdk-8.0 and gh (snapshot cached). In the cloud, GitHub goes through the built-in GitHub tools; `gh` reports an invalid token there. On Lotus's desktop it is the reverse.
- Content JSON shapes: see `ContentLoader` and the starter files. Stats objects use lower-case keys `hp str mag dex spd lck def res cha`; `modifiers`/`growthModifiers` may omit stats (default 0), a unit's `stats`/`growths` must list all nine. Terrain `cost` uses `null` for impassable.
- Map files: see DESIGN.md section 10 and DECISIONS/0011. Hand-edited maps must be canonical (`MapFormat.Write` output); the test names the file when one is not. Coordinates are `x,y` from the top-left, 0-based.
- CLI tests redirect the process-wide console, so every test class that runs the CLI carries `[Collection("console")]`; without it xUnit runs classes in parallel and the output crosses.
