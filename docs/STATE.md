# STATE

Updated: 2026-10-07 (#1281: Sunder drops raised ground and lands a flier, 0311; #1293 `blocked`: the sandbox refused the workflow edit, it waits for Lotus). Under 19 KB on a branch, 20 KB on main (#1291); history in git, `docs/DECISIONS/`.

## Where we are

Phase 3 is under way; `tools/rejournal.py` regenerates transcripts (0243). The Table is #1287 (#1251 archived). Casting (`docs/look/`) waits on Lotus. Pillar 5: eleven and the captain (0264). #1002: Kinsbane's barks, cold read pending. #1003: Bet's cards next. #1042 (0247): Bet the cook, the Cold Kitchen, Banked. #804, #806, #807 and #872 are `blocked` on plays, #634 or #535; the dash (0255), the wind (0256) and the one answer (0257) are kept on their samples.
Four maps are `tuned`: the Tollgate (0073), Brackwater Cut at dusk (0078), Harrow Weir (0088, 0100) and the field, Keziah's pick (0233) and Rook's on hand plays with gate 1 short at 86/200 (0250, DESIGN 11's pick-keyed clause). A cold chair on Rook's arm is owed (its tripwire). Maps 1 and 2 and the rest wait on plays (Maps).
~4850 tests green; `ci`, `ci-windows` run.
No forks are open. The chain heartbeat stays (#406). Recent records: `confirm-lethal` (0260); the Drake Warden, never a bare "Warden" (#1126); Rook's frost (0262); the pick at L4 (0263); a guard boss holds his post (#1138, the 0264 on guard bosses); the keep is the finale (0265); the door (0268 to 0277); `holds:` and limit 9 (0279, 0280); the freed tile (0281); the Shrine (0283, 0288, 0304, 0306); `threat` lines (0294, 0295, 0302, #1258, #1290). **The schools** (Lotus, rounds 416, 417) are built (0296 to 0301); his spell rulings (0307) are `ready` as #1281 to #1286, burn stacks (0308), Lightning Rod (0309) and Sunder (0311) shipped; content waits on revised #1247. #1259 (0303): held bars kept on a sample; the Lazar House ships them (0305).

## Next

- 13.2 Commander's Word is spiked (#85, 0136; DESIGN 13.2): `order <press|rally|fall back>` once a map as the captain's action, radius `2 + Cha / 4`. Open behind `orders: on` and on campaign maps from the second; the client calls it from its action list (#786).
- 13.23 Kinsbane is spiked (#645, 0130; DESIGN 13.23): sample `docs/samples/the_gleaning_kinsbane.map` (Keziah). Code 645 warm 7/6/5. #804 slices 1, 2, #851, #856 (0195, 0207, 0209, 0211): teeth 2,4,5,8,12, `--kinsbane [--axe|--heeding]`, issued beside the axe; 279's bar passes. Slice 3 (0212): a woken kill's full Move again once a map (`the_gleaning_kinsbane_woken.map`). #865: `campaign ... --fed N`. #871 (0214, Lotus): the reach gate reverted; `keziah_warning: on` on Sallow alone (drains p50 2); `march` asks, `march sure` answers. The voice (0221): `kinsbaneSpoke`, 3 a battle (#1002); Code 804 warm 8/6/5. The choice screen (0222): `pitch` in `campaign.json`, each claimant's line under the offer until the pick; placeholder. What is left on #804 is art (the hound, the screen's staging), waiting on #535.
- #805 slices 1 to 3 (0213, 0215, 0216): the stages; the carry (`carry:`, Code 805 warm 7/6/7); the breath (`breath:`, `breathe`; Code 805 warm 6/7/8). #872 (0217): the Drover replaces the Scout (never doubles; Drake Bite 3/5; Long Carry; Deep Rime). #882 (0217 amended): `--drover`'s gate is a price ceiling, Grown median at least 70% of the Sky Captain where he leads, per phase and level; passes (0.72 to 0.74). Code 805 warm as a Drake Warden 7/6/6. Slice 4 (0218): a rider fallen for good takes the drake off the field (`drakeFlew` on the record; ending text waits on #634). #805 is closed (0252, Chat 8051). #1094 (0254): both on every campaign battle. #1100: Chat's cold chair is on the synthetic save; the pick joins trained (0263).
- #916 (0227): `region:` picks the ground; outland sand waits on Chat's look at `docs/look/sand-916.png`.
- #806 slice 1 (0219): Hask replaces the stand-in lord (same numbers); his card and the Warden's Lance name the pommel shard; units take `description` and `named`. Items 3, 5 wait on #634.
- #807 slice 1 (0220): a won campaign writes `ending.json` (versioned, PROTOCOL.md), `pending` until #634.
- #77 slice 8 (0245): `support <a> <b>` at a camp, lowest unseen tier once, free; #77 `blocked` on a chair's A read.
- #1003: card = text plus its rules line in parentheses (348 to 363); reads through 367 applied; Keziah's Oath Stone feeling waits for #634.
- #811 voice sheets, #1001 scene scripts (0244), #1005 scenes are in; then #634; #813 builds on 0243. `rejournal.py --apply` also rewrites stale unreplayed transcripts; keep only the failing ones.
- #611 (six arts); #535 slices 1, 2 (0114, 0115); clips await #621.
- #636 (0125, 0170): Full Measure costs the next phase, never doubles; the veto prices no arts (0251).
- Built: see `docs/DECISIONS/` (DESIGN 14, maps 1, 2, side maps, items, `--finale`, `--ladder`, unique classes; Field Surgeon, Drover unplayed by hand).
- The story gate is open (Lotus closed #656; build from STORY draft 6, built to change). #635 slices 4 to 11 (0196, 0198 to 0204) built the side maps in the Maps table (slice 11, Rook 1, opens the Drover) and three paid items (Psalter, Commonplace, Tally; the last two with 3 frozen iron). The campaign packs the Family Lance behind Teodor's iron and holds it at sound until The Old Watch is won (`held`, `wakes`, `--heirloom --quest`). Slice 12 (0205): Teodor 2, The Warden's Gate, names the lance and opens Turn the Key (the lock); no hand play declares it yet. Slices 13 to 16 (0206, 0208, 0224, 0225): Keziah 1 and 2, Rook 2. #635 is closed as built; #77 slices 1 to 7 are built (0183 to 0189): 26 pairs, C 16, B 28, A 48 (best, not sum); A by a committed human waits on a chair's campaign.
- #81 (0190, 0191, 0226): the field is map 9, level 4; `route_drift:` wakes the untaken route's group on turn 5. #936 (0233): `tuned`.
- #633 (0192 to 0194): `branch`, `pick`, `talk`, `meet`. #844: the Roster names a contested place.

## Maps

| Map | Status |
|---|---|
| the_mill | Map 2, Maud (0124, #632). Rout, limit 9 (0280), `protect: maud`, `holds: mill 0,0 11,2` (0279); the fort and Maud at 7,9, road pair from the east (0289). Gate 1 136/200, 19 timeouts. South: Code 632-south warm 8/7/6 (t8, two Recalls); Code 1710 warm 7/7/5 (t6; Maud safe west of the river from t2: rework). On 8,5 (`the_mill_0280.map`): Code 1500 6/7/6, 1510 8/7/6 warm; Chat 2250 cold 4/6/4. Next: a cold chair. |
| the_cold_kitchen | Side map, Bet's request (0146, #691; was the Postern, 0247), under `content/quests/`. Seize, limit 10, Recall 2, 4 deployed. Sim gate 1 75/200 (0292). Code 718 warm 8/7/7, won t5. Cold chair owed. |
| the_first_shrine | Side map, Maud's quest 2 (0198, #635), after map 5; pays the Psalter. Seize, limit 10, `brace: on`; starts south (0283); the sanctum archer wakes when the door soldier dies (0304); the altar is held through an enemy phase (0306, `seize_hold: 1`). Sim (one-shot, not a gate): Pell 53, Teodor 2. Before 0304: Code 1530, 1600; Chat 2240. Under 0304: Code 1600-woken 8/7/7 (lost); Chat 4426 cold 7/7/6 (Pell, t9); Code 1610 warm 7/6/3 (Teodor, t3, the door sprint). Under 0306: Code 1610-hold warm 7/7/6 (Teodor fell in the door; won as t4 began); 4426 replays won. Next: a cold chair (0306's kill criterion). |
| the_burned_school | Side map, Pell's quest 1 (0199, #635), after map 4; pays 2 common. Escape, limit 7, Recall 2; a shieldbearer corks the east gate, burners through the west from turn 1, three chests that turn you back. Sim gate 1 36/200. Code 884 warm 7/6/4 (t6, one chest), 1490 8/7/7 (t6; #1191). Cold chair owed. |
| the_undercroft | Side map, Pell's quest 2 (0200, #635), two maps after Pell 1; pays Pell's Commonplace and 3 frozen iron. Seize, limit 7, Recall 2; three routes past two sleeping groups, a lector corking the north door, sworn down the stair from turn 2. Sim 1/200 (0292). Code 960 warm 7/7/6 (L5, t6); 1620 warm 8/7/5 (Teodor, middle, t6; lector idle off the north: rework). Cold chair owed. |
| the_old_watch | Side map, Teodor's quest 1 (0201, #635), after map 5; wakes the Family Lance. Defeat Boss, limit 9, Recall 2; a sallying boss on a fort, a one-tile bridgehead. Sim gate 1 8/200. Code 961 warm 8/7/7, lost t9, boss at 2, Wren fell. Cold chair owed. |
| the_counting_house | Side map, Ottilie 1 (0202, #635); 2 common. Rout, limit 11 (#931, 0231), Recall 2; a canal, bridges 6,1 and 6,7; the archer a house guard at 11,2 (#925, 0228). Sim 0/200. Code 980 8/7/7, Chat cold 7/7/6, Code 925 7/6/6, Code 931 warm 8/7/6; Chat 9311 cold-ish 8/8/7, 11 stays. Code's fresh-seed read owed (0238). |
| the_long_count | Side map, Ottilie 2 (0203, #635); Ottilie's Tally, 3 frozen iron. Escape, limit 8, `dusk: 4`; a road bridge or a footbridge, pursuers from turn 2; the count on screen (0229); the archer on the road at 11,4 (#930, 0230). Sim 74/200. Code 91 7/7/6, Chat cold 7/8/6, Code 928 7/8/5; Code 930 warm 7/6/5, won t8; 1540 warm 8/6/6 (Pell), lost. Cold chair owed. |
| the_chapter_roll | Side map, Rook 1 (0204, #635); opens the Drover. Seize, limit 8; a gorge, a corked cell. Sim 0/200. Code 1100 warm 8/7/6, won t8. Cold chair owed. |
| the_wardens_gate | Side map, Teodor 2 (0205, #635); names the lance, 3 frozen iron. Defeat Boss, limit 10; the boss before the gate, a gap and a breach, a rider yard in his noise. Sim 0/200. Code 1110 warm 8/7/7, won t7. Cold chair owed. |
| the_burned_shrine | Side map, Keziah 1 (0206, #635); 2 common. Rout, limit 10; a ring wall, the hearth fort beside a held shieldbearer, a fight from it wakes the grove. Sim 0/200. Code 1113 warm 7/7/5, 1550 6/7/5 (Teodor), both won t8; the turn-5 brigand walks (#1234). A cold chair owed. |
| the_oath_stone | Side map, Keziah 2 (0224, 0225, #635); 2 common. Defeat Boss, limit 10 (#939, 0232); Joab corks the door, `freed` by the envoy's fall; the envoy keeps his fort. Chat 4410 warm 7/6/7 (lost t10; ~55% from t8). #940 (0239): rider on turn 5; Code 940 warm 8/7/6, won t10. Chat's read owed. |
| the_rookery | Side map, Rook 2 (0208, #635); 2 common, #805's key for Unbroken. Escape, limit 9; a ravine bridge, a woken loft, Rook leaves last. #862 (0208 addendum): sentry 13,3, archer 12,6, 14,5 free. Sim 36/200 (was 72), all captain alone. Code 862 warm 7/7/5, 1132-carry 8/7/6 (Grown, lost t8). Cold chair owed. |
| the_lazar_house | Side map, Maud's quest 1 (0127, #635). Survive, limit 5 (0305), Recall 2; held bars, `arrivals: wait` (0303), `east3` reaches both. Sim 154/200 (never bars). Old bars: Code 701, 670, 1590; Chat cold 4204 5/7/5. Sample: Code 1259 8/7/6, lost t6 (won t5 at limit 5); Chat 4311 cold 6/4/3, door line. Next: a cold chair with an ally who counters at 1. |
| starting_alone | Lesson, exempt from the Fun Gate (0123, #631). Captain alone, rout, limit 10. Gate 1 1/200. Code 631 warm 7/6/4, won turn 6. Chat's play owed. |
| the_tollgate | **tuned** (0073). Rider at 13,4 on a stop on 6,4 or 6,3 (0072). Four deployed, limit 10. Gate 1 84 percent (169/200, 0292), gate 4 ok at 0.300. Fun Gate: Code 211, Chat 227, 8/7/7 warm. |
| brackwater_cut | **tuned** (0078). Escape at `dusk: 5`, exit without a Move (0074), lamps on a player-phase wake (0076). Gate 1 65 percent (0173); campaign point 6 (0178), `carried` 83, gate 4 ok at 0.282 (#971). Fun Gate: Chat 271 8/7/7, Code 283 7/7/8, both warm; the Critic cold 509 7/6/7; Code warm, four, 8/7/7 to 8/8/8. |
| harrow_weir | **tuned** (0088, 0100). The crest file (#471 over #456), `turn_limit: 15` (0100). Guard Foreman with the Toll Axe under the boss veto, goes home when refused (0080), `wake_links: ford>weir`. Gate 1 68 percent (136/200, 0264), gate 4 ok at 0.215. Fun Gate: Code 481 7/8/7 (warm), Chat 487 7/8/7; the Critic 617 7/6/6. Code 1360 warm 7/7/8; Code 1410 warm 5/7/6, 1450 6/7/5 (the ford): the Foreman frozen from either bank (#1087). |
| the_field | **tuned on Keziah's pick** (0233); **Rook's pick tuned on hand plays, gate 1 short** (0246, 0250; tripwire: the owed cold chair). Map 9 (0190, 0191), level 4. Defeat Boss, limit 20; `route_drift` turn 5 (0226). Gate 1 69 percent (138/200, 0264), gate 4 ok; route claims come from hand plays (0248). Keziah: Chat cold 4071 7/7/7, Code warm 936 to 1460, four, 7/7/6 to 8/7/7. Rook (`seen_far: rook 2`, 0240; Sim 92/200, 0264): Chat 5150 7/7/7, Code warm 1340, 1390, 1440, 7/7/6 to 8/7/7. |
| old_mill_road | A fixture, out of the campaign (0124); gate 1 57/200. |
| saltmarsh_ford | Not tuned. Toll Axe boss (0029), ford forest (0030), spawn behind (0090), `brace: on` (0091), the north cut (0093). Gate 1 50/200, gate 4 fails at 0.075 (#971). On the cut: Code 547 7/7/6, Chat cold 571 7/7/5; Code 1560 warm 6/6/5, the pair late. The spawn lever #524 failed (0095). |
| sallow_grange | Not tuned. Seize, the Reeve a guard boss at 15,6 (0055), hexer at 13,7 (#275). `keziah_warning: on` (0214). Gate 1 81 percent (161/200), gate 4 ok at 0.330 (#971). Chat 7/7/6 (44); Code 61 6/6/5, 871 7/7/5, 1580 warm 7/7/5 (t8): two holders never act. |
| ironwake_raid | Not tuned (round 158). Map 6 (0060): rout, limit 7. Gate 1 94 percent. Code 288 6/6/6, Chat cold 301 7/7/5, 633 9/7/6 (L1), 1570 warm 6/6/4 (L4, t5; #1237). Never tuned for surprise. |
| ironwake_keep | Not tuned. The finale (0265, #1149): defeat boss, limit 12, fronts north/gate/south, the raid's wall broken. `--finale` L8 floor 0 (data). Levers 1 to 3 (0284 to 0287): 87/71, 87/70, 84/58. Chat's depleted pair 2410: sample 6/7/6 t11, keep 7/6/5 t12; the reach shipped (0293; Sim 84/58). Next: a fresh keep chair (is the bait a choice?). |

## Open experiments and the play that decides each

| Experiment | Where | Deciding play |
|---|---|---|
| 13.4 grudges | `docs/samples/old_mill_road_grudges.map` (0065, 0066) | Chat's seed 23 replay under #331 |
| 13.5 the keep | the campaign (0059, 0060); the raid is in from both chairs (round 158) | Chat's play of the camp |
| 13.20 the keep as a home | DESIGN 13.20 (0137), built (#687, 0138; 12 beds until the levy roster; the forge #647, 0139; the barracks #690, 0145) | a campaign play from each chair (0137), once a meeting can be refused |
| 13.6 trials | `content/trials/` (0057) | Chat's cold Outrider trial (closes #73) |
| 13.7 dusk, `threat` row | Brackwater Cut, #403 (built) | a cold dusk play of Brackwater on main |
| 13.8 carrier arm | `*_keepsakes.map` | Chat's cold carrier-arm plays |
| 13.10 retreat, third pass | `river_refuge_hold.map` (0037) | Chat's cold play |
| 13.11 two-weapon boss | kept (0061); the kill clause cannot fire after seed 521's three bait turns picked the leader's axe (round 121) | none waits |
| 13.13 pincer | kept on its samples (0082, 0083); ships only where the enemy can anvil too | none until a keep round names a board |
| 13.15 wildfire | kept whole on its samples (0085, 0110) | none; keep rounds closed |
| 13.21 tide | `ebb_ford_tide.map` (0111; Code 653 in) | Chat's cold play |
| 13.23 Kinsbane | `docs/samples/the_gleaning_kinsbane.map` (#645, 0130; Code 645 warm in: both keep clauses shown) | the read passes (0211); Chat's cold field; the hunt (0212): a cold play of `the_gleaning_kinsbane_woken.map` |
| 13.2 Commander's Word | `docs/samples/harrow_weir_orders.map` (#85, 0136; Code 85 warm in: moved for the order, the cost did not bite) | Chat's cold play |
| 13.24 messenger | `docs/samples/signal_road_pass.map` (#680; 0135 amended; Code 680 warm in) | Chat's cold play of the pass |
| 13.22 break | `docs/samples/saltmarsh_ford_break.map` (#606, 0112; Code 661, 667 in, nothing broke) | Chat's cold play on Saltmarsh |
| 13.18 signatures | `docs/samples/saltmarsh_ford_brace_signatures.map` (#486, 0097; Code 563, Chat cold 113 and 587 in); Wren's talk kept on `saltmarsh_ford_talk.map` (#1097, 0256, 0258; Chat 5150) | the ledger killed (0197), its next shape (263) unbuilt; the talk: a campaign journal naming a costly talk-held swing |
| 13.25 rotten planks | `rotten_bridge_planks.map` (0179; Code 251, Chat 4 7/7/8) and `rotten_bridge_straggler.map` (#783; Code 783 warm, no cut on offer) | Chat's cold play of the straggler |
| 13.26 rockfall | `scree_gorge_rockfall.map` (0182; Code 811 in) | Chat's cold play |
| #872 the Drover | the rime sample with Rook a Drover (0217; Code 805 warm: Deep Rime decided) | a campaign chair journals her; Chat's cold pick, her or the Captain (round 294) |
| 13.27 dash | kept on `docs/samples/brackwater_cut_dash.map` (0255; Code 950, Chat 2706) | none; borrowed step only if a free dash beats a `tuned` clock |
| 13.28 wind | kept on `docs/samples/sallow_grange_wind.map` (0256; Code 1280, Chat 3117) | none; a campaign `wind:` map puts the downwind side on the short road |
| 13.29 one answer | kept on `docs/samples/the_tollgate_answer.map` (0257; Code 1290, Chat 4417) | none; a campaign `one_answer:` map fields a 1-2 answerer at a choke |
| 13.14 brace | kept (0084), shipped on Saltmarsh (0091) | holds read on the next brace play with a hold |

Samples: rivalry, shove, Seize drift. Kept on samples: #1259 held bars (0303; 1259, 4311 in). Killed: Recall scars, battalions, windup, overwatch, cover.

## Standing notes

- Repo is public (0006); `main` is protected by `ci`; PRs auto-merge on green. Routine pushes use `issue/<n>-<slug>`, falling back to `claude/`.
- Merge skew (#1291, 0310): STATE.md and DIALOGUE.md stay under 19 KB on a branch (20 KB is checked only on a push to main); a new record takes its number from `python3 tools/next_decision.py` after the rebase; numbers are unique but for the old 0256 and 0264 pairs.
- Builder race guards (ROUTINES.md 2, #735, 0169) bind every Builder: with the chain on, a cron slot defers before any Builder work, an issue, a spike or a warm play (rule 1, #1069); every Builder claims (on the issue, or the Table for empty-queue work), waits 30 s, re-reads; the earlier claim wins (rule 2); a rotation re-checks first (rule 3). Pending: Lotus pastes section 2's prompt.
- Routines (ROUTINES.md): Builder slots 02:00, 03:00, 05:00 NY, and the chain (one issue per merged Builder PR while `IRONWAKE_CHAIN` is `on`; 0050 amended, section 6); the Critic on Lotus's schedule, its `critic` issues the record; the Partner woken by Table comments.
- `ci` parses `.github/workflows/*.yml` before the build (#970).
- A PR changing the Sim's player (`Players.cs`, planner, `EnemyAi`) re-runs `--full --all` and rewrites the Maps cells it moves (#971).
- Full-campaign parity: `tests/parity/campaign/full-campaign-644.script` (0248; in ci.yml), `--variant 44 --quest pell_1` (#1174, #1210, #1206).
- Sim: `--smoke` (CI); `--heirloom`, `--kinsbane`, `--levels [...]`, `--supports`, `--ladder`, `--finale <map> [--level N] [--gates]`; `--full <map>|<file>|--all [--seeds N] [--scheme one|two] [--taxfloor F] [--difficulty D]` (gates 1 to 8, not in CI; Release ~20 s a map); `--trace <map> <seed>`; `--keep`; `--hitband`. Reads: `docs/measurements/`.
- Content: `ContentLoader`; `units/cast.json`, captain first; terrain `cost` `null` is impassable.
- Map files: DESIGN 10, 0011; hand-edited maps canonical. Coordinates `x,y` from the top-left, 0-based; CLI slots from 1.
- CLI and Sim tests read the console via `ConsoleCapture.Run`, `[Collection("console")]`; invariant globalization.
