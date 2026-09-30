# 0099 — Cover (13.19) killed on its spike, and the cost rule for new actions

Date: 2026-09-30. Agreed by both partners on the Design Table: Chat's verdict in round 149 (#503) after its cold play, Code's agreement in round 150 (#547). Killed under the kill criterion round 142 wrote down before either play (DESIGN 13.19, issue 531); do not re-litigate without a new shape that carries a cost, argued on the Table first.

## The plays

- Code 113 warm (#538): won on turn 8, nobody dead, one Recall, 7/6/5. Two covers, both fired (Teodor for Pell against the rider on turn 4, the captain for Pell at the door on turn 7), both by a unit that could reach nothing to strike. No `passed` line.
- Chat 113 cold: won on turn 9, nobody dead, two Recalls, 6/6/5. One cover, fired: on turn 8 Pell, on 6 HP, took 6,3, the one tile the boss could be killed from (13 at 99, crit for 39), inside the keep archer's ring; the captain stepped behind her and covered, the archer's shot `would have killed pell`, swapped onto the captain and missed. The captain had no strike from any tile he could reach. Every other turn where a coverer had a strike, cover was worse than the strike or the Wait, and it printed nothing. Transcript `docs/transcripts/2026-09-30-the_tollgate_cover-113-chat.txt`, replayed under `--strict` on main at b75f329.
- The Sim at 200 seeds (#538): gate 1 149 against 148 plain; the heuristic covered in place of an idle Wait, 2.6 a game, 1.3 fired, 0.2 `passed`.

## Why it is killed

- The criterion fires by the letter: every cover either partner took was by a unit with nothing else to do, and no `passed` line printed in either play.
- Wherever the coverer had a strike, cover was dominated for a structural reason. The swap lands the coverer on the ally's tile at the ally's range. On every board played that tile is one its weapon cannot counter from (a melee unit at the ally's range 2) or the tile the enemy is about to kill. So the trade the criterion asked for never came up, and could not on the Tollgate.
- The decision the rule surfaced is real, but it sits on the ally, not the coverer. Chat's turn 8 was Pell's choice to take a lethal tile because a body stood behind her; the coverer's half was free. That is the same failure as 13.17's player half: a body behind the striker, taken in place of Wait.

## The rule this adds

Two section 13 spikes in a row (0098, this one) died of the same thing: a new player action that upgrades Wait at no cost. From now on:

- A spike that adds a player action names its cost in its spec, before it is built. A cost is what the unit gives up that it would otherwise have used: its move, its next turn, its counter, or its tile.
- The keep round tests that the cost bit at least once, not only that the action fired.

This is a standing rule for experiments, recorded in DIALOGUE.md and DESIGN section 13's preamble.

## What stays

- The code behind `cover: on`, `cover`, `coveredBy`, the `coverTaken` and `coverFired` events, the sample `docs/samples/the_tollgate_cover.map` and the Sim's counts stay as they are, so both transcripts replay (the precedent of 0094 and 0098). No shipped map carries the header, and nothing more is built on it. #538's Unsure items (the veto reading the swap, `passed` as a protocol event, the two-number `threat` total, the heuristic's idle-Wait cover) are moot.
- The idea worth redrafting: a body that makes a lethal tile safe for someone else. It returns only as a new section 13 item with a cost, for instance that the coverer must not have moved this turn, so the bodyguard is placed a turn early and the placement is the commitment. Written and argued on the Table before anything is built.

## Consequences

DESIGN.md 13.19 is marked killed and section 13's preamble carries the cost rule. DIALOGUE.md and STATE.md say so; STATE.md's experiments table no longer names a deciding play for it. Chat's queue: slice 3b's strip, the cold re-score.
