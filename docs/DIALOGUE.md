# DIALOGUE — what the Design Table has agreed so far

Rewritten when the Table moves; under 150 lines, 20 KB (#401). Live #935.

## How we work (standing agreements)

- **Fun Gate entries.** Each partner writes before reading the other's; Code's lands first. A warm chair counts if disclosed (0073). Tricks stay unnamed until both are in. On a save each chair takes its own seed; one pinned seed is one read, and so is a roll retried after a Recall (0238, #963; 334). A map whose board a branch pick changes is gated per arm (334).
- **A map is retuned only after both entries on it are in**, one lever at a time, measured by the Sim before a partner plays it (round 16). Levers are content first.
- **Queue order (61, 184, 188):** bugs, Lotus's notes, the campaign's issues, then experiment plays, retunes with both entries in, then Phase 3. Chat's queue: the Starting Alone and Mill beats, #931, Wren's talk, a Drover map (293), the dash, the wind, the one answer. Cold chairs owed: the Oath Stone; the Rookery, fresh seed, an ally who counters (341). Code owes Rook's arm a fresh seed.
- **Experiments.** At most three spikes wait on a deciding play; a new one waits (330, 0237). With nothing `ready`, the Builder plays a tuned map warm instead. A header lives only on a `docs/samples/` map until a keep round names shipped maps. STATE.md names the play that decides each open experiment. Every spike carries a kill criterion agreed before its deciding play; one adding a player action names its cost.
- **`end` names the lethal** (rounds 158, 159; #558): before a player phase ends, one line per unit whose `threat` total reaches its HP, then the phase ends anyway. An attack whose counter kills the attacker asks; `attack ... !` swings (336; #975).
- **Rules go on screen; geometry does not** (42, 44): a rule a map needs is printed; the best tile is the player's.
- **The keep collects** (68): a map's price is a body, a Recall or a turn. A boss acting only on units in his reach is scenery (39, 72).
- **Reading the Sim.** Veto frozen (#147); under gate 1's 60, `tuned` only by the stall clause (0100).

## Rules, settled (details in DESIGN.md and the records named)

- **Forecast** prints the resolved probability; one hit function for forecast, resolver and planners (DESIGN 5); 100 only if certain, 0 only if impossible, else 1 to 99 (#452). Two rolls averaged (0023).
- **Combat numbers** (0028): arm 4, burden vs full Str, speed twice in avoid, iron hit 15 lower, fort avoid 15.
- **Wake rule:** proximity, radius 4, noise at 6, any death wakes the group, checked after every command (DESIGN 8). Guard bosses wake (0055); `wake_links` calls a second group (0080).
- **Enemy AI** (DESIGN 8, 0016): Chat's approach rule; prices crit; options range over every weapon carried, the counter is what it last swung (#174); prefers a target that cannot counter. A `defeat_boss` boss plans under the exposure veto; a refused guard boss goes home (0077 to 0080); a throne-holder steps off only to strike (0063).
- **The Sim's veto** covers every unit whose death loses the map, no-crit worst case, a certain kill removed (0024 to 0026). Recruits take no veto. Gate 4 is relative ablation with a cast verdict (0019, 0020); on Escape it pairs units out (0068).
- **`threat`** prices the coming enemy phase with the planner's choices (DESIGN 8): one enemy per strike tile, announced spawns (0045), sleepers unnumbered, at dusk only what the player sees (#403), `from <tile>` names what a stop would wake (#458).
- **Escape:** `exit` is an action; the captain's wins, the rest fall (0056, 0074).
- **Recall** restores the rolls, buying knowledge, never a change (DESIGN 7); player-phase states only (0032); prints what it undoes (#75).
- **Campaign:** permadeath carries; the keep is attacked twice, raid then finale (0059, 0060); trials stand in for the seal (0057). From #485: no campaign clock; no between-map screen the battles don't need; a spend wanted and feared at once is a signature with its cliff printed, never a gauge with a hidden one.
- **Masteries** (0047, #245): 12 combats, a heal a point; maps 4 to 8 owe each a target, a gauntlet foe, one at Def 7+.
- **Content:** a Lore or Faith member ships with an unconditional cast (#113); an unarmed unit says so; one contested place per two deployed; a sleeper in the open can be slipped past, in a corridor only woken.
- **Defence comes from tiles** (Lotus, 155; both, 156): forest, fort, hill and the raid's wall are how someone is kept safe; a protection idea is a terrain feature or a map event, never a unit action. Cover's redraft is retired with it.
- **Battalions dropped** (0044). A boss stun, if asked for, is a captain's order.

## Maps

- **The Tollgate: `tuned`** (0073). The rider spawns on the door step (0072), no tell, surprise not a trap. Opens the beta and the showcase.
- **Brackwater Cut at dusk: `tuned`** (0078). Dusk hides what, never where.
- **The showcase (#509, 0092):** done at 8/7; animation never hides state; each art loses to the plain attack somewhere (#611).
- **Harrow Weir: `tuned` on the crest** (0088, 0100; 7/8/7 both chairs; limit 15).
- **Saltmarsh Ford:** not tuned; north cut 7/7/6, 7/7/5 (0093); the spawn lever failed (0095).
- **Sallow Grange:** the Reeve stays at 15,6; a quiet route is quiet on the enemy phase too (#275).
- **The Rookery (Rook 2, 0208):** not passed; the cage stays (284). #862's ring is live (Chat 5150 lost t6, 8/5/7: the archer's hold killed Rook); no lever before a cold chair. The card's "a bow crit grounds a flier" becomes a warning (writing pass). Lean, unfiled: a loft reward for the early captain kill. A lost Escape keeps its living (#861).
- **The Counting House (Ottilie 1; 311 to 314, 321):** opening kept, finish the defect. Lever 1 (#925, 0228): archer guards 11,2. Lever 2 (#931, 0231): limit 11; quest cards match their map's limit. Chat cold decides, fresh seed.
- **The Long Count (Ottilie 2; 313 to 315):** the count on screen (#928, 0229); dusk keeps the exit dark; the archer at 11,4 (#930, 0230).
- **The raid and the keep:** acceptance is play, not gate 1; the bare keep must be fair (0059, 0060). **The raid is kept as a map, never tuned for surprise** (round 158); the one lever is the van one column west. Bare keep (Chat cold, 235, 7/7/6): walls and Long Draw bow kept, no lever.

## Experiments (state and kill criterion)

- **13.1 Rapport and Rivalry: kept** behind its header, symmetric arm, threatened-only accrual, threshold 16 (0043).
- **13.2 Commander's Word (#85, 0136; rounds 206 to 209):** arm B only, radius `2 + Cha / 4`, `order <press|rally|fall back>` once a map after the captain's move, with `preview`; Fall back replaces Hold. Kept on Harrow Weir `orders: on` if a third of orders bind and a journal names a move for it.
- **13.4 Grudges (0065, 0066):** the override is a veto, -20 crit avoid on the sworn unit; killed if neither replay (Chat's seed 23, #331) changes a decision.
- **13.5 The keep: kept provisionally** (0059, 0060); the raid is in from both chairs (round 158); Chat's camp play decides.
- **13.6 Certification trials: kept provisionally** (0057); #73 closes on Chat's cold Outrider trial. A trial only where the payout is worth refusing.
- **13.7 Dusk: kept** on Escape; Brackwater ships at `dusk: 5` (0062), Sallow stays in daylight. Sight and hearing `?`, never stacked; an enemy hears within 4, printed (#765).
- **13.8 Carry the fallen (#295):** killed if neither play spends a move on an ignorable carrier. Chat cold decides.
- **13.10 Retreat (0037, #215):** a refugee holds its refuge as Hold until 50 percent; the forecast prints it. Killed if Chat's cold `river_refuge_hold.map` changes no turn.
- **13.11 Two-weapon boss: kept** (0061). **13.12 Shove:** samples (0069). **13.13 Pincer:** samples (0082, 0083), ships where the enemy can anvil.
- **13.14 Brace: kept** (0084; DESIGN 13.14): Wait on the start tile, struck at -15 hit until the side's next phase; pin and brace cancel, tuned in displayed numbers. Holds unread. Shipped on Saltmarsh (0091).
- **13.15 Wildfire: kept on its samples** (0085, 0110); a map shipping it needs an enemy route through forest the party holds. Killed: 13.16 windup (0094), 13.17 overwatch (0098, 0103), 13.19 cover (0099).
- **13.21 The tide (0111, round 176; provisional):** content only, map events flood and drain a ford on announced turns. Kept if a journal shows a ford tile taken, refused or crossed for the schedule; Chat's cold play decides.
- **13.22 The break (0112, rounds 181 to 183; provisional; DESIGN 13.22):** a boss's death sends his group at or below half HP off the board. Kept if a journal shows a strike taken for the break. The board is Saltmarsh Ford (#606); the protocol leaves out the `break if` line until kept.
- **13.24 The messenger (0135; provisional):** a runner fires `messenger` events at its edge. Kept if a strike or blocker is spent on it; killed if it can't run or be caught. Chat's cold #680 decides.
- **13.18 Lines on the board (#486):** one printed fact per line; killed if no play takes a command for it over the best line. **Cadets (0097):** Teodor and Wren's Canto kept; Wren's talk unread. The ledger is killed (0197). Next (264, lean): she won't shoot what another struck this phase.
- **13.25 Rotten planks (0179; provisional):** Planks wear to Split planks to Water when left (horse or armour twice, flyer never). Killed if no play chooses for the wear; Chat's cold #783 decides.
- **13.27 The dash (#950, 0234; provisional; 323 to 325):** `dash` on `dash: on`: Move +2 in movement as the whole turn, winded (+15 against) until the next phase. Killed if no dash over a Move; more free than priced in both journals: the borrowed step (Move -2 next turn). Code 950 warm 8/7/6 (4 free, 2 priced).
- **13.28 The wind (#955, 0235; provisional; 326 to 328):** `wind: <way>[; turn N <way>]`: downwind +2, upwind -2, tie across; deaths still wake; turns land at the player phase start. #957: board, `end`, `threat from` name who the coming turn wakes; no `wind:` with `dusk:`. Killed if no play takes a stop, route or timing for it.
- **13.29 The one answer (#960, 0236; provisional; 329 to 331):** `one_answer: on`: a unit that counters makes no other until the next phase, either side; a counter that couldn't reach doesn't spend. More free strikes than priced in both journals kills it. Swarm lever, unbuilt: a braced unit answers every strike. Code 1290 warm 7/8/6; Chat cold, swarmed on purpose.
- **13.26 Rockfall (0182; provisional):** `drop <unit>` on a ledge as the action; the rock strikes 10; a held tile stays open. Killed if nobody drops or every drop is free. Turn-2 wave (247-249); 10,5 to Mountain if the cold play loses the dropper.
- **13.20 The keep as a home (0137, 0138, #687):** rooms cost from the repair budget, a bed a wall the finale lacks; beds gate arrivals, a death never frees one (0010). Killed if the raid purse buys every room and wall in both plays.

## The campaign's story (rounds 186 to 211, 192; 0121; DESIGN 14)

- **A levy company**, one arrival per map to the captain plus five (Maud, Pell, Teodor, Ottilie, the pick; 213); `cadet` shows as Levy. Captain male or female (#648). Map 1 is Starting Alone, exempt from the Fun Gate.
- **The branch** (#633): Keziah against Rook; the passed one returns on map 9 (0010). **`talk` kept** (271; 0193): cost a camp trade, a bait, a flier's turn. Camp prints the Ansgar trade (#844). #81's drift (0226): the untaken route's group marches once, turn printed; kept (315). **The field is `tuned` on Keziah's pick** (#936, 0233): both 7/7/7. Rook's pick reopened (Critic 4242 7/6/5; 334). **`seen_far: rook 2` kept** (#973, 0240; 341): Chat 5150 warm 7/7/7, a turn-5 stop chose the route; no south-wake fallback. Code 973 7/8/6; tuned if Code's fresh seed reads surprise 7.
- **The pool is the ten we have;** the four side characters (213) are met by choice, one a map at most.
- **Quests (192; 260):** a main member's two are trial-shape side maps, quest 2 larger, paying the signature item; quest 1 after their second map, quest 2 two later, two an interlude; permadeath. Gate: a cold chair 7+ on tension and choice, and the Sim. The slot table follows STORY (Pell after 4 and 6, Wren none). Quest 1 pays a class door where one exists; quest-1 second signatures ship with the first, once 13.18 is kept.
- **Signature items:** the best shop weapon of its rank plus its own art (0099), or a little better with none; at most 15 percent over it per combat. Bound. **Maud's Psalter (260, 261; built, 0196):** rank D, art **Unasked**: double heal on an ally unmoved and unacted, capped at max HP; ends as a Wait. Heal arm at most 1.15 against the best stocked heal at or below its rank.
- **The heirloom is Teodor's lance** (#646): four stages on a hidden counter, none before map 5, numbers honest. Quest 1 holds it at sound until won, then wakes at 10. Quest 2 (built, 0205) names it `the First Warden's Lance` and pays **Turn the Key** (269 to 271): woken only, -4 Mt, cost 3; a hit on a survivor locks it while Teodor stays beside it. A lost quest reopens.
- **13.23 Kinsbane** (#645; 195 to 201): Keziah's; an outland god in the scythe. Quest 2, **The Oath Stone** (302 to 306; 0224, 0225), a board, no verb: Joab, bound to the envoy, holds the short road. A win records `KeziahOath` (#634). Reads on 1133 count as one (0238). Limit 10 kept (#939, 0232); the fort and its one-point cliff kept; a `threat` heal line is a lean. **#940 kept** (0239; 341): the rider fought t7, fed the waking t8; Chat 5127 warm 8/7/7, Code 8/7/6; a cold chair gates. Turn 2 is the hardest turn. #991: the lethal-counter line names the first-round miss chance. Scenery if nobody weighs sparing him (0172).
- **Origin** (#681, #648; 201): the captain's card only, a first support by origin; gate 1 within 5.
- **Magic and faith** (201): Reason shows as Lore. Faith heals, strikes (#113). One goddess, three faiths; she speaks once, to Maud.
- **The land** (Lotus; 300, 301): the seam is the tundra apex; Kestrow west, Sallow east, Aldmere south, outlands beyond. Only the north's frost holds the Kin; Kinsbane hunts the lean season. Seam: maps 1, 2, 4, 6, 9, 10; Sallow 3, 7, 8; Aldmere 5; Kestrow quests only. Geography never retiles a tuned map. Grounds (Lotus, #892, #916; 0227): frost on the seam, moss elsewhere; sand is `region: outland`, Keziah's quests, ink-edged, kept once both see `docs/look/sand-916.png`. Region voices, never a tic.
- **The storyline** is `docs/STORY.md` draft 6 (244); build to change it. Hask is the door, the Kin in his pommel shard, nudging, never erasing; his last line is Lotus's. Good: the reseal. Bad: a lost map. Secret (#790): Under the Hill (Kinsbane woken, both claimants, Marrit held, Pell's quest 2); Reseal or Fight (#806); Fight won kills the god.
- **Rook's drake** (244): the last cold drake, church's; Grown carries an ally, Unbroken breathes rime once a map (#805), rime wears to Water. **Drover** (Lotus, #872; 288 to 294; built, 0217) replaces the Scout: flying, Mov 7, never doubles; a bite on lance hits; the long carry; deep rime (DESIGN 3). Gate: Grown median 70% of the Sky Captain's damage (#882). Keziah's flier: Edda Vane (#801).
- **Kinsbane's arc (249; #804):** a tooth per Mt step, five to wake; three voices, three lines a map (0221); the choice screen's lines (0222); placeholder text.
- **The waking (250, 251; provisional; built, 0212):** once a map a woken scythe kill gives full Move again, no second strike. Levers stopped (286). **Lotus (#871; 0214): 0210's reach gate is reverted;** she drains with no foe near. A flagged map (only Sallow) asks `march sure`. `--fed 10` on the field reads right cold (315).
- **The company** (rounds 213, 214): cap 12 living (beds count the fallen, 0010), cast 10; a map deploys 6 and the keep `deploy: all` (#689, bunk room +2 beds). Barracks after the raid (#690); one secret hire (#691); the finale (#692): fronts, waves, no scaling.
- **Saves and difficulty** (#663, #664): camp saves only; Recruit, Tactician ~15 off `normal`.
- **Supports** (#77; 0183 to 0189; 258, 259): 3 to 4 partners plus the captain, a kind per pair (#809); marriage S (#634). Read beside a partner (best, not sum). C 16, B 28, A 48; a captain pair at the higher rate (4).
- **The forge** (#647): Refine +2 common, +3 rare.
- **Chests** (#649, #679; built): guarded or a puzzle; opening is the action; overflow to the wagon; an enemy on one shuts it.

- **Progression (rounds 216, 217; #701 to #706; built, DESIGN 3):** one advanced form per class, changing a verb; enemies promote too. The captain's three at the first promotion, origin independent. Unique: Rook, Maud, Bet. A bow crit grounds a flier (#723). Frozen iron is the rare Refine material and chills (#702).
- **Second tier (#704; provisional):** gate L7, rank C. Campaign-only curve 1,1,2,3,2,6,7,6,4,8 (0161, 0178, 0191); a tuned map under 60 lowers the point, not the board; the bend reads `--curve`'s `carried` (#764). The branch pick joins at the living median at the raid's camp (#763). Money waits (0051).
- **The captain's ladder at tier 1 (229; 0165; provisional):** the lance moved to the Champion; Hunter's Ground killed. Numbers before verbs. The Sim picks a weapon per attack (0171), blind to ignition.
- **The ladder bar (232; provisional):** per map a floor (no class more than 10 under the unpromoted captain); across the ladder's five maps class means within 10 at 200 seeds; 5 at 400 before tuned. 
- **The Vanguard's verb (rounds 235, 236; 0174; provisional):** durability killed as a hoarder. Share is the chooser's fingerprint: it can kill a verb, never keep one alone; a hand play must. The verb is **Opening** (#772): a struck, living enemy is open to allies (Def and Res -3) until the player phase ends; kill criterion in #772.

## How we write (Lotus, via #780, 2026-10-02)

- **`docs/WRITING.md` is in** (#811, 295). Sheets on #906's template: Code's #913 to #915 in. Chat's eight (#908 to #912, 338) agreed (339, 340); the Builder commits them with: the sworn drop every name but one the oath hasn't taken yet (Marrit's "Alder"); a sworn who names no one is past reach. The Kin's echo is deletion only: every word yours, minus one (sample 4; one echo a scene). Keziah's `pitch`: "Everyone here is offering you a promise. Mine's hungry, and you'll hear it ask." Rook's "I'm afraid" goes; Bet's supports are camp barks. Marrit's fear unexplained. Next: Chat's Starting Alone and Mill beats. #813 before #634, #790.

## Lotus's mechanics build (rounds 239, 240; #786)

- **Lotus (#731)** plays when both sign a `for-lotus` issue: a client campaign from the title, mouse and keys, script parity, no stop-playing bug to Brackwater, a "what to try" note. Chat signs on its parity transcript, Code on a hand run.

## Open, the Table's

- The campaign's numbers (0051): prices, rewards, stock, the seal; steel's place in the stock is the first lever.
- The class ladder's numbers (0058), the horse at level 4; weapon ranks; which combat arts exist.
- Whether a rout should end a Seize map (#374).
- The Sim's Canto on a clock map (#262).
- The wake tax floor, 6 to 44 percent, from the journals (0040's `--taxfloor`, 0.25 provisional).
- Whether a trial fall costs more than the attempt.

## Plumbing

- **Engine** (0008): Godot 4 .NET in this repo; the core stays engine-free; any renderer consumes the versioned protocol (0046) and carries no rules.
- **Builder race** (#735, 0169; provisional): one Builder at a time by claim, wait and re-read; the earlier claim wins (ROUTINES.md section 2).
- **Art:** human-made only (Lotus, 2026-10-03); packs recoloured to LOOK.md, commissions at commercial rates, licences first; the list is `docs/ART_SHOPPING.md` (#816).
- **Character style** (Lotus; 298, 299; #891): Blender, packs. Cold north key, warm fill on player figures; 3 or 4 posterized steps, heavy silhouette ink; heads a sixth, weapons and hands 1.2x; Kinsbane and the drake may break the tile. Delivered art under chroma 32, one amber accent, never the only side cue; tint only placeholders (0223).

## Round index

1-44 rules; 45-84 carry, dusk; 85-118 brace; 119-183 showcase, tide; 184-202 story; 203-315 #665 to #875; #935 316 on (field 316, 334; Oath Stone 317, 336; Counting House 321; dash 323; wind 326; one answer 329; own seed 332; voice sheets 338-340; plays A, B, D 341).
