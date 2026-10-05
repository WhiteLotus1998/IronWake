# 0256 — Wren's talk gets a board that can decide it

Date: 2026-10-05. Chat's 4611 chair on #1063 (https://github.com/WhiteLotus1998/IronWake/issues/1063#issuecomment-5995582642), agreed by Code with the pair-weight addition (https://github.com/WhiteLotus1998/IronWake/issues/1063#issuecomment-5995661012); built in #1097. Restates the Table's agreement; the placement is Code's content call.

## Context

DESIGN 13.18's talk (a combat Wren is in wakes a sleeping group within 8, not the noise radius of 6) never fired in six plays of `docs/samples/saltmarsh_ford_brace_signatures.map`. The ford pair comes south and fights 9 to 11 from the fort, and every tile a fort member can be struck from is within 4 of another member, so proximity always wakes the fort before a talk could.

## Decision

1. **A new sample, the old one kept.** `docs/samples/saltmarsh_ford_talk.map` is the signature sample with four columns added to the west (water on the river row, plain elsewhere; every old coordinate moves 4 east) and one sleeping guard group, `marsh`, of two riders. The signature sample stays as it is, so the 563 and 587 replays and its brace-sample test keep their history.
2. **Measured, not read.** The placement was chosen from 200-seed sweeps of the Sim's ford fight on each candidate board, since the riders' placement moves the fight. The numbers and the tiles are on #1097, off the Table, because Chat plays the deciding chair cold to them.
3. **The pair weighs.** Riders move far enough that a pair woken mid-ford arrives in the same enemy phase.
4. **Kill clause, fixed before the play:** if the deciding chair takes no deployment or command for the talk (no Wren held back from a kill, no tile chosen by its distance to the pair), the talk is killed and Wren keeps Canto alone. If it binds, it is kept with Teodor's orders and Canto.

## Cost

The rout now needs two more kills, so the sample is longer than the six plays it replaces. That is the price of a pair that costs something when it wakes.
