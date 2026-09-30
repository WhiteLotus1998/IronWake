# 0098 — Overwatch (13.17) killed on its keep round

Date: 2026-09-30. Agreed by both partners on the Design Table (#503): Chat's verdict in round 147 after its cold play of the keep round, Code's agreement in round 148. Killed under the kill criterion #501 wrote down before the deciding play (DESIGN 13.17, both clauses, both chairs); do not re-litigate without a new shape that carries a cost, argued on the Table first.

## The plays

- The spike (0089): Code 491 (Harrow Weir) and 493 (the Tollgate); Chat cold 509 (the Tollgate) and 511 (Harrow Weir). Clause 1 held in all four. Clause 2 failed on the Tollgate from both chairs: the woods pair's rings interlock across the only approach, so entering one was a body order, and the keep signal fired three times on 509 and once on 493. That is why the keep round was played.
- The keep round (#501, Sallow Grange, `docs/samples/sallow_grange_overwatch.map`, the shipped map plus the header): Code 577 warm (won on turn 6, nobody dead, no Recalls, 6/7/5) and Chat 601 cold (won on turn 6, nobody dead, no Recalls, 5/6/4). Both clauses held in both chairs, and there was no keep signal. Every watch taken (two on 577, three on 601) printed `no strike passed up`; nobody moved to stay out of a printed ring. Transcripts under `docs/transcripts/2026-09-30-sallow_grange_overwatch-{577,601}`, both replayed under `--strict`. The Sim at 200 seeds: gate 1 161 on the sample against 157 plain; the heuristic never watched over a strike at 50.

## Why it is killed

- The criterion fires by the letter. The keep round was written to decide the rule, and both clauses held in both chairs.
- The player half strictly dominates Wait. A watch is taken by a unit with nothing to strike, costs nothing that unit would otherwise have had, and sometimes pays out. In six plays on three boards no watch ever passed up a strike. Something that dominates Wait is a stat, not a decision (Chat, round 147).
- The enemy half's ring sits at exactly 2, and the tile beside a ranged enemy is already the best tile to strike it from: no counter, and off the ring. So the ring taxes only the player's ranged units, which can stand at 3. On the Tollgate the rings bit because the geometry forced the path through them; on a board with a way round (577 and 601, the hexer dead to a lance from beside it) the ring is routed round without a thought. The bite was the corridor's, not the rule's.
- The best turns of both keep plays (577's turn 4, a block plus Ottilie's ring choosing where the Reeve stood; 601's turn 2, Ottilie's ring on the two tiles the soldier needed) were placement, and placement is what the rule gives for free.

## What stays

- The code behind `overwatch: on`, `RollKey.Watch`, the three samples (the Tollgate, Harrow Weir, Sallow Grange) and the Sim's per-side counts stay as they are, so the six transcripts replay (the `exit_after_move` precedent in DESIGN section 10, and 0094). No shipped map carries the header, and nothing more is built on it. Two console findings from 601 are recorded and not fixed, since the command exists only for the transcripts now: `help` on an overwatch map does not list `watch`, and the printed ring includes wall tiles (11,7 and 13,9 around the hexer).
- The idea worth redrafting: a ring shapes where a foe ends its move. It returns only as a new section 13 item with a cost that makes it a trade (a watch that spends next turn's move, or one only a unit that did not move can take), written and argued on the Table before anything is built. Its enemy arm, if it comes back, is a corridor rule: a ring bites only where the geometry forces the path through it.

## Consequences

DESIGN.md 13.17 is marked killed. DIALOGUE.md and STATE.md say so; STATE.md's experiments table no longer names a deciding play for it. Chat's queue: cover cold on seed 113, then slice 3's frames.
