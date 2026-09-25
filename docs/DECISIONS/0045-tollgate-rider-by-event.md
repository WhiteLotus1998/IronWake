# 0045 — The Tollgate: the rider arrives by map event on enemy phase 4

Date: 2026-09-25. Issue 222, from the thirty-second round (#216; Chat's lean, Code agreed): the rider is the map's one mover and died the same way in all seven plays, baited on turn 1 and finished by Pell on turn 2. Built by Code in build-only mode; the shape (a turn trigger, no announcement) is the Table's, and the turn and tile were left to the Builder by the issue.

## Decisions

1. **No rider among the placements.** `E rider 12,5 group:flank behavior:aggressive` is gone.
2. **One spawn event: `riders turn 4 enemy spawn rider 13,5 group:flank behavior:aggressive`.** Turn 4, because on the seed-131 and seed-151 transcripts the woods fight opens on player phase 4, and without the rider to bait on turns 1 and 2 it can open on turn 3 (Code's seed 163 did), so enemy phase 4 lands inside it either way. 13,5, because it is the east edge of row 5 and row 5 is plain from 8 to 13: an outrider's Mov 6 reaches 9,5 on the phase it arrives and strikes whoever stands on the woods fight's east side (8,5 is the tome's tile against the brigand on both transcripts). The id is `rider-1`, since the map places no other rider (0036 item 4).
3. **No announcement.** The event's own transcript line (`event riders` / `rider-1 arrives at 13,5`) is the only tell, after the fact (thirty-second round; Chat on #216).
4. **The held-tile rule applies as written.** A unit standing on 13,5 as enemy phase 4 opens spends the event and no rider comes (0036 item 3). It is nine tiles from the woods and costs a body for the fight; a play, if a far one.
5. **The limit stays at 10.** Gate 1's p90 is 9.

## Measured (200 seeds, two rolls, `docs/measurements/2026-09-25-full-tollgate-rider-event-200seeds.txt`)

| The Tollgate | Gate 1 | Losses | Gate 4 |
|---|---|---|---|
| DECISIONS/0041, rider at 12,5 | 144 (72 percent), median 8, p90 9 | 56 timeout, 0 captain, tail 0.3 | ok, 0.225: Pell 0.495, Teodor 0.225, Wren 0.085 |
| Rider by event (shipped) | 159 (80 percent), median 8, p90 9 | 41 timeout, 0 captain, tail 0.4 | ok, 0.310: Pell 0.535, Teodor 0.310, Wren 0.270 |

All eight gates pass. Wren's share rises from 0.085 to 0.270: with no rider to kill at the start, the party reaches the woods whole and her sword counts there.

## The hand play

Seed 163, seize on turn 10 of 10 with one Recall, nobody lost. The rider arrived on enemy phase 4 and hit Pell for 11 at 8,5 beside the woods fight; on turn 5 Pell's 99 at it missed and it killed him on the next phase, so the Recall went to the captain taking it at 87 instead. PLAYTEST.md has the entry; the seed-131 replay test is replaced by this script's.

## Not decided

- `threat` names the rider before it exists. On player phase 4, `threat pell` listed `rider-1 from 9,5` with its forecast, because the query plays the coming enemy phase and the event fires at its start. So the first arrival is readable to a player who asks the right unit on the right turn. Whether that is the tool working (#217 says the enemy phase is priced before it is entered) or a leak of the one surprise the Table chose not to announce is for the Table; nothing is changed here.
- Chat's cold re-rate on this version, and whether Code's surprise clears 7 from a chair that did not know the turn.
