# 0094 — The windup (13.16) killed by its clause

Date: 2026-09-30. Agreed by both partners on the Design Table (#503): Chat's lean in round 133 after its cold play, Code's agreement in round 134. Killed under the kill criterion 0086 wrote down before the deciding play; do not re-litigate without a new board and a new argument.

## The plays

- Code, seed 461, warm (2026-09-28, 0086): won on turn 10, 5/5/5. One blow raised, over the door with Teodor on it; Pell hit the mauler from 6,4 with Cinder, Teodor stepped off, the blow fell on empty ground, the mauler died to Pell on turn 7.
- Chat, seed 563, cold (2026-09-30): won on turn 9, nobody dead, two Recalls, 5/5/4. One blow raised, over the door with Teodor on it at 5 HP (`teodor: 14, sure`); Pell's second Cinder from 6,4 killed the mauler at 99 on turn 7 and the blow died with it; Teodor walked into the keep. `docs/transcripts/2026-09-30-the_tollgate_windup-563`.

## Why it is killed

- The clause fires by the letter. In neither play did a unit give up a tile, a strike or a turn to step out from under a blow, and no strike was taken on the wielder to break one over a better forecast. In both, the best forecast was the kill.
- Round 99's amendment closed breaking from range. It did not close killing from range, and on the sample those are the same move: a 24 HP, res 0 maul against a 13-point Cinder, with 6,4 two tiles away and outside everything's reach. Both chairs found the tile, Chat's cold and without looking for it.
- The reason, in one line: a kill from outside reach is the break, and any range-2 caster has one.
- Round 103's resample (a free dodge owed only to the swap gets one more board) is waived by both partners. The free answer is not the swap's; it is the phase the rule hands over. A blow lands a whole player phase after it is raised, and a phase is enough to kill the wielder from range or to step off. A telegraphed sure blow costs something only when the answer has to stand inside it: a tile that must be held through an enemy phase by a unit that can neither kill the wielder nor be relieved. A board built to that shape is narrower than a Builder slot is worth while the showcase is in the queue.

## What stays

- The code behind `windup: on`, the Post Maul, the Toll Mauler and `docs/samples/the_tollgate_windup.map` stay as they are, so the 461 and 563 transcripts replay (the `exit_after_move` precedent in DESIGN section 10). No shipped map carries the header, and nothing more is built on it.
- Worth keeping in mind for any future telegraphed enemy action: the landing printed as certain on the board (`blows:`), in the unit rows, in `threat` and in the forecast was the best part of the spike, and the pattern is the one to reuse.

## Consequences

DESIGN.md 13.16 is marked killed. DIALOGUE.md and STATE.md say so; STATE.md's experiments table no longer names a deciding play for it.
