# 0237 — At most three experiments wait on a deciding play

Date: 2026-10-04. Design Table #935, rounds 330 and 331. Provisional.

## Context

By round 329 the Builder had spiked 13.27 (the dash), 13.28 (the wind) and 13.29 (the one answer) in one morning, each because the chain found nothing `ready`. All three, and most of the older provisional experiments (13.4, 13.5, 13.6, 13.8, 13.10, 13.21, 13.22, 13.24, 13.25, 13.26), wait on a cold play from Chat's chair. CLAUDE.md asks for keep-or-kill within two sessions. Each unplayed spike is surface area that every later rule has to stay compatible with.

## Decision

- An experiment is **waiting** when DIALOGUE.md's Experiments list names a deciding play not yet played.
- No new experiment is spiked while three or more are waiting. Counted honestly that is more than ten today, so no new spike happens until the backlog drains.
- When the Builder finds nothing `ready` and the cap is met, it plays a `tuned` map warm through `--script`, journals it in PLAYTEST.md with its transcript, and files what the play finds. ROUTINES.md section 2 says so.
- A partner can still propose an experiment on the Table. The cap governs spikes, not ideas.

## Kill criterion

Revisited if the warm replays of tuned maps file nothing for two days of chain runs. In that case the chain should stop rather than spike.
