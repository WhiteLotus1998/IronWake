# STATE

Updated: 2026-09-16 (issue 42, the first of the Critic's bugs; Table fourth round: the front-loaded block is 10 and 11)

## Where we are

Issue 42 done: `Movement.Reach` refuses an origin its movement type cannot stand on (a footman on a wall, a rider in water) with an error in the loader's own wording, so Core is as strict as the content loader about the same fact; `ironwake reach` checks first and prints `ERROR: infantry cannot stand on Wall at 9,5`. A flyer over water is a valid origin. 289 tests. The Critic's other bugs (41, 43, 44, 45) are next in the bug queue.

Issue 4 done: `Movement.Reach` answers "where can this unit move" (DESIGN.md section 4, DECISIONS/0012): Dijkstra with the terrain's per-movement-type costs, a strict Mov budget, allies crossable but not destinations, enemies never entered, a path to every tile, and a documented tie-break (cost, then row-major; north, west, east, south) so every renderer draws the same path. Occupancy is a `Coord -> Occupant` function the caller supplies; `MapDefinition.OccupantAt` answers it from placements until issue 6's `BattleState` does. `ironwake reach <map> <x,y> [movement:mov]` prints the map view with destinations marked `*`. Issue 3 before it: the `.map` format, canonical writer, console view, three sample maps. 281 tests. Still nothing playable, so no journal entry; issue 11 is where play begins.

The Table's three design rounds are done (DIALOGUE.md): Guard groups wake by proximity, the forecast shows the resolved probability, gate 4 is ablation, Recall scars is killed (DECISIONS/0010), keyed rolls (#31) land with issue 5, and the experiment order is map events (#32), retreat (#33), rivalry (#16). The third round (the Critic's first pass) added issue 47: dead, quiet, and live turns with the wake tax, and the free prefix beside both slack figures, printed by `--full`, gated on nothing yet. The fourth round moved play ahead of EXP and inventory: 10 and 11 are the front-loaded block, `item` refuses out loud, and the first two journal entries are systems entries, not Fun Gate entries.

## Next

Bugs first: 44, 45 (phase 1), then 41, 43 (phase 2), one per run. Then the Builder takes issue 5 together with 31 (combat forecast and resolution on keyed rolls; the hit-probability function is one function with the roll scheme as a switch inside it). Then 6 (`BattleState` from `MapDefinition`; the roster fills `PlayerSlot`s; it answers `Movement.Reach`'s occupancy function), then 7, then the front-loaded block (Table, 2026-09-16): 10 (enemy AI, so the brigand comes to the player line), then 11 (playable CLI; `item` refuses with a usage error and `help` lists it unavailable; `play` prints one line naming the systems the build lacks). Then 8 and 9, which carry `blocked` until both 10 and 11 merge; the run that merges the second of them removes the label. The first two PLAYTEST entries after 11 are systems entries on a format sample, not the Fun Gate (DIALOGUE.md, fourth round).

## Open forks (need Lotus)

None.

## Open on the Design Table

Nothing waits on Chat. Leaning, not agreed: one roll versus two (A/B after issue 5), rivalry's arm (the spike), the wake tax floor (issue 13's journals), and 10 before 11 inside the front-loaded block (Code's lean, the priority rule's own order; argue it on 10's PR). Code's two issue-47 additions are agreed with Chat's amendments (p90 slack and the boundary refund; the wake tax beside the quiet state) and are in the issue body. Decisions 0011 (map format) and 0012 (movement query, tie-break, strict budget) are Code's rulings made without the Table; argue them on the PRs if they read wrong.

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
- Chat also runs as a webhook-woken cloud routine (DECISIONS/0007). Both partners answer the Table within minutes. Code (Builder hourly 02:00 to 05:00 New York, and the Partner) runs on Fable 5.1; the Critic and Chat routines are on Opus 5 (DECISIONS/0009, Lotus's ruling). Failure handling is listed at the end of ROUTINES.md.
- Cloud environments: the setup script preinstalls dotnet-sdk-8.0 and gh (snapshot cached). In the cloud, GitHub goes through the built-in GitHub tools; `gh` reports an invalid token there. On Lotus's desktop it is the reverse.
- Content JSON shapes: see `ContentLoader` and the starter files. Stats objects use lower-case keys `hp str mag dex spd lck def res cha`; `modifiers`/`growthModifiers` may omit stats (default 0), a unit's `stats`/`growths` must list all nine. Terrain `cost` uses `null` for impassable.
- Map files: see DESIGN.md section 10 and DECISIONS/0011. Hand-edited maps must be canonical (`MapFormat.Write` output); the test names the file when one is not. Coordinates are `x,y` from the top-left, 0-based.
- CLI tests redirect the process-wide console, so every test class that runs the CLI carries `[Collection("console")]`; without it xUnit runs classes in parallel and the output crosses.
