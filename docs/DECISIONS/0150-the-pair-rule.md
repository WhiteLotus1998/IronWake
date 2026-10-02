# 0150 — The pair rule, and the finale's two strongest units

Date: 2026-10-02. Issue #692 slice 4 (Chat's round 214: "neither can kill a full-HP unit that stands in a two-unit pair in one enemy phase"; Lotus: Hask and Marrit "should be really strong"). The bound is the Table's. The rule that makes it hold, the numbers and the expected level are the Builder's, and all of them are provisional.

## Decided (the Table, restated)

- Hask and Marrit are the two strongest units in the game.
- They are bounded by `threat` (no lethal it does not print) and by the pair rule, so a plan beats them rather than a lucky crit.
- Enemy numbers never scale to the company.

## Decided (the Builder, provisional)

- **The pair rule is a printed rule, not just a ceiling on stats.** As a pure stat bound, "never kills a paired unit" would hold the bosses below a third of the weakest body's HP per strike once a crit's x3 is counted. Then they would be weaker than an L6 brigand. So instead: `pair_rule: <group>[, <group> ...]` (a map header, like `oathbound:`) binds the named enemy groups. A bound enemy that strikes or answers a unit with an ally orthogonally beside it strikes **once, never doubling, never critting**. Against a unit alone it keeps its double and its crit.
- **Read live, on the board as it stands.** The ally is read when the strike is priced, the same as the pincer. If a partner falls earlier in the enemy phase, the guard is gone for the next strike, and the forecast printed before that strike says so. Seed 695 shows it: Teodor fell to a brigand, then the hunter's forecast on Dunstan read `x2 crit 8%`, and she killed him.
- **One flag carries it** (`Combatant.PairHeld`), so the forecast, `threat`, the planner's score, the heuristic's exposure and the resolver all read one number.
- **The stand-ins:** `finale_lord` (Sworn Lord, bulwark L10, steel lance, the spawned boss) and `finale_hunter` (Sworn Hunter, outrider L10, iron lance and sword, the hunter). They are template ids rather than names, because the names are story and #656 holds them.
- **Held by content tests at an expected finale level of 8** (`FinaleStrengthTests.ExpectedFinaleLevel`). Each cast member is read in its own class, and each cadet in every class it could certify into, with every stocked weapon. The barracks hires are read at 8 less the barracks' two.
  - **Strong:** more HP and a larger stat total than any of them.
  - **Beatable:** every cast member has a stocked weapon that deals at least 3 per strike.
  - **Fair:** held by the pair rule, one combat never kills any of them at full HP on open ground.
  - **Dangerous alone:** the captain can die to either of them on the double or a crit.
- **The weakest bodies set the ceiling.** A hire adept has 17 HP at level 6 and the cast's casters 18, which caps one held strike at about Atk 18. That is why the lord is a wall (47 HP, Def 12 effective) rather than a hammer.
- **`play --level N`** raises each deployed member below N to N on the average growth enemies scale by. It lets a late-campaign board be played without the campaign before it. It is a play tool; no map or record reads it.

## Found in play (Code 695 warm, `docs/samples/ironwake_keep_pair.map` at `--level 8`)

- The rule reads. The board names it and who it binds. With Pell beside Wren, the hunter's forecast was `dmg 12 hit 80% crit 0%`, one strike. Wren still fell on turn 4: the archer's 6 plus the hunter's 12 against her 15 HP, exactly the 18 `threat` printed. The rule bounds the boss, and the wave still kills.
- The first lord (Def 15 effective) took 0 from every iron weapon at level 8; only Pell's Cinder hurt him. That would be a stat wall, so the beatable test was added and his Def lowered to 12. The lord never fought in the play (he waits on arrival, as in 694), so the replay is unchanged.

## Not built here (slice 5 of #692)

`--full` measuring the keep full, depleted and floor with its time and median turns. Also whether the arriving boss advances.

## Kill / revisit

- If plays show that standing in pairs is always right, so the rule is a tax and not a choice, the rule drops to crit only. The numbers then come down to match.
- If a partner's death mid-phase reads as a betrayal rather than a warning, the pair is read once as each enemy phase begins, as the hunt's front is (0148).
- Expected level 8 is replaced by the measured finale level once slice 5 reports it.
