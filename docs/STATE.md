# STATE

Updated: 2026-09-15 (issue 3; Table unchanged since the second design round)

## Where we are

Issue 3 done: the `.map` format from DESIGN.md section 10 parses (`MapFormat.Parse`, errors naming file and line), writes canonically (`MapFormat.Write`, a fixed point held over every file under `content/maps/` by a test), and renders as a console view (`MapRenderer`, players uppercase, enemies lowercase, boss `!`, legend with the terrain under each unit). Core gained `Coord`, `MapDefinition`, `Placement`, and the `Side`/`Behavior`/`WinCondition`/`PlayerSlot` enums. Three sample maps: Old Mill Road (the doc example, `recruit:mira` renamed `recruit:wren`), Saltmarsh Ford (two one-tile fords, a boss on a fort), The Tollgate (a walled keep with a one-tile gate, a Seize). `ironwake validate` counts maps; `ironwake show <map>` prints the view. 219 tests. Still nothing playable, so no journal entry this run; issue 11 is where play begins.

The Table's two design rounds are done (DIALOGUE.md): Guard groups wake by proximity, the forecast shows the resolved probability, gate 4 is ablation, Recall scars is killed (DECISIONS/0010), keyed rolls (#31) land with issue 5, and the experiment order is map events (#32), retreat (#33), rivalry (#16).

## Next

Nightly Builder takes issue 4 (movement: reachable tiles; `MapDefinition.TerrainIdAt` and `Terrain.MoveCost` are the inputs). Then 5 together with 31, then 6 (BattleState from `MapDefinition`; the roster fills `PlayerSlot`s), then 7, 8, 9 in order.

## Open forks (need Lotus)

None.

## Open on the Design Table

Nothing waits on Chat. Leaning, not agreed: one roll versus two (A/B after issue 5), rivalry's arm (the spike). Decision 0011 (map format semantics) is Code's ruling on issue 3, made without the Table; argue it on the PR if it reads wrong.

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
