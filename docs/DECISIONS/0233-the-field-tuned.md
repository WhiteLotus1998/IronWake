# 0233 — The Field Before the Keep is `tuned` with the route drift

Date: 2026-10-04. Issue #936, rounds 315 to 317.

## Context

The field (campaign map 9) has carried `route_drift:` since #81 slice 4 (0226). Chat's cold play (seed 4071, Keziah's pick, `--fed 10`, round 315) scored 7/7/7. Code's warm play (seed 81, Rook's pick) scored 7/7/6. Round 316 declined to re-rate that 6 without a play: the turns that held it there were turns 3 and 4, a march around the empty south bank on Rook's pick. Chat's 820, also Rook's pick, named the same stretch, and Chat's 4071 at fed 10 did not, because the drain filled those turns. Round 316 read this as a gap that depends on the pick, and round 317 agreed. #936 asked Code to play Keziah's pick at fed 10 on a fresh seed: 7+ on all three makes the map `tuned` (0073, where a disclosed warm chair counts), and under 7 the Table picks one lever.

## Decision

- **The field is `tuned`.** Code's warm play (seed 936, `campaign --seed 936 --from the_field --pick keziah --fed 10 --level 5`, transcript `docs/transcripts/2026-10-04-the_field-936.*`) scored 7/7/7, with turn 5 named as the best turn. Chat's cold 4071 scored 7/7/7, with turn 2 as the best turn. Gate 1 is 61 percent and gate 4 is ok (0190, 0191). Both partners are at 7+ on each axis.
- This play took the north route, which neither earlier chair had taken. The line group woke first, so the drift sent the south group's rider to 12,8. It fired on turn 6's enemy phase, not turn 5's, because nothing had woken by turn 5. That is the rule as built ("or the first enemy phase after the route is fixed", 0226). The rider then came over the bridge into the party's rear. `threat` priced Ottilie at 18 against 18 on turn 7, which cost a Recall.
- Nothing on the map changes. Rook's empty south bank stays a known soft stretch on her pick. No lever is filed for it, since both Keziah plays filled it, and the next chair to play Rook's pick and score it under 7 reopens it.

## Next

The before-route line reads `drift: on turn 5's enemy phase ...` and does not say "or later". This play showed that it can come later. Whether the line should say so is an Unsure on the PR, not part of this record. (#968 settled it: the line prints the current turn once the header's has passed.)

## Addendum, rounds 334 and 335 (2026-10-04)

The Critic's cold play of Rook's pick (seed 4242, 7/6/5, transcript `docs/transcripts/2026-10-04-the_field-4242-critic.*`) is the "next chair to play Rook's pick and score it under 7", so Rook's pick is reopened. The map is `tuned` **on Keziah's pick**. A map whose board a branch pick changes is gated per arm. Three Rook entries on three seeds (81, 820, 4242) name the same soft stretch, turns 3 to 7, so one lever is due now and no further read is needed first. The lever has to be keyed to the pick so Keziah's board doesn't change. It has to cost something in turns 3 to 7. It must not make the boss harder. The talk on turn 2 and the boss's exposure walk stay. Chat's lean was a turn-3 drift, but that is a no-op as built: the drift waits for the route to be fixed, and on Rook's arm that happened on turn 5 or 6. #973 builds `seen_far: rook 2` instead, with a turn-3 wake of the south group on Rook's pick as the fallback. The Critic's two talks from 7,12 after a Recall replayed the same keys, so they are one read: the talk costing the flier is true on 4242 and unknown in general (0238).
