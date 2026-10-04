# 0239 — The Oath Stone lever 2: the rear rider arrives on turn 5

Date: 2026-10-04. Issue #940, rounds 317 to 319 and 336, 337.

## Context

Limit 10 (0232) read as a coin flip from turn 8 on a fair seed (Chat 4410, round 336). The road between them was soft. The turn-3 rider died on turn 4 inside the camp fight, and on turns 5 to 7 every `threat` read `no enemy can strike her`. Round 337 aimed the lever at those turns. If the rider comes with the rear brigand on turn 5, the road becomes a fight, a 4-HP chaplain is behind Keziah, and the rider is the meal that keeps the hunt alive for the door.

## Decision

- `content/quests/the_oath_stone.map`: `rear1 turn 3` becomes `rear1 turn 5`. Same tile (0,0), same `aggressive`, same group. The board, the limit, the fort and the envoy are unchanged.
- Keziah's quest-2 card says "More come up behind you from turn 5, announced."
- Two replay tests journaled with the rider on turn 3 (`-1133-fort`, `-1133-939`) now replay on `Fixture.OathRiderOnTurnThreeContentDirectory()`: real content with the rider line and the card put back. Their transcripts are records of the build they were played on, so they don't change.

## Kill criterion (round 337, unchanged)

Kept if a journal names a turn-5-to-7 fight with the rider, or the door chosen with the waking in view. Reverted if no chair reaches the door alive. Round 337 also named a revert if the Sim on the reseeded oath save loses more than 10 points. That measure doesn't exist: no Sim mode loads a campaign save (0232), and round 333 parked building one. So the clause waits on that mode, and the hand plays decide.

## Measured

- `--full` on the map file, 200 seeds, before and after: gate 1 at 0 of 200 both times (turn 3: 122 losses, 78 timeouts; turn 5: 104 losses, 96 timeouts). This plays the map's own roster, the captain and a recruit with no Kinsbane, not the save, so it decides nothing.
- Code warm, reseeded 940 (`2026-10-04-the_oath_stone-940-code.*`): **won on turn 10**, one Recall spent, nobody fell. The rider arrived on enemy phase 5 and walked through Joab's door tile toward the yard. On enemy phase 7 it struck Keziah where she held the breach at 11,6 for Maud, and she killed it on turn 8. The rear brigand came round behind Maud on turn 8 and killed her on enemy phase 9 in the first branch (Recalled). Kinsbane woke on turn 7 from the archer's kill, so the rider was not the waking meal on this seed. Joab was weighed on turn 5 as the waking kill (79 for 22 against his 26, his counter 19 against her 21) and passed. Warm 8/7/6. Both keep clauses were shown, but warm. Chat's read decides.

## Next

Chat reads the Oath Stone with the rider on turn 5, on a reseeded save of its own seed.
