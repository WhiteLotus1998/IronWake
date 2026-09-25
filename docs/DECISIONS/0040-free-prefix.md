# 0040 — The free prefix and the turn-state counters (issue 47)

Date: 2026-09-25. Issue 47, agreed on the Table (the Critic's first pass, Chat's reply, Code's two additions and Chat's two amendments; DIALOGUE.md). Built by Code. It is an instrument: nothing here changes an exit code, and gate 9 is not written.

## Decision

`--full` prints, after gates 1 to 8 on each map:

- **The free prefix.** For N in 2, 4 and 6, `PrefixPlayer` plays turns 1 to N as the random legal player (seeded as gate 2) and hands over to the heuristic from turn N + 1. This is paired by seed against gate 1's baseline, using gate 4's drop and standard error (sqrt(b + c) / n over the discordant seeds). A length is **free** when its drop is at most twice its standard error. A negative drop, where a random opening helps, is free too, since it is not a cost. The free prefix is the largest tried length with every shorter one free, so a free 6 behind a costly 4 still reads 2.
- **Both slack figures.** Gate 1's median slack and `limit - p90(winning turn)`, on the free prefix's own line.
- **The refund.** At the boundary N only (the first length that is not free), that arm re-runs once with the limit raised by N, against the unraised baseline. If it is still not free, the cost is **positional**. If it is free, the cost is **at most tempo**. It never runs on a map free at every length tried.
- **The turn-state counters.** These are read at the start of each player phase of the baseline's own games, since those are the boards gate 1 measured. A turn is **live** when a player unit can reach a tile from which a weapon it carries strikes an enemy, or an enemy can do the same to a player unit. A holding enemy strikes only from its own tile, which is how `Exposure` reads it. A turn is **quiet** when it is not live and a player unit can end within the wake radius of a sleeping Guard group. Otherwise it is **dead**. Members of a sleeping group count on neither side, because striking one is the decision to wake it, and that decision is the quiet state's. The wake tax is the reach set's overlap with sleeping radii, own tile included, from the player unit with the largest fraction. The report prints medians over games: dead turns, quiet turns, the leading dead run, first contact, and first quiet at or above the tax floor. A game that never gets there counts as later than every game that does, so the median is a dash when at least half never do. It then prints one row per turn with the count of games in each state and the median tax.
- **The tax floor is 0.25, provisional,** set with `--taxfloor F`. The issue named 6 percent as noise and 44 percent as a decision, and left the value to the journals. A quarter is the lean between those two points, and nothing gates on it.

Lengths stop at 6 for now, including on a map free at 6 (Saltmarsh Ford below). With its baseline at 11 percent, a longer arm can only read free again (see the reading below). Extending the lengths waits until that baseline moves.

## Measured (two rolls, 200 seeds; `docs/measurements/2026-09-25-full-free-prefix-200seeds.txt`)

| Map | Gate 1 | Free prefix | Arms (drop, se) | Refund | Slack median / p90 | Leading dead, first contact |
|---|---|---|---|---|---|---|
| old_mill_road | 29 percent | **2** | 2: 0.030 (0.041); 4: 0.225 (0.038); 6: 0.290 (0.039) | at 4: 0.155 (0.040), **positional** | 4 / 2 | 1, turn 2 |
| saltmarsh_ford | 11 percent | **6** (floor, see below) | 2: -0.020; 4: -0.020; 6: 0.035 (0.030) | none | 9 / 5 | 1, turn 2 |
| the_tollgate | 69 percent | **0** | 2: 0.150 (0.050); 4: 0.475; 6: 0.680 | at 2: 0.055 (0.048), **at most tempo** | 2 / 1 | 1, turn 2 |

`--full` on Old Mill Road went from 53 to 94 seconds, less than double.

## How to read it

- **Old Mill Road agrees with both journals.** Turns 1 and 2 can be thrown away. Turns 3 and 4 read quiet in 99 and 108 games of 200 and dead in 10 and 45, with a median wake tax of 0.15 and 0.06. Both partners' entries on 0034 name those same two turns as the walk to the stand. Throwing away four turns is a positional cost that survives the refund. The mill is where the map's decisions are. First quiet above the floor is turn 5, the stand.
- **Saltmarsh Ford's free 6 is a floor, not a finding.** With the heuristic winning 11 percent, a random opening has almost nothing to lose. The number becomes readable only once gate 1 is.
- **The Tollgate's 0 is tempo.** The limit sits one turn past the p90 winning turn, and refunding the two turns brings the arm back inside noise. That matches the twenty-third and twenty-ninth rounds, where the limit was set to the honest line plus one.
- **No map has a quiet turn after the opening except Old Mill Road.** Saltmarsh's fort group and the Tollgate's keep never read quiet at the start of a phase. The reason is not that neither has sleepers. Every turn on those maps is already live from the open fight.

## Not done

- Nothing is gated. Gate 9 waits for the Table, per issue 47, which also requires a map where the number is non-zero and the journal disagrees with it.
- The per-turn rows read the baseline's boards only. The random arms' own boards are not read.
