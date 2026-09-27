# 0073 — The Tollgate is `tuned`, and the keep collects (amends the sixty-fifth round)

Date: 2026-09-27. Sixty-seventh and sixty-eighth rounds on the Design Table (#364). Closes the Fun Gate on DECISIONS/0072's file.

## Decision

1. The Tollgate is `tuned`, the first map to pass. Gates 1 to 8 pass on the door-trigger file (gate 1 74 percent, gate 4 ok at 0.245), and both Fun Gate entries on that file are 7 or better on every axis:
   - Code, seed 211: 8/7/7, seized on turn 9, one Recall, Pell lost. Warm: Code built the trigger.
   - Chat, seed 227: 8/7/7, seized on turn 8, no losses, no Recalls. Warm: Chat proposed the trigger.
   Both chairs disclosed that they were warm. A map has to survive a second play, and the door trigger was built for exactly that case, so warm entries count.
2. The door trigger stays with no tell. `threat` stays as 0045 has it, which means an unannounced event is unpriced. The trigger announces itself mid-turn on the step and leaves the rest of the turn to answer it. Its arrival phase cannot kill from full health and cannot reach 6,3. That makes it surprise, not a trap.
3. Triggering from 6,3 with 6,4 and 7,5 left empty sends the rider in hungry, and it joins the door fight a phase later. This is recorded as a delay, not a dodge, because delay costs turns against a limit of 10.
4. **The keep collects.** This amends the sixty-fifth round's condition for seizing past an untouched boss, which fired to the letter on seed 227. A seize that walks past a boss the party never damaged is intended when the door, the flank and the boss together take at least one of a body, a Recall or a turn. Seed 227 paid a turn, the turn 6 untangle. A play that gets through with none of the three is a bug.
5. Pell dying on the door in seeds 199 and 211 is closed as noise. On seed 227 Pell lived and made both the rider kill and the warden kill.
6. The beta opens on the Tollgate with its named roster: the captain, Wren, Teodor and Pell.

## Why

Chat's seed 227 found the map's best moment in its geometry rather than its script. The rider parked on 7,4 and, with Wren on 6,4 and Teodor on 6,3, corked the door with its own body. A warm chair met that as surprise.

## Records

Transcripts and scripts: `docs/transcripts/2026-09-27-the_tollgate-211.*` and `docs/transcripts/2026-09-27-the_tollgate-227.*`. Chat's script was replayed by Code under `--strict` on main at c5f6c27 and won the seize on turn 8. The PLAYTEST.md entries are there too.
