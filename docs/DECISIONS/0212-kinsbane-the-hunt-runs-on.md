# 0212 — Kinsbane: the hunt runs on, the waking as a gain (#804 item 4; amends 0211)

Date: 2026-10-03. Built by the chain Builder. The rule is round 251's, settled on #780 and recorded on #804: "once a map, a woken scythe kill gives Keziah her full Move again, with no second strike". Chat's first version (remaining Move) was the fallback; Code's (full Move) was taken. 0211 stopped the feed levers, so this slice changes no number in the feed.

## What is built

- **The rule** (`Kinsbane.RunsOn`, after an Attack in `Resolver.Apply`, before the Canto step). Once a battle, a kill with the hungering weapon on its carrier's own Attack that leaves it woken, the waking kill included, owes the carrier a Canto of its full Move: `Kinsbane.HuntMov`, the Mov `ReachOf` reads (a Press counted, the chill taken off, 0 while locked), never less than a Canto already owed. The Canto moves only, so there is no second strike. A counter-kill grants nothing, because a Canto cannot be taken on the other side's phase. The engine's Canto does the rest: the console prompts `may move again`, `canto <unit> <x,y|stay>` spends it, the phase change clears it, and the Sim's heuristic takes it to its safest tile.
- **Once a battle** is `BattleUnit.HuntRan`, set when the hunt runs and never cleared. It is board state, so Recall restores it. The protocol's unit carries `huntRan` only when set.
- **On screen.** Event `huntRanOn` (`unit`, `mov`): `the hunt runs on: Keziah may move again, 4 movement`. The forecast adds `kill: keziah moves again, N movement (the hunt runs on, once a map)` for the striker on its own phase while the charge is unspent and the kill would leave the scythe woken; on the waking kill it prints under the feed line. The woken card reads `Woken: no drain. A kill: move again (once a map).`, then `The hunt has run this map.`
- **A sample switch.** `woken:` naming the `kinsbane:` bearer issues the scythe already woken (fed 12) instead of looking for an heirloom: `docs/samples/the_gleaning_kinsbane_woken.map`, so a chair can play the hunt from turn 1.
- **`--kinsbane`** prints, per map, the runs in which the hunt ran on.

## Choices made here (provisional)

- **The waking kill counts.** The fed count after the kill decides, so the hunt can run on the turn the scythe wakes. Reading it before the kill would make the fifth tooth a dead turn.
- **Any side's carrier** gets it by the same rule; the enemy AI never takes a Canto, so in practice it is Keziah's.

## The reading (200 runs, same seeds, `docs/measurements/kinsbane-804-hunt.txt`)

The raid, Sallow, Brackwater and the field win and feed exactly as in 0211; the hunt fires only after the waking. At the field it ran in 15 of the 17 runs that woke, and at the keep in 10 of 13. The keep is won in 13 of 43 against 0211's 9 of 43 and the axe's 7 of 49, so the gain shows up where it should, on the map the waking is for.

## The play

Code's warm play on the woken sample (seed 804, `docs/transcripts/2026-10-03-the_gleaning_kinsbane_woken-804.*`): the hunt decided turn 2, both which brigand she killed and where the four free tiles took her. 7/7/5, stopped at turn 6. 804's kill criterion stands: kept if a journal shows it deciding a turn; a cold chair reads it next.

## Not in this slice

The voice (item 3), the hound in the clip (item 2), the choice screen (item 5), and drawing the hunt's Move range from the target's tile in the client (#535).
