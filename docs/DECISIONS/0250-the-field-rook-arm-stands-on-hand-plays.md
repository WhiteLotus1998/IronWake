# 0250 — The field, Rook's pick: the arm stands on its hand plays, gate 1 recorded short

Date: 2026-10-05. Rounds 370 and 371 (#1033); closes #1044. Measurements: `docs/measurements/field-rook-arm-1044.txt`, `field-rook-seen-far-off-1044.txt`, `field-rook-seen-coming-1044.txt`, `field-rook-arm-diagnostic.txt`.

## Context

Rook's arm of the field met the Fun Gate (0246) and owed gate 1 (round 364): 86/200 on 0248's baseline, 120 needed. The seats are one cluster (86 with `seen_far`, 99 without, 91 empty; round 369). The turn-3 south wake measured 89 and was not shipped (0249). Its traced timeouts are the party lost by turn 9. Round 370 put three next steps to the Table: a content lever on turns 2 to 9, vetoing recruits' strikes in the Sim, or standing the arm on its hand plays.

## Decision

1. **Rook's arm is `tuned` on its hand plays, with gate 1 recorded short at 86/200.** This is the Sim's limit on a fragile-flier pick, not the board's verdict. The only content Rook's pick adds to the empty-slot board is `seen_far: rook 2`, measured within noise, so as far as gate 1 sees, the arm is Keziah's board minus Keziah. The 134 to 86 gap is what Keziah adds in the Sim (drop 0.380, 788 swings), not something the board does on turns 2 to 9.
2. **DESIGN 11 gains a narrow pick-keyed clause** (below the stall clause). An arm keyed to a branch pick may stand on its hand plays with gate 1 short only when all four hold, and the record names the numbers:
   1. The arm's board differs from an arm that passes gate 1 only by content measured within noise. Here: `seen_far`, 86 against 99.
   2. The picked unit's seat is within noise of the empty slot. Here: 86 against 91; #1054's key parks her.
   3. Every hand play on the arm won, and at least one was cold. Here: 81, 820 (Chat, cold), 4242 (the Critic, cold), 973, 5150, 1340, all won.
   4. The Fun Gate is met on the arm (0246).
   No map other than Rook's arm of the field stands on it.
3. **Both cold plays predate the board as shipped** (820 predates the drift, 0226; 4242 predates the header, 0240, which it caused). So condition 3 is met on the arm's history, and the first cold play of the arm as shipped is owed. It is the tripwire play, and a cold chair (the Critic, since both partners are warm on the arm) takes it.
4. **The tripwire.** A cold loss on the arm reopens it. So does a cold play that has to spend every Recall before turn 4, as 4242 did on the header-less board. 4242 does not trip it, since the header was the answer to it. A reopen means option 1, a content lever aimed at turns 2 and 3 (the picket opening), not 4 to 9.
5. **Declined for now:** a content lever on turns 2 to 9 would tune the board to the Sim (rounds 364, 368). Vetoing recruits' strikes would make the Sim less crude on every map to rescue one arm. The turn-3 south wake stays killed (0249).
6. **One check, not a lever (#1061).** 0249's trace note says Rook died on seed 4, turn 3, to a stop beside the sleeping rider, "since the key prices only woken enemies". `Exposure.Of` prices a sleeping group the command does not wake from where it stands, and a group it certainly wakes (`seen_far` included, through `WakeCheck`) as awake. Either the rider woke on a later command, which is 0025's regime and needs nothing, or the stop woke it and the key left its strike unpriced, which is a `bug` on every map. Either answer leaves this decision as it is.

## Cost

Five recruits on this board win 91 of 200 in the Sim, so the field leans hard on its sixth body, and on Rook's arm that body is a 17 HP flier. The Critic's cold 4242 won with nobody fallen but spent all three Recalls by turn 3, which matches where the Sim dies. The tripwire exists because of that.
