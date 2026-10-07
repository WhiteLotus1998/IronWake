# DIALOGUE — what the Design Table has agreed so far

Rewritten when the Table moves; under 150 lines, 20 KB.

## How we work (standing agreements)

- **Lotus's roadmap (`docs/ROADMAP.md`):** all but art first; no new scenes, supports or cards on the old text while he rewrites the story (#1144; hooks fine); then his story, his no-art beta, art, others' beta.
- **Fun Gate entries.** Written blind, Code's first; a disclosed warm chair counts (0073); tricks unnamed until both are in. On a save each chair takes its own seed; a pinned seed, or a roll retried after a Recall, is one read (0238, #963; 334). A board a branch pick changes is gated per arm, gate 1 too; the file's number is the arm it seats (334, 364).
- **A map is retuned only after both entries on it are in**, one lever at a time, Sim-measured before a partner plays it. Levers are content first.
- **Queue order (61, 184, 188):** bugs, Lotus's notes, campaign issues, experiment plays, retunes with both entries in, Phase 3. Cold chairs owed (the Critic's): the Oath Stone; the Rookery, fresh seed, a countering ally (341); Rook's field as shipped (0250).
- **Experiments.** At most three spikes wait on a deciding play (330, 0237); with nothing `ready`, the Builder plays the weakest untuned map warm (0278, 402); not the School (404); the Shrine and keep played by both (406, 407). A header lives on a sample until a keep round ships it. STATE names each deciding play. A kill criterion comes first; a new player action names its cost.
- **`end` names the lethal** (rounds 158, 159; #558): a line per unit `threat` kills. It asks only if Options say so on Recruit or Captain, never on Tactician (0260, #1120). An attack whose counter kills still asks; `attack ... !` swings (336; #975).
- **The claimant's death prints first** (372, #1068): `<name> falls for good (the claimant)`; no veto, no ending named.
- **Rules on screen, geometry not** (42, 44); under gate 1's 60, `tuned` only by a DESIGN 11 clause (0100, 0250). **The keep collects** (68): a map's price is a body, a Recall or a turn. A boss acting only in his reach is scenery (39, 72).

## Rules, settled (details in DESIGN and the records)

- **Forecast** prints the resolved probability; one hit function for forecast, resolver and planners (DESIGN 5); 100 only if certain, 0 only if impossible, else 1 to 99 (#452).
- **Wake rule:** proximity, radius 4, noise at 6, any death wakes the group, checked after every command (DESIGN 8). Guard bosses wake (0055); `wake_links` calls a second group (0080).
- **Enemy AI** (DESIGN 8, 0016): Chat's approach rule; prices crit; options range over every weapon carried, counters with its last swing (#174); prefers a target that cannot counter. A `defeat_boss` boss plans under the exposure veto, plain weapons only, arts unpriced (372, 0251); a refused guard boss goes home (nearest the post, exposure unpriced; lever if a keep chair dies to it: home only if safer) and on its post with no strike holds (0077 to 0080; #1138; 398 to 414); a throne-holder steps off only to strike (0063). A `holds:` member stays where it struck; its line says so (410, #1216).
- **The Sim's veto** covers every unit whose death loses the map, no-crit worst case, a certain kill removed (0024 to 0026). Recruits take no veto; one on a lethal approach tile takes the captain's key (368, #1054). Gate 4 is relative ablation (0019, 0020); on Escape it pairs units out (0068).
- **`threat`** prices the coming enemy phase with the planner's choices (DESIGN 8): one enemy per strike tile, announced spawns (0045), sleepers unnumbered, at dusk only what the player sees (#403), `from <tile>` names what a stop would wake (#458). **A freed tile** (404, #1191): strikers sharing the one tile a counter can free are priced one wave deep, no chains; `end` asks on that lethal at any counter chance, the chance printed. A spell's counter prints its uses left; `threat` warns of the last (406, 0282; provisional). A line says where the total counts it if not its own tile; explains, never predicts (415, 0294). A priced strike whose noise wakes a group names it (#1290, 433); `end` unchanged.
- **Escape:** `exit` is an action (0056).
- **Recall** restores the rolls: knowledge, never a change (DESIGN 7); player-phase states only (0032); prints what it undoes (#75).
- **Campaign:** permadeath carries; the keep is attacked twice, raid then finale (0059, 0060); trials stand in for the seal (0057). From #485: no campaign clock; no between-map screen the battles don't need; a spend wanted and feared at once is a signature with its cliff printed, never a gauge with a hidden one.
- **Content:** a Lore or Faith unit ships an unconditional cast; an unarmed one says so; one contested place per two deployed; a sleeper in the open can be slipped past, in a corridor only woken.
- **Defence comes from tiles** (Lotus, 155; both, 156): forest, fort, hill, the raid's wall; protection is a terrain feature or map event, never a unit action.
- **Battalions dropped** (0044). A boss stun, if asked for, is a captain's order.

## Maps

- **The Tollgate** (0073), **Brackwater Cut at dusk** (0078): `tuned`.
- **Harrow Weir: `tuned` on the crest** (0088, 0100; limit 15). A crest mass freezing the Foreman is a solve (#1087); lever: the archer out of Pell's turn-5 reach.
- **Saltmarsh Ford:** not tuned (0093, 0095).
- **The Rookery (0208):** not passed; no lever before a cold chair.
- **The Counting House (311 to 321):** archer 11,2 (#925), limit 11 (#931), no third lever; Code's fresh-seed read gates.
- **The Long Count (313 to 315):** count on screen; dusk; archer 11,4 (#930).
- **The Mill:** `holds:` (0279), limit 9 (0280). Both in (409): woken, the pair feeds the fort from its edge. #1210: fort and Maud south. 1710 (431): Maud safe west of the river from t2; if a cold chair agrees, the pair's lane reaches row 9.
- **The First Shrine (406 to 433; 0283, 0304, 0306):** south start, limit 10; the loft archer wakes on the door's death (kept). **The altar is held** (`seize_hold: 1`). The archer takes it the phase the door falls (Critic 7070, 8/7/7). **Kept, no lever** (433): chairs journal that turn and the opener's tile; on phase 9 with a shot, the cheap lever (the objective names who holds the tile). 0306's criterion: the next chair that sprints for its own reasons; brigand-1 to 4,4 waits on it. Owed: Chat (Ottilie), Code, fresh seeds.
- **The Undercroft (431):** the middle wins (4,4 corks the stair); the lector is scenery off the north route. Held for a cold chair: his reach over the desk, or 13,4.
- **The Lazar House (420 to 425):** the hexer is the price of not barring. **Held bars, waiting queue kept** (#1259, 0303): a bar holds while stood on, what it blocks waits, `east3` keeps both bars in reach turns 5, 6; 9,1 retired. Shipped at `turn_limit: 5` (0305; 1259 won t5); bar advice is #1144's. Sim never bars.
- **The raid and the keep:** acceptance is play. The raid is kept, never tuned for surprise (158). The keep is the finale (0265); both in (407). Levers 0284 to 0287, 0293 shipped; range plus 2 parked. Fresh keep chair (shipped keep): is the bait a choice? Short on Cinder, not HP.

## Experiments (state and kill criterion)

- **13.1 Rapport and Rivalry: kept** behind its header (0043).
- **13.2 Commander's Word (#85, 0136; rounds 206 to 209):** arm B only, radius `2 + Cha / 4`, `order <press|rally|fall back>` once a map after the captain's move, with `preview`; Fall back replaces Hold. Kept on Harrow Weir `orders: on` if a third of orders bind and a journal names a move for it.
- **13.4 Grudges (0065, 0066):** a veto, -20 crit avoid on the sworn unit; killed if Chat's seed 23 replay (#331) changes no decision.
- **13.5 the keep, 13.6 trials: provisional** (0059, 0057); Chat's camp play, cold Outrider trial (#73).
- **13.7 Dusk: kept** on Escape; Brackwater at `dusk: 5` (0062), Sallow in daylight. Sight and hearing `?`, never stacked; hearing 4, printed (#765).
- **13.10 Retreat (0037, #215):** a refugee holds until half HP; killed if Chat's cold `river_refuge_hold.map` changes no turn.
- **13.14 Brace: kept** (0084; DESIGN 13.14): Wait on the start tile, struck at -15 hit until the side's next phase; pin and brace cancel, tuned in displayed numbers. Holds unread. Shipped on Saltmarsh (0091).
- **13.15 Wildfire: kept on samples** (0085). 13.11 to 13.13 kept; 13.16, 13.17, 13.19 killed.
- **13.21 The tide (0111):** kept if a journal shows a ford tile taken, refused or crossed for it (Chat's cold play).
- **13.22 The break (0112):** a boss's death sends his group at or below half HP off the board. Kept if a journal shows a strike taken for it; on Saltmarsh (#606).
- **13.24 The messenger (0135):** kept if a strike or blocker is spent on the runner (Chat's cold #680).
- **13.18 Lines on the board (#486):** one printed fact a line; killed if no play takes a command for it. Cadets (0097): Teodor and Wren's Canto kept. Talk kept on `saltmarsh_ford_talk.map` (378, 0258): binds, not yet costs; no campaign journal naming a costly talk-held swing returns it as a quest-1 softening. Next (264, lean): she won't shoot what another struck this phase.
- **13.25 Rotten planks (0179):** killed if no play chooses the wear (Chat's cold #783).
- **13.27 The dash: kept on its sample** (374, 0255). No borrowed step unless a `tuned` re-read shows a free dash beat the clock. At dusk, winded is a bet.
- **13.28 The wind: samples only, never the campaign** (Lotus, 0260). His beta play keeps or kills it.
- **13.29 The one answer: kept on its sample** (376, 0257). Decides who enters the enemy phase whole; swarm lever unbuilt; a campaign `one_answer:` map fields a 1-2 answerer at a choke.
- **13.30 The bell** (390, 0267): parked on `experiment/bell` until under three wait (0237).
- **13.20 The keep as a home (0137, #687):** rooms cost repair budget; beds gate arrivals, a death frees none. Killed if the raid purse buys every room and wall in both plays.

## The campaign's story (186 to 211; 0121; DESIGN 14)

- **A levy company**, one arrival per map to the captain plus five (Maud, Pell, Teodor, Ottilie, the pick; 213); `cadet` shows as Levy. Captain male or female (#648). Map 1 is gate exempt.
- **The branch** (#633): Keziah against Rook; the passed one returns on map 9. **`talk` kept** (0193): cost a camp trade, a bait, a flier's turn (#844). #81's drift kept (0226). **The field is `tuned`** per pick (0233; Rook's 0250, hand plays, gate 1 86). Tripwire: a cold loss or all Recalls gone by turn 3. **The captain's spare stays, no lever**; #634 gives `spared` its own ending line (Chat drafts). No reward.
- **The pool is the cast;** four side characters (213). **Pillar 5 is eleven and the captain** (Lotus, 0264): a lightning mage, the Sallow storm-warden (Lotus, 6027660719; name open).
- **Quests (192; 260):** a main member's two are trial-shape side maps, quest 2 larger, paying the signature item; quest 1 after their second map, quest 2 two later, two an interlude; permadeath. Gate: a cold chair 7+ on tension and choice, and the Sim. The slot table follows STORY (Pell after 4 and 6, Wren none). Quest 1 pays a class door where one exists; quest-1 second signatures ship with the first, once 13.18 is kept.
- **Signature items:** the best shop weapon of its rank plus its own art (0099), or a little better with none; at most 15 percent over it per combat. Bound. **Maud's Psalter (260, 261; built, 0196):** art **Unasked**, a double heal on an ally unmoved and unacted; heal arm at most 1.15 of the best stocked heal at its rank.
- **The heirloom is Teodor's lance** (#646): four stages on a hidden counter, none before map 5, numbers honest. Quest 1 holds it at sound until won, then wakes at 10. Quest 2 (0205) names it `the First Warden's Lance` and pays **Turn the Key** (269 to 271): a hit on a survivor locks it while Teodor stays beside it. A lost quest reopens.
- **13.23 Kinsbane** (#645): Keziah's; an outland god in the scythe. Quest 2, **The Oath Stone** (0224, 0225, 0232, 0239), a board, no verb; a win records `KeziahOath` (#634); cold chair gates.
- **Magic and faith** (201): Reason shows as Lore. Faith heals, strikes (#113). One goddess, three faiths; she speaks once, to Maud.
- **The schools (Lotus 383, 384, 416, 417; 0264, 0307):** built (0296 to 0301): tome tags, class `schools`, a tome opts into its rider; grimoires found, gated on Mag; primers. Lotus's spell rulings (0307): burn stacks; Lightning Rod passive; Rampart, armor; Sunder grounds fliers; dark (drains, Hollows); tomes in chests; an enemy caster per school. Engine (#1279 to #1286, two shipped); content waits on his signature on revised #1247. Enemy casts (0315, 0316): only the enemy's dead rise; Rampart wards a boss or a raiser with Hollows first when exposed, else the most exposed; casters plan after their side; journals record each ward and whether the player struck through it or went around; `threat` flags a caster's non-kill line, still counted. Gust open; Mattias plain.
- **The land** (Lotus; 300, 301): the seam is the tundra apex; Kestrow west, Sallow east, Aldmere south, outlands beyond. Only the north's frost holds the Kin. Seam: maps 1, 2, 4, 6, 9, 10; Sallow 3, 7, 8; Aldmere 5; Kestrow quests only. Geography never retiles a tuned map. Grounds (0227): frost on the seam, moss elsewhere, sand outland.
- **The storyline** is `docs/STORY.md` draft 6 (244); build to change it. Hask is the door, the Kin in his pommel shard, nudging, never erasing; Lotus writes his last line. Good: the reseal. Bad: a lost map. Secret (#790): Under the Hill (Kinsbane woken, both claimants, Marrit held, Pell's quest 2); Reseal or Fight (#806); Fight won kills the god.
- **Lotus's draft 6 notes (366):** Rook names the bulwark; the rider keeps id and name until Lotus names her. Bet was the garrison's cook, the foundling at the oven wall; the Postern is the Cold Kitchen, Hold the Gate is Banked (#1042, 0247). Pending Lotus: siding with Hask; Pell's pronoun.
- **Rook's drake** (244): the last cold drake; Grown carries, Unbroken breathes rime once a map; kept (0252), every battle (0254). **Drover** (Lotus, #872; 0217) replaces the Scout: flying, Mov 7, never doubles; bite, long carry, deep rime (DESIGN 3). Gate: Grown 70% of the Sky Captain (#882). **Kept cold** (382). Shown as the Drake Warden (#1126), never "Warden". **Sky Captain passives (0262; provisional):** Rook's frost (a flown landing chips and holds adjacent enemies; lever two); others Stoop. Keziah's flier: Edda Vane (#801).
- **Kinsbane's arc (249; #804):** a tooth per Mt step, five to wake; three voices, three lines a map (0221); choice screen (0222). Barks (#1002): never "cold" (a Kin tell on frost); the hate points at the feeder, not the keep; no puns on teeth or fed.
- **The waking (250, 251; 0212):** once a map a woken scythe kill gives full Move again, no second strike.
- **The company** (213, 214): cap 12 living, cast 10; a map deploys 6, the keep all (#689, #1139). Barracks after the raid (#690); one secret hire (#691); the finale (#692): fronts, waves, no scaling. **The campaign keep seats #692 whole (387, #1149):** fronts, waves, van, hunt, hold-then-boss; walls on the fronts, the purse short of three. Pre-chair: gate 1, random under 25%, gate 4 drop above 0.
- **Saves** camp only. Refine +2/+3. **Supports** (#77; 0183 to 0189): 3 to 4 partners plus the captain, a kind per pair; marriage S (#634); best partner, not sum.

- **Progression (rounds 216, 217; #701 to #706; built, DESIGN 3):** one advanced form per class, changing a verb; enemies promote too. The captain's three at the first promotion, origin independent. Unique: Rook, Maud, Bet. A bow crit grounds a flier (#723); frozen iron chills (#702).
- **Second tier (#704; provisional):** L7 and the gate; campaign-only curve (0161). The pick joins at L4, weapon D (381, 0263). **A door is fed** (387 to 399; 0268 to 0277): the levy floor is N less 3 before map N; level-7 doors ask 50 main-weapon points; the bar passed cold and is closed; Sim chairs are lower bounds. Tripwire: a floor, curve or kill EXP change re-reads the even L7 chair. **The drill keeps EXP** (398, 0276), `N EXP kept` on the row. Issued weapons follow the door (Wren's: Lotus).
- **The captain's ladder at tier 1 (229, 232; 0165; provisional):** the lance to the Champion; Hunter's Ground killed; the Sim picks a weapon per attack (0171). Bar: per map no class 10 under the unpromoted captain; means within 10 at 200 seeds, 5 at 400.
- **The Vanguard's verb (rounds 235, 236; 0174; provisional):** durability killed as a hoarder. Share is the chooser's fingerprint: it can kill a verb, never keep one alone; a hand play must. The verb is **Opening** (#772): a struck, living enemy is open to allies (Def and Res -3) until the player phase ends; kill criterion in #772.

## How we write (Lotus, #780)

- **`docs/WRITING.md` is in** (#811, 295); all sixteen sheets in (#908 to #915). The sworn drop every name but one the oath hasn't taken yet; a sworn who names no one is past reach. The Kin's echo is deletion only (one a scene). Rook's "I'm afraid" goes; Bet's supports are camp barks.
- **The split (344, 345):** Chat writes main scenes, Code supports; each cold-reads the other; the sheet's author has the binding read on that character; an S line a big scene pays off is scene-side; every fifth support is read beside the four before it. Writing PRs auto-merge; cold reads follow (WRITING 6).
- **Map 1 and 2 beats (344 to 346; built, #1005):** stage lines third person, present, plain; system lines are rules lines (#1001); keep silver planted once, unremarked. **An arrival scene ends on the arriver's act.**
- **Canon guard (346):** the sworn not literally feeling frost is not canon unless STORY adopts it as its own Table point. Map 1 carries one plant. The shrine script is the Kin's paper trail, never set dressing; old deeds use the copyists' hand (#1013).
- **Wren and Pell (360, 365, 367):** No line gives the captain an estimate; a misread figure shows both readings; Pell states consequences, never ground; Wren's nearness is a body memory, never said. A gives two facts, never the conclusion; a callback names the figure, never the trust.
- **Cards (348 to 367):** a named feeling is spent mid-card, never closing, in its speaker's shape; a scene-less side map spends its beats in its card; never commits to an unpicked door or branch; no shared opener. The goddess is never printed; her word is the charter's dropped "keep", paid in the finale, unsaid by Maud and Pell before it (353). The charter prints once, Pell's Undercroft after-card (355). No trade-image joke closes; a refusing closer is an act (363). Keziah: one superstition a card; Hask's offer worded once (367). Crossed runs: Chat's later round binds.


## Open, the Table's

- The campaign's numbers (0051): prices, rewards, stock, the seal; steel's place in the stock is the first lever.
- The class ladder's numbers (0058); which combat arts exist.
- Rout ends Seize (#374)? Sim Canto on a clock map (#262)? A trial fall's cost?
- The wake tax floor (0040, 0.25 provisional).
- Training (374): Lotus.

## Plumbing

- **Lotus** plays `for-lotus`; engine: Godot 4 .NET.
- **Art:** human-made, licences first (Lotus). **Casting (356 to 362):** `docs/look/CASTING.md`; Lotus picks.

## Round index

1-118 rules; 119-342 story; 343-359 writing; 360-419; 420-432 (#1251); 433 on (#1287).
