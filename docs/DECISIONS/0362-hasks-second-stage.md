# 0362: Hask's second stage is built on the Warden sample

Date: 2026-10-08. Issue #1385; Lotus's rework and trigger (relayed on #1368 2026-10-08), Chat's round 487; unblocked by Lotus 2026-10-08 ("build Hask now, and tune the keep once"). Every number is provisional on #1247.

## Built

- **The swallow.** A template's `swallow` block (`hp`, `def`, `res`, `heal`, optional `description`) gives a boss a second stage, carried on the battle unit (`kin`) and in the protocol. The hit that takes him to 0 before he has swallowed (an attack or counter, a watch shot, an area cast, a line strike, a windup blow, Frozen Iron) is not his death: `shardSwallowed`, he stays on his tile on a fresh bar of 40 with Def and Res +3, and the card's line reads that the pommel is empty. That hit pays no kill EXP, frees no bond, swears no grudge, drops nothing and does not win the map.
- **Frozen Iron.** From the next phase start, at every phase start of either side, every unit on the board takes it, his side and him too, flat past Def and Res: 2, then +2 each phase. It can kill (`frozenIronFell`, then `unitDied` per kill).
- **The Kin's heal.** 6 at his side's phase start, after the Frozen Iron, to max (`kinHealed`).
- **His fall.** `coldDrained` follows his `unitDied` in stage 2: he is himself for his last line (placeholder, Lotus writes it) and the shard lies on his tile.
- **The Sim.** `--finale` prints a `stage 2:` line: games that reached it, won, median phases, games by player units the clock killed.
- `hask_warden` carries HP 40, Def +3, Res +3, heal 6. The campaign keep keeps the stand-in `hask` (0346), with no stage.

## Code's leans (open to the Table)

- **Frozen Iron stops at 10.** The issue lists 2, 4, 6, 8, 10; whether it climbs on past 10 is Lotus's.
- **Order at his phase start:** Frozen Iron lands, then the Kin heals. So the heal answers the cold, and a dose that kills him lands before the heal.
- **Riders on the swallowing hit land on stage 2** as on a survivor (chill, burn, mark), as round 500 ruled for the shell.
- **The lance's own description is unchanged.** His card's description line carries the empty pommel. Content has one Warden's Lance description, and the stand-in shares the lance.
- **The planners do not price the stage.** The Sim's player reads a stage-1 kill as a kill, and the boss's veto still guards stage 1's bar. The Sim gate's reading (`--finale` on the sample) says whether that matters.
