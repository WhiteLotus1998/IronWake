# 0147 — Fronts: the keep finale's first slice, as built

Date: 2026-10-02. Issue #692 (Chat's round 214, from Lotus's finale ruling). The finale's shape is the Table's; the shapes below are the Builder's and are provisional.

## Decided (the Table, restated)

- The finale has fronts, and a fallen front does not end the map: its wave comes inside and the board prints it.
- The sworn never break, and the break forecast says so.
- These are headers and group flags, not keep-only code, built on a `docs/samples/` stand-in until #656 opens the keep's authoring.

## Decided (the Builder, provisional)

- **`fronts:` is one header.** `fronts: north 10,1; gate 10,5 10,6; south 10,10`. Names are one lower-case word, underscores print as spaces. A tile belongs to one front, and no enemy may start on one.
- **A front falls when an enemy stands on or moves through one of its tiles,** checked after every accepted command, once per front. The first build read only standing. In the hand play the gate's wave walked through 10,5 into the courtyard without felling it, because nobody stopped on the tile. A breach the enemy has walked through has fallen, so the rule reads the move's path too.
- **A fall fires `falls <front>` events.** That trigger's spawns may land off the edge (the wave inside the courtyard). Every spawn keeps the held-tile rule, so standing on the inside tile stops it. That makes "block the leak" a second verb beside "hold the breach".
- **What a fall costs is authored, not ruled.** The fall itself only prints and records. The cost is whatever the map's `falls` events bring. The map is never lost by a fall.
- **The sworn are the oath-bound.** Round 212's flag is #691's `oathbound:` group header. On a `break: on` map a sworn member never breaks, and the boss's forecast prints `sworn: will not break: <names>`. This builds what 0146 left "not yet": the mechanic is the issue's, and only the oath's story text waits on #656.
- **The planner is blind to fronts,** as it is to the tide. Enemies fell a front only by walking where they were going anyway. Marrit's hunt (slice 2) is the first enemy that reads one.
- **Sample:** `docs/samples/ironwake_keep_fronts.map`. It is the keep's 16x12 with three breaches in the wall at column 10, six deployed, survive to turn 8, waves on turns 2 and 3, and the assault announced for turn 5. One inside spawn per breach (two at the gate), an oath-bound van with its boss, and `break: on`.

## Not built here (slices 2 and 3 of #692)

Marrit's lowest-HP-front hunt and `threat` naming her next front; `threat` grouped by front; the hold-then-Defeat-Boss objective with Hask's escape as a loss; the pair rule's test; `--full` reporting the keep's time and median turns over full, depleted and floor companies; `deploy: all` placement on the stand-in.

## Kill / revisit

The issue's own: if both partners' plays say the fronts read as three small maps instead of one battle, they merge to two. Code's warm play (seed 692) lost on turn 7 with six units. The army is not tuned, and slice 3's measurement sets the wave sizes.
