# 0168 — A bow's crit on a flier grounds instead of tripling (#723)

Date: 2026-10-02. Issue #723. The rule is the Table's (rounds 219 and 220 on #665; supersedes round 216's "+20 crit, x3 kept" and 0156's non-lethal-only clause). The shapes below are the Builder's and provisional. Amends 0156's Open.

## Decided

- **The rule (round 220).** When a bow crits a flier, the strike deals its plain damage and the flier, if it is standing when the combat ends, is grounded until its side's next phase ends. Both sides: an enemy archer's crit grounds a player flier (Rook) the same way. A crit from any other weapon, and a bow's crit on anything but a flier, still triples. Bows keep no effective tag against fliers, Gust keeps its own, and the bonus stays +20.
- **One number, read everywhere.** `Weapon.GroundsAgainst(movement)` is true for a bow facing a flier unless the bow is effective against fliers, which is exactly an `effective_bows: on` sample after `Grounding.ForMap`. So the samples keep the old bow and their transcripts replay unchanged. `Combat.ForSide` sets `SideForecast.CritGrounds` from it, and `SideForecast.CritDamage` (plain when it grounds, else x3) replaces the six places that tripled a crit by hand: the resolver, the watch shot, `Exposure.Worst`, `EnemyAi.Expected`, `SignatureCeiling.DamagePerCombat` and the Sim's `HeuristicPlayer.Outcomes`. Where the crit does not ground, the planners' floating-point expressions are kept as they were, so no other matchup's decision can move by rounding.
- **On screen.** The forecast prints `grounds N%` in place of `crit N%` for that side, including the counter, and drops 0156's ` (grounds)` suffix. The damage column is the same either way, and the console never printed crit damage. The item card reads `A crit on a flier grounds it for plain damage.` The protocol side carries `critGrounds: true` only when it is true (a reader takes false when it is absent), so no other forecast's JSON changes. The thin client's row prints `grounds` too. The Godot panel still labels the column CRIT.
- **The Sim.** Gate 5 now checks a crit's damage against the forecast's `CritDamage` as well as a plain hit's (before this, a crit's damage was never checked). Its line adds `bow strikes on fliers N (grounded G, killed K)` whenever a bow struck a flier. The counts come from gate 5's stream, which carries gate 6's games on the map, not from gate 1's heuristic games.

## Measured

`--full saltmarsh_ford`, 200 seeds: gate 1 50/200, against 47/200 under 0156 (inside the noise); gate 4 median drop 0.075, against 0.065 (still failing, as before). Gate 5: 76 bow strikes on fliers, 10 grounded, 21 killed, damage mismatches 0. `--smoke`: 91, 11 and 22.

## Open

- Code's warm play 723 never saw a grounding. Ottilie's bow hit the wingrider plainly at `grounds 23%`, and Teodor's lance killed it. Whether bows are still worth bringing against fliers is read on the Sim's kill count and a cold play. If they stop being worth it, the lever is the bonus (+20 up to +30), never the tag or the x3 (the issue).
