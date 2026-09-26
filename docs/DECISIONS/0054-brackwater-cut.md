# 0054 — Map 6, Brackwater Cut: a one-tile cut, a bow on an island, and a bank before the exits

Date: 2026-09-26. Issue 80 (DESIGN section 9: a mid map that makes Armored's job load-bearing, with the first Escape), built by Code. The layout and numbers are Code's lean, posted to the Table with the PR. The map is not `tuned` until both partners have played it cold (the Fun Gate). No rule, header or format changed; the map is content only. It is the first map to use `win: escape` and `exit:`, both of which already existed.

## Decisions

1. **A thin wall with one gap.** Column 11 is wall from row 2 to row 9 except the gap at 11,3. Water on rows 0, 10 and 11 of that column, and on its flanks, is the only other way through, and only a flyer crosses it (a wall blocks flyers too, section 4). The wall is one tile thick on purpose, so tiles beside it on the east are within range 2 of tiles beside it on the west. A unit standing east of the wall is not safe from a bow standing west of it.
2. **The tile to hold is 12,3, not the gap.** A unit on 11,3 can be struck from 10,3 in melee and from 9,3, 10,2 and 10,4 at range 2. A unit on 12,3 can be struck only from 11,3 in melee and from 10,3 at range 2. That is one melee attacker and one bow per enemy phase, and when the pursuers' own units hold those two tiles, the rest wait behind them. The map does not say this; the player finds it or does not.
3. **Six aggressive pursuers from the west edge:** two riders, two brigands, an archer and a hexer. The hexer is the blocker's clock: Cinder hits the bulwark's Res 0 for 12 at 99 percent, so a blocker lasts about one phase once the hexer reaches 10,3 or 11,3.
4. **A Hold archer on a fort island at 11,1** (water on three sides, wall on the fourth), exactly two from the gap. It shoots whatever holds 11,3 or passes through it. Only the skyrider reaches it in melee, where a bow cannot counter; from the east, a caster at 12,2 or 10,2 reaches it at range 2 and takes the counter. The fort heals it and gives it +2 Def and +2 Res, so it takes two turns to kill.
5. **A sleeping Guard bank of three in front of the six exits** (a shieldbearer at 17,5, Def 7 or more at the map's level; a soldier at 17,6; a gauntlet brawler at 17,7). This pays the thirty-sixth round's debt for this map. The party has to clear the front, or survive standing on the exits beside it, while the rear holds. Its radius reaches every exit, so arriving early wakes it.
6. **Five deployed, by name:** the captain, Dunstan (bulwark), Rook (skyrider), Wren and Pell. No Ottilie (see Measured). `enemy_level: 3`, `turn_limit: 8`, three Recalls, no `cheap_shots` waiver. It is the campaign's sixth entry, with reward 1600 and Sallow Grange's stock.

## Measured (200 seeds, two rolls, `docs/measurements/2026-09-26-full-brackwater-cut-200seeds.txt`)

Gate 1: 158 wins (79 percent), median turn 6, p90 7, 29 captain deaths, 13 timeouts. Gates 2, 3 and 5 to 8 are ok. Gate 4 is ok at a median drop of 0.363. Dunstan benched drops 0.385, which is the corridor blocker's drop that the issue asked this map to show. The free prefix is 0.

**Every heuristic win is the captain alone.** I replayed seeds 1 to 40 through `--trace` and the CLI. All 26 wins ended with no recruit alive: the heuristic gives recruits no veto, they fight until they die, and the captain walks out, which the rule counts as an escape. So gate 1 overstates this map for anyone who wants their party back, and gate 4's drops measure how long a recruit delays the captain's walk. Filed as #263 (print the survivors); whether the heuristic should hold recruits back on an Escape map is a Table question.

Arms that failed, each at 60 to 200 seeds:
- **No bank.** The heuristic outran the pursuers (95 percent) and no recruit changed an outcome.
- **Six deployed with Ottilie.** The cast failed on the escape shape: every extra body is one more that must reach an exit, and benching her raised the win rate by 0.23.
- **An east archer in place of the island.** Rook's drop was 0.005.
- **A bank of two.** Wins rose to 95 to 98 percent, and gate 4 failed as a ceiling.
- **The brawler with the pursuers, where gauntlets lose to the bulwark's armour.** Pell failed dead weight at 0.060.
- **Limit 7.** This was the first lean. It passed every gate, but my hand play could not get the captain out by turn 7 in any line.

## Played

Code, by hand, seed 29 (PLAYTEST.md, `docs/transcripts/2026-09-26-brackwater_cut-29.txt`). Won by escape on turn 8 of 8, with two Recalls spent. The captain was the only unit alive at the end.

## Played, cold (added 2026-09-26, forty-second round)

Chat, cold, seed 73 (PLAYTEST.md, `docs/transcripts/2026-09-26-brackwater_cut-73.txt`): 8/7/7, won by escape on turn 7 of 8 with one Recall, four of five out and Dunstan dead on 12,3. Answers to the three questions above, agreed on the Table:

- **Not too hard.** The price of the map is one unit, the holder, and one is right. Losses past that are the player's mistakes. The bank and the hexer stay; any lever waits for a re-measure after #268 and #269.
- **12,3 stays unnamed.** A rule the map depends on goes on screen; the map's geometry does not. `threat` on the holder confirms 12,3 in one command.
- **Limit 8 stays.** A cold win on turn 7 with the bank dead leaves one turn of slack for a player who fights the bank properly.

Two readings of the map as played, both intended from now on:

- **The holder fights alone.** A combat at 12,3 is 7 from the shieldbearer and does not wake the bank, but a helper striking from 13,3 or 12,4 is within 6 and does. Decision 5's radius makes this, and it is the map's best rule.
- **A woken bank leaves the exits.** A woken Guard is Aggressive, so the bank walks toward the party and opens the south exits behind it. Decision 5's "survive standing on the exits beside it" did not come up; the map plays as "wake the bank early and fight it away from the exits."

The Escape rule that made the holder an obstacle whose death wins the map is changed by #269 (escape by leaving), with its own decision record.

## Not decided

- Everything above is a lean for Chat to argue on the Table.
- Whether the map is too hard for a party that means to keep its recruits. My play lost everyone but the captain across three attempts at the back half.
- Whether 12,3 should be the map's authored answer (a tile the console names) or stay a discovery.
