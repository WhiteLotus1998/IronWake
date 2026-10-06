# 0279 — A held group holds its ground (the Mill's lever b)

Date: 2026-10-06. Issue #1189; Design Table #1187, rounds 402 (Chat) and 403 (Code). Provisional.

## Context

Both Mill entries are in (Code 1480 warm 7/6/6, Chat 2061 warm 6/6/4). In Code's line Maud's fort strike woke the mill on turn 2. Woken guards are Aggressive, so they marched to the fort and fed themselves into Maud's counters, and the map ended on turn 4 of 12. The early wake was the strong line and it cost nothing. Chat's lean was lever (b) first: woken mill guards hold the north bank. Lever (a), a limit of 9, would be measured on top later. Nothing in the engine kept a woken non-boss guard near its ground.

## Decision

- A map header `holds: <group> <x,y> <x,y>` names a group with a Guard member and a rectangle that holds every member of the group.
- A woken member of that group with no strike this phase approaches only over the rectangle's tiles. Off the rectangle, it walks back.
- A strike is taken from any tile the member can reach, so `threat`, the exposure sum and counters are unchanged. This is the guard boss's going home (0080) applied to a group.
- The board prints the rule while a member stands.
- The Mill carries `holds: mill 0,0 11,2`.

## Killed before it shipped

The first build put the rule in the reach, so a held member never ended a move off its ground. Code's hand play (seed 1500) showed Maud hitting the soldier from 8,4 at range 2 with nothing able to answer: #1087's gallery. The Sim read 173/200, up from 133. The shipped rule leaves strikes alone.

## Measured

Sim, 200 seeds: gate 1 went from 133 to 126 (67 to 63 percent), Maud was lost in 73 games instead of 66, and the median win turn stayed at 7. Code 1500 warm, 6/7/6: won on turn 8, the woken mill waited on the bank, and the fort became the captain's bait tile. Chat's 2061 line plays the same game under the header, so its tripwire (the bait line unwinnable without Maud) holds.

## Kept for the replays

`docs/samples/the_mill_0278.map` is the Mill before the header. The 632, 1480 and 2061 replays and Chat's campaign 3971 read it.

## Next

Lever (a): a limit of 9, measured on top of this. The limit of 12 still never binds (1500 won on turn 8). After the levers, the next Mill read is a cold chair.
