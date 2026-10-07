# 0304: The Shrine's sanctum archer wakes when the door falls (`wake_on_death:`)

Date: 2026-10-07. Issue #1264. Source: Design Table #1251, round 424 and Chat's answer to it. Provisional: one content lever on a rework candidate, played warm once.

## Context

From the south start the First Shrine is a sprint when the ally can cork 7,4: Maud opens the braced door with two Radiance strikes on turns 2 and 3, and the walk to the altar is free. Two of three south plays won on turn 4 (one clean, one bought with both Recalls), and the sanctum archer at 5,1, a Hold unit out of reach of every tile the door is struck from, had not acted in four plays. Code leaned brigand-1 to 4,4; Chat argued the axis that fails is surprise, which the brigand does not touch (it taxes turn 2, the turn that already worked), and asked for the archer to stay asleep until the door falls, then fight, with the rule on the screen.

## Decision

- **`wake_on_death: <group> by <other>[, ...]`** (map header, section 10; section 8): the named Guard group is deaf. It sleeps through proximity and noise and wakes only on a death in `<other>` or its own group. The wake is `death` with `<other>` as its caller (`The loft group wakes (a death in the sanctum group)`; the protocol's `groupWoke.by`). `WakeCheck` is still the one predicate, so the resolver, the exposure sum, `threat from` and the wind warnings agree; a deaf group has no wakers.
- **On screen.** While the group sleeps, the board and `threat`'s sleeping rows print `deaf: group loft hears and sees nothing; it wakes only when a unit of group sanctum or its own dies`. The general wake legend prints only while a group that can hear sleeps.
- **The Shrine.** The archer moves from `group:sanctum behavior:hold` to `group:loft behavior:guard`, and the map carries `wake_on_death: loft by sanctum`. The soldier stays on Hold. Brigand-1 stays at 2,3; the limit stays at 10. A sleeping archer does not brace (only a unit that waits may), which no play has turned on.
- **Old plays.** The transcripts made before this (875, 2130, 1530, 1600) replay on a fixture copy with the archer back on Hold (`Fixture.ShrineArcherHeldContentDirectory`).

## What the first play showed

Code's warm replay of 1600's script to the door (Ottilie, `2026-10-07-the_first_shrine-1600-woken`): the soldier fell on turn 3, the archer woke, walked onto the altar itself and shot Maud, and the brigand finished her, with `end` asking first. One Recall to after the hexer's death, Maud stepped off the door; Ottilie fell on turn 4 and Radiance ran dry on turn 5, so the braced Maud lived to turn 10 but could not open the door. Lost, 8/7/7.

## Kill criterion and next

Kept if a play other than this one names a turn decided by the archer's wake (a door strike held back for it, or a step onto 7,2 or the altar priced by it). Revert to Hold if Chat's cold chair (Wren or Pell, the line that does not cork) finds the map unwinnable from the south with a non-corking ally. If the lever plays flat, brigand-1 to 4,4 is next (round 424).
