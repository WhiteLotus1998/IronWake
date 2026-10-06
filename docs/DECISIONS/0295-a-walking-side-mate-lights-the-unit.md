# 0295 — A walking side-mate lights the unit

Date: 2026-10-07. Issue #1104 (Chat, a bug found in a second dash chair, seed 2716). Builder's call on a bug; the Table may argue it.

## Context

Issue 987 priced an enemy that does not know where the unit is once a side-mate acting before it stands within sight of the unit, but the only side-mates it asked were those with a line on the unit, standing on a tile they would strike it from. On the dash sample, turn 3, the brawler walked to 16,4 to strike the captain and the brigand walked to 12,3 toward him. Neither struck Dunstan on 14,3, but both stood within sight 3 of him, so the rider and the shieldbearer struck and killed him, while `threat dunstan` printed `Shieldbearer: cannot see you (dark)`.

## Decision

- A lighter is also an earlier enemy with no line on the unit whose own plan on the phase-start board (`EnemyAi.PlanUnit`, the board every line reads) ends its move within sight of the unit. The lit enemy is priced with the walker on that tile, marked `(once <walker> lights you)`, or `(once a side-mate in the dark lights you)` when the player does not see the walker.
- The order rule, the phase-start board and 13.7's rule that an unseen enemy stays out of the rows and the total are unchanged. On seed 2716 the shieldbearer is now priced (5 against 10 hp), and the rider, in the dark, stays unpriced.
- Display only: the planner and the Sim's player do not read it; `end`'s lethal ask reads it, as it reads every lit line.

## Revisit

If a walker's planned stop proves a poor guess in play (a lit line that never struck because an earlier enemy's move changed the walker's plan), the Table decides whether to drop walkers or keep them as the worst case.
