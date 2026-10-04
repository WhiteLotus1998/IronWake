# STATE

Updated: 2026-10-04. Rewritten, not appended; under 20 KB (#401); history is in git, PRs and `docs/DECISIONS/`.

## Where we are

Phases 1 and 2 (DESIGN 12) are built; Phase 3 is under way. The showcase (#509, 0092; slices #510 to #516) is built and closed at 8/7 from both chairs (round 169); per-slice scores are in the PRs and DIALOGUE.
Three maps are `tuned`: the Tollgate (0073), Brackwater Cut at dusk (0078) and Harrow Weir (0088, 0100). Starting Alone (#631, 0123) is campaign map 1, a lesson exempt from the Fun Gate; The Mill (#632, 0124) is map 2, where Maud arrives. Others wait on plays (Maps).
4255 tests green; `ci` and `ci-windows` run; the `godot-*` checks run, not required.
No forks are open. The builder-chain heartbeat stays (Lotus's ruling on #406, 2026-09-27).

## Next

- 13.2 Commander's Word is spiked (#85, 0136; DESIGN 13.2): `order <press|rally|fall back>` once a map as the captain's action, radius `2 + Cha / 4`; `fallback <unit> <x,y|stay>`; `order <kind> preview [from <x,y>]`. Open behind `orders: on` and on campaign maps from the second; the client calls it from its action list (#786). Code 85 warm 6/7/5 on `harrow_weir_orders.map`: the cost did not bite.
- 13.23 Kinsbane is spiked (#645, 0130; DESIGN 13.23): sample `docs/samples/the_gleaning_kinsbane.map` (Keziah). Code 645 warm 7/6/5. #804 slices 1, 2, #851, #856 (0195, 0207, 0209, 0211): teeth 2,4,5,8,12, `--kinsbane [--axe|--heeding]`, issued beside the axe; 279's bar passes. Levers stop. Slice 3 (0212): the hunt runs on, a woken kill's full Move again once a map; `woken: keziah` on `the_gleaning_kinsbane_woken.map`. Code 804 8/7/6, woken 7/7/5; Chat cold 858 8/7/5. #865: `campaign ... --fed N` (Chat's field cold at 10 is open). #871 (0214, Lotus): the reach gate reverted; `keziah_warning: on` on Sallow alone (drains p50 2); `march` asks, `march sure` answers. The voice (0221): `kinsbaneSpoke` (a starving drain, a tooth, the waking), 3 a battle; placeholder; Code 804 warm 8/6/5. The choice screen (0222): `pitch` in `campaign.json`, each claimant's line under the offer until the pick; placeholder. What is left on #804 is art (the hound, the screen's staging), waiting on #535.
- #805 slices 1 to 3 (0213, 0215, 0216): the stages; the carry (`carry:`, Code 805 warm 7/6/7); the breath (`breath:`, `breathe`; Code 805 warm 6/7/8). #872 (0217): the Drover replaces the Scout (never doubles; Drake Bite 3/5; Long Carry; Deep Rime). #882 (0217 amended): `--drover`'s gate is a price ceiling, Grown median at least 70% of the Sky Captain where he leads, per phase and level; passes (0.72 to 0.74). Code 805 warm as a Drover 7/6/6. Slice 4 (0218): a rider fallen for good takes the drake off the field (`drakeFlew` on the record; ending text waits on #634). #805's items are built; it closes on the carry and breath keep rounds.
- #928 (0229): Escape maps print the count (`count:` under the exits: turns to stand on an exit, last start), `end` warns with `Count:` when a phase's end passes a unit's last start, the captain's `exit` names who it leaves first; protocol `count` query and `countPassed`. The Long Count save records Rook 1 won, so both 91 scripts replay on main. #930: the Long Count archer to 11,4 (round 313).
- #891 (0223): ART_SPEC's Figures; delivered figures keep their colour; #896 tints only `generated.txt` sheets.
- #916 (0227): `region:` picks the ground (frost seam, moss elsewhere, peat out); outland sand, ink-edged, kept once Chat looks at `docs/look/sand-916.png`.
- #806 slice 1 (0219): Hask replaces the stand-in lord (same numbers); his card and the Warden's Lance name the pommel shard; units take `description` and `named`. Items 3, 5 wait on #634.
- #807 slice 1 (0220): a won campaign writes `ending.json` with a versioned `ending` block for a sequel (PROTOCOL.md), `pending` until #634 (slice 2).
- #811: `docs/WRITING.md` is in (round 295). Voice sheets (#906, `docs/voices/`; Chat cold-reads): Alder (#913), Ottilie, Pell, Teodor (#914), Wren, Brannock, Dunstan, Ansgar (#915). Chat's (#908 to #912) land when drafted; then beats, scenes (#634).
- 13.18 (#486): signatures behind `signatures: on`; the ledger killed at 65 (0197), next shape a lean; the other eight wait for boards (`cast_audit.md`).
- Standing rule (0099): a spike adding a player action names its cost; its keep round shows it biting.
- #131's north cut is built (0093; Saltmarsh in Maps): the map plays as the pair, then the boss. The lever #524 failed its floor (0095); the next lever has to buy gate 1 back first. Open: turn 1 is a march.
- #611 is built: six arts, the attack menu; Chat's Tollgate play with it closes #611. #535 slices 1 and 2 are built (0114, 0115); clips await #621.
- #636 (0125, 0170): Full Measure costs the captain his next phase and never doubles.
- Built, records in `docs/DECISIONS/`: the story's DESIGN 14 (#630, #644, 0121, 0126); map 1 and Maud's arrival (#631, #632); side maps, bound items, `pays` (#635); lance, chests, rooms, forge, wagon, barracks (0131 to 0145); Bet's Postern and the Sergeant (#691, 0146); the finale, `--finale` (#692); advanced forms and the curve (#704; `--curve`, `--carry`, `--items`); the captain's ladder, `--ladder` (#705, #758); unique classes (#706; no hand play of the Field Surgeon or the Drover yet); `freed:` (#750); `joins` (#763). Durability killed. Opening (#772, 0177): a Vanguard-captain Saltmarsh from Chat's chair decides it. #648's Word variant can be built on #85.
- The story gate is open (Lotus closed #656; build from STORY draft 6, built to change). #635 slices 4 to 11 (0196, 0198 to 0204) built the side maps in the Maps table (slice 11, Rook 1, opens the Drover) and three paid items (Maud's Psalter, Pell's Commonplace, Ottilie's Tally; the last two with 3 frozen iron). The campaign packs the Family Lance behind Teodor's iron and holds it at sound until The Old Watch is won (`held`, `wakes`, `--heirloom --quest`). Slice 12 (0205): Teodor 2, The Warden's Gate, names the lance and opens Turn the Key (the lock); no hand play declares it yet. Slices 13, 14 (0206, 0208): Keziah 1, Rook 2 (#805's key for Unbroken). Slice 15 (0224): Keziah 2, The Oath Stone, the bound man on the short road against the hunger. Slice 16 (0225): Joab named, the envoy keeps his fort, `threat`'s counter feed, `KeziahOath` for #634; Code 1133 8/7/5, the oath came up. #635 is closed as built; #77 is `blocked` on a chair's campaign. #77 slices 1 to 7 are built (0183 to 0189; DESIGN 14): 26 support pairs, tiers C 16, B 28, A 48 through the aura (best, not sum), rapport on every main map, the camp prints `Supports:`. Round 259's bars met (`supports-numbers-77.txt`). A reachable by a committed human waits on a chair's campaign (else 44). Writing waits on voice sheets (DIALOGUE).
- #81 slices 1, 2 (0190, 0191): the field is map 9 after the Hollin card (placeholder), level 4; fixtures drop it for nine-map transcripts. Slice 4 (0226): `route_drift:` (rounds 273, 274), the untaken route's group wakes on turn 5 and marches on the taken crossing; Code 81 warm 7/7/6. Chat's cold field decides it.
- #633 slices 1 to 3 (0192 to 0194): the raid's `branch` offers Keziah or Rook, `pick <unit>` is final, the passed one never joins; on the field the passed claimant returns with the pickets (8,12) at the pick's level, `talk <unit> <target>` turns them (a bed free) or the captain spares them (`returned`); `campaign --from <map> --pick <id> [--level N]`. Side characters are met at a camp (`meets`, `meet <unit>`, final, one a map; `met`). Chat cold field (271, 6/7/6) kept `talk` (0193 amended); #633 can close. #844: the Roster names a contested place.
- Blocked on plays: #13, #160, #78 to #80; #83 is open.

## Maps

| Map | Status |
|---|---|
| the_mill | Campaign map 2, Maud's arrival (0124, #632). Rout, limit 12, `protect: maud`. Gate 1 133/200 (66 Maud). Code 632 warm 6/5/3, won turn 6. Chat's play owed. |
| the_postern | Side map, Bet's request (0146, #691), under `content/quests/`. Seize, limit 10, Recall 2, 4 deployed. Sim gate 1 74/200. Code 718 warm 8/7/7, won turn 5. A cold chair owed. |
| the_first_shrine | Side map, Maud's quest 2 (0198, #635), after map 5; pays the Psalter. Seize, limit 10, `brace: on`; a braced cork at the sanctum door, pursuers over a causeway. Sim gate 1 5/200. Code 875 warm 5/6/5, won turn 4. A cold chair owed. |
| the_burned_school | Side map, Pell's quest 1 (0199, #635), after map 4; pays 2 common. Escape, limit 7, Recall 2; a shieldbearer corks the east gate, burners through the west from turn 1, three chests that turn you back. Sim gate 1 36/200. Code 884 warm 7/6/4 at level 4, won turn 6 with one chest. A cold chair owed. |
| the_undercroft | Side map, Pell's quest 2 (0200, #635), two maps after Pell 1; pays Pell's Commonplace and 3 frozen iron. Seize, limit 7, Recall 2; three routes past two sleeping groups, a lector corking the north door, sworn down the stair from turn 2. Sim gate 1 2/200. Code 960 warm 7/7/6 at level 5, won turn 6. A cold chair owed. |
| the_old_watch | Side map, Teodor's quest 1 (0201, #635), after map 5; wakes the Family Lance. Defeat Boss, limit 9, Recall 2; a sallying boss on a fort, a one-tile bridgehead. Sim gate 1 8/200. Code 961 warm 8/7/7, lost turn 9 with the boss at 2, Wren fell. A cold chair owed. |
| the_counting_house | Side map, Ottilie 1 (0202, #635); 2 common. Rout, limit 10, Recall 2; a canal, bridges 6,1 and 6,7; the archer a house guard at 11,2 (#925, 0228). Sim 0/200 (timeouts). Code 980 warm 8/7/7, Chat cold 7/7/6, Code 925 warm 7/6/6, all lost on the clock. Next lever: limit 11 (#931). |
| the_long_count | Side map, Ottilie 2 (0203, #635); Ottilie's Tally, 3 frozen iron. Escape, limit 8, `dusk: 4`; a road bridge or a footbridge, pursuers from turn 2. Sim 2/200. Code 91 warm 7/7/6, Chat cold 7/8/6, both won only by recalling a lost clock; Code 928 warm 7/8/5 with the count on, won leaving Teodor. The fort archer never acted. Next: the archer to 11,4 (#930). |
| the_chapter_roll | Side map, Rook 1 (0204, #635); opens the Drover. Seize, limit 8; a gorge, a corked cell. Sim 0/200. Code 1100 warm 8/7/6, won turn 8; the sentry never acted. A cold chair owed. |
| the_wardens_gate | Side map, Teodor 2 (0205, #635); names the lance, 3 frozen iron. Defeat Boss, limit 10; the boss before the gate, a gap and a breach, a rider yard in his noise. Sim 0/200. Code 1110 warm 8/7/7, won turn 7; archer-2 never acted. A cold chair owed. |
| the_burned_shrine | Side map, Keziah 1 (0206, #635); 2 common. Rout, limit 10; a ring wall, the hearth fort beside a held shieldbearer, a fight from it wakes the grove. Sim 0/200. Code 1113 warm 7/7/5, won turn 8; the turn-5 brigand is a walk. A cold chair owed. |
| the_oath_stone | Side map, Keziah 2 (0224, 0225, #635); 2 common. Defeat Boss, limit 9; Joab corks the door, `freed` by the envoy's fall; the envoy keeps his fort. Code 1133 8/5/6, then 8/7/5, both lost turn 6. A cold chair; the camp lever if the long road is a wall. |
| the_rookery | Side map, Rook 2 (0208, #635); 2 common, #805's key for Unbroken. Escape, limit 9; a ravine bridge, a woken loft, Rook leaves last. #862 (0208 addendum): sentry 13,3, archer 12,6, 14,5 free. Sim 36/200 (was 72), all captain alone. Before: Code 8/6/6, Chat cold 8/6/8. Code 862 warm 7/7/5. A cold chair owed. |
| the_lazar_house | Side map, Maud's quest 1 (0127, #635). Survive, limit 6, Recall 2; lanes the ally can bar. Sim 4/200. Code 701 warm 8/7/5, Wren fell. A cold chair owed. |
| starting_alone | Lesson, exempt from the Fun Gate (0123, #631). Captain alone, rout, limit 10. Gate 1 1/200. Code 631 warm 7/6/4, won turn 6, one Recall. Chat's play owed. |
| the_tollgate | **tuned** (0073). Rider spawns at 13,4 when a unit stops on 6,4 or 6,3 (0072). Four deployed, limit 10. Gate 1 74 percent, gate 4 ok at 0.245. Fun Gate: Code seed 211 8/7/7, Chat seed 227 8/7/7, both warm. |
| brackwater_cut | **tuned** (0078). Escape at `dusk: 5`, exit without a Move (0074), lamps on a player-phase wake (0076). Gate 1 65 percent at 200 seeds on the weapon-choosing Sim (0173; 67 before it); campaign point 6 (0178), `carried` 83, gate 4 ok at 0.158. Fun Gate: Chat seed 271 8/7/7, Code seed 283 7/7/8, both warm; the Critic's cold seed 509 7/6/7. |
| harrow_weir | **tuned** (0088, 0100). The crest file (#471 over #456), `turn_limit: 15` (0100). Guard Foreman with the Toll Axe under the boss veto, goes home when refused (0080), `wake_links: ford>weir`. Gate 1 61 percent (123/200), gate 4 ok at 0.280. Fun Gate: Code 481 7/8/7 (warm), Chat 487 7/8/7; the Critic 617 7/6/6. |
| the_field | Campaign map 9 (0190, 0191), level 4 (`carried` 68). Defeat Boss, limit 20; five groups by distance; `route_drift` turn 5 (0226). Gate 1 61 percent, gate 4 ok. Code 811 7/7/5, 633 8/7/8, 81 (drift) 7/7/6, all warm; Chat cold 820 6/7/6. A cold chair on the drift owed. |
| old_mill_road | Out of the campaign (0124); a fixture (gate 1 33 percent). |
| saltmarsh_ford | Not tuned. Toll Axe boss (0029), ford forest (0030), spawn behind (0090), `brace: on` (0091), the north cut (0093). Gate 1 23 percent, gate 4 fails at 0.055. Last rates on the cut: Code 547 7/7/6 (warm), Chat cold 571 7/7/5. The spawn lever (#524) failed gate 1's floor (0095). |
| sallow_grange | Not tuned. Seize, the Reeve a guard boss at 15,6 (0055), hexer at 13,7 (#275). `keziah_warning: on` (0214). Gate 1 79 percent, gate 4 ok at 0.380 (#450). Last entries: Chat 7/7/6 (seed 44, pre-#275), Code 6/6/5 (61), Code 871 7/7/5 (Keziah). |
| ironwake_raid | Not tuned, kept as a map (round 158). Campaign map 5 under `content/keep` (0060): rout, limit 7. Gate 1 94 percent. Code 288 6/6/6, Chat cold 301 7/7/5; never tuned for surprise; the van lever no longer waits on #556 (killed, 0103). |
| ironwake_keep | Not tuned. The campaign's last map, survive, limit 8, fought on the record's keep (0059, 0060). Acceptance is play, not gate 1. Plays: Code 82, 287, 288; Chat 91. |

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
| 13.18 signatures | `docs/samples/saltmarsh_ford_brace_signatures.map` (#486, 0097; Code 563, Chat cold 113 and 587 in: Teodor and Wren's Canto kept, talk unread, the ledger at 50 killed; at 65 since #540) | the ledger killed at 65 (0197, Chat cold 831); its next shape (round 263) waits to be built, then a cold play |
| 13.25 rotten planks | `rotten_bridge_planks.map` (0179; Code 251, Chat 4 7/7/8: the relay met clause 1) and `rotten_bridge_straggler.map` (#783; Code 783 warm, no cut on offer) | Chat's cold play of the straggler |
| 13.26 rockfall | `docs/samples/scree_gorge_rockfall.map` (0182; round 247's board; Code 811 warm 8/7/7: the timing decided, warm) | Chat's cold play |
| #805 the drake's carry | `docs/samples/kestrow_water_carry.map` (0215; Code 805 warm in on `free`: no setting named) | Chat's cold play on `waited` and `free` |
| #805 the rime breath | `docs/samples/kestrow_water_rime.map` (0216; Code 805 warm in: a route taken for it, the thaw decided) | Chat's cold play |
| #872 the Drover | the rime sample with Rook a Drover (0217; Code 805 warm: Deep Rime decided) | a campaign chair journals her; Chat's cold pick, her or the Captain (round 294) |
| 13.14 brace | kept (0084, round 121): spike on `harrow_weir_brace.map`, keep round on `saltmarsh_ford_brace.map` (Code 449, Chat cold 521) | in; shipped on Saltmarsh (0091); holds read on the next brace play with a hold |

Kept on samples: rivalry (0043), shove (0069), Seize drift. Killed: Recall scars (0010), battalions (0044), the windup (0094), overwatch (0098, 0103), cover (0099).

## Standing notes

- Repo is public (0006); `main` is protected by the `ci` check and PRs auto-merge on green. Routine pushes use `issue/<n>-<slug>`, falling back to `claude/`.
- Builder race guards (ROUTINES.md section 2, #735, 0169), bind every Builder that reads this file: with the chain on, a cron slot defers (rule 1); every Builder claims with a comment, waits 30 s and re-reads, the earlier claim winning (rule 2); a rotation re-checks first (rule 3). Pending: Lotus pastes section 2's prompt lines into the stored routine once (not a fork).
- Routines (ROUTINES.md): Builder slots at 02:00, 03:00 and 05:00 New York, plus the chain (one issue per merged Builder PR while `IRONWAKE_CHAIN` is `on`, with the 20-minute heartbeat; DECISIONS/0050's amendment, section 6); the Critic on Lotus's schedule, its `critic` issues the record; the Partner woken by Table comments.
- Sim: `--smoke` (CI, gates 5 to 8, the signature ceiling, origin by class); `--heirloom <item>` (#646); `--kinsbane` (#804); `--levels` (#704, #738); `--supports [--pair <a> <b>]` (#77); `--ladder [--map <id>]` (#705); `--finale <map> [--level N]` (#692); `--full <map>|<file>|--all [--seeds N] [--scheme one|two] [--taxfloor F] [--difficulty D]` (gates 1 to 8, about a minute and a half per map at 200 seeds in Debug; not in CI); `--trace <map> <seed>` (a script the CLI replays under `--strict`); `--keep [<edit> <x,y>]...`; `--hitband`. Reads: `docs/measurements/`.
- Content shapes: `ContentLoader`. `units/cast.json` is the roster, captain first; `rules.json` the map-free constants. Stats keys `hp str mag dex spd lck def res cha`; terrain `cost` `null` is impassable.
- Map files: DESIGN section 10 and 0011; hand-edited maps are canonical (`MapFormat.Write`). Coordinates are `x,y` from the top-left, 0-based; CLI slots count from 1.
- CLI and Sim tests read the console only through `ConsoleCapture.Run` under `[Collection("console")]`; every project runs under invariant globalization.
- Cloud: dotnet-sdk-8.0 and gh are preinstalled; GitHub goes through the built-in tools, since `gh`'s token check fails in the sandbox.
