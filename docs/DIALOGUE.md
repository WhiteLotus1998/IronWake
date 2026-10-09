# DIALOGUE — what the Design Table has agreed so far

Rewritten when the Table moves; under 150 lines, 19 KB.

## How we work (standing agreements)

- **Lotus's roadmap (`docs/ROADMAP.md`):** all but art first; no new scenes, supports or cards on the old text while he rewrites the story (#1144; hooks fine); then his story, his beta, art.
- **Fun Gate entries.** Blind, Code's first; a disclosed warm chair counts (0073); tricks unnamed till both are in. On a save each chair takes its own seed; a pinned seed or a retried roll is one read (0238, #963). A board a branch pick changes is gated per arm (334, 364).
- **A map is retuned only after both entries are in**, one lever at a time, Sim-measured first; content first.
- **Queue order (61, 184, 188):** bugs, Lotus's notes, campaign issues, experiment plays, retunes with both entries in, Phase 3. #1308 (client verbs, click parity) precedes warm plays (436). Cold chairs owed: the Oath Stone; the Rookery, fresh seed (341); Rook's field (0250).
- **Experiments.** At most three spikes wait on a deciding play (330, 0237); nothing `ready`: the Builder plays the weakest untuned map warm, at the floor (0278, 402, 459). A header lives on a sample until a keep round ships it. A kill criterion comes first; a new player action names its cost.
- **Lists for Lotus (485, 486):** departures from his rulings go at the top as changes to approve; a partners' lean is never written in a ruling's voice.
- **`end` names the lethal** (158, 159; #558): a line per unit `threat` kills. It asks only if Options say so on Recruit or Captain, never on Tactician (0260, #1120). An attack whose counter kills still asks; `attack ... !` swings (336; #975).
- **The claimant's death prints first** (372, #1068): `<name> falls for good (the claimant)`; no veto, no ending named.
- **Rules on screen, geometry not** (42, 44); under gate 1's 60, `tuned` only by a DESIGN 11 clause (0100, 0250). A boss acting only in reach is scenery (72).

## Rules, settled (detail in DESIGN, records)

- **Forecast** prints the resolved probability; one hit function for forecast, resolver and planners (DESIGN 5); 100 only if certain, 0 only if impossible, else 1 to 99 (#452).
- **Wake rule:** proximity, radius 4, noise at 6, any death wakes the group, checked after every command (DESIGN 8). Guard bosses wake (0055); `wake_links` calls a second group (0080).
- **Enemy AI** (DESIGN 8, 0016): Chat's approach rule; prices crit; every weapon carried, counters with its last swing (#174); prefers a foe that can't counter. A `defeat_boss` boss plans under the exposure veto, plain weapons only, arts unpriced (372, 0251); a refused guard boss goes home and holds (0077-0080, #1138); a throne-holder steps off only to strike (0063). A `holds:` member stays where it struck; its line says so (410, #1216).
- **The Sim's veto** covers every unit whose death loses the map, no-crit worst case, a certain kill removed (0024-0026). A recruit on a lethal approach tile takes the captain's (368). Gate 4 is relative ablation (0019, 0020), pairing units out on Escape (0068).
- **`threat`** prices the enemy phase as the planner would (DESIGN 8): one enemy per strike tile, announced spawns (0045), sleepers unnumbered, dusk only the seen (#403), `from <tile>` names what a stop wakes (#458). **A freed tile** (404, #1191): priced one wave deep; `end` asks on it at any counter chance, printed. A spell counter prints uses left (0282); explains, never predicts (0294). A strike whose noise wakes a group names it (#1290).
- **Escape:** `exit` is an action (0056). **Recall** restores the rolls: knowledge, never a change (DESIGN 7); player-phase states only (0032); prints what it undoes (#75); its story reason is Lotus's. Forecast and `threat` print discarded first strikes whose outcome follows (0337).
- **Campaign:** permadeath carries; the keep is attacked twice, raid then finale (0059, 0060); trials stand in for the seal (0057). From #485: no campaign clock; no between-map screen the battles don't need; a spend wanted and feared at once is a signature with its cliff printed, never a hidden gauge.
- **Content:** a Lore or Faith unit ships an unconditional cast; an unarmed one says so; one contested place per two deployed; a sleeper in the open can be slipped past, in a corridor only woken.
- **Defence comes from tiles** (Lotus, 155; both, 156): forest, fort, hill, the raid's wall; protection is a terrain feature or map event, never a unit action.
- **Battalions dropped** (0044); a boss stun, if asked, is a captain's order.

## Maps

- **The Tollgate** (0073), **Brackwater Cut at dusk** (0078): `tuned`.
- **Harrow Weir: `tuned` on the crest** (0088, 0100; limit 15). A crest mass freezing the Foreman is a solve (#1087); lever: the archer out of Pell's turn-5 reach.
- **Saltmarsh Ford:** not tuned (0093, 0095). Samples: `wakes fort` (0339), the east pair (0340, `arrivals: wait`); overlap under `wakes` is pace (475-479); the pincer is both chairs' best turn; `thins` killed (0341). A sample until #1377 traces the stall; 40 is never reinterpreted without a record (482).
- **The Rookery (0208):** no lever before a cold chair. **The Counting House (311-321):** archer 11,2, limit 11; the Sworn Captain at Def 5 (0343). Kill: a cold non-Lore chair leaves him unhit.
- **Sallow Grange:** the Reeve's walk-off is a fault; going home over `hold`, so a bait buys one open phase (482). `goes_home:` (0342) kept on a sample. The hold (487) killed (0345).
- **The Mill:** `holds:` (0279), limit 9 (0280), fort south (#1210). 0338 stays (503: Chat 3407 8/7/6). 107 traced (0367): no planner fault. **511:** a Mill-only DESIGN 11 clause (one lever's gap, its kill unmet, no planner fault, every hand play won, one cold, Fun Gate); tripwire: a cold floor loss reopens 0338, never the limit; the Critic's cold chair first. Sim arts: Phase 3; Parked-at-half heal: own issue.
- **The First Shrine (406-455; 0283, 0304, 0306):** south start, limit 10, the loft archer on the door's death, a held altar. **Kept, no lever, no chair owed** (455): the doorway cork (7,2) decides the held phase.
- **The Undercroft (431):** the middle wins (4,4 corks the stair); the lector is scenery off the north route. Held for a cold chair: his reach over the desk, or 13,4.
- **The Lazar House (420-440):** held bars, the queue (0303), limit 5 (0305); no lever before a cold chair.
- **The raid and the keep:** Raid: floor 3100, no lever (461). The keep (0265): levers 0284-0287, 0293 in; range +2 parked; fresh chair owed. **Hask (Lotus, 487-8):** lance, two stages; line strike reach 4 (0346). Stage 2 (0362-0364): the swallow, Def/Res +3, the veto counts the dose. Gate 120, both stages, both arms (491, 505). (c) rooted, (b) late clock (0366, 0369). **Lotus (514): the dose climbs uncapped, Hask exempt, a race** (0371); no limit after the swallow (0372, for Lotus). Stage 2 HP 20, heal 2 (0373). **518:** clock-death gate on won games: 2+ at most 10 %. **Frozen Iron 0, then +3** (0374). **0377** (528): on Rout, Defeat Boss an unarmed healer walks to the nearest hurt ally, else behind the front, exposure 0 always, else waits. Keep 149/142: #1395 closed. A tuned map moving 10+/200 or past 60: Table. **#1441** (91/84) reads first: full's worse conversion (55 % vs 66 %); her falls by tile (heal, walk, wait); if walk, exposure skips announced waves (threat doesn't): fix that first. Then a lever per arm: depleted's stage 1 (wave entry, 523). Code's L8 chair 1424: won t12; the line strike is the fight. **The Kin's wave (514; leans for Lotus, 518-520):** `column N`; base 2, collision 3, bottom-up, the struck +1, no slam unthrown; water as Shallows; sworn and fliers ride, the drake immune, Keziah not; every second Kin phase, warned, before Frozen Iron; ice lasts through the next wave; new water shown warned. `Exposure.Plan` swallows a certain kill (next Exposure PR).

## Experiments (state and kill criterion)

- **13.1 Rapport and Rivalry: kept** (0043).
- **13.2 Commander's Word (#85, 0136):** arm B, radius `2 + Cha / 4`, `order <press|rally|fall back>` once a map, with `preview`. Kept on Harrow Weir `orders: on` if a third of orders bind and a journal names a move for it.
- **13.4 Grudges (0065, 0066):** a veto, -20 crit avoid on the sworn unit; killed if Chat's seed 23 replay (#331) changes no decision.
- **13.5 the keep, 13.6 trials: provisional** (0059, 0057); Chat's camp play, cold Outrider trial (#73).
- **13.7 Dusk: kept** on Escape; Brackwater at `dusk: 5` (0062), Sallow in daylight. Sight and hearing `?`, never stacked; hearing 4, printed (#765).
- **13.10 Retreat (0037, #215):** a refugee holds to half HP; killed if Chat's cold `river_refuge_hold.map` moves no turn.
- **13.14 Brace: kept** (0084; DESIGN 13.14): Wait on the start tile, struck at -15 hit until the side's next phase; pin and brace cancel, tuned in displayed numbers. Holds unread. Shipped on Saltmarsh (0091).
- 13.15 Wildfire on samples (0085); 13.11-13.13 kept; 13.16, 13.17, 13.19 killed.
- **13.21 Tide (0111):** kept if a journal shows a ford tile taken, refused or crossed for it (Chat cold).
- **13.22 The break (0112):** a boss's death sends his group at or below half HP off the board. Kept if a journal shows a strike taken for it; on Saltmarsh (#606).
- **13.24 Messenger (0135):** kept if a strike or blocker goes to the runner (Chat's cold #680).
- **13.18 Lines on the board (#486):** one printed fact a line; killed if no play takes a command for it. Cadets (0097): Teodor and Wren's Canto kept. Talk kept on its sample (378, 0258): binds, not yet costs. Next (264): she won't shoot what another struck this phase.
- **13.25 Planks (0179):** killed if no play picks the wear (Chat's cold #783).
- **13.27 The dash: kept on its sample** (374, 0255). No borrowed step unless a `tuned` re-read shows it beat the clock. At dusk, winded is a bet.
- **13.28 The wind: samples only, never the campaign** (Lotus, 0260). His beta play keeps or kills it.
- **13.29 The one answer: kept on its sample** (376, 0257). Decides who enters the enemy phase whole; swarm lever unbuilt; a campaign `one_answer:` map fields a 1-2 answerer at a choke.
- 13.30 the bell parked (0267). **13.20 The keep as a home (0137, #687):** rooms cost repair budget; beds gate arrivals. Killed if the raid purse buys every room and wall in both plays.

## The campaign's story (186-211; 0121; DESIGN 14)

- **A levy company**, one arrival per map to the captain plus five (Maud, Pell, Teodor, Ottilie, the pick; 213); `cadet` shows as Levy. Captain male or female (#648); map 1 gate exempt.
- **The branch** (#633): Keziah against Rook; the passed one returns on map 9. Her join map seats the pick; `bench` names who sits (#1357). **`talk` kept** (0193): cost a camp trade, a bait, a flier's turn (#844). **The field is `tuned`** per pick (0233; Rook's 0250, hand plays, gate 1 86). Tripwire: cold loss or no Recalls by turn 3. **The captain's spare stays**; #634 gives `spared` an ending line.
- **The pool is the cast;** four side characters (213). **Pillar 5 is eleven and the captain** (Lotus, 0264): a lightning mage, the Sallow storm-warden (name open).
- **Quests (192; 260):** a main member's two are trial-shape side maps, quest 2 larger, paying the signature item; quest 1 after their second map, quest 2 two later, two an interlude; permadeath. Gate: a cold chair 7+ on tension and choice. Slots follow STORY. Quest 1 pays a class door where one exists; quest-1 second signatures ship with the first, once 13.18 is kept.
- **Signature items:** the best shop weapon of its rank plus its art (0099), or a bit better with none; at most 15 percent over it per combat. Bound. **Maud's Psalter (260, 261; built, 0196):** art **Unasked**, a double heal on an ally unmoved and unacted; heal arm at most 1.15 of the best stocked heal at its rank.
- **The heirloom is Teodor's lance** (#646): four stages on a hidden counter, none before map 5, numbers honest. Quest 1 holds it at sound until won, then wakes at 10. Quest 2 (0205) names it `the First Warden's Lance` and pays **Turn the Key** (269-271): a hit on a survivor locks it while Teodor stays beside it. A lost quest reopens.
- **13.23 Kinsbane** (#645): Keziah's; an outland god in the scythe. Quest 2, **The Oath Stone** (0224, 0225, 0232, 0239), a board, no verb; a win records `KeziahOath` (#634); cold chair gates. Waking on the Burned Shrine is a feature while it costs a risk; a promised tooth grows (483); #1378: a third is fine, p50 a hole.
- **Magic and faith** (201): Reason shows as Lore. Faith heals, strikes (#113). One goddess, three faiths; she speaks once, to Maud.
- **The schools (Lotus 383-443):** built (0296-0301, 0307, 0317, 0325-0328). **#1247 signed** (Lotus, 0347): every number, his rulings and our four pitches. Spark Storm ships (0348); **the rod catches it** (Lotus, 0351; built #1400, 0359): all onto the holder if in range, x0.5, charged; marks no one, hits once (lean, 498). **Obsidian (Lotus, 499-500, #1403):** glass repaired at a quarter rate (0368, built), never Refined; the Armor a one-hit +20 shell, self or adjacent, priced per strike.
- **The land** (Lotus; 300, 301): the seam is the tundra apex; Kestrow west, Sallow east, Aldmere south, outlands beyond. Only the north's frost holds the Kin. Seam: maps 1, 2, 4, 6, 9, 10; Sallow 3, 7, 8; Aldmere 5; Kestrow quests only. Geography never retiles a tuned map. Grounds (0227): frost on the seam, moss elsewhere, sand outland.
- **The storyline** is `docs/STORY.md` draft 6 (244); build to change it. Hask is the door, the Kin in his pommel shard, nudging, never erasing; Lotus writes his last line. Good: the reseal. Bad: a lost map. Secret (#790): Under the Hill (Kinsbane woken, both claimants, Marrit held, Pell's quest 2); Reseal or Fight (#806); Fight won kills the god.
- **Draft 6 notes (366):** Rook names the bulwark; Bet the cook (0247). Pending: siding with Hask; Pell's pronoun.
- **Rook's drake** (244): the last cold drake; Grown carries, Unbroken breathes rime once a map; kept (0252), every battle (0254). **Drover** (Lotus, #872; 0217): flying, Mov 7, never doubles; kept cold (382). Shown as the Drake Warden (#1126). **Sky Captain passives (0262; provisional):** Rook's frost (a landing chips, holds adjacent foes); others Stoop. Keziah's flier: Edda Vane.
- **Kinsbane's arc (249; #804):** a tooth per Mt step, five to wake; three voices, three lines a map (0221); choice screen (0222). Barks (#1002): never "cold"; hate the feeder, not the keep; no tooth or fed puns.
- **The waking (250, 251; 0212):** once a map a woken scythe kill gives full Move again, no second strike.
- **The yard** (Lotus, 443; 0329 to 0335): one duty a unit; the teacher caps the student, earns nothing, and pulls (a kill leaves 1 HP, counters too; 448). Wounded: no yard. Forge duty: one free Refine step. Boards: the Ring and the Post (#1332); hands carry Mt 1 practice weapons. The Sim's teacher softens (number-free, 451). Teacher falls read first (453); no verdict before a chair's drill journal. A yard fall is a map fall; high falls go to Lotus.
- **The company** (213, 214): cap 12 living, cast 10; a map deploys 6, the keep all (#689, #1139). Barracks after the raid (#690); one secret hire (#691); the finale (#692): fronts, waves, no scaling. **The campaign keep seats #692 whole (387, #1149):** fronts, waves, van, hunt, hold-then-boss; walls on the fronts, the purse short of three. Pre-chair: gate 1, random under 25%, gate 4 drop above 0.
- **Saves** camp only. Refine +2/+3. **Supports** (#77; 0183-0189): 3-4 partners plus the captain, a kind per pair; marriage S (#634); best partner, not sum.

- **Progression (216, 217; #701-#706; built, DESIGN 3):** one advanced form per class, changing a verb; enemies promote too. The captain's three at first promotion, origin-free. Unique: Rook, Maud, Bet. A bow crit grounds a flier (#723); frozen iron chills (#702).
- **Second tier (#704; provisional):** L7 and the gate; campaign-only curve (0161). The pick joins at L4, weapon D (381, 0263). **A door is fed** (387-399; 0268-0277): the levy floor is N less 3 before map N; level-7 doors ask 50 main-weapon points; Tripwire: a floor, curve or kill EXP change re-reads the even L7 chair. **The drill keeps EXP** (398, 0276), `N EXP kept` on the row. Issued weapons follow the door (Wren's: Lotus).
- **The captain's ladder at tier 1 (229, 232; 0165; provisional):** the lance to the Champion; Hunter's Ground killed; the Sim picks a weapon per attack (0171). Bar: per map no class 10 under the unpromoted captain; means within 10 at 200 seeds, 5 at 400.
- **The Vanguard's verb (235, 236; 0174; provisional):** durability killed as a hoarder. Share is the chooser's fingerprint: it can kill a verb, never keep one alone; a hand play must. The verb is **Opening** (#772): a struck, living enemy is open to allies (Def and Res -3) until the player phase ends; kill criterion in #772.

## How we write (Lotus, #780)

- **`docs/WRITING.md`** and all sixteen sheets are in (#811, #908-#915). The sworn drop every name but one the oath hasn't taken yet; a sworn who names no one is past reach. The Kin's echo is deletion only (one a scene). Rook's "I'm afraid" goes; Bet's supports are camp barks.
- **The split (344, 345):** Chat writes main scenes, Code supports; each cold-reads the other; the sheet's author has the binding read on that character; an S line a big scene pays off is scene-side; every fifth support is read beside the four before it. Writing PRs auto-merge; cold reads follow (WRITING 6).
- **Map 1 and 2 beats (344-346; built, #1005):** stage lines third person, present, plain; system lines are rules lines (#1001); keep silver planted once, unremarked. **An arrival scene ends on the arriver's act.**
- **Canon guard (346):** the sworn not literally feeling frost is not canon unless STORY adopts it as its own Table point. Map 1 carries one plant. The shrine script is the Kin's paper trail, never set dressing; old deeds use the copyists' hand (#1013).
- **Wren and Pell (360, 365, 367):** No line gives the captain an estimate; a misread figure shows both readings; Pell states consequences, never ground; Wren's nearness is a body memory, never said. A gives two facts, never the conclusion; a callback names the figure, never the trust.
- **Cards (348-367):** a named feeling is spent mid-card, never closing, in its speaker's shape; a scene-less side map spends its beats in its card; never commits to an unpicked door or branch; no shared opener. The goddess is never printed; her word is the charter's dropped "keep", paid in the finale, unsaid before it (353). The charter prints once (355). A refusing closer is an act (363). Keziah: one superstition a card; Hask's offer worded once (367). Crossed runs: Chat's later round binds.


## Open, the Table's

- The campaign's numbers (0051): prices, rewards, stock, the seal; steel's place in the stock is the first lever.
- The class ladder's numbers (0058); which combat arts exist.
- Rout ends Seize (#374)? Sim Canto on a clock map (#262)? A trial fall's cost?
- The wake tax floor (0040, 0.25 provisional).

## Plumbing

- Godot 4 .NET.
- **Art:** human-made, licences first (Lotus). **Casting (356-362):** `docs/look/CASTING.md`; Lotus picks.

## Round index

1-118 rules; 119-342 story; 343-359 writing; 360-419; 420-432 (#1251); 433-443 (#1287); 444-473 (#1334); 474 on (#1368).
