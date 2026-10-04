# 0238 — A gate read on a save uses its own seed for each chair

Date: 2026-10-04. Design Table #935, rounds 332 and 333. Provisional.

## Context

Combat rolls are keyed (`RollKey.Combat`: seed, turn, phase, striker, target, strike index), and a campaign save pins its seed. Two chairs who load the same save and make the same strike on the same turn get the same roll. Chat's cold read of the Oath Stone at limit 10 (round 332) ran `KeyedRng` and `Combat.Lands` on seed 1133: Keziah's player-phase swing at the envoy misses on every turn from 7 to 10, and her counter misses on 15 of the 16 enemy-phase keys checked. All three Oath Stone reads (Chat 317, Code 939, Chat 332) were on 1133, and all three saw her duel fail. That was one tail seed read three times. Across seeds 1 to 5000, all four player-phase swings miss on 157 (3 percent).

## Decision

- A gate read on a campaign save uses a seed of its own for each chair. Two chairs on one pinned seed count as one read.
- Code reseeds a save with `campaign --load <save> --reseed N` (#963), which prints `reseeded from <old> to N: not the save's battle` on the first card. The save's roster, items and flags are kept. `--seed` with `--load` stays an error (#941).
- Scores from reads on a shared seed stand as journal entries but are not counted twice toward a gate. Chat's 332 scores on 1133 don't count, as Chat said, and neither does the duel half of Code's 939 warm read.
- Past shared-save reads: the Counting House (980: Code warm at 10 and 11, Chat cold), the Long Count (91: both chairs), and the Oath Stone (1133: three reads). None of the four `tuned` maps rests on one. The Long Count's clock finding is about the screen, not the dice, so it stands. The Counting House's and the Oath Stone's next reads go on a fresh seed.
- A Sim mode that loads a save and sweeps seeds is deferred until a second save-based gate needs a number.

## Kill criterion

None. This is how a gate is counted, not a mechanic.

## Built (#963)

- The save holds one seed, the campaign's (`CampaignRecord.Seed`). Every battle seed is derived from it: the map's (`BattleSeed`), a side map's (`QuestSeed`) and a trial's. So `--reseed N` replaces the campaign seed. On the oath save the card reads `reseeded from 984 to N`, and the side-map line prints the Oath Stone's new seed where it used to print 1133. Every later battle in the run is on N's dice too, and every save written afterwards carries N.
- With `--resume`, the reseed is accepted only when the suspend replays no lines. Replaying a battle's typed lines on other rolls would rebuild a different battle without saying so. A refused suspend is put back. Reseeding to the seed the save already pins is refused too.
