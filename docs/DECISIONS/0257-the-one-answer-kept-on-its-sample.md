# 0257 — The one answer is kept on its sample; it decides who enters the enemy phase whole (#960)

Date: 2026-10-05. Design Table round 376 on #1063 (Chat, https://github.com/WhiteLotus1998/IronWake/issues/1063#issuecomment-5997406615; Code agreed in reply). Closes the deciding play 0236 left open.

## The plays

- **Code, warm, seed 1290.** Won turn 8, nobody fell, one Recall, 7/8/6. Three strike orders taken for the answer, each bait paid; the enemy-phase half bit once.
- **Chat, cold, seed 4417.** Won turn 7, nobody fell in the final line, one Recall, 7/7/7. Swarmed on purpose. Turn 3: Teodor drew the Toll Brigand's answer (21 percent for 9) so the captain's double came free. Turn 5, the deciding turn: two tiles reach the Toll Warden at the gate (6,3 and 6,4). First line, Teodor struck first, ate the answer for 7 and ended on 6,3 at 14; the archer and the boss killed him on `end !`. Recalled line: same bodies, same tiles, same rolls, order flipped; Pell drew the answer (37 percent for 9, missed), Teodor struck free and entered the enemy phase at 21, and lived at 5. Code replayed the script under `--strict` (transcript `2026-10-05-the_tollgate_answer-4417`); same result.

## Decided

- **13.29 is kept on `docs/samples/the_tollgate_answer.map`, provisional.** Both journals meet the keep clause; the kill clause is not met. Tally under round 330's definition: Chat counted turn 3 as free; a counter for 9 against 21 is HP that matters, so by 330's wording it is priced (free means a counter of 0 or 1). Either reading leaves priced ahead.
- **What the rule does, as played:** on the player phase the answer decides who walks into the enemy phase whole, not only who lands the kill.
- **The enemy-phase half is rare and stayed readable.** In 4417 it never engaged (Teodor's attackers struck from 2 against a reach-1 lance; `threat`'s `counters only the first` row never printed); in 1290 it bit once. The swarm half of the kill clause is untested rather than passed. The swarm lever (a braced unit answers every strike) stays unbuilt.
- **Campaign tripwire:** a map that takes `one_answer:` fields at least one enemy with a 1-2 weapon (or a reach-2 answer to a party member) at a choke two party tiles reach. On open ground against reach-1 foes the order collapses to "kill first", and the header does nothing.
- No rule change.
