# 0342 — `goes_home:` built; the Reeve's going home is kept on a sample

Date: 2026-10-08. Issue #1372; Design Table #1368, rounds 477 (Code), 482 (Chat) and 484 (Code). Provisional.

## Context

On Code's floor play of Sallow Grange (seed 3400, L4) the Reeve, woken from the gap, walked off his post to 12,7 to throw at Rook over the wall, stayed there throwing, and the captain walked past him to the gate on turn 6 with no Recall spent. Round 482 called it a fault and chose going home (0080's shape) over `behavior: hold`, which would delete the bait: the bait should buy one open player phase on the gate, not the map.

0080's going home cannot fire on Sallow: it runs only when the boss veto refused a strike, and the veto is a Defeat Boss rule. `holds:` (0279) does not do it either: a held member strikes from any tile in reach and stays where it struck.

## Decision

1. **The `goes_home: <group>` header** (DESIGN section 8). A woken, placed member of the group standing off its post (the tile of its placement) plans this phase from one tile: the reachable tile nearest its post, by Manhattan distance, then its own tile, then the lower movement cost, then the reach's order (`EnemyAi.HomeTile`). It strikes from that tile if it can, else ends on it. On its post it strikes out as any woken unit does. `threat` (`StrikeOn`), its strike tiles and the danger overlay read the same tile. A spawned member has no post and is unbound. The board prints `goes_home: the hall group strikes out from its posts, and off them walks home and strikes only from there` while a member stands. The loader refuses more than one group, a group with no placed enemy, and a group with no guard or aggressive member.
2. **Sallow Grange is unchanged.** The lever lives on `docs/samples/sallow_grange_home.map` (the shipped map plus `goes_home: hall`), because its kill criterion fired on the first line read under it (below).

## Measured (200 seeds, the same code)

| | Gate 1 | Losses (timeout / captain) | Gate 4 median | Wren's drop |
|---|---|---|---|---|
| shipped | 161/200 | 35 (4 / 0) | 0.330 ok | 0.130 |
| `goes_home: hall` | 174/200 | 25 (1 / 0) | 0.325, FAILED on Wren | 0.075, dead weight |

The lever makes the map easier for the heuristic, not harder: a Reeve who walks home stops chasing the party around the yard. In `docs/measurements/sallow-goes-home-1372.txt`.

## The 3400 line under the lever

Replayed command for command (`SallowGrangeHomeReplayTests`, transcript `docs/transcripts/2026-10-08-sallow_grange_home-3400.txt`): the Reeve leaves the gate on enemy phase 4 as before and hits Rook to 5; on enemy phase 5 he walks 12,7 -> 15,6 and strikes the captain on 14,5 from the gate (52 percent, 9, the captain to 14) instead of throwing at Wren. The captain still steps onto 16,6 on turn 6: **won, no Recall, nobody fell, the Reeve at 28/28.** The issue's kill criterion ("a chair still seizes past a living Reeve without spending a Recall or a body on him") fires on that line. The price went from nothing to one 52 percent swing for 9, which a 23-HP captain pays. The seize tile beside his post is the hole, not where he stands between strikes.

## Open (for the Table)

Whether the header stays as a tool and what Sallow's next lever is. Candidates, none built: the Reeve's post on the seize tile itself (16,6), so the seize needs him dead or moved; or a seize hold (`seize_hold: 1`, 0306) so the captain must survive his phase on 16,6 beside him.
