# 0170 — Full Measure strikes once (#739)

Date: 2026-10-02. Issue #739. The rule is the Table's (#731, from Chat's chair play 737 on the #734 curve). The shapes below are the Builder's and provisional. Amends 0125.

## Decided

- **The rule.** An art may carry `single: true`. An attack that declares it strikes once, whatever the speed gap, and the target's counter is unchanged. Full Measure carries it: +8 Mt, +30 Hit and +20 Crit on one strike opens a boss fight but does not end it, the way 17 x2 did on Saltmarsh and 19 x2 did on Harrow in 737.
- **One number, read everywhere.** `CombatArtEffect.Single` is loaded, validated and serialized with the other art fields. `BattleUnit.ToCombatant` sets `Combatant.SingleStrike` from the declared art, and `Combat.Doubles` refuses a double to that side, the same way the pair rule's `PairHeld` does. The forecast, the resolver, `Queries.AttackOptions` and the client all build the striker there, so they read one number. Only a strike declares an art, so a counter never carries the flag.
- **It suppresses the double only.** A weapon type that strikes more than once a round still does so on its one round. No art of that kind exists.
- **On screen.** The forecast's art line adds `x1, never doubles`, and the damage column drops its `x2`. Full Measure's own text says `Strikes once, never doubles.` The menu row's forecast already reads `x1` through `StrikeCount`.
- **Transcripts.** Code's 636 strike play doubled with Full Measure. Its replay test now runs on a content copy with `single` taken off (`Fixture.DoublingStrikeContentDirectory`), since a transcript is a record of the build it was played on. A second test reads the shipped forecast at the same moment.

## Measured

The planner and the Sim's player declare no arts (#611), so `--curve` gate 1 was not expected to move, and it did not: Saltmarsh 50/200 before and after, Harrow Weir 123/200 before and after (200 seeds, file and campaign at level 2).

## Open

- Whether the next-phase cost is still worth paying when the strike no longer ends the map. That is read on Chat's 737 feed-the-recruits run and the next boss-map play. If nobody declares it, the lever is the cost (keep the phase), never the double.
