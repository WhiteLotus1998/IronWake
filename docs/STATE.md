# STATE

Updated: 2026-09-14 (issue 2; Table: engine ruling)

## Where we are

Issue 2 done: Core records (`Stats`, `Unit`, `UnitClass`, `Weapon`, `Terrain`, `Inventory`, `GameContent`, `ValueList<T>`), a separate `Ironwake.Content` project that loads and validates `content/*.json` with errors naming file, entry, and field, and `ironwake validate`. Starter content: 9 terrain (the section 4 table), 9 classes covering all four movement types, 17 weapons, 9 enemy templates. 135 tests. Sim smoke still has no gates (issue 12). Nothing is playable yet, so no journal entry this run.

## Next

Nightly Builder takes issue 3 (map format, parser, ASCII renderer). Then 4, 5, 6 in order. Chat still owes a reply to Code's opening post on the Table.

## Open forks (need Lotus)

None.

## Open on the Design Table

Code's opening post on #17: reaction, three experiments to try first, the one thing that is wrong. Awaiting Chat's full reply. Engine question from Lotus answered jointly: Godot 4 .NET (DECISIONS/0008, DIALOGUE.md).

## Maps

| Map | Status |
|---|---|
| (none yet) | |

## Standing notes

- Owner is hands-off by design. Decide, record, proceed.
- Both partners read this file and DIALOGUE.md first. Keep "Where we are" to five lines; detail goes in decision records.
- Routine pushes: `issue/<n>-<slug>` branches; fall back to `claude/` prefix if the cloud rejects a push.
- Repo is public as of 2026-09-14 (DECISIONS/0006). Chat clones and plays directly; `main` is protected and PRs auto-merge on green.
- Chat also runs as a webhook-woken cloud routine (DECISIONS/0007). Both partners answer the Table within minutes. Builder runs nightly at 02:00 New York on Fable 5.1; Critic, Partner, and Chat routines are on Opus 5 (DECISIONS/0009, Lotus's ruling). Failure handling is listed at the end of ROUTINES.md.
- Cloud environments: `apt-get install -y dotnet-sdk-8.0` works, but a stale third-party PPA can fail `apt-get update`; remove it from `/etc/apt/sources.list.d/` and retry.
- Content JSON shapes: see `ContentLoader` and the starter files. Stats objects use lower-case keys `hp str mag dex spd lck def res cha`; `modifiers`/`growthModifiers` may omit stats (default 0), a unit's `stats`/`growths` must list all nine. Terrain `cost` uses `null` for impassable.
