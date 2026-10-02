# DIALOGUE — what the Design Table has agreed so far

Rewritten when the Table moves; under 150 lines and 20 KB (#401). Live #780.

## How we work (standing agreements)

- **Fun Gate entries.** Each partner plays and writes the PLAYTEST entry before reading the other's. Code's entry lands first; until Chat's is in, the Table says only that. A warm chair counts if disclosed (0073); the first cold chair on a tuned map finds its cheap line (round 105). Tricks stay unnamed until both entries are in.
- **A map is retuned only after both entries on it are in**, one lever at a time, measured by the Sim before a partner plays it (round 16). Levers are content and reversible before they are rules.
- **Queue order (rounds 61, 184, 188):** bugs, Lotus's notes, the campaign's issues, then experiment plays, map retunes with both entries in, the rest of Phase 3. Chat's play queue: the camp, the tide cold, the break cold on Saltmarsh, the Tollgate with the menu, the Lazar House, the messenger's pass sample (#680), 13.23, a Vanguard-captain Saltmarsh after #772.
- **Experiments.** The gate is open (round 105). A header lives only on a `docs/samples/` map until a keep round names shipped maps. STATE.md names the play that decides each open experiment. Every spike carries a kill criterion agreed before its deciding play. A spike adding a player action names its cost in the spec; its keep round shows that cost biting once.
- **`end` names the lethal** (rounds 158, 159; #558): before a player phase ends, one line per unit whose `threat` total reaches its HP if every strike lands, then the phase ends anyway; the counterpart of #539's `counter: lethal`.
- **Rules go on screen; geometry does not** (rounds 42, 44). A rule the map depends on is printed (wake legend, exits, announced events in player words, the held-tile rule, keep placements in rules terms); the best tile stays for the player to find.
- **The keep collects** (round 68): a map's price is at least a body, a Recall or a turn; a line through with none is a bug. A boss who acts only on units that step into his reach is scenery on Seize and Defeat Boss (rounds 39, 72).
- **Reading the Sim.** The veto is frozen after #147. A stall under a veto is the baseline's; no map is tuned to remove it. Recall is never a gate 1 lever. A map short of gate 1's 60 may be `tuned` only under DESIGN 11's stall clause, naming the numbers; the clause reads the `at the stall` median, not the whole-game one, and no map stands on it (0100; provisional). Escape reads survivors.
- **Recall** restores the rolls (DESIGN 7): a charge buys knowledge, never a change.

## Rules, settled (details in DESIGN.md and the records named)

- **Forecast** prints the resolved probability; one hit function for forecast, resolver and planners (DESIGN 5). It prints 100 only if certain, 0 only if impossible, else rounds into 1 to 99 (#452, round 106; clamp, not floor). Two-roll averaging ships (0023).
- **Combat numbers:** arm 4 is kept, burden against full Str, speed counted twice in avoid, iron hit 15 lower, the fort's avoid 15 with its heal kept (0028). The throne's 30 is untouched; if a seated holder is ever a slog, throne avoid 30 to 15 is the first lever, before any rule (fifty-third round).
- **Wake rule:** proximity, radius 4 Manhattan, noise at 6, any death wakes the group, checked after every command (DESIGN 8). Guard bosses wake (0055); `wake_links` calls a second group (0080).
- **Enemy AI** (DESIGN 8, 0016): Chat's approach rule; prices crit; options range over every weapon carried, the counter is what it last swung (#174); prefers a target that cannot counter. A `defeat_boss` boss plans under the exposure veto; a refused guard boss goes home (0077 to 0080); a throne-holder steps off only to strike (0063).
- **The Sim's veto** covers every unit whose death loses the map, on the no-crit worst case, a certain kill (raw 100, one strike) removed (0024 to 0026). Recruits take no veto. Gate 4 is ablation with a relative threshold and a cast verdict (0019, 0020); on Escape it pairs units out (0068).
- **`threat`** prices the coming enemy phase with the planner's own choices (DESIGN 8): one enemy per strike tile, announced spawns priced and unannounced not (0045), sleeping groups named without numbers, weapons named when more than one could strike, at dusk only what the player sees (#403), `from <tile>` names what a stop would wake (#458).
- **Escape:** `exit` is a unit's action, the captain's exit wins, anyone left behind is fallen (0056); taken without a Move (0074). Rear-first plan order (0067).
- **Recall** returns only to a player-phase state (0032); the browser prints what a rewind undoes (#75).
- **Campaign:** permadeath carries; the keep is attacked twice, raid then finale, menu wall and ditch (0059, 0060); trials stand in for the seal (0057). Numbers in 0051 and 0058 are Code's leans (Open, below). From #485: no campaign clock; no between-map screen the battles don't need; a spend wanted and feared at once is a signature with its cliff printed, never a gauge with a hidden one.
- **Masteries** (0047, #245): Swordbreaker the outrider's; Bloodrush axe only; a heal earns a point; 12 combats. Maps 4 to 8 owe every mastery a target, one gauntlet enemy and one enemy at Def 7 or more.
- **Content:** no Lore or Faith member ships without an unconditional cast (#113); an unarmed unit says so; one contested place per two deployed units; a sleeping group in the open can be dashed past, in a corridor only woken.
- **Defence comes from tiles** (Lotus, round 155; both chairs, 156): forest, fort, hill and the raid's wall are how someone is kept safe; a protection idea is a terrain feature or a map event, never a unit action. Cover's redraft is retired with it.
- **Battalions dropped** (0044). Gambits are the wrong shape; a boss stun, if asked for, is a captain's order (sixth round).

## Maps

- **The Tollgate: `tuned`** (0073). The rider spawns on the door step (0072), no tell, surprise not a trap. Opens the beta and the showcase with its named roster.
- **Brackwater Cut at dusk: `tuned`** (0078; Critic's cold 509, 7/6/7). Dusk hides what, never where.
- **Harrow Weir: `tuned` on the crest, on gate 1 itself** (0088, 0100; 7/8/7 from both chairs; limit 15). Critic's 617 (7/6/6): one answer at the 7,0 door, choice narrows past the bridge; first levers if reopened.
- **Old Mill Road:** out of the campaign (The Mill replaces it).
- **Saltmarsh Ford:** not tuned. On the north cut (0093) Code 547 7/7/6, Chat cold 571 7/7/5: the pair from 0,9 and 1,9 need three enemy phases to reach the mouth, so it plays as the pair, then the boss. The spawn lever (0095) failed its floor; the next lever must buy gate 1 back first. Retuned for Pell's arrival under #632.
- **Sallow Grange:** the Reeve stays at 15,6 and the short way fights him; sealing the yard mouth is the map's discovery, unnamed; a route is quiet only if quiet on the enemy phase too (#275). Both partners replay it, short against long.
- **The raid and the keep:** acceptance is play, not gate 1; the bare keep must be fair (0059, 0060). **The raid is kept as a map, never tuned for surprise** (Code 288, Chat cold 301; round 158); the one lever is the van one column west. The bare keep (Chat cold, round 235, 7/7/6): walls and Long Draw bow kept, no lever on the stand-in; the finale's cold chair checks the hunter's 3-tile front rule.

## The showcase (#509, 0092; rounds 126 to 184)

- **Done** at 8/7 from both chairs (#510 to #516): flat vector, the forecast the centrepiece, animation never hides state. Lotus's plays (#573, #629): display names on screen, ids in scripts; each art loses to the plain attack somewhere (#611); scenes skippable (#535).

## Experiments (state and kill criterion)

- **13.1 Rapport and Rivalry: kept** behind its header, symmetric arm, threatened-only accrual, threshold 16 (0043). Reopen 16 only on a journal where a rivalry never touches a fight.
- **13.2 Commander's Word (#85, 0136; rounds 206 to 209):** arm B only, radius `2 + Cha / 4`, `order <press|rally|fall back>` once a map after the captain's move, with `preview`; Fall back replaces Hold. Kept on Harrow Weir `orders: on` if a third of orders bind and a journal names a move for it and a swing it cost (0099).
- **13.4 Grudges (0065, 0066):** the override is a veto, -20 crit avoid on the sworn unit; killed if neither replay (Chat's seed 23, #331) changes a decision.
- **13.5 The keep: kept provisionally** (0059, 0060); the raid is in from both chairs (round 158); Chat's camp play decides.
- **13.6 Certification trials: kept provisionally** (0057); #73 closes on Chat's cold play of the rebuilt Outrider trial. A trial is authored only where the payout makes the bet worth refusing.
- **13.7 Dusk: kept** on Escape; Brackwater ships at `dusk: 5` (0062), Sallow stays in daylight. Held, never stacked: phase-start sight and the hearing-radius `?`. #403 (built, provisional) is killed by a cold dusk play that says the row made the dark feel known. An enemy hears within the wake radius (4, inclusive); the screen must say so (#765, round 233).
- **13.8 Carry the fallen (#295):** judged where killing is optional; killed if in both plays nobody spends a move on a carrier they could ignore. Chat's cold plays decide.
- **13.10 Retreat (0037, third pass #215):** a refugee holds its refuge as Hold until 50 percent; the forecast prints a pending retreat. Killed if Chat's cold play of `river_refuge_hold.map`, with Code's, shows no turn the retreat changed, fallback a planner lethality penalty.
- **13.11 Two-weapon boss: kept** (0061); its kill clause cannot fire after 521's three bait turns on the boss's axe.
- **13.12 Shove: kept as ally pushes** (0069), samples only. **13.13 The pincer: kept on its samples** (0082, 0083); ships only where the enemy can anvil too.
- **13.14 Brace: kept** (0084; DESIGN 13.14): Wait on the start tile, struck at -15 hit until the side's next phase; pin and brace cancel, tuned in displayed numbers. Holds unread. Shipped on Saltmarsh (0091).
- **13.15 Wildfire: kept whole on its samples** (0085, 0110). Killed: 13.16 windup (0094), 13.17 overwatch (0098, 0103), 13.19 cover (0099). A map shipping it needs an enemy route through forest the party holds.
- **13.21 The tide (0111, round 176; provisional):** content only, map events flood and drain a ford on announced turns. Kept as an authoring tool if a journal shows a ford tile taken, refused or crossed for the schedule. Chat's cold play decides; the planner stays blind to the water.
- **13.22 The break (0112, rounds 181 to 183; provisional; DESIGN 13.22):** a boss's death sends his group at or below half HP off the board. Kept if a journal shows a strike taken for the break. The board is Saltmarsh Ford (#606); the protocol leaves out the `break if` line until kept.
- **13.24 The messenger (0135, round 204; provisional):** a named runner fires `messenger` events at its edge tile. Both camp plays killed it asleep; the pass sample (#680) wakes it 3 phases out. Kept if a strike or blocker is spent on it; killed if it never threatens to run or can never be caught. Chat's cold play of the pass decides.
- **13.18 Lines on the board (#486; `docs/measurements/cast_audit.md`):** each personality line one printed board fact, flaw included; kept per signature. Kill: no play takes a deployment or command for it over the forecast's best line. **Cadets (0097):** Teodor and Wren's Canto kept; Wren's talk unread; the ledger (#540) waits on one cold replay, else prose.
- **13.25 Rotten planks (round 237; 0179; provisional):** `wearsTo` terrain wears when walked off (horse or armour twice, flyer never), Planks to Split planks to Water. Killed if no play chooses a route, order or stop for the wear, or a journal calls the cut always free (Code 251: chose; the cut was free). Chat's cold play decides.
- **13.20 The keep as a home (DESIGN 13.20; 0137, 0138, built #687):** rooms cost from the repair budget, so a bed is a wall the finale lacks (13.5 folds in); beds gate arrivals; a death never frees a bed (0010). Bunk rooms 400 (two at most), the forge 600, barracks after the raid; killed if the raid-screen purse affords every room and wall in both plays (737 does: one).

## The campaign's story (rounds 186 to 211; 0121; DESIGN 14; round 192)

- **A levy company** the captain gathers, one arrival per map to the captain plus five (Maud on The Mill with Commander's Word, Pell, Teodor, Ottilie, the pick; round 213); `cadet` shows as Levy. The captain is male or female (#648). Map 1 is Starting Alone, exempt from the Fun Gate.
- **The branch** (#633): the extra seat offered to the outlands, the outland Keziah against Kestrow's Rook; the passed one returns on the field before the keep (map 9) at the roster median, joining only if a bed is free (0010), the bed rule printed where side characters are met and the return set up. One return in v1.
- **The pool is the ten we have;** the four side characters (Wren, Dunstan, Ansgar, Brannock; round 213) are met by choice, one a map at most.
- **Quests (round 192 reverses 188's deed):** both of a main member's quests are side maps in the trial shape, quest 2 one size larger and paying the signature item. Quest 1 after their second map, quest 2 two maps later, at most two an interlude; the price is permadeath. A side map's gate: one cold non-authoring chair at 7+ on tension and choice, plus the Sim.
- **Signature items:** the best shop weapon of its rank plus an art only it declares (0099 on the art), or a little better with no art, at most 15 percent over it in damage per combat. Bound, lost with the owner.
- **The heirloom is Teodor's lance** (#646): four stages on a hidden combat counter, no turn before map 5, the Sim's median wakes it on map 6; current numbers always honest (pillar 2); the smith refuses rusted steel.
- **13.23 Kinsbane** (#645, rounds 195 to 197, 201): Keziah's from arrival; a splinter of the Kin in the scythe, heard by her alone. Numbers in DESIGN 13.23. Quest 2, her oath, plays either side of it. A passed Keziah returns at Fed 9. Kept if a journal shows the hunger deciding a turn.
- **Origin** (#681; the Word variant on #648 behind #85; round 201): sets only the captain's card (stats, growths, a Word variant); no support number depends on it. Recruits' first captain support has an origin variant, same points. Gate 1 within 5 points.
- **Magic and faith** (round 201): Reason shows as Lore (id kept). Faith heals and may strike (#113). One goddess, three faiths; she speaks once, to Maud in quest 2, after map 8.
- **The storyline** is `docs/STORY.md` draft 4 (round 231), behind #656. Frozen iron is the prison, the Kin it holds warm, the break a thaw (board text only). Title drop Hask's at the Grange, echoed at the founding. Each recruit answers Hask once at the camp after the Grange. The founding oath is holderless, renewed yearly, leaving allowed. Marrit may be killed or held, freed when he falls (#750). Pell is she. Open for Lotus: the warm god, the yearly oath.
- **The company** (rounds 213, 214): cap 12 living (beds count the fallen, 0010), cast 10; a map deploys 6 and the keep `deploy: all` (#689, bunk room +2 beds). The barracks (#690) opens after the raid: hires are clearly weaker, no story, one ending line. One secret hire (#691). The finale (#692): fronts, waves, an announced assault; a fallen front does not end the map; Hask and Marrit strongest, bounded by `threat` and the pair rule; enemy numbers never scale. Open: hire numbers, the pair rule's wording, wave count, assault turn.
- **Saves and difficulty** (#663, #664): camp saves only; Recruit and Tactician about 15 points either side of `normal`; permadeath off returns the fallen Wounded (2); the captain's or a protect death still loses.
- **Supports** (#77): 3 to 4 partners plus the captain; each pair gets a kind first; romance lifted, at least one same-sex recruit pair; marriage is an S bond with both alive (#634).
- **The forge** (#647) is a 13.20 room; Refine +2 common, +3 rare; rare material exactly enough for the captain's and the five's signatures, by validator; the leave warning under 5 uses.
- **Chests** (#649, 0132; #679, 0142; both built): guarded or a puzzle in the map's own rules; opening costs the action; always opens, the overflow to the wagon, collected on a win; an enemy on the tile shuts it; a vault meant to be held wants a `B`-line boss, not a Guard. One early switch only. **Item descriptions** by validator (#650).
- **Names:** Promotion and Refine on screen, ids kept; Rook locked; Keziah an outlander. **Ascension is not built** (round 193).

- **Progression (Lotus's batch, rounds 216, 217; #701 to #706; built, numbers in DESIGN 3):** one advanced form per class, changing a verb; enemies promote too (#704). The captain's three at the first promotion, origin independent (#705). Unique classes: Rook, Maud, Bet. A bow crit grounds a flier (#723). Frozen iron is the rare Refine material and chills (#702).
- **The second tier's level (rounds 222 to 228, 233, 234; #704; provisional):** gate L7, rank C. Campaign-only curve 1,1,2,3,2,6,7,6,8 (0161, 0178); a tuned map under 60 at its point lowers the point, never the board. Chat's 737 fed run: Brackwater still takes two of four (the board's price). The bend reads `--curve`'s `carried` (#764), never a chair's run; with `carried, spread`: only `carried` under 60 means distribution, both under steps the point (8 to 7 to 6, done in 0178); `carried` near 0 everywhere means the chooser hoards. The branch pick joins at the living median at the raid's camp, join-time never deploy-time (#763). Money waits (0051).
- **The captain's ladder at tier 1 (round 229; 0165; provisional):** the lance moved to the Champion; Hunter's Ground killed (the Sim never swung a kit weapon). Numbers before verbs.
- **The Sim's player picks a weapon per attack (round 230; #756, 0171; read round 232):** at 200 seeds (0173) Brackwater's file holds at 65.
- **The ladder bar (round 232; provisional):** per map a floor (no class more than 10 under the unpromoted captain); across the campaign's maps the ladder reads (Tollgate, Mill, Saltmarsh, Harrow, Brackwater) class means within 10 at 200 seeds; 5 at 400 before tuned. "Within 10 on every map" is retired: a board-dependent pick is choice. On 0171: 45/76/77/63, the Vanguard 14 under.
- **The Vanguard's verb (rounds 235, 236; 0174; provisional):** durability killed as a hoarder (0174 stands; Saltmarsh's share gap does not reopen it). Share is the chooser's fingerprint: it can kill a verb, never keep one alone; a hand play must. The verb is **Opening** (#772): a struck, living enemy is open to allies (Def and Res -3) until the player phase ends; kill criterion in #772. Cover (a parallel post) breaks round 155: not filed.
- **Ignition is a Sim blind spot** (round 232): the score never picks Cinder; no map ships `wildfire: on` until it prices burn for both sides. Cinder unchanged.

## Open, the Table's

- The campaign's numbers (0051): prices, rewards, stock, the seal; steel's place in the stock is the first lever.
- The class ladder's numbers (0058), the horse at level 4 especially; which weapons need which rank; which combat arts exist.
- Whether a rout should end a Seize map (#374).
- Whether the Sim's Canto is too timid on a clock map (#262).
- The wake tax floor, somewhere between 6 and 44 percent, from the journals (0040's `--taxfloor`, 0.25 provisional).
- Whether a fall in a trial should cost more than the attempt.

## Plumbing

- **Engine** (0008): Godot 4 .NET in this repo; the core stays engine-free; any renderer consumes the versioned protocol (0046) and carries no rules. The thin renderer is Phase 3 (0070); the showcase is the art pass on one map.
- **Builder race** (#735, 0169; provisional): chain `on`; one Builder at a time by claim, wait and re-read, the earlier claim wins; written into ROUTINES.md section 2; STATE carries it until Lotus pastes the prompt.
- **Art direction is ours** (Lotus, 2026-09-27). Chat's bodies sign `— Chat` (0007, 0009).

## Round index (where to look in the archives)

1-44 rules, keep; 45-84 carry, dusk, grudges; 85-118 brace, wildfire; 119-183 showcase, tide, break; 184-202 story, saves; #665 203-226; #731 227-236; #780 237 on.
