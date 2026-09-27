# STATE

Updated: 2026-09-27. Rewritten, not appended: this file is what is true now. Kept under 20 KB (issue 401); history lives in git, the PR descriptions and `docs/DECISIONS/`.

## Where we are

Phases 1 and 2 of DESIGN section 12 are built: rules, content, the cast of eleven, maps, combat on keyed rolls, Recall, the enemy AI, the console game (`ironwake play`, `campaign`, `threat`, `recall list`, `--protocol`) and the Sim with gates 1 to 8. Phase 3 is under way: abilities, ranks, arts, Breakers and masteries, gauntlets, Canto, certification and trials, the between-map campaign with the keep, difficulty as data, map events, and the Godot client (slices 1, 2 and 2b, the Windows export, the readability pass).
Three maps are `tuned`: the Tollgate (0073), Brackwater Cut at dusk (0078), Harrow Weir (0081). The other five are playable and waiting on plays (Maps, below).
1800 tests green (four hold STATE.md and DIALOGUE.md to their caps, #401); `ci` and `ci-windows` both run; `godot-parity`, `godot-windows-export` and `godot-windows-launch` run but are not required.
No forks are open. The builder-chain heartbeat stays (Lotus's ruling on #406, 2026-09-27).

## Next

- #403 is built: at dusk `threat` lists the `?` tiles within the content's longest reach (8), nearest first, unpriced. Its deciding play is a cold dusk play of Brackwater.
- #415 is built: at dusk a run of dark acts prints as one counted console line (`(x14)`), order kept; the event log and the protocol keep one per command.
- #419 is built (0083): on a `pincer: on` map an enemy steps in as an anvil so a group-mate strikes pinned, and acts ahead of the rest to do it. Header-off plan order is unchanged. In the Sim an anvil acted in 19 of 40 heuristic games; in Code's hand play (seed 131, warm) none did, because Sallow's geometry kept the field group off every back tile. Chat's cold play (seed 433) drew no anvil either: Sallow cannot judge the arm, so its deciding play moves to a daylight Brackwater sample (#429).
- The Table rotated to #420 (#364 closed, rounds 63 to 82). #420 is found by its title and `design-table` label; the tools cannot pin it.
- 13.14 Brace is built (#425, 0084) and passed its kill criterion on Chat's cold-to-rule play (seed 439, 8/8/6; Code seed 307): on a `brace: on` map a unit that waits on the tile it began its phase on is struck at -15 hit until its side's next phase; a sleeping Guard never braces; a vetoed boss keeps his brace. Its keep round is `saltmarsh_ford_brace.map` (#430), after Chat's #131 re-rate.
- Queue: #429 (the Brackwater pincer sample), #430 (the Saltmarsh brace sample). The queue order in DIALOGUE.md: bugs, map retunes with both entries in, the beta, the rest of Phase 3, Map 7 and Supports. An empty queue spikes an experiment (the gate is open).
- #349's readability pass is built; both partners play the Tollgate in the client and journal it.
- #157 (the refused-kill row prints the stall's refusal too) waits behind the Fun Gate under the veto freeze.
- Blocked until plays or the Table: #13, #131, #160 (Chat's re-rates), #77 Supports, #78 to #81 (their maps' Fun Gates), #85 (after map 8), #83 Map 8 (open, `content`).

## Maps

| Map | Status |
|---|---|
| the_tollgate | **tuned** (0073). Rider spawns at 13,4 when a unit stops on 6,4 or 6,3 (0072). Four deployed, limit 10. Gate 1 74 percent, gate 4 ok at 0.245. Fun Gate: Code seed 211 8/7/7, Chat seed 227 8/7/7, both warm. |
| brackwater_cut | **tuned** (0078). Escape at `dusk: 5`, exit without a Move (0074), lamps on a player-phase wake (0076). Gate 1 64 percent (survivors p50 0 of 4, captain alone 81; the planner's margin, not the map's), gate 4 ok at 0.158. Fun Gate: Chat seed 271 8/7/7, Code seed 283 7/7/8, both warm; the Critic's cold seed 509 7/6/7. |
| harrow_weir | **tuned** (0081). Guard Foreman with the Toll Axe under the boss veto, goes home when refused (0080), `wake_links: ford>weir`. Gate 1 67 percent, gate 4 ok at 0.305. Fun Gate: Code seed 419 8/7/7, Chat seed 421 7/7/7, both warm; no cold chair yet. |
| old_mill_road | Not tuned. `supplies: 1` (0039). Gate 1 29 percent (FAILED, 109 captain deaths, read as the heuristic's policy). Last entries: Chat 6/7/5 cold (seed 131, on 0034), Code 6/6/4 (seed 139, on 0039). Waits on Chat's cold re-rate (#160), then gate 1. |
| saltmarsh_ford | Not tuned. Toll Axe boss (0029), ford forest (0030). Gate 1 11 percent, gate 4 fails at 0.030 (the heuristic's stall before the healing boss). Fails on choice from both chairs; waits on Chat's re-rate (#131). |
| sallow_grange | Not tuned. Seize, the Reeve a guard boss at 15,6 (0055), hexer at 13,7 (#275). Gate 1 79 percent, gate 4 ok at 0.375. Last entries: Chat 7/7/6 (seed 44, pre-#275), Code 6/6/5 (seed 61, the long way). Both replay it and score short against long. |
| ironwake_raid | Not tuned. Campaign map 5 under `content/keep` (0060): rout, limit 7. Gate 1 94 percent. Code seed 288 6/6/6. Waits on Chat's play of the raid and the camp. |
| ironwake_keep | Not tuned. The campaign's last map, survive, limit 8, fought on the record's keep (0059, 0060). Acceptance is play, not gate 1. Plays: Code 82, 287, 288; Chat 91. |

## Open experiments and the play that decides each

| Experiment | Where | Deciding play |
|---|---|---|
| 13.4 grudges | `docs/samples/old_mill_road_grudges.map` (0065, 0066) | Chat's seed 23 replay under #331 |
| 13.5 the keep | the campaign (0059, 0060) | Chat's play of the raid and the camp |
| 13.6 trials | `content/trials/` (0057) | Chat's cold play of the rebuilt Outrider trial (closes #73) |
| 13.7 dusk, `threat` row | Brackwater Cut, #403 (built) | a cold dusk play of Brackwater on main |
| 13.8 carrier arm | `docs/samples/*_keepsakes.map` | Chat's cold carrier-arm plays |
| 13.10 retreat, third pass | `docs/samples/river_refuge_hold.map` (0037) | Chat's cold play |
| 13.11 two-weapon boss | `docs/samples/saltmarsh_ford_chief.map` (0061) | Chat's cold play |
| 13.13 pincer | `docs/samples/sallow_grange_pincer.map` (0082, 0083), arm 2 on `brackwater_cut_pincer.map` (#429) | Chat's cold play of the Brackwater sample |
| 13.14 brace | `docs/samples/harrow_weir_brace.map` (0084), passed; keep round `saltmarsh_ford_brace.map` (#430) | both partners' plays of the Saltmarsh sample, after #131 |

Kept behind headers on samples only: rivalry (0043), shove (0069), Seize drift. Killed: Recall scars (0010), battalions dropped (0044).

## Standing notes

- The weekend (Lotus, 2026-09-25): all routines run on Opus 5.5 until 2026-09-28T18:00:00Z, then the Partner and the Critic return to Fable and the Critic reviews everything merged over the weekend; the Builder stays on Opus 5.5. Weekend decisions are provisional in the ordinary way.
- Repo is public (0006); `main` is protected by the `ci` check and PRs auto-merge on green. Routine pushes use `issue/<n>-<slug>`, falling back to `claude/`.
- Routines (ROUTINES.md): Builder slots at 02:00, 03:00 and 05:00 New York, plus the chain (one issue per merged Builder PR while `IRONWAKE_CHAIN` is `on`, with the 20-minute heartbeat; DECISIONS/0050's amendment, section 6); the Critic on Lotus's schedule, its `critic` issues the record; the Partner woken by Table comments.
- Sim: `--smoke` (CI, gates 5 to 8); `--full <map>|--all [--seeds N] [--scheme one|two] [--taxfloor F] [--difficulty D]` (gates 1 to 8, about a minute and a half per map at 200 seeds in Debug; not in CI); `--trace <map> <seed>` (a script the CLI replays under `--strict`); `--keep [<edit> <x,y>]...`; `--hitband`. Kept measurements are under `docs/measurements/`.
- Content shapes: `ContentLoader` and the starter files. `units/cast.json` is the roster in order, captain first. `rules.json` holds map-free constants and the difficulties. Stats use lower-case keys `hp str mag dex spd lck def res cha`; terrain `cost` is `null` for impassable.
- Map files: DESIGN section 10 and 0011; hand-edited maps must be canonical (`MapFormat.Write` output). Coordinates are `x,y` from the top-left, 0-based; CLI slots count from 1.
- CLI and Sim tests read the console only through `ConsoleCapture.Run` under `[Collection("console")]`; every project runs under invariant globalization.
- Cloud: dotnet-sdk-8.0 and gh are preinstalled; GitHub goes through the built-in tools, since `gh`'s token check fails in the sandbox.
