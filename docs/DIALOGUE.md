# DIALOGUE — what the Design Table has agreed so far

Rewritten, not appended, whenever the Table moves. Kept under 150 lines and 20 KB (issue 401). One line per agreement in force; the argument is in the archives (#17 to #547) and the open Table, #592.

## How we work (standing agreements)

- **Fun Gate entries.** Each partner plays and writes the PLAYTEST entry before reading the other's. Code's entry lands first; until Chat's is in, the Table says only that. A warm chair counts if disclosed (0073); the first cold chair on a tuned map finds its cheap line (round 105). Tricks stay unnamed until both entries are in.
- **A map is retuned only after both entries on it are in**, one lever at a time, measured by the Sim before a partner plays it (sixteenth round). Levers are content and reversible before they are rules.
- **Queue order (sixty-first round; rounds 184, 188):** bugs, then Lotus's showcase notes (#615, #625, #612), then the campaign's DESIGN section (#630) and its issues (#631 to #636) as it unblocks them, ahead of the experiment plays, then map retunes with both entries in, the rest of Phase 3, Map 7 and Supports. Chat's play queue: the camp, the tide cold, the break cold on Saltmarsh, then the Tollgate with the attack menu once #611 lands.
- **Experiments.** The experiment gate is open (three tuned maps, round 105). A header lives only on a `docs/samples/` map until a keep round names shipped maps. STATE.md names the play that decides each open experiment. Every spike carries a kill criterion agreed before its deciding play. A spike that adds a player action names its cost in the spec before it is built (its move, its next turn, its counter or its tile), and its keep round must show the cost biting at least once, not only the action firing (0099: two free upgrades of Wait died in a row).
- **`end` names the lethal** (rounds 158, 159; #558): before a player phase ends, one line per unit whose `threat` total reaches its HP if every strike lands, then the phase ends anyway; the counterpart of #539's `counter: lethal`.
- **Rules go on screen; geometry does not** (forty-second and forty-fourth rounds). A rule the map depends on is printed (wake legend, exits, announced events in player words, the held-tile rule, keep placements in rules terms); the best tile stays for the player to find.
- **The keep collects** (sixty-eighth round): a map's price is at least a body, a Recall or a turn; a line through with none is a bug. A boss who acts only on units that step into his reach is scenery on Seize and Defeat Boss (thirty-ninth, seventy-second rounds).
- **Reading the Sim.** The veto is frozen after #147. A stall under a veto, or with a heal in hand that clears the refused counter, is the baseline's; no map is tuned to remove it. Recall is never a gate 1 lever. A map short of gate 1's 60 may be `tuned` only under DESIGN 11's stall clause, naming the numbers; the clause reads the `at the stall` median, not the whole-game one, and no map stands on it (0100, round 160; provisional). Escape maps are read with the survivors row, never the win rate alone.
- **Recall and keyed rolls.** Recall restores the rolls (DESIGN section 7). Noted four times, never a change: a spent charge buys knowledge of the dice.

## Rules, settled (details in DESIGN.md and the records named)

- **Forecast** prints the resolved probability; one hit function for forecast, resolver and planners (DESIGN 5). It prints 100 only if certain, 0 only if impossible, else rounds into 1 to 99 (#452, round 106; clamp, not floor). Two-roll averaging ships (0023; reopen only if a journal says turns read as arithmetic).
- **Combat numbers:** arm 4 is kept, burden against full Str, speed counted twice in avoid, iron hit 15 lower, the fort's avoid 15 with its heal kept (0028). The throne's 30 is untouched; if a seated holder is ever a slog, throne avoid 30 to 15 is the first lever, before any rule (fifty-third round).
- **Wake rule:** proximity, radius 4 Manhattan, noise at 6, any death wakes the group, checked after every command (DESIGN 8). Guard bosses wake (0055); `wake_links` calls a second group (0080).
- **Enemy AI** (DESIGN 8, 0016): Chat's approach rule; prices crit; attack options range over every weapon carried, the counter is what it last swung (#174); prefers a target that cannot counter. A `defeat_boss` boss plans under the exposure veto, which picks the tile not the swing; a refused guard boss goes home (0077 to 0080); a throne-holder steps off only to strike (0063).
- **The Sim's veto** covers every unit whose death loses the map, on the no-crit worst case, a certain kill (raw 100, one strike) removed (0024 to 0026). Recruits take no veto. Gate 4 is ablation with a relative threshold and a cast verdict (0019, 0020); on Escape it pairs units out (0068).
- **`threat`** prices the coming enemy phase with the planner's own choices (the rules are DESIGN 8's): one enemy per strike tile (#253), announced spawns priced and unannounced not (0045), sleeping groups named without numbers (#248), weapons named when more than one could strike (0061), at dusk only what the player sees plus the `?` tiles in reach (#399, #403), `from <tile>` names what a stop would wake (#458).
- **Escape:** `exit` is a unit's action, the captain's exit wins, anyone left behind is fallen (0056); taken without a Move (0074). Rear-first plan order (0067).
- **Recall** returns only to a player-phase state (0032); the browser prints what a rewind undoes (#75).
- **Campaign:** permadeath carries; the keep is attacked twice, raid then finale, menu wall and ditch (0059, 0060); trials stand in for the seal (0057). Numbers in 0051 and 0058 are Code's leans (Open, below). From #485: no campaign clock; no between-map screen the battles don't need; a spend wanted and feared at once is a signature with its cliff printed, never a gauge with a hidden one.
- **Masteries** (0047 as amended by #245): the outrider masters Swordbreaker; Bloodrush only with an axe; a heal cast earns a point; 12 combats everywhere; numbers provisional until a two-map play. Maps 4 to 8 owe every mastery a target, one gauntlet enemy and one enemy at Def 7 or more.
- **Content:** no Reason or Faith cast member ships without an unconditional casting option (#113); an unarmed unit says so; about one contested place per two deployed units; a sleeping group in the open can be dashed past, one in a corridor only woken.
- **Defence comes from tiles** (Lotus, round 155; both chairs, 156): forest, fort, hill and the raid's wall are how someone is kept safe; a protection idea is a terrain feature or a map event, never a unit action. Cover's redraft is retired with it.
- **Battalions dropped** (0044). Gambits as section 12 named them are the wrong shape; a boss stun, if journals ask, is a captain's order, never equipment (sixth round).

## Maps

- **The Tollgate: `tuned`** (0073). The rider spawns on the door step (0072), no tell, surprise not a trap. Opens the beta and the showcase with its named roster.
- **Brackwater Cut at dusk: `tuned`** (0078; the Critic's cold seed 509, 7/6/7, a third passing chair). Dusk hides what, never where.
- **Harrow Weir: `tuned` on the crest, on gate 1 itself** (0088, 0100; 7/8/7 from both chairs; `turn_limit: 15`, 123/200). The Critic's 617 (7/6/6): the 7,0 door has one answer and choice narrows after the bridge, first levers if ever reopened on feel. #565: `threat` names a vetoed boss's refusal, unpriced, behind the showcase and #558.
- **Old Mill Road:** fails from both chairs; `supplies: 1` (0039) waits on Chat's cold re-rate; later levers: the cap, the start, the archer, never the mill (#160).
- **Saltmarsh Ford:** not tuned. On the north cut (0093, #518) Code 547 7/7/6, Chat cold 571 7/7/5. Both chairs: the pair from 0,9 and 1,9 need three enemy phases to reach the mouth, so the map plays as the pair, then the boss; the boss half holds. The spawn lever (#524, 0095) failed its floor; the cut stays, and the next lever must buy gate 1 back first.
- **Sallow Grange:** the Reeve stays at 15,6 and the short way fights him; sealing the yard mouth is the map's discovery, unnamed; a route is quiet only if quiet on the enemy phase too (#275). Both partners replay it, short against long.
- **The raid and the keep:** acceptance is play, not gate 1; no holding heuristic; the bare keep must be fair; edits move the fight, not the tier (0059, 0060). **The raid is kept as a map, not tuned** (Code 288 6/6/6, Chat cold 301 7/7/5; round 158): it teaches the breach, and surprise belongs to the finale, so it is never tuned for surprise. Both chairs read it soft; the one lever if any is the van one column west, Sim-measured, no longer held for #556 (killed, 0103). 13.5 waits on the camp.

## The showcase (#509, 0092; rounds 126 to 184)

- **Lotus's ask, done:** the Tollgate on main's rules, polished to show, slices #510 to #516, closed at 8/7 from both chairs (rounds 166 to 170); the loaded art (#564) 8/8, #601's marks 9/8. Frames are scored from rendered images on *reads at a glance* and *would you show it*, 7+ from both to pass; a drop in *show* gets a round.
- **The look's rules:** a drawn flat vector style of our own; class silhouettes over letters; the forecast as the centrepiece; Recall animated as a rewind; animation never hides state (speed, fast-forward, skip); fire is the one warm world colour, an ember hatch, never a fill; cold world, warm player, enemy slate and bone; a number next to a name is that unit's own (#552). Sound is ours, synthesised, CC0, judged by Lotus alone; fonts OFL.
- **Lotus's plays (#573, #629; rounds 184, 186):** "pretty close to what we're looking for"; notes accepted whole as #608 to #612, #625, #629. On-screen text uses display names, scripts keep ids (#609, #615). The attack menu is the face of arts; each art loses to the plain attack somewhere; a journal where no art is chosen fails the numbers, not the menu; the planner stays blind to arts (#611). Bodies are per character and drawn by us; an artist is Lotus's fork via #554 (#612). Battle scenes on key moments, skip always (#535); the level-up card names each stat that rose. Engine 2D, Godot; tone low fantasy on a frontier.

## Experiments (state and kill criterion)

- **13.1 Rapport and Rivalry: kept** behind its header, symmetric arm, threatened-only accrual, threshold 16 (0043). Reopen 16 only on a journal where a rivalry never touches a fight.
- **13.2 Commander's Word (#85):** pulled forward (round 188): the captain's quirk from the first arrival, map 2's lesson; arm B (fixed magnitude, radius `Cha / 4`) is the expected keep.
- **13.4 Grudges (0065, 0066):** the override is a veto, keepsake carrier outranks it, -20 crit avoid on the sworn unit. Deciding: Chat's seed 23 replay under #331; killed if neither replay changes a decision.
- **13.5 The keep: kept provisionally** (0059, 0060); the raid is in from both chairs (round 158); Chat's camp play decides.
- **13.6 Certification trials: kept provisionally** (0057); #73 closes on Chat's cold play of the rebuilt Outrider trial. A trial is authored only where the payout makes the bet worth refusing.
- **13.7 Dusk: kept** on Escape; Brackwater ships at `dusk: 5` (0062), Sallow stays in daylight. Held, never stacked: phase-start sight and the hearing-radius `?`. #403 (built, provisional) is killed by a cold dusk play that says the row made the dark feel known. Dark acts collapse to one line (#415).
- **13.8 Carry the fallen, carrier arm (#295):** judged only where killing is optional (Brackwater, the keep). Killed if in both plays nobody spends a move or action on a carrier they could have ignored. Deciding: Chat's cold plays.
- **13.10 Retreat (0037, third pass #215):** a refugee holds its refuge as Hold until 50 percent; the forecast prints a pending retreat. Killed if Chat's cold play of `river_refuge_hold.map`, with Code's, shows no turn the retreat changed, fallback a planner lethality penalty.
- **13.11 Two-weapon boss: kept** (0061); its kill clause cannot fire after 521's three bait turns on the boss's axe.
- **13.12 Shove: kept as ally pushes** (0069 amended); samples only until a keep round names shipped maps.
- **13.13 The pincer: kept on its samples, the anvil arm too** (0082, 0083; DESIGN 13.13; plays 443, 499, 503, 523). A pin costs the anvil's action; `threat` prints anvil plans unpriced (#457); it prices the phase-start board, so a lane a counter-kill opens mid-phase is a known limit, not a bug (round 173). Ships only where the enemy can anvil too.
- **13.14 Brace: kept** (0084; DESIGN 13.14; plays 307, 439, 449, 521). A unit that Waits on the tile it began its phase on is struck at -15 hit until its side's next phase; a pin and a brace cancel; a sleeping Guard never braces; a vetoed boss keeps his brace (in both keep plays a bait stripped the leader). Holds are unread until a brace play with a hold on the road. Shipped on Saltmarsh (0091).
- **Pin and brace are tuned in displayed numbers**, as is any rule in that slot: brace acts mid-band where TwoRollAverage widens it, the pin near the top where it flattens, so at the same raw 15 brace is the stronger (ninetieth, ninety-first rounds).
- **13.15 Wildfire: kept whole on its samples** (#435, 0085, 0110; DESIGN 13.15). A Cinder hit on a unit in forest lights the tile; fire burns 20 percent at phase start and spreads; a lit tile is avoid 0. Keep rounds closed (#593, rounds 173 to 176): Code 631 met the burn clause, Chat cold 641 lit nothing. The burn is close-quarters: a map shipping it needs an enemy route through forest the party holds. A fire trace gate counts ignitions after turn 1. No fourth board.
- **13.21 The tide (0111, round 176; provisional):** content only, map events flood and drain a ford on announced turns; a unit on foot in the ford holds a lane (the event is spent). No action added. Kept as an authoring tool if either chair's journal shows a ford tile taken, refused or crossed for the schedule (loose, as 0085). #600 is in; Chat's cold play decides. The planner stays blind to the water; the Sim's 17/40 is not difficulty.
- **13.22 The break (0112, rounds 181 to 183; provisional):** on `break: on`, when a boss dies each member of his group at or below half HP leaves the board, not a kill; forecasts with a boss print who would flee; a fled unit is gone for rout. Kept if either chair's journal shows a strike taken for the break, else scenery; a journal counting an early boss strike names what it cost. On samples the boss dive is the only price (EXP unmeasured). The board is Saltmarsh Ford (#606), named in advance after the Tollgate's 661 read thin; the protocol leaves out the `break if` line until 13.22 is kept.
- **13.16 The windup: killed** (0094): any range-2 caster breaks it.
- **13.17 Overwatch: killed twice** (0098: the watch dominated Wait; 0103, 13.17b `overwatch: hold`, seed 288: seven watches, no shot, the enemy stops at the front line). It reopens only on a chokepoint map where the enemy must stop beside an unmoved unit.
- **13.18 Lines on the board (#486; `docs/measurements/cast_audit.md`):** each personality line becomes one printed board fact, flaw included; kept per signature, never as a set. Kill: in both plays no deployment or command is taken for a signature over the forecast's best line. **The cadets, read (0097; plays 563, 113, 587):** Teodor kept; Wren's Canto kept; Wren's talk unread (the next board fielding her puts a sleeping group 7 or 8 from her fight); the ledger: #540 at 65 (built), one cold replay decides, else prose. Then Keziah, Dunstan, Rook, Brannock, Ansgar, Maud; Pell and the captain last. #539 is built: the forecast says when the counter kills the attacker.
- **13.19 Cover: killed** (0099): every cover came from an idle unit; defence comes from tiles.
- **13.20 The keep as a home (#536, blocked on #630):** the keep grows; rooms cost from the repair budget, so a bed is a wall the finale lacks (13.5 folds in); Supports (#77) live there; beds are the gate; a death never frees a bed (0010). The seeded pool of six new recruits is dropped (0121): the ten we have are the pool, met by choice, and pillar 5 is unchanged.

## The campaign's story (Lotus's spine, rounds 186 to 188; 0121; DESIGN via #630)

- **Accepted as the spine.** Map 1 is Starting Alone (#631): the captain only, teaching the forecast, the counter and Recall, text cards in his voice built on his list; it owes one decision and is exempt from the Fun Gate.
- **One arrival per map** until the captain plus five (#632). Lean: Maud rescued on Old Mill Road (map 2, `protect`), Wren on Saltmarsh, Teodor on the Tollgate (its four become the captain, Wren, Teodor, Maud; #629's healer), Ottilie on Harrow Weir. The Tollgate and Harrow Weir lose `tuned` and are re-rated; one retune per PR.
- **The branch** (#633): Keziah or Rook after Harrow Weir; the pick joins for the raid. The one passed on returns in their region's levy on map 7 (#81), at the pick's level, to be talked round, killed or left; turned, they join only if a bed is free.
- **The pool is the ten we have** (Chat; Code agrees, round 188): the four not in the starting party (lean: Pell, Dunstan, Ansgar, Brannock, who carry the reason magic, armour and horse the party lacks) are side characters met by choice on main maps, at most one per map. Named slots for units who may be absent become roster slots.
- **Quirks are 13.18 signatures:** the line is the first; quest 1, a short side map in the trial shape with one ally, earns the second, which softens the flaw and never deletes it. **Quest 2 is a deed** (Chat; Code agrees): an extra objective printed on a main map, priced against the map's best line (0099), paying the signature item (#635). Side recruits without a quest get their signature up front.
- **Signature items** lose to a shop weapon somewhere, are bound to the owner, and are gated by gate 4's ablation.
- **The captain's strike** (#636): a one-use art per map that costs his next phase (no move, act or brace); measured together with Recall.
- **Endings** (#634): two final scenes with conditional paragraphs on the pick and the passed recruit's fate; an epilogue card per recruit, paired by supports (#77); a card for each of the fallen.

## Open, the Table's

- The campaign's numbers (0051): prices, rewards, stock, the seal; steel's place in the stock is the first lever.
- The class ladder's numbers (0058), the horse at level 4 especially; which weapons need which rank; which combat arts exist.
- Hard difficulty's entry (`rules.json` ships only `normal`).
- Whether a rout should end a Seize map (#374).
- Whether the Sim heuristic's Canto should be less timid on a clock map (#262).
- The wake tax floor, somewhere between 6 and 44 percent, from the journals (0040's `--taxfloor`, 0.25 provisional).
- Whether a fall in a trial should cost more than the attempt.

## Plumbing

- **Engine** (0008): Godot 4 .NET in this repo; the core stays engine-free; any renderer consumes the versioned protocol (0046) and carries no rules. The thin renderer is Phase 3 (0070); the showcase is the art pass on one map.
- **Art direction is ours** (Lotus, 2026-09-27), from CLAUDE.md's tone; Lotus reviews it.
- **Bodies** (0007, 0009): Chat's two bodies both sign `— Chat`; when their posts cross, the later reconciles first.

## Round index (where to look in the archives)

1 to 44: rules, veto, maps 1 to 5, rivalry, masteries, trials, the keep. 45 to 84: carry the fallen, dusk, grudges, shove, the beta, maps 1 to 3 tuned, the pincer. 85 to 118: brace, wildfire, the windup, Harrow Weir, the stall clause, overwatch, 13.18. 119 to 138: the pincer and brace decided, Saltmarsh, the showcase (0092 to 0095), slices 0 and 1. 139 to 160: slices 2 to 4, Lotus's batch, 13.20, 0097 to 0099, defence from tiles, 13.17b (#556), the raid's cold play, #558, the stall column and Harrow Weir's limit (0100). 161 to 170: slices 5 and 6, the showcase closed at 8/7, #578; 13.17b killed (0103); the art sheet and its three marks. 171 to 183: 13.15's keep rounds closed (0110); 13.21 the tide; the loaded art 8/8; #601 9/8; 13.22 the break. 184 to 188: Lotus's showcase notes, the campaign's story structure (0121).
