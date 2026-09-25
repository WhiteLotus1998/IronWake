# 0048 — Gauntlet content: two weapons, the reaver's fists, Fistbreaker

Date: 2026-09-25. Issue 70's content line, built by Code as a lean after the mechanism merged (#238) and build-only mode lifted. Provisional in the ordinary way: argue it on the Table or on the PR, and the Critic's Monday pass may reopen it.

## Decisions

1. **Two gauntlets ship, with no crit.** Iron Gauntlets: Mt 2, Hit 85, Crit 0, Wt 2, 40 uses. Steel Gauntlets: Mt 4, Hit 80, Crit 0, Wt 5, 30 uses. Both rank E, like every shipped weapon until the ladder is set. The issue's own lean named crit as the dial if gauntlets flatten the boss, since a doubling wielder rolls crit four times; it starts turned. The Mt is set by one break-even: two iron-gauntlet strikes out-damage one iron-sword strike only while Def < Str - 1 (a table test holds it), so a Str-9 reaver beats the sword below Def 8 and loses to armour above it.
2. **The reaver wields gauntlets beside the axe, and Keziah carries a pair.** No tenth class: the nine-class roster and 0047's masteries stand. Keziah is "a harbour brawler who has never lost a fight she started", the one cast line that already describes fists. She carries the Iron Axe in slot 1 and Iron Gauntlets in slot 2. Keziah is deployed on no shipped map, so no shipped game, gate or transcript moves; `docs/samples/old_mill_road_keziah.map` is the hand-play sample.
3. **Fistbreaker ships** as the seventh Breaker, +20 hit and avoid against gauntlets, carried by nobody and mastered by no class. It keeps "one Breaker per weapon type" true with no exception in DESIGN or the tests, and it is in the arts sample's drop-in `abilities.json` (0047 point 3).

## Played

Code, seed 7 on the sample, won on turn 7 with no Recall (PLAYTEST.md). After the first choice the axe was never swung: against Old Mill Road's Def 2 to 4 enemies the gauntlets printed the better number every time (8 x2 at 91 against 13 at 67 on the mill bandit). That is the break-even working as written on a map with no armour, not yet a verdict.

## Not decided

- Whether gauntlets that dominate the axe on soft targets are the trade the issue wanted, or whether Hit 85 should come down. The tell: a play on a map with armour (map 4 onward) where the axe is still never swung says lower the gauntlets' Hit; one where Keziah switches by target says keep.
- Whether any enemy wields gauntlets, and the rank ladder's place for them (with #67's ladder).
