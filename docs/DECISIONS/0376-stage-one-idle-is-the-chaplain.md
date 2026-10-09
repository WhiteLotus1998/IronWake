# 0376: stage 1's idle split three ways on the Warden sample; depleted's and floor's idle is one unarmed healer the planner never walks

Date: 2026-10-09. Issue #1395, from the Design Table's round 523 (Chat: before any stage-1 lever, split idle three ways, by unit; a planner hold is a fix and comes first). A read only: no board, unit or planner change; the sample reads 91 / 66 as 0374 left it. Every Hask number stays provisional on #1247.

## Decided

- **`--finale` prints a `stage 1 idle` line** (`FinaleRun.IdleLine`): idle unit-phases (0375) split into fell before acting (not standing as the phase closed), no enemy seen, and an enemy seen out of reach, with how many of the last had a nearer tile the unit could end on; the unread ones (no command of their own, read at the close) beside them; then the out-of-reach ones by unit, with its kind (the captain, a healer with no weapon it can strike with, unarmed, armed) and its median distance to the nearest enemy seen (`StageOne.LookAt`).
- **The read** (`docs/measurements/keep-1395-idle.txt`), full / depleted / floor: idle 195 / 1384 / 1065; fell 0 on every arm; no enemy seen 0 on every arm (the keep has no dusk); an enemy seen out of reach is all of it, and nearly all could have closed (189 / 1379 / 1004). Unread 57 / 108 / 8.
- **It is one unit.** Tamsin, the hired chaplain, an unarmed healer, is 1198 of depleted's 1384 idle (median 13 tiles from the nearest enemy) and 975 of floor's 1065 (median 8). The heuristic's approach returns no destination for a unit with no weapon on a Rout or Defeat Boss map (`Players.cs`, `Approach`), so a healer with no one hurt in reach waits where she stands, every phase. Full fields no unarmed unit; its 195 are armed units, a few dozen phases each.
- So round 523's third bucket (a planner hold) is the largest by far, and it is lever 2's. Without her, depleted's idle is about 3 % of its stage-1 unit-phases, full's shape, and its distance is its 22 % move only.

## Open, for the Table

- The fix: an unarmed healer with no heal on offer walks with the company (toward the nearest hurt ally, else the nearest ally nearest the enemy), under the veto's exposure rule. It changes the Sim's player on every map, so it re-runs `--full --all` and `--finale` and rewrites the Maps cells it moves (#971, 0352). Round 523 calls a planner hold a fix; the shape of the walk is the Table's to agree before it is built.
- Whether a chaplain who walks moves depleted's number at all: she strikes no one, so her phases become heals or move only, not blows. The read after the fix says whether depleted's gap was ever hers, or full's distance (move only) on a smaller company.
