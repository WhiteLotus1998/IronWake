# 0205 — The Warden's Gate, Teodor's quest 2: the First Warden's Lance and Turn the Key (#635 slice 12)

Date: 2026-10-03. Built by the chain Builder. What to build is the Table's: rounds 268 to 271 on #820 (recorded in DIALOGUE by #841 and #845). Quest 2 names the lance and pays its art; the art is the lock, woken-only, -4 Mt, `single`, cost 3, range 1; the lock is tethered to Teodor standing beside it; 3 frozen iron; the id `family_lance` is kept. The board's shape, the content shape of the naming and the art's gate are Code's lean, and Chat can argue any of them on the PR.

## What is built

- **The naming.** A ladder's optional `named` is the weapon's true name (`the First Warden's Lance`). A part 2 quest's `names` names a heirloom bound to its member with a `named` ladder. The loader refuses it on a part 1 quest, on a weapon without `named`, and beside `pays`. A win sets the stack's `named`, which rides the protocol and the save. From then on the pack, the card and every weapon line print the true name, at whatever stage the lance has reached. The win line is `teodor wins teodor_2; Family Lance is the First Warden's Lance now; the stores take 3 frozen iron`. `Forge.RareNeeded` counts a named heirloom as an issued signature, so the campaign's frozen iron is 9 of 9.
- **The art.** `turn_the_key` in `abilities.json`: lance E, cost 3, Mt -4, `single`, `woken`, `locks`, `item: family_lance`. Teodor knows it from the start, as Ottilie knows Paid in Full. `woken` makes it declarable only when its heirloom is at its last stage and, where the ladder has a true name, named (`Heirloom.ArtOpen`). Otherwise the refusal is `Turn the Key waits until Family Lance is woken and named`, and the card's technique list leaves it out. The loader refuses `woken` on an art whose item is not an heirloom.
- **The lock** (`Lock`). A hit with a `locks` art on a unit that survives sets its chill clock (1) and `lockedBy`. While the clock runs and the locker stands orthogonally beside it, `ReachOf` gives it Mov 0. After every accepted command, a lock whose locker has fallen or stepped away drops (`lockDropped`) and does not come back. The chill's clock clearing at the phase change clears it too. `unitLocked` and `lockDropped` are protocol events, and the unit's `lockedBy` rides the state. The CLI prints the art line's `locks: ...`, the card line `locked by Teodor: Mov 0 while Teodor stands beside, else chilled until ...`, and the event lines.
- **The board.** `content/quests/the_wardens_gate.map`: defeat boss, 15x10, limit 10, Recall 2, enemy level 5, `announce: on`. The boss stands on open ground at 7,2 before the gate's wall, with held archers in the forest at 6,1 and 8,1. A wall on row 5 has a one-tile gap at 4,5 and a two-tile breach at 10,5 and 11,5. A sleeping soldier waits at 1,3 by the gap, a sleeping rider pair in the east yard at 12,3 and 13,4. An announced rear pair arrives at 0,8 on turn 3 and at 14,8 on turn 4. The twist is the reach against the yard: the boss refuses to step off (the veto), Long Thrust from 5,2 strikes him for 10 with no counter under one archer, and every strike on him lands within 6 of the yard and wakes the riders.
- `campaign.json`'s `teodor_2`: part 2, `names: family_lance`, 3 frozen iron, opening two maps after quest 1 by the part-2 rule. It is appended last, so no other side map's seed moves (0198). The cards are placeholders with the rules line until the writing pass (#811).
- Test fixtures: the Long Count's journaled play replays without `teodor_2`. Its save's camp, with Teodor's quest 1 won after map 5, now offers Teodor 2 ahead of Ottilie 2 (earlier opening, DESIGN 14's overflow), which is the rule working. The Chapter Roll's transcript lists Teodor 2 at its camp.

## Why the boss is not on a fort

The first cut put him on a fort. In Code's probe (same save and seed, discarded), the fort's heal of 5 a phase cancelled Long Thrust's 8. In the open his counter plus his own swing (10 and 10) killed a 20 HP Teodor in one exchange, and by turn 8 no line won without staking Teodor. `behavior:boss` holds under the veto just as the guard does, so making him come out was not a lever. On open ground the reach is the answer, and waking the yard is its price.

## Measured and played

- Sim, 200 seeds, `--lead teodor --lead wren`, level-1 cast: gate 1 0/200 (191 timeouts, 9 Teodor deaths), the shape of the other side maps. The gate is a cold chair (round 194). The Sim's player declares no art.
- Code's warm play (side-map seed 1110, from the Chapter Roll's camp save with Teodor's lance hand-set woken and in front, ally Wren): won on turn 7, nobody fell, no Recall. The journal is in PLAYTEST. Archer-2 never acted: that is the first lever if a cold chair finds the poke cheap.
- Turn the Key is tested (the lock, the tether on a move and on a death, the clock, the miss, the gate on woken and named, the protocol), but no hand play declares it yet. It cannot be declared on its own quest board.

## Not in this slice

A hand play of the lock (the next Teodor map after the quest), the Sim's player declaring it, the writing, and the Godot client's lock line.
