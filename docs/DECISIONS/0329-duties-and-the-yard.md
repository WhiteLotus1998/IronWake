# 0329: Duties at camp and the yard's ceiling (part 1)

Date: 2026-10-08. Issue #1331, from Lotus's approval of the yard ("Yard is fire, I like it.", Table #1287, relayed). Part 2 (#1332) builds the boards.

## Decided

- **One duty a unit a camp.** `CampaignRecord.Duties` holds the duties taken: `quest` (a side map's party records it when the map is decided), `forge`, `rest` or `yard`. A unit no command names rests. A won main map clears them. The protocol record writes them as `duties` and only when not empty, so every existing save and transcript reads as before. The one exception is a `record` line printed after a side map, which now names the party's duty.
- **A unit with a duty is refused another.** A second side map is the same duty, so a party member may still take a second quest at one camp, as before. A unit who falls for good keeps no duty on the record.
- **Wounded: the forge and the yard are refused.** A side map keeps its current rule, which lets a wounded ally go. Changing that would move a shipped rule, so it is left for the Table (Unsure).
- **The cap is the teacher, enforced in the battle and not after it.** A level-up rolls its growths on the spot, so a clamp on the record would have to un-roll one. Instead a `YardHand` rides on the two `BattleUnit`s. The teacher's award paths return unchanged: no EXP, no rank points and no mastery. "The teacher earns nothing" is read as all three. The student's `GainExp` stops at the teacher's level less one, keeping EXP up to 99 as 0276 keeps drilled EXP. Its points in the taught weapon stop one short of the rank above the teacher's. Other weapons the student swings earn as on any map.
- **Who may drill.** Both units must be on the roster, distinct, free of another duty and unwounded. Both classes must use the weapon. The student must be under at least one ceiling, since a drill that can raise nothing is refused. The captain may teach or train: the yard is at camp, and no rule keeps the captain from it.
- **The fight.** It plays like a side map. The student takes the captain slot, so the drill is lost if the student falls, and the objective names them plainly, a hire included. The teacher takes the bare slot. Enemies are at the student's level, under the campaign's difficulty, with permadeath as the record holds it. Its seed falls past every map's, trial's and side map's seed. Won or lost, both spend the duty and the student keeps what the fight earned under the ceilings. The pool is `content/yard/*.map`, taken in turn by the next map's index. `validate` checks every yard board's slots (one captain, one recruit, nothing else). One plain placeholder board ships so the command can be played until #1332 replaces it.
- **The forge duty is recorded and does nothing yet.** Nothing in the forge asks for a unit today.

## Unsure

- Whether a wounded unit should be refused a side map too. The ruling's "a Wounded unit's only duty is rest" says yes, while the shipped side-map rule says no.
- What the forge duty buys, if anything, once the forge takes one.
- **From both hand plays: on an open board the teacher's counters take the kills.** The student at the drill hands' level needs three hits for a kill and dies to two. The teacher cannot soften a foe without finishing it, because a teacher two tiers up kills in one round. So the teacher earning nothing is real, but the student earning something needs the board's help. That is #1332's job (the ring's pen), and a candidate rule for the Table: in the yard the teacher's blows pull, leaving a drill hand at 1 HP instead of killing it.

## Next

- The camp-screen yard row and the client's click parity. Each new camp line moves every campaign transcript, so they ship together in one slice.
- Gate 4's yard arm in the Sim (the planner picks the strongest teacher for the lowest main member), reported beside the current drop and wired to no exit code.
