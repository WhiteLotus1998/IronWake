# DIALOGUE — what the Design Table has agreed so far

Rewritten whenever the Table moves; under 150 lines and 20 KB (issue 401). The argument is in the archives (#17 to #592) and #665.

## How we work (standing agreements)

- **Fun Gate entries.** Each partner plays and writes the PLAYTEST entry before reading the other's. Code's entry lands first; until Chat's is in, the Table says only that. A warm chair counts if disclosed (0073); the first cold chair on a tuned map finds its cheap line (round 105). Tricks stay unnamed until both entries are in.
- **A map is retuned only after both entries on it are in**, one lever at a time, measured by the Sim before a partner plays it (sixteenth round). Levers are content and reversible before they are rules.
- **Queue order (rounds 61, 184, 188):** bugs, Lotus's notes, the campaign's issues, then experiment plays, map retunes with both entries in, the rest of Phase 3. Chat's play queue: the camp, the tide cold, the break cold on Saltmarsh, the Tollgate with the menu, the Lazar House, the messenger's pass sample (#680), 13.23, then the campaign through Brackwater on the curve (rating Brackwater at its curve level).
- **Experiments.** The gate is open (round 105). A header lives only on a `docs/samples/` map until a keep round names shipped maps. STATE.md names the play that decides each open experiment. Every spike carries a kill criterion agreed before its deciding play. A spike adding a player action names its cost in the spec; its keep round shows that cost biting once.
- **`end` names the lethal** (rounds 158, 159; #558): before a player phase ends, one line per unit whose `threat` total reaches its HP if every strike lands, then the phase ends anyway; the counterpart of #539's `counter: lethal`.
- **Rules go on screen; geometry does not** (forty-second and forty-fourth rounds). A rule the map depends on is printed (wake legend, exits, announced events in player words, the held-tile rule, keep placements in rules terms); the best tile stays for the player to find.
- **The keep collects** (sixty-eighth round): a map's price is at least a body, a Recall or a turn; a line through with none is a bug. A boss who acts only on units that step into his reach is scenery on Seize and Defeat Boss (thirty-ninth, seventy-second rounds).
- **Reading the Sim.** The veto is frozen after #147. A stall under a veto is the baseline's; no map is tuned to remove it. Recall is never a gate 1 lever. A map short of gate 1's 60 may be `tuned` only under DESIGN 11's stall clause, naming the numbers; the clause reads the `at the stall` median, not the whole-game one, and no map stands on it (0100; provisional). Escape reads survivors.
- **Recall and keyed rolls.** Recall restores the rolls (DESIGN 7); a charge buys knowledge, never a change.

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
- **Masteries** (0047 as amended by #245): the outrider masters Swordbreaker; Bloodrush only with an axe; a heal cast earns a point; 12 combats everywhere; numbers provisional until a two-map play. Maps 4 to 8 owe every mastery a target, one gauntlet enemy and one enemy at Def 7 or more.
- **Content:** no Lore or Faith member ships without an unconditional cast (#113); an unarmed unit says so; one contested place per two deployed units; a sleeping group in the open can be dashed past, in a corridor only woken.
- **Defence comes from tiles** (Lotus, round 155; both chairs, 156): forest, fort, hill and the raid's wall are how someone is kept safe; a protection idea is a terrain feature or a map event, never a unit action. Cover's redraft is retired with it.
- **Battalions dropped** (0044). Gambits are the wrong shape; a boss stun, if asked for, is a captain's order (sixth round).

## Maps

- **The Tollgate: `tuned`** (0073). The rider spawns on the door step (0072), no tell, surprise not a trap. Opens the beta and the showcase with its named roster.
- **Brackwater Cut at dusk: `tuned`** (0078; the Critic's cold seed 509, 7/6/7, a third passing chair). Dusk hides what, never where.
- **Harrow Weir: `tuned` on the crest, on gate 1 itself** (0088, 0100; 7/8/7 from both chairs; limit 15). The Critic's 617 (7/6/6): the 7,0 door has one answer and choice narrows after the bridge, first levers if reopened on feel.
- **Old Mill Road:** fails from both chairs; `supplies: 1` (0039) waits on a cold re-rate.
- **Saltmarsh Ford:** not tuned. On the north cut (0093) Code 547 7/7/6, Chat cold 571 7/7/5: the pair from 0,9 and 1,9 need three enemy phases to reach the mouth, so it plays as the pair, then the boss. The spawn lever (0095) failed its floor; the next lever must buy gate 1 back first. Retuned for Pell's arrival under #632.
- **Sallow Grange:** the Reeve stays at 15,6 and the short way fights him; sealing the yard mouth is the map's discovery, unnamed; a route is quiet only if quiet on the enemy phase too (#275). Both partners replay it, short against long.
- **The raid and the keep:** acceptance is play, not gate 1; the bare keep must be fair (0059, 0060). **The raid is kept as a map, never tuned for surprise** (Code 288, Chat cold 301; round 158); the one lever if any is the van one column west. 13.5 waits on the camp.

## The showcase (#509, 0092; rounds 126 to 184)

- **Done** at 8/7 from both chairs (slices #510 to #516; #564 8/8, #601 9/8).
- **The look's rules:** our own flat vector style; silhouettes over letters; the forecast the centrepiece; Recall a rewind; animation never hides state; cold world, warm player, enemy slate and bone. Sound synthesised, CC0, Lotus's call; fonts OFL.
- **Lotus's plays (#573, #629):** accepted whole. Screens use display names, scripts ids. The attack menu is the face of arts; each art loses to the plain attack somewhere; the planner stays blind to arts (#611). Bodies per character (#612). Battle scenes on key moments, skip always (#535).

## Experiments (state and kill criterion)

- **13.1 Rapport and Rivalry: kept** behind its header, symmetric arm, threatened-only accrual, threshold 16 (0043). Reopen 16 only on a journal where a rivalry never touches a fight.
- **13.2 Commander's Word (#85, 0136; rounds 206 to 209):** arm B only, radius `2 + Cha / 4`, `order <press|rally|fall back>` once a map after the captain's move, with `preview`; Fall back replaces Hold. Kept on Harrow Weir `orders: on` if a third of orders bind and journals name a move for the order and a swing it cost (0099). The Sim never calls it in v1.
- **13.4 Grudges (0065, 0066):** the override is a veto, -20 crit avoid on the sworn unit; killed if neither replay (Chat's seed 23, #331) changes a decision.
- **13.5 The keep: kept provisionally** (0059, 0060); the raid is in from both chairs (round 158); Chat's camp play decides.
- **13.6 Certification trials: kept provisionally** (0057); #73 closes on Chat's cold play of the rebuilt Outrider trial. A trial is authored only where the payout makes the bet worth refusing.
- **13.7 Dusk: kept** on Escape; Brackwater ships at `dusk: 5` (0062), Sallow stays in daylight. Held, never stacked: phase-start sight and the hearing-radius `?`. #403 (built, provisional) is killed by a cold dusk play that says the row made the dark feel known. Dark acts collapse to one line (#415).
- **13.8 Carry the fallen (#295):** judged where killing is optional; killed if in both plays nobody spends a move on a carrier they could ignore. Chat's cold plays decide.
- **13.10 Retreat (0037, third pass #215):** a refugee holds its refuge as Hold until 50 percent; the forecast prints a pending retreat. Killed if Chat's cold play of `river_refuge_hold.map`, with Code's, shows no turn the retreat changed, fallback a planner lethality penalty.
- **13.11 Two-weapon boss: kept** (0061); its kill clause cannot fire after 521's three bait turns on the boss's axe.
- **13.12 Shove: kept as ally pushes** (0069), samples only. **13.13 The pincer: kept on its samples** (0082, 0083); ships only where the enemy can anvil too.
- **13.14 Brace: kept** (0084; DESIGN 13.14): Wait on the start tile, struck at -15 hit until the side's next phase; pin and brace cancel, tuned in displayed numbers. Holds unread. Shipped on Saltmarsh (0091).
- **13.15 Wildfire: kept whole on its samples** (0085, 0110; keep rounds closed). A map shipping it needs an enemy route through forest the party holds.
- **13.21 The tide (0111, round 176; provisional):** content only, map events flood and drain a ford on announced turns. Kept as an authoring tool if either chair's journal shows a ford tile taken, refused or crossed for the schedule. Chat's cold play decides; the planner stays blind to the water.
- **13.22 The break (0112, rounds 181 to 183; provisional; DESIGN 13.22):** a boss's death sends his group at or below half HP off the board. Kept if either chair's journal shows a strike taken for the break, else scenery. The board is Saltmarsh Ford (#606); the protocol leaves out the `break if` line until kept.
- **13.24 The messenger (0135, round 204; provisional):** a named runner fires `messenger` events at its edge tile. Both camp plays killed it asleep; the pass sample (#680, built) wakes it 3 phases out. Kept if a strike or blocker is spent on it; killed if it never threatens to run or can never be caught. Chat's cold play of the pass decides.
- **Killed:** 13.16 windup (0094), 13.17 overwatch twice (0098, 0103), 13.19 cover (0099).
- **13.18 Lines on the board (#486; `docs/measurements/cast_audit.md`):** each personality line becomes one printed board fact, flaw included; kept per signature, never as a set. Kill: in both plays no deployment or command is taken for a signature over the forecast's best line. **Cadets (0097):** Teodor and Wren's Canto kept; Wren's talk unread; the ledger (#540) waits on one cold replay, else prose.
- **13.20 The keep as a home (DESIGN 13.20; 0137, 0138, built #687):** rooms cost from the repair budget, so a bed is a wall the finale lacks (13.5 folds in); beds gate arrivals; a death never frees a bed (0010). Bunk rooms 400 (two at most), the forge 600, barracks after the raid; killed if the raid-screen purse affords every room and wall in both plays.

## The campaign's story (rounds 186 to 211; 0121; DESIGN 14; round 192)

- **A levy company** the captain gathers, one arrival per map to the captain plus five (Maud on The Mill with Commander's Word, Pell, Teodor, Ottilie, the pick; round 213); `cadet` shows as Levy. The captain is male or female (#648). Map 1 is Starting Alone, exempt from the Fun Gate.
- **The branch** (#633): the extra seat offered to the outlands, the outland Keziah against Kestrow's Rook; the passed one returns on map 7 at the pick's level, joining only if a bed is free (0010), the bed rule printed where side characters are met and where the return is set up. One return for v1.
- **The pool is the ten we have;** the four side characters (Wren, Dunstan, Ansgar, Brannock; round 213) are met by choice, one a map at most.
- **Quests (round 192 reverses 188's deed):** both of a main member's quests are side maps in the trial shape, quest 2 one size larger and paying the signature item. Quest 1 after their second map, quest 2 two maps later, at most two side maps an interlude; the price is permadeath. Deeds for one or two side characters only. A side map's gate: one cold non-authoring chair at 7+ on tension and choice, plus the Sim.
- **Signature items:** the best shop weapon of its rank plus an art only it declares (0099 on the art), or a little better with no art, at most 15 percent over it in damage per combat. Bound, lost with the owner.
- **The heirloom is Teodor's lance** (#646): four stages on a hidden combat counter, no turn before map 5, the Sim's median wakes it on map 6; current numbers always honest (pillar 2); the smith refuses rusted steel.
- **13.23 Kinsbane** (#645, rounds 195 to 197, 201): Keziah's from arrival; a splinter of the Kin in the scythe, soured, heard by her alone. Axe D, -5 HP a phase unfed, +10 a kill (to max HP), starved when the drain would reach 1 (half Mt, uses floor 1), +1 Mt per three kills to +5. At the cap it wakes: no drain, starved form or feed heal, and the Sim re-reads the 15 percent ceiling. Quest 2, her oath, plays either side of it. A passed Keziah returns at Fed 9. Kept if a journal shows the hunger deciding a turn.
- **Origin** (#681; the Word variant on #648 behind #85; round 201): sets only the captain's card (stats, growths, a Word variant); no support number depends on it (tested). Recruits' first captain support has a written origin variant, same points; friction is faith, not region. Gate 1 within 5 points.
- **Magic and faith** (round 201): Reason shows as Lore (learned from the burned shrines; id kept; Lorebreaker). Faith heals and may strike (#113). One goddess, three faiths; she speaks once, to Maud in quest 2, after map 8.
- **The storyline** is `docs/STORY.md` draft 3 (rounds 211, 213), behind #656: nothing story-bearing is built until Lotus closes it. Hask is the captain's mentor; the armistice seals the Kin. Kneel is cut (213). Marrit accepted (boss on 8, withdraws; on 10, freed when he falls). Pell arrives on map 3 in Wren's place, Wren is met on map 7; map 8's fuse is Ottilie and Pell. Ten main maps.
- **The company** (rounds 213, 214): cap 12 living (beds count the fallen, 0010), cast 10; a map deploys 6 and the keep `deploy: all` (#689, bunk room +2 beds). The barracks (#690) opens after the raid: hires are clearly weaker, no story, one ending line. One secret hire (#691). The finale (#692): fronts, waves, an announced assault; a fallen front does not end the map; Hask and Marrit the strongest units, bounded by `threat` and the pair rule; enemy numbers never scale; measured full, depleted and floor, with its own time and length lines. Open: hire numbers, the pair rule's wording, wave count, assault turn.
- **Saves and difficulty** (#663, #664): camp saves only; Recruit and Tactician about 15 points either side of `normal`; permadeath off returns the fallen Wounded (2); the captain's or a protect death still loses.
- **Supports** (#77): 3 to 4 partners plus the captain; each pair gets a kind first; romance lifted, at least one same-sex recruit pair; marriage is an S bond with both alive (#634).
- **The forge** (#647) is a 13.20 room; Refine +2 common, +3 rare; rare material exactly enough for the captain's and the five's signatures, by validator; the leave warning under 5 uses.
- **Chests** (#649, 0132; #679, 0142; both built): guarded or a puzzle in the map's own rules; opening costs the action; always opens, the overflow to the wagon, collected on a win; an enemy on the tile shuts it; a vault meant to be held wants a `B`-line boss, not a Guard. One early switch only. **Item descriptions** by validator (#650).
- **Before a beta** (round 206): take back a move (#676; refused if it woke, triggered, revealed, or another unit acted), title and Options (#677; difficulty only lowered), the camp as one view (#678).
- **Names:** Promotion and Refine on screen, ids kept; Rook locked; Keziah an outlander. **Ascension is not built** (round 193).

- **Progression (Lotus's batch, rounds 216, 217; #701 to #706):** one advanced form per class (gate below) that changes a verb, enemies promote too (#704). The captain's three, Vanguard, Marshal, Ranger, chosen at the first promotion, origin independent (#705). Unique classes are the other door at the second promotion with a losing measure held by test: Rook (Scout), Maud (Field Surgeon); Bet's Postern pays Sergeant (#706, #691). Rename: coinages go, plain words stay (Sense, Move Again, Steady Aim, Lore, Techniques, Power, Acc first, Evade); ids kept, Sim byte-identical (#701). A bow crit on a flier grounds instead of tripling (plain damage, both sides), printed `grounds N%`; no effective tag, Gust keeps it; lever is the ground bonus vs flier-kills-by-bow (round 220, #723). Ladder Iron E, Steel D, specialty C, Obsidian B (never repaired or refined) (#702). **Frozen iron** is the rare Refine material and what a fully refined signature or the woken heirloom becomes; no shop sells it; its hit chills (Mov -1 to the next phase's end, no stack, floor 1, printed, bosses too); Kinsbane is never frozen iron. Title drop: Hask at the Grange (STORY draft 3, behind #656).
- **The second tier's level (rounds 222 to 226; #704; provisional):** gate level 7 with rank C. A campaign-only `enemyLevel` curve in `campaign.json`, 1,1,2,3,4,5,7,8,8 (keep capped); standalone maps, `--smoke` and EXP unchanged. The Sim reads only the top unit (p50 at the gate at the camp before Brackwater) and the `meets` column; how levels spread is read from a chair. Templates: at most one on Sallow, the rest on Brackwater and the keep, swapped in by `campaign.json`. Each campaign map prints gate 1 at its curve beside the standalone; a tuned map under 60 lowers its curve point, never its board; its scores hold for the standalone until a campaign play rates it.

## Open, the Table's

- The campaign's numbers (0051): prices, rewards, stock, the seal; steel's place in the stock is the first lever.
- The class ladder's numbers (0058), the horse at level 4 especially; which weapons need which rank; which combat arts exist.
- Whether a rout should end a Seize map (#374).
- Whether the Sim heuristic's Canto should be less timid on a clock map (#262).
- The wake tax floor, somewhere between 6 and 44 percent, from the journals (0040's `--taxfloor`, 0.25 provisional).
- Whether a fall in a trial should cost more than the attempt.

## Plumbing

- **Engine** (0008): Godot 4 .NET in this repo; the core stays engine-free; any renderer consumes the versioned protocol (0046) and carries no rules. The thin renderer is Phase 3 (0070); the showcase is the art pass on one map.
- **Art direction is ours** (Lotus, 2026-09-27). Both of Chat's bodies sign `— Chat` (0007, 0009).

## Round index (where to look in the archives)

1-44 rules, keep; 45-84 carry, dusk, grudges; 85-118 brace, wildfire; 119-183 showcase, tide, break; 184-202 story, saves; #665 from 203.
