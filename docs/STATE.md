# STATE

Updated: 2026-09-17 (Table, sixth round closed: the battalions fork's trigger is either Cha job, its deadline is #74, issue 16 carries the rapport rate)

## Where we are

Issue 43 done: `ironwake reach` treats any trailing argument with a colon as a `movement:mov` spec and reports why it failed (`'flyng' is not one of: infantry, cavalry, flying, armored`, the loader's own wording; `mov must be at least 0, got -1`; `mov 'four' is not a number`; wrong part count) with the usage line and exit 2, instead of reclassifying it as a content directory. Bare arguments are still the content directory. Mov 0 stays legal for a probe: the own tile is a destination at any budget, and the renderer's count excludes it by convention, so `cavalry:0` prints `0 tiles` with the origin marked. 316 tests. All five Critic bugs (41 to 45) are closed; issue 5 with 31 is next.

Issue 41 done: `MapRenderer.Letters(map, content)` skips every letter a terrain in the loaded content draws with (`F`, `M`, `T`, `n` in the starter set) on both sides, so the sixth recruit is `G` and a fort is never mistaken for a unit; the skip is by the content's whole glyph set, not the map's, so a slot keeps its letter across maps. Content whose glyphs use all 26 letters of a case is refused, and a test shows it firing. DESIGN.md section 10 carries the rule. 308 tests. The Critic's last bug (43) is next.

Issue 45 done: DESIGN.md section 4's tie-break no longer names neighbour order, which did no work: settle order is (cost, row-major) and a tile's path arrives through the row-major-first neighbour that offers it its cost, so every path is derivable from the map alone. Held by a theory over five origins on Old Mill Road that checks every reachable tile's parent and path prefix; flipping the settle order fails it and the mountain test, reversing `Coord.Neighbors()` fails only `Coord`'s own test. DECISIONS/0012 carries a dated amendment. 302 tests. The Critic's remaining bugs (41, 43) are next.

Issue 44 done: the enemy level floor (DESIGN.md section 10, DECISIONS/0005) lives in `Unit.ScaledTo(floor)` (raised by `AtLevel` when below, untouched when at or above) and has one production caller, `MapDefinition.EnemyUnit(placement, content)`, which the map view asks for the level and issue 6's `BattleState` will ask for the whole unit. `MapRenderer` no longer computes `Math.Max` on its own. 297 tests. One question raised for Chat on the PR: `AtLevel` scales by the unit's own growths, section 3 levels up on unit plus class growth; nothing reads scaled stats yet, so no map number is wrong today. The Critic's remaining bugs (45, then 41, 43) are next.

Issue 42 done: `Movement.Reach` refuses an origin its movement type cannot stand on (a footman on a wall, a rider in water) with an error in the loader's own wording, so Core is as strict as the content loader about the same fact; `ironwake reach` checks first and prints `ERROR: infantry cannot stand on Wall at 9,5`. A flyer over water is a valid origin.

The Table's three design rounds are done (DIALOGUE.md): Guard groups wake by proximity, the forecast shows the resolved probability, gate 4 is ablation, Recall scars is killed (DECISIONS/0010), keyed rolls (#31) land with issue 5, and the experiment order is map events (#32), retreat (#33), rivalry (#16). The third round (the Critic's first pass) added issue 47: dead, quiet, and live turns with the wake tax, and the free prefix beside both slack figures, printed by `--full`, gated on nothing yet. The fourth round moved play ahead of EXP and inventory: 10 and 11 are the front-loaded block, `item` refuses out loud, and the first two journal entries are systems entries, not Fun Gate entries.

## Next

The Critic's bugs are done. The Builder takes #84 first by the priority rule (`bug`: enemy template scaling on effective growth, clamped per stat; Table, sixth round), then issue 5 together with 31 (combat forecast and resolution on keyed rolls; the hit-probability function is one function with the roll scheme as a switch inside it). Then 6 (`BattleState` from `MapDefinition`; the roster fills `PlayerSlot`s; it answers `Movement.Reach`'s occupancy function), then 7, then the front-loaded block (Table, 2026-09-16): 10 (enemy AI, so the brigand comes to the player line; it builds section 8's approach rule, fifth round, and its named test is the brigand's move from 6,5 to 3,6 on Old Mill Road), then 11 (playable CLI; `item` refuses with a usage error and `help` lists it unavailable; `play` prints one line naming the systems the build lacks). Then 8 and 9, which carry `blocked` until both 10 and 11 merge; the run that merges the second of them removes the label. 11's PR carries a debug pass before it opens: a scripted approach with no player attacks over Old Mill Road, committed as a pair under `docs/transcripts/`, the prediction (`-debug.prediction.md`, with its `-debug.script`) in one commit and the run (`-debug.txt`) in the next; the PR description names every divergence, and agreement is not the acceptance. A fault in 10 found by the pass is a `bug` issue whose PR carries a fresh pair; cold play waits on a pair that ran clean on the build both partners will play. The first two PLAYTEST entries after 11 are systems entries on a format sample, not the Fun Gate (DIALOGUE.md, fourth round).

## Open forks (need Lotus)

None.

## Open on the Design Table

Issue 44's question is answered (effective growth, clamped; #84). Sixth round (2026-09-17): the Phase 3 backlog is filed as #66 to #83 with the numbers as the build order (#77 and #83 `blocked`); Cha's two jobs are the lean, Commander's Word (#85, two arms; arm B, reach by Cha, is the shape both partners expect, Chat having withdrawn arm A as its lean, and #85 logs the binding fraction and the captain's exposure) and the rapport rate (issue 16); battalions stay in section 12 unfiled, and their drop is a `fork` for Lotus filed by the decision PR of whichever Cha spike runs first (issue 16 by the queue; its body now carries the rapport rate), with #74 `blocked` until the fork is ruled; #85 runs at its number, after map 8, since that is where its binding fraction can be read. For Lotus, not forks: ROUTINES.md section 2's Builder prompt now protects step 3 and the decision record at the new pace, and the Critic's cadence should follow the merge rate; both are his to paste and schedule. Leaning, not agreed: one roll versus two (A/B after issue 5), rivalry's arm (the spike), the wake tax floor (issue 13's journals). 10 before 11 is agreed flat, and section 8's approach rule (fifth round) is Chat's key order, agreed, with the third tied tile found by Code. Code's two issue-47 additions are agreed with Chat's amendments (p90 slack and the boundary refund; the wake tax beside the quiet state) and are in the issue body. Decisions 0011 (map format) and 0012 (movement query, tie-break, strict budget) are Code's rulings made without the Table; argue them on the PRs if they read wrong.

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
- Chat also runs as a webhook-woken cloud routine (DECISIONS/0007). Both partners answer the Table within minutes. Code (Builder hourly 02:00 to 05:00 New York, each run working the queue for about 50 minutes, and the Partner) runs on Fable 5.1; the Critic and Chat routines are on Opus 5 (DECISIONS/0009, Lotus's ruling). Failure handling is listed at the end of ROUTINES.md.
- Cloud environments: the setup script preinstalls dotnet-sdk-8.0 and gh (snapshot cached). In the cloud, GitHub goes through the built-in GitHub tools; `gh` reports an invalid token there. On Lotus's desktop it is the reverse.
- Content JSON shapes: see `ContentLoader` and the starter files. Stats objects use lower-case keys `hp str mag dex spd lck def res cha`; `modifiers`/`growthModifiers` may omit stats (default 0), a unit's `stats`/`growths` must list all nine. Terrain `cost` uses `null` for impassable.
- Map files: see DESIGN.md section 10 and DECISIONS/0011. Hand-edited maps must be canonical (`MapFormat.Write` output); the test names the file when one is not. Coordinates are `x,y` from the top-left, 0-based.
- CLI tests redirect the process-wide console, so every test class that runs the CLI carries `[Collection("console")]`; without it xUnit runs classes in parallel and the output crosses.
