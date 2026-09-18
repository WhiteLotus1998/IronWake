# PLAYTEST — both partners' play journals

Dated entries, signed "— Code", "— Chat", or "— Critic". Feelings, not metrics: what was tense, what was boring, the best single turn, the moment you stopped caring.

Entry format:

    ## 2026-09-14 — <map name> — <signature>
    Seed: <n>   Result: <win/loss, turn n>
    Tension: n/10   Choice: n/10   Surprise: n/10
    Best turn: <one or two sentences>
    Notes: <a short paragraph>

Chat writes its entries on the Design Table and Code copies them in.

## 2026-09-18 — Old Mill Road — Code

Seed: 7   Result: loss, turn 6 (the captain, alone at 7,3, with a Field Dressing unused in slot 1)
Systems entry, not a Fun Gate entry. Question: does the Item action read as a choice, and does durability show?
Tension: 6/10   Choice: 6/10   Surprise: 7/10
Best turn: turn 5. The captain dashed 5,5 to 7,3 and woke the mill group on purpose to see whether a wounded unit with a dressing in the bag could stand a round; the archer and the soldier put him from 22 to 9 and I had the answer, and then on turn 6 I typed `end` instead of `item captain 1` because I had miscounted the phases, and he died at 4 HP with the dressing still there. That is the oldest feeling in the genre and the game produced it on its second night of having items.
Notes: The brigand came to 3,6 on enemy phase 1 and attacked the captain on 2,6, the captain doubled it dead on the counter, and the first Field Dressing on turn 3 read exactly right: `captain uses field_dressing (2 left)`, `captain heals 10 (hp 22)`, action spent, and `show` lists the slots with uses. The refusal on a full-HP unit (`captain is at full HP`) is the right refusal; a script cannot burn a use on nothing. Durability is visible (`Iron Sword x38` after two counters) but did not matter in six turns and will not matter on a 40-use sword in a 20-turn map; it is a campaign number, which is fine, but it means the broken fallback is a thing only the Sim and the tests have seen. What was not tense: turns 2 to 4, walking with nobody in range, the dead approach the Critic named, unchanged. Transcript: `docs/transcripts/2026-09-18-old_mill_road-7-items.txt`. Not read before writing: nothing of Chat's, since nothing of Chat's is here yet.

## 2026-09-18 — Old Mill Road — Code
Seed: 7   Result: win, turn 12
Tension: 5/10   Choice: 5/10   Surprise: 5/10
Question this entry answers (a systems entry on a format sample, not a Fun Gate entry): does the wake radius teach itself, and does the forecast read honestly?
Best turn: turn 5, stepping Wren onto the fort at 6,2. The fort is exactly four tiles from the soldier, so taking the only healing tile on the map is the same act as waking the mill group, and I knew it before I did it, which is the wake rule working as taught. Both enemies then missed her on the fort's 30 avoid, and she cut the soldier to 4 on the counters.
Notes: Transcript in docs/transcripts/2026-09-18-old_mill_road-7.txt. Turns 1 to 4 were the approach the Critic already called dead: nothing to do but walk, with the one real question on turn 2 being who takes the brigand's counter (the captain did, alone, because both his strikes land at 100 against an avoid of -3 and the double kills; the brigand is a gift of 30 EXP, not a threat). The forecast was honest every time: every printed number was the number that happened, including the 80 percent counter that took the captain from 19 to 3 on turn 12, which I had priced and accepted because 19 minus 16 is alive. The surprise was the archer choosing the captain on open ground over Wren on the fort on turn 6; the scorer's arithmetic is readable after the fact and it made the fort feel like it mattered. The moment I stopped caring: turns 9 to 11, healing on the fort in front of a boss that never leaves its tile. A Boss that cannot move is a puzzle with a time cost, and with turn limit 20 there was no cost, so the last four turns were bookkeeping. Two facts for issue 13: the turn limit should bite on a Hold boss, and the synthetic roster has zero growths, so Wren's level 2 raised nothing, which read as a bug until I remembered the placeholder. Recall was never needed and never tempting, because the forecast let me plan around every hit; whether it becomes a choice depends on maps where the numbers are closer.
