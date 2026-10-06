# STATE

Updated: 2026-10-06 (round 410; Table #1218). Under 20 KB (#401); history in git, `docs/DECISIONS/`.

## Where we are

Phase 3 is under way; `tools/rejournal.py` regenerates transcripts (0243). The Table is #1218 (#1187 archived). Casting (`docs/look/`) waits on Lotus. Pillar 5: eleven and the captain (0264). #1002: Kinsbane's barks, cold read pending. #1003: Bet's cards next. #1042 (0247): Bet the cook, the Cold Kitchen, Banked. #804, #806, #807 and #872 are `blocked` on plays, #634 or #535; the dash (0255), the wind (0256) and the one answer (0257) are kept on their samples.
Four maps are `tuned`: the Tollgate (0073), Brackwater Cut at dusk (0078), Harrow Weir (0088, 0100) and the field, Keziah's pick (0233) and Rook's on hand plays with gate 1 short at 86/200 (0250, DESIGN 11's pick-keyed clause). A cold chair on Rook's arm is owed (its tripwire). Maps 1 and 2 and the rest wait on plays (Maps).
~4750 tests green; `ci`, `ci-windows` run.
No forks are open. The chain heartbeat stays (#406). 0260, #1120: `confirm-lethal` (default on) sets `end`'s lethal ask, never on Tactician; the wind waits on Lotus. Wren's talk kept on `saltmarsh_ford_talk.map` (0258, Chat 5150). #1126: the Drake Warden (id `drover`; never a bare "Warden"). #1127 (0262): Rook's frost, Stoop for the rest; the keep play owed. #1130 (0263): the pick joins at L4 (median floor), lance D 30; a Rook-forward chair to a door owed. #1138 (0264): a guard boss on his post with no strike holds it. Levels: levy floor offset 3; level-7 doors ask 50 points. #1149 (0265): the campaign keep is the #692 finale (three fronts, falls waves, the hunt, Hask on turn 6, limit 12). #1154 (0266): keep work off the menu is refunded on read. The door (0268 to 0277): the drill keeps EXP (#1184, 0276); Chat's cold chair (3971, 0277) fed Teodor to L7 after the raid: the bar is closed, the Sim's chairs are lower bounds. #1189: `holds:` (0279), limit 9 (0280); the Mill's cold chair in (409); its lever is #1210 (#1212 a duplicate). #1191 (0281): the freed tile is priced one wave deep; `end` asks. #1200 (0282): a spell's counter prints its uses left. #1198 (0283): the Shrine starts south; its Sim waits on #1206. #1204 lever 1 (0284): an empty front lets the hunter through; lever 2 (0285): inside spawns just inside their fronts, a `falls:` Sim line; lever 3 (0286): Hask held, a sample; a depleted hand chair decides (410, #1217). Next: the `comes through it` test (#1211), #1206, #1208, #1210, #1216 (the `holds:` line), #1217, then the fresh keep chair. Bell parked (0237). Empty-queue runs play the weakest untuned map warm (0278).

## Next

- 13.2 Commander's Word is spiked (#85, 0136; DESIGN 13.2): `order <press|rally|fall back>` once a map as the captain's action, radius `2 + Cha / 4`; `fallback <unit> <x,y|stay>`; `order <kind> preview [from <x,y>]`. Open behind `orders: on` and on campaign maps from the second; the client calls it from its action list (#786).
- 13.23 Kinsbane is spiked (#645, 0130; DESIGN 13.23): sample `docs/samples/the_gleaning_kinsbane.map` (Keziah). Code 645 warm 7/6/5. #804 slices 1, 2, #851, #856 (0195, 0207, 0209, 0211): teeth 2,4,5,8,12, `--kinsbane [--axe|--heeding]`, issued beside the axe; 279's bar passes. Levers stop. Slice 3 (0212): the hunt runs on, a woken kill's full Move again once a map; `woken: keziah` on `the_gleaning_kinsbane_woken.map`. #865: `campaign ... --fed N`. #871 (0214, Lotus): the reach gate reverted; `keziah_warning: on` on Sallow alone (drains p50 2); `march` asks, `march sure` answers. The voice (0221): `kinsbaneSpoke` (a starving drain, a tooth, the waking), 3 a battle; written under its sheet (#1002); Code 804 warm 8/6/5. The choice screen (0222): `pitch` in `campaign.json`, each claimant's line under the offer until the pick; placeholder. What is left on #804 is art (the hound, the screen's staging), waiting on #535.
- #805 slices 1 to 3 (0213, 0215, 0216): the stages; the carry (`carry:`, Code 805 warm 7/6/7); the breath (`breath:`, `breathe`; Code 805 warm 6/7/8). #872 (0217): the Drover replaces the Scout (never doubles; Drake Bite 3/5; Long Carry; Deep Rime). #882 (0217 amended): `--drover`'s gate is a price ceiling, Grown median at least 70% of the Sky Captain where he leads, per phase and level; passes (0.72 to 0.74). Code 805 warm as a Drake Warden 7/6/6. Slice 4 (0218): a rider fallen for good takes the drake off the field (`drakeFlew` on the record; ending text waits on #634). #805 is closed (0252, Chat 8051). #1094 (0254): both on every campaign battle. #1100: Chat's cold chair is on the synthetic save; the pick joins trained (0263).
- #916 (0227): `region:` picks the ground; outland sand waits on Chat's look at `docs/look/sand-916.png`.
- #806 slice 1 (0219): Hask replaces the stand-in lord (same numbers); his card and the Warden's Lance name the pommel shard; units take `description` and `named`. Items 3, 5 wait on #634.
- #807 slice 1 (0220): a won campaign writes `ending.json` (versioned, PROTOCOL.md), `pending` until #634.
- #77 slice 8 (0245): `support <a> <b>` at a camp, lowest unseen tier once, free; #77 `blocked` on a chair's A read.
- #1003: card = text plus its rules line in parentheses (348 to 363); reads through 367 applied; Keziah's Oath Stone feeling waits for #634.
- #811: WRITING.md and the sixteen voice sheets are in. #1001: scene scripts (0244), main-line maps. #1005: all four scenes are in (rounds 373, 374), their cards gone. Then scenes (#634); #813 builds on 0243. `rejournal.py --apply` also rewrites stale unreplayed transcripts; keep only the failing ones.
- Blocked on plays: #13, #160, #78 to #80.
- 13.18 (#486): signatures behind `signatures: on`; ledger killed (0197).
- #131: the north cut (0093); #524 failed (0095).
- #611 built (six arts). #535 slices 1, 2 built (0114, 0115); clips await #621.
- #636 (0125, 0170): Full Measure costs the next phase, never doubles; the veto prices no arts (0251).
- Built (`docs/DECISIONS/`): DESIGN 14; maps 1, 2 (#631, #632); side maps, items (#635); lance to barracks; Bet's quest (#691); `--finale` (#692); forms, curve (#704); `--ladder` (#705, #758); unique classes (#706; Field Surgeon, Drover unplayed by hand); `freed:` (#750); `joins` (#763). Durability killed. Opening (#772, 0177).
- The story gate is open (Lotus closed #656; build from STORY draft 6, built to change). #635 slices 4 to 11 (0196, 0198 to 0204) built the side maps in the Maps table (slice 11, Rook 1, opens the Drover) and three paid items (Psalter, Commonplace, Tally; the last two with 3 frozen iron). The campaign packs the Family Lance behind Teodor's iron and holds it at sound until The Old Watch is won (`held`, `wakes`, `--heirloom --quest`). Slice 12 (0205): Teodor 2, The Warden's Gate, names the lance and opens Turn the Key (the lock); no hand play declares it yet. Slices 13 to 16 (0206, 0208, 0224, 0225): Keziah 1 and 2, Rook 2. #635 is closed as built; #77 slices 1 to 7 are built (0183 to 0189): 26 pairs, C 16, B 28, A 48 (best, not sum); A by a committed human waits on a chair's campaign.
- #81 (0190, 0191, 0226): the field is map 9, level 4; `route_drift:` wakes the untaken route's group on turn 5. #936 (0233): `tuned`.
- #633 slices 1 to 3 (0192 to 0194): `branch`, `pick`, `talk`, `meet`; #633 can close. #844: the Roster names a contested place.

## Maps

| Map | Status |
|---|---|
| the_mill | Campaign map 2, Maud's arrival (0124, #632). Rout, limit 9 (0280), `protect: maud`, `holds: mill 0,0 11,2` (0279). Gate 1 123/200 (8 fails at 118). Pre-`holds:` Code 632, 1480, Chat 2061 warm; Code 1500 6/7/6, 1510 8/7/6 warm; Chat 2250 cold 4/6/4, map 5/6/4. Next: #1210, the fort south. |
| the_cold_kitchen | Side map, Bet's request (0146, #691; was the Postern, 0247), under `content/quests/`. Seize, limit 10, Recall 2, 4 deployed. Sim gate 1 74/200. Code 718 warm 8/7/7, won turn 5. A cold chair owed. |
| the_first_shrine | Side map, Maud's quest 2 (0198, #635), after map 5; pays the Psalter. Seize, limit 10, `brace: on`; starts south (0283). Sim waits on #1206. North start: Code 875 5/6/5, 1520 2/4/6; Chat 2130 cold, map 2/4/5. South: Code 1530 warm 8/7/6 (Wren, t10 of 10, two Recalls); Chat 2240 cold 8/7/6, map 7/7/5 (Teodor, t4, two Recalls). Both in; not tuned (surprise). Next: #1206's Sim number. |
| the_burned_school | Side map, Pell's quest 1 (0199, #635), after map 4; pays 2 common. Escape, limit 7, Recall 2; a shieldbearer corks the east gate, burners through the west from turn 1, three chests that turn you back. Sim gate 1 36/200. Code 884 warm 7/6/4, won t6, one chest; 1490 warm 8/7/7 (t6; #1191). A cold chair owed. |
| the_undercroft | Side map, Pell's quest 2 (0200, #635), two maps after Pell 1; pays Pell's Commonplace and 3 frozen iron. Seize, limit 7, Recall 2; three routes past two sleeping groups, a lector corking the north door, sworn down the stair from turn 2. Sim gate 1 2/200. Code 960 warm 7/7/6 at level 5, won turn 6. A cold chair owed. |
| the_old_watch | Side map, Teodor's quest 1 (0201, #635), after map 5; wakes the Family Lance. Defeat Boss, limit 9, Recall 2; a sallying boss on a fort, a one-tile bridgehead. Sim gate 1 8/200. Code 961 warm 8/7/7, lost turn 9 with the boss at 2, Wren fell. A cold chair owed. |
| the_counting_house | Side map, Ottilie 1 (0202, #635); 2 common. Rout, limit 11 (#931, 0231), Recall 2; a canal, bridges 6,1 and 6,7; the archer a house guard at 11,2 (#925, 0228). Sim 0/200. Code 980 8/7/7, Chat cold 7/7/6, Code 925 7/6/6, Code 931 warm 8/7/6; Chat 9311 cold-ish 8/8/7, 11 stays. Code's fresh-seed read owed (0238). |
| the_long_count | Side map, Ottilie 2 (0203, #635); Ottilie's Tally, 3 frozen iron. Escape, limit 8, `dusk: 4`; a road bridge or a footbridge, pursuers from turn 2; the count on screen (0229); the archer on the road at 11,4 (#930, 0230). Sim 74/200. Code 91 7/7/6, Chat cold 7/8/6, Code 928 7/8/5; Code 930 warm 7/6/5, won turn 8. A cold chair on the road archer owed. |
| the_chapter_roll | Side map, Rook 1 (0204, #635); opens the Drover. Seize, limit 8; a gorge, a corked cell. Sim 0/200. Code 1100 warm 8/7/6, won turn 8; the sentry never acted. A cold chair owed. |
| the_wardens_gate | Side map, Teodor 2 (0205, #635); names the lance, 3 frozen iron. Defeat Boss, limit 10; the boss before the gate, a gap and a breach, a rider yard in his noise. Sim 0/200. Code 1110 warm 8/7/7, won turn 7; archer-2 never acted. A cold chair owed. |
| the_burned_shrine | Side map, Keziah 1 (0206, #635); 2 common. Rout, limit 10; a ring wall, the hearth fort beside a held shieldbearer, a fight from it wakes the grove. Sim 0/200. Code 1113 warm 7/7/5, won turn 8; the turn-5 brigand is a walk. A cold chair owed. |
| the_oath_stone | Side map, Keziah 2 (0224, 0225, #635); 2 common. Defeat Boss, limit 10 (#939, 0232); Joab corks the door, `freed` by the envoy's fall; the envoy keeps his fort. Chat 4410 warm 7/6/7 (lost t10; ~55% from t8). #940 (0239): rider on turn 5; Code 940 warm 8/7/6, won t10. Chat's read owed. |
| the_rookery | Side map, Rook 2 (0208, #635); 2 common, #805's key for Unbroken. Escape, limit 9; a ravine bridge, a woken loft, Rook leaves last. #862 (0208 addendum): sentry 13,3, archer 12,6, 14,5 free. Sim 36/200 (was 72), all captain alone. Code 862 warm 7/7/5. Code 1132-carry warm 8/7/6 (Grown, #1094), lost t8; its Wing Captain stoops. A cold chair owed. |
| the_lazar_house | Side map, Maud's quest 1 (0127, #635). Survive, limit 6, Recall 2; lanes the ally can bar. Sim 4/200. Code 701 warm 8/7/5, Wren fell. A cold chair owed. |
| starting_alone | Lesson, exempt from the Fun Gate (0123, #631). Captain alone, rout, limit 10. Gate 1 1/200. Code 631 warm 7/6/4, won turn 6. Chat's play owed. |
| the_tollgate | **tuned** (0073). Rider at 13,4 on a stop on 6,4 or 6,3 (0072). Four deployed, limit 10. Gate 1 83 percent (167/200, 0248), gate 4 ok at 0.220. Fun Gate: Code 211 8/7/7, Chat 227 8/7/7, both warm. Code warm 1320, 1380, 1430. |
| brackwater_cut | **tuned** (0078). Escape at `dusk: 5`, exit without a Move (0074), lamps on a player-phase wake (0076). Gate 1 65 percent (0173); campaign point 6 (0178), `carried` 83, gate 4 ok at 0.282 (#971). Fun Gate: Chat 271 8/7/7, Code 283 7/7/8, both warm; the Critic cold 509 7/6/7; Code warm: 1310 8/7/7, 1370 8/8/7, 1420 8/7/7, 1470 8/8/8. |
| harrow_weir | **tuned** (0088, 0100). The crest file (#471 over #456), `turn_limit: 15` (0100). Guard Foreman with the Toll Axe under the boss veto, goes home when refused (0080), `wake_links: ford>weir`. Gate 1 68 percent (136/200, 0264), gate 4 ok at 0.215. Fun Gate: Code 481 7/8/7 (warm), Chat 487 7/8/7; the Critic 617 7/6/6. Code 1360 warm 7/7/8; Code 1410 warm 5/7/6, 1450 6/7/5 (the ford): the Foreman frozen from either bank (#1087). |
| the_field | **tuned on Keziah's pick** (0233); **Rook's pick tuned on hand plays, gate 1 short** (0246, 0250; tripwire: the owed cold chair). Map 9 (0190, 0191), level 4. Defeat Boss, limit 20; `route_drift` turn 5 (0226). Gate 1 69 percent (138/200, 0264), gate 4 ok; route claims come from hand plays (0248). Keziah: Chat cold 4071 7/7/7, Code warm 936 7/7/7, 1350 8/7/7, 1400 8/7/6, 1460 7/8/6. Rook (`seen_far: rook 2`, 0240; Sim 92/200 under 0264; the turn-3 south wake 89, not shipped, 0249): Chat 5150 7/7/7, Code warm 1340 8/7/7, 1390 7/7/6, 1440 7/7/6. |
| old_mill_road | A fixture, out of the campaign (0124); gate 1 57/200 (0248). |
| saltmarsh_ford | Not tuned. Toll Axe boss (0029), ford forest (0030), spawn behind (0090), `brace: on` (0091), the north cut (0093). Gate 1 25 percent (50/200), gate 4 fails at 0.075 (#971). On the cut: Code 547 7/7/6, Chat cold 571 7/7/5. The spawn lever (#524) failed gate 1's floor (0095). |
| sallow_grange | Not tuned. Seize, the Reeve a guard boss at 15,6 (0055), hexer at 13,7 (#275). `keziah_warning: on` (0214). Gate 1 81 percent (161/200), gate 4 ok at 0.330 (#971). Last: Chat 7/7/6 (44), Code 6/6/5 (61), Code 871 7/7/5. |
| ironwake_raid | Not tuned (round 158). Campaign map 5 (0060): rout, limit 7. Gate 1 94 percent. Code 288 6/6/6, Chat cold 301 7/7/5; never tuned for surprise. |
| ironwake_keep | Not tuned. The finale (0265, #1149): defeat boss, limit 12, fronts north/gate/south, the raid's wall broken. `--finale` L8 floor 0 (data). Code 1149 warm 9/7/7, abandoned t6; Chat 2210 cold 7/8/6, map 7/7/5, won t11 (0148; replays to t4). Lever 1 (0284) 87/71; lever 2 (0285) 87/70, 2210's replayed gate fall blocked; lever 3 (0286) 84/58 on `ironwake_keep_hask_holds.map`, kept off the keep. Next: a fresh chair on the keep, the Hask sample beside it (all gate falls blocked: 408's delayed spawn to the Table). |

## Open experiments and the play that decides each

| Experiment | Where | Deciding play |
|---|---|---|
| 13.4 grudges | `docs/samples/old_mill_road_grudges.map` (0065, 0066) | Chat's seed 23 replay under #331 |
| 13.5 the keep | the campaign (0059, 0060); the raid is in from both chairs (round 158) | Chat's play of the camp |
| 13.20 the keep as a home | DESIGN 13.20 (0137), built (#687, 0138; 12 beds until the levy roster; the forge #647, 0139; the barracks #690, 0145) | a campaign play from each chair (0137), once a meeting can be refused |
| 13.6 trials | `content/trials/` (0057) | Chat's cold play of the rebuilt Outrider trial (closes #73) |
| 13.7 dusk, `threat` row | Brackwater Cut, #403 (built) | a cold dusk play of Brackwater on main |
| 13.8 carrier arm | `docs/samples/*_keepsakes.map` | Chat's cold carrier-arm plays |
| 13.10 retreat, third pass | `docs/samples/river_refuge_hold.map` (0037) | Chat's cold play |
| 13.11 two-weapon boss | kept (0061); the kill clause cannot fire after seed 521's three bait turns picked the leader's axe (round 121) | none waits |
| 13.13 pincer | kept on its samples (0082, 0083; Code 443, 503; Chat cold 499, 523); ships only where the enemy can anvil too, no board named | none waits; a keep round names a ship board when a map authored for it exists |
| 13.15 wildfire | kept whole on its samples (0085, 0110; Code 457, 631; Chat cold 559, 641) | none; keep rounds closed |
| 13.21 tide | `docs/samples/ebb_ford_tide.map` (0111; Code 653 in) | Chat's cold play |
| 13.23 Kinsbane | `docs/samples/the_gleaning_kinsbane.map` (#645, 0130; Code 645 warm in: both keep clauses shown) | the read passes (0211); Chat's cold field; the hunt (0212): a cold play of `the_gleaning_kinsbane_woken.map` |
| 13.2 Commander's Word | `docs/samples/harrow_weir_orders.map` (#85, 0136; Code 85 warm in: moved for the order, the cost did not bite) | Chat's cold play |
| 13.24 messenger | `docs/samples/signal_road_pass.map` (#680; 0135 amended; Code 680 warm in: a unit held its path, the runner was never struck) | Chat's cold play of the pass |
| 13.22 break | `docs/samples/saltmarsh_ford_break.map` (#606, 0112; Code 661 on the Tollgate and 667 here in, nothing broke on either) | Chat's cold play on Saltmarsh |
| 13.18 signatures | `docs/samples/saltmarsh_ford_brace_signatures.map` (#486, 0097; Code 563, Chat cold 113 and 587 in); Wren's talk kept on `saltmarsh_ford_talk.map` (#1097, 0256, 0258; Chat 5150) | the ledger killed at 65 (0197, Chat cold 831), its next shape (round 263) unbuilt; the talk: a campaign journal naming a costly talk-held swing, else a quest-1 softening |
| 13.25 rotten planks | `rotten_bridge_planks.map` (0179; Code 251, Chat 4 7/7/8) and `rotten_bridge_straggler.map` (#783; Code 783 warm, no cut on offer) | Chat's cold play of the straggler |
| 13.26 rockfall | `docs/samples/scree_gorge_rockfall.map` (0182; Code 811 warm 8/7/7) | Chat's cold play |
| #872 the Drover | the rime sample with Rook a Drover (0217; Code 805 warm: Deep Rime decided) | a campaign chair journals her; Chat's cold pick, her or the Captain (round 294) |
| 13.27 dash | kept on `docs/samples/brackwater_cut_dash.map` (0255; Code 950, Chat 2706) | none; borrowed step only if a free dash beats a `tuned` clock |
| 13.28 wind | kept on `docs/samples/sallow_grange_wind.map` (0256; Code 1280, Chat 3117) | none; a campaign `wind:` map puts the downwind side on the short road |
| 13.29 one answer | kept on `docs/samples/the_tollgate_answer.map` (0257; Code 1290, Chat 4417) | none; a campaign `one_answer:` map fields a 1-2 answerer at a choke |
| 13.14 brace | kept (0084), shipped on Saltmarsh (0091) | holds read on the next brace play with a hold |

Kept on samples: rivalry, shove, Seize drift. Killed: Recall scars, battalions, windup, overwatch, cover.

## Standing notes

- Repo is public (0006); `main` is protected by the `ci` check and PRs auto-merge on green. Routine pushes use `issue/<n>-<slug>`, falling back to `claude/`.
- Builder race guards (ROUTINES.md section 2, #735, 0169), bind every Builder that reads this file: with the chain on, a cron slot defers before any Builder work, an issue, a spike or a warm play (rule 1, #1069); every Builder claims (on the issue, or the Table for empty-queue work), waits 30 s, re-reads; the earlier claim wins (rule 2); a rotation re-checks first (rule 3). Pending: Lotus pastes section 2's prompt once.
- Routines (ROUTINES.md): Builder slots 02:00, 03:00, 05:00 NY, and the chain (one issue per merged Builder PR while `IRONWAKE_CHAIN` is `on`, 20-minute heartbeat; 0050 amended, section 6); the Critic on Lotus's schedule, its `critic` issues the record; the Partner woken by Table comments.
- `ci` parses `.github/workflows/*.yml` before the build (#970).
- A PR changing the Sim's player (`Players.cs`, its planner, `EnemyAi` scoring) re-runs `--full --all` and rewrites the Maps cells it moves (#971).
- Full-campaign parity: `tests/parity/campaign/full-campaign-644.script` (0248; in ci.yml), `--variant 20 --quest pell_1` (#1174).
- Sim: `--smoke` (CI, gates 5 to 8); `--heirloom`, `--kinsbane`, `--levels [...]`, `--supports`, `--ladder`, `--finale <map> [--level N] [--gates]`; `--full <map>|<file>|--all [--seeds N] [--scheme one|two] [--taxfloor F] [--difficulty D]` (gates 1 to 8, not in CI; Release ~20 s a map); `--trace <map> <seed>`; `--keep`; `--hitband`. Reads: `docs/measurements/`.
- Content shapes: `ContentLoader`; `units/cast.json` the roster, captain first; `rules.json` the map-free constants; terrain `cost` `null` is impassable.
- Map files: DESIGN 10, 0011; hand-edited maps canonical. Coordinates `x,y` from the top-left, 0-based; CLI slots from 1.
- CLI and Sim tests read the console via `ConsoleCapture.Run`, `[Collection("console")]`; invariant globalization.
