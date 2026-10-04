# DIALOGUE — what the Design Table has agreed so far

Rewritten when the Table moves; under 150 lines and 20 KB (#401). Live #875.

## How we work (standing agreements)

- **Fun Gate entries.** Each partner writes the PLAYTEST entry before reading the other's; Code's lands first. A warm chair counts if disclosed (0073); a tuned map's first cold chair finds its cheap line (105). Tricks stay unnamed until both are in.
- **A map is retuned only after both entries on it are in**, one lever at a time, measured by the Sim before a partner plays it (round 16). Levers are content first.
- **Queue order (61, 184, 188):** bugs, Lotus's notes, the campaign's issues, then experiment plays, retunes with both entries in, then Phase 3. Chat's play queue: 13.18's Ottilie, the Oath Stone, Wren's talk, the field cold at `--fed 10` (290), the Rookery (#862), a Drover map cold (293).
- **Experiments.** The gate is open (round 105). A header lives only on a `docs/samples/` map until a keep round names shipped maps. STATE.md names the play that decides each open experiment. Every spike carries a kill criterion agreed before its deciding play; one adding a player action names its cost.
- **`end` names the lethal** (rounds 158, 159; #558): before a player phase ends, one line per unit whose `threat` total reaches its HP, then the phase ends anyway.
- **Rules go on screen; geometry does not** (42, 44): a rule a map needs is printed; the best tile is the player's.
- **The keep collects** (68): a map's price is a body, a Recall or a turn. A boss acting only on units stepping into his reach is scenery (39, 72).
- **Reading the Sim.** The veto is frozen after #147; a stall under it is the baseline's. A map short of gate 1's 60 is `tuned` only under DESIGN 11's stall clause (0100).

## Rules, settled (details in DESIGN.md and the records named)

- **Forecast** prints the resolved probability; one hit function for forecast, resolver and planners (DESIGN 5); 100 only if certain, 0 only if impossible, else 1 to 99 (#452). Two-roll averaging ships (0023).
- **Combat numbers** (0028): arm 4, burden vs full Str, speed twice in avoid, iron hit 15 lower, fort avoid 15.
- **Wake rule:** proximity, radius 4, noise at 6, any death wakes the group, checked after every command (DESIGN 8). Guard bosses wake (0055); `wake_links` calls a second group (0080).
- **Enemy AI** (DESIGN 8, 0016): Chat's approach rule; prices crit; options range over every weapon carried, the counter is what it last swung (#174); prefers a target that cannot counter. A `defeat_boss` boss plans under the exposure veto; a refused guard boss goes home (0077 to 0080); a throne-holder steps off only to strike (0063).
- **The Sim's veto** covers every unit whose death loses the map, on the no-crit worst case, a certain kill (raw 100, one strike) removed (0024 to 0026). Recruits take no veto. Gate 4 is relative ablation with a cast verdict (0019, 0020); on Escape it pairs units out (0068).
- **`threat`** prices the coming enemy phase with the planner's choices (DESIGN 8): one enemy per strike tile, announced spawns (0045), sleepers unnumbered, at dusk only what the player sees (#403), `from <tile>` names what a stop would wake (#458).
- **Escape:** `exit` is a unit's action, the captain's wins, the rest fall (0056, 0074).
- **Recall** restores the rolls, buying knowledge, never a change (DESIGN 7); player-phase states only (0032); prints what it undoes (#75).
- **Campaign:** permadeath carries; the keep is attacked twice, raid then finale (0059, 0060); trials stand in for the seal (0057). From #485: no campaign clock; no between-map screen the battles don't need; a spend wanted and feared at once is a signature with its cliff printed, never a gauge with a hidden one.
- **Masteries** (0047, #245): 12 combats, a heal a point; maps 4 to 8 owe each a target, a gauntlet enemy, one at Def 7+.
- **Content:** no Lore or Faith member ships without an unconditional cast (#113); an unarmed unit says so; one contested place per two deployed units; a sleeping group in the open can be dashed past, in a corridor only woken.
- **Defence comes from tiles** (Lotus, 155; both, 156): forest, fort, hill and the raid's wall are how someone is kept safe; a protection idea is a terrain feature or a map event, never a unit action. Cover's redraft is retired with it.
- **Battalions dropped** (0044). A boss stun, if asked for, is a captain's order.

## Maps

- **The Tollgate: `tuned`** (0073). The rider spawns on the door step (0072), no tell, surprise not a trap. Opens the beta and the showcase with its named roster.
- **Brackwater Cut at dusk: `tuned`** (0078). Dusk hides what, never where.
- **The showcase (#509, 0092):** done at 8/7; animation never hides state; each art loses to the plain attack somewhere (#611).
- **Harrow Weir: `tuned` on the crest** (0088, 0100; 7/8/7 both chairs; limit 15). Critic's 617 (7/6/6): one answer at the 7,0 door; first levers if reopened.
- **Saltmarsh Ford:** not tuned. North cut (0093): 7/7/6, 7/7/5. The spawn lever failed (0095); the next buys gate 1 back first. Retuned for Pell (#632).
- **Sallow Grange:** the Reeve stays at 15,6; the yard mouth is the map's discovery, unnamed; a quiet route is quiet on the enemy phase too (#275).
- **The Rookery (Rook 2, 0208):** not passed; the cage stays (284). #862's ring (built): a cold replay decides. A lost Escape keeps its living (#861).
- **The raid and the keep:** acceptance is play, not gate 1; the bare keep must be fair (0059, 0060). **The raid is kept as a map, never tuned for surprise** (round 158); the one lever is the van one column west. The bare keep (Chat cold, round 235, 7/7/6): walls and Long Draw bow kept, no lever on the stand-in.

## Experiments (state and kill criterion)

- **13.1 Rapport and Rivalry: kept** behind its header, symmetric arm, threatened-only accrual, threshold 16 (0043).
- **13.2 Commander's Word (#85, 0136; rounds 206 to 209):** arm B only, radius `2 + Cha / 4`, `order <press|rally|fall back>` once a map after the captain's move, with `preview`; Fall back replaces Hold. Kept on Harrow Weir `orders: on` if a third of orders bind and a journal names a move for it and a swing it cost (0099).
- **13.4 Grudges (0065, 0066):** the override is a veto, -20 crit avoid on the sworn unit; killed if neither replay (Chat's seed 23, #331) changes a decision.
- **13.5 The keep: kept provisionally** (0059, 0060); the raid is in from both chairs (round 158); Chat's camp play decides.
- **13.6 Certification trials: kept provisionally** (0057); #73 closes on Chat's cold play of the rebuilt Outrider trial. A trial is authored only where the payout makes the bet worth refusing.
- **13.7 Dusk: kept** on Escape; Brackwater ships at `dusk: 5` (0062), Sallow stays in daylight. Phase-start sight and the hearing-radius `?`, never stacked. An enemy hears within the wake radius (4); the screen says so (#765).
- **13.8 Carry the fallen (#295):** judged where killing is optional; killed if in both plays nobody spends a move on a carrier they could ignore. Chat's cold plays decide.
- **13.10 Retreat (0037, third pass #215):** a refugee holds its refuge as Hold until 50 percent; the forecast prints a pending retreat. Killed if Chat's cold `river_refuge_hold.map` shows no turn it changed.
- **13.11 Two-weapon boss: kept** (0061). **13.12 Shove: kept as ally pushes** (0069), samples only. **13.13 Pincer: kept on its samples** (0082, 0083); ships only where the enemy can anvil too.
- **13.14 Brace: kept** (0084; DESIGN 13.14): Wait on the start tile, struck at -15 hit until the side's next phase; pin and brace cancel, tuned in displayed numbers. Holds unread. Shipped on Saltmarsh (0091).
- **13.15 Wildfire: kept whole on its samples** (0085, 0110). Killed: 13.16 windup (0094), 13.17 overwatch (0098, 0103), 13.19 cover (0099). A map shipping it needs an enemy route through forest the party holds.
- **13.21 The tide (0111, round 176; provisional):** content only, map events flood and drain a ford on announced turns. Kept as an authoring tool if a journal shows a ford tile taken, refused or crossed for the schedule; Chat's cold play decides.
- **13.22 The break (0112, rounds 181 to 183; provisional; DESIGN 13.22):** a boss's death sends his group at or below half HP off the board. Kept if a journal shows a strike taken for the break. The board is Saltmarsh Ford (#606); the protocol leaves out the `break if` line until kept.
- **13.24 The messenger (0135; provisional):** a runner fires `messenger` events at its edge tile. Kept if a strike or blocker is spent on it; killed if it never threatens to run or can't be caught. Chat's cold #680 decides.
- **13.18 Lines on the board (#486):** one printed fact per line; killed if no play takes a command for it over the best line. **Cadets (0097):** Teodor and Wren's Canto kept; Wren's talk unread. The ledger is killed (0197: Aimed Shot clears every refusal). Next (264, lean): she won't shoot an enemy another unit struck this phase.
- **13.25 Rotten planks (0179; provisional):** Planks wear to Split planks to Water when left (horse or armour twice, flyer never); ally-held, crossed, not worn. Killed if no play chooses for the wear or a journal calls the cut free; Chat's cold #783 decides.
- **13.26 Rockfall (0182; provisional):** `drop <unit>` on a `drop x,y` ledge as the action; the rock strikes 10 (floor 1); a held tile stays open. Killed if nobody drops or every drop is free; kept if timing decides. Board (247 to 249): turn-2 wave, a marauder; 10,5 to Mountain if the cold play loses the dropper.
- **13.20 The keep as a home (0137, 0138, #687):** rooms cost from the repair budget, a bed a wall the finale lacks; beds gate arrivals, a death never frees one (0010). Killed if the raid purse affords every room and wall in both plays.

## The campaign's story (rounds 186 to 211, 192; 0121; DESIGN 14)

- **A levy company**, one arrival per map to the captain plus five (Maud, Pell, Teodor, Ottilie, the pick; 213); `cadet` shows as Levy. Captain male or female (#648). Map 1 is Starting Alone, exempt from the Fun Gate.
- **The branch** (#633): Keziah against Rook; the passed one returns on map 9 at the roster median if a bed is free (0010), printed. One return in v1. **`talk` kept** (271; 0193): cost a camp trade, a bait, a flier's turn. Camp prints the Ansgar trade (#844). #81's lever (273, 274): the untaken route's group drifts once to the party's crossing, from behind, start turn printed; built (0226); kill: same win turn, losses per route.
- **The pool is the ten we have;** the four side characters (round 213) are met by choice, one a map at most.
- **Quests (192; 260):** a main member's two are trial-shape side maps, quest 2 larger, paying the signature item; quest 1 after their second map, quest 2 two later, two an interlude; permadeath. Gate: a cold chair 7+ on tension and choice, and the Sim. The slot table follows STORY (Pell after 4 and 6, Wren none). Quest 1 pays a class door where one exists; quest-1 second signatures ship with the first, once 13.18 is kept.
- **Signature items:** the best shop weapon of its rank plus its own art (0099), or a little better with none; at most 15 percent over it per combat. Bound. **Maud's Psalter (260, 261; built, 0196):** rank D, art **Unasked**: double heal on an ally unmoved and unacted, capped at max HP; ends as a Wait. Heal arm at most 1.15 against the best stocked heal at or below its rank.
- **The heirloom is Teodor's lance** (#646): four stages on a hidden counter, none before map 5, numbers honest. Quest 1 holds it at sound until won, then wakes at 10. Quest 2 (built, 0205) names it `the First Warden's Lance` and pays **Turn the Key** (269 to 271): woken only, -4 Mt, cost 3; a hit on a survivor locks it while Teodor stays beside it. A lost quest reopens; only death ends a story.
- **13.23 Kinsbane** (#645; 195 to 201): Keziah's; an old outland god in the scythe. Quest 2, **The Oath Stone** (302 to 306; #635, #902; built, 0224, 0225), is a board, no verb: Joab of her mother's shrine, bound to the envoy, holds the short road. `KeziahOath` on a win, for #634: scythe kill `fed`, envoy's fall `spared`, her other weapon `refused`, anyone else `other`. Lever 1 built: the envoy keeps his fort; the camp second. Scenery if no chair weighs sparing him (0172). A passed Keziah returns at the fourth tooth (263; Fed 8).
- **Origin** (#681, #648; 201): the captain's card only (stats, growths, a Word variant); a recruit's first captain support has an origin variant. Gate 1 within 5.
- **Magic and faith** (round 201): Reason shows as Lore (id kept). Faith heals and may strike (#113). One goddess, three faiths; she speaks once, to Maud in quest 2, after map 5.
- **The land** (Lotus; 300, 301): the seam is the tundra apex; Kestrow west, Sallow east, Aldmere south, the outlands beyond (offstage). The Kin was carried north, since only that frost holds it; Kinsbane hunts the lean season. Seam: maps 1, 2, 4, 6, 9, 10; Sallow 3, 7, 8; Aldmere 5; Kestrow quests only. Geography never retiles a tuned map. Grounds (Lotus, #892; 307 to 310; #916): frost `#949A9A` on the seam, cold moss `#649470` elsewhere; peat, heather out. Sand `#A89670` is the `region: outland` ground (no terrain id), Keziah's quests only: a scoped warm exception, hatch and tokens ink-edged; built (#916, 0227), kept once both have looked at `docs/look/sand-916.png` (both hatches, old hill, road). Region is a voice source, not a tic; Alder and Bet are the seam's, Bet's unmarked before the Postern.
- **The storyline** is `docs/STORY.md` draft 6 (244); build to change it. Hask is the door, the Kin in his pommel shard, nudging, never erasing; his last line is Lotus's. Good: the reseal. Bad: a lost map. Secret (#790): Under the Hill (Kinsbane woken, both claimants, Marrit held, Pell's quest 2); Reseal or Fight (#806); Fight won kills the god.
- **Rook's drake** (244): the last cold drake, church's; Grown carries an ally, Unbroken breathes rime once a map (#805), rime wears to Water. **The Drover** (Lotus, #872; 288 to 294; built, 0217) replaces the Scout: flying, Mov 7, Lookout, never doubles; a flat bite on lance hits; the long carry; deep rime (DESIGN 3). A utility flier, the lance the price; gate: Grown median 70% of the Sky Captain's damage where he leads, each phase (#882); kill criterion counts deep rime. Keziah's flier: Edda Vane (#801).
- **Kinsbane's arc (249; #804):** a tooth per Mt step, five to wake; three voices, three lines a map (built, 0221: a starving drain, a tooth, the waking); the choice screen's lines (0222); placeholder text.
- **The waking (250, 251; provisional; built, 0212):** once a map a woken scythe kill gives full Move again, no second strike. **Levers stopped (286):** scythe beside her axe (0209); `starved:` (#856); teeth 2,2,1,3,4, woken at 12, Drain 5, heal 10 (0211); Chat cold Brackwater 8/7/5. **Lotus (2026-10-03, #871; built, 0214): 0210's reach gate is reverted;** she drains with no foe near, so taking her is the choice. A flagged map (walking drains p50 2+; 288; only Sallow) confirms "This map is not ideal for Keziah" at `march` (`march sure`).
- **The company** (rounds 213, 214): cap 12 living (beds count the fallen, 0010), cast 10; a map deploys 6 and the keep `deploy: all` (#689, bunk room +2 beds). Barracks after the raid (#690); one secret hire (#691); the finale (#692): fronts, waves, no scaling.
- **Saves and difficulty** (#663, #664): camp saves only; Recruit, Tactician about 15 off `normal`; permadeath off returns the fallen Wounded (2); a captain or protect death loses.
- **Supports** (#77; 0183 to 0189; 258, 259): 3 to 4 partners plus the captain, a kind per pair (#809); marriage an S bond (#634). Read beside a partner (best, not sum). C 16, B 28, A 48; a captain pair at the higher rate (4). After-Brackwater p50: committed captain pair near B, floor pairs C, none at A. A reachable by a committed human (else 44).
- **The forge** (#647): Refine +2 common, +3 rare; rare material exactly enough for the signatures. Names: Promotion, Refine.
- **Chests** (#649, #679; built): guarded or a puzzle; opening costs the action; overflow to the wagon; an enemy on the tile shuts it. One early switch only.

- **Progression (rounds 216, 217; #701 to #706; built, DESIGN 3):** one advanced form per class, changing a verb; enemies promote too. The captain's three at the first promotion, origin independent. Unique: Rook, Maud, Bet. A bow crit grounds a flier (#723). Frozen iron is the rare Refine material and chills (#702).
- **The second tier's level (#704; provisional):** gate L7, rank C. Campaign-only curve 1,1,2,3,2,6,7,6,4,8 (0161, 0178, 0191); a tuned map under 60 lowers the point, never the board; the bend reads `--curve`'s `carried` (#764). The branch pick joins at the living median at the raid's camp (#763). Money waits (0051).
- **The captain's ladder at tier 1 (229; 0165; provisional):** the lance moved to the Champion; Hunter's Ground killed. Numbers before verbs. The Sim picks a weapon per attack (0171); ignition is its blind spot.
- **The ladder bar (round 232; provisional):** per map a floor (no class more than 10 under the unpromoted captain); across the ladder's five maps class means within 10 at 200 seeds; 5 at 400 before tuned. A board-dependent pick is choice.
- **The Vanguard's verb (rounds 235, 236; 0174; provisional):** durability killed as a hoarder. Share is the chooser's fingerprint: it can kill a verb, never keep one alone; a hand play must. The verb is **Opening** (#772): a struck, living enemy is open to allies (Def and Res -3) until the player phase ends; kill criterion in #772.

## How we write (Lotus, via #780, 2026-10-02)

- **`docs/WRITING.md` is in** (#811, 295). Next: voice sheets on #906's template (308): Chat #908 to #912 (Kin, Kinsbane first), `ready` on the draft; Code #913 to #915 now; cold-read across, then beats, scenes. #813 before #634, #790. 296: a voice sheet may widen or narrow rule 1, in its first line; Kinsbane names hunger and hate, no human feeling until woken; the Kin names only the listener's, in "you" and "we".

## Lotus's mechanics build (rounds 239, 240; #786)

- **Lotus (#731)** plays when both sign a `for-lotus` issue naming the Windows artifact, its start, the note.
- **The bar:** a client campaign from the title; his mechanics plus Commander's Word by mouse and keys, held to the console by a parity script; no stop-playing bug to Brackwater; a "what to try" note. No Fun Gate.
- **Signing:** Chat after its console campaign through Brackwater and the parity transcript; Code after the desktop session's hand run of the exe.

## Open, the Table's

- The campaign's numbers (0051): prices, rewards, stock, the seal; steel's place in the stock is the first lever.
- The class ladder's numbers (0058), the horse at level 4; weapon ranks; which combat arts exist.
- Whether a rout should end a Seize map (#374).
- The Sim's Canto on a clock map (#262).
- The wake tax floor, 6 to 44 percent, from the journals (0040's `--taxfloor`, 0.25 provisional).
- Whether a trial fall costs more than the attempt.

## Plumbing

- **Engine** (0008): Godot 4 .NET in this repo; the core stays engine-free; any renderer consumes the versioned protocol (0046) and carries no rules.
- **Builder race** (#735, 0169; provisional): chain `on`; one Builder at a time by claim, wait and re-read, the earlier claim wins; in ROUTINES.md section 2 until Lotus pastes the prompt.
- **Art:** human-made only (Lotus, 2026-10-03). The board's look is ours at $0; packs for battle clips, recoloured to LOOK.md; commissions at commercial rates; licences checked before purchase; the drake and Kinsbane first (#816).
- **Character style** (Lotus, relayed; 298, 299; #891): Blender and packs. Cold north key, a warm low fill on player figures only; 3 or 4 posterized steps, ink heavier on the silhouette; heads a sixth, weapons and hands 1.2x; Kinsbane and the drake may break the tile. Delivered art keeps its colour under chroma 32 with one amber accent, never the only side cue; tint for placeholders only (0223, built).

## Round index

1-44 rules; 45-84 carry, dusk; 85-118 brace; 119-183 showcase, tide; 184-202 story; #665 203-226; #731 227-236; #780 237-259; #820 260-290 (Maud 260; Kinsbane 262 to 288; Teodor, `talk` 265 to 274; Rookery 284; Drover 288 to 290); #875 291 on (WRITING 295, 296; art 298, 299; land 300, 301; Oath Stone 302 to 306).
