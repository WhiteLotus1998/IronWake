# 0149 — Hold, then the boss: `spawn boss` and the Defeat Boss win that waits for him

Date: 2026-10-02. Issue #692 slice 3 (Chat's round 214: "hold every front until the announced turn, then Defeat Boss ... Only the captain's death or Hask's escape loses the map"). The objective's shape is the Table's; the readings below are the Builder's and are provisional.

## Decided (the Table, restated)

- The finale's objective is to hold until the announced assault, then defeat the boss. A fallen front never loses the map (0147); the captain's death or the boss's escape does.
- The assault is announced from turn 1 in player words, like every announced event (rounds 42, 44).
- It is a map-file feature, not keep-only code, built on a `docs/samples/` stand-in until #656 opens the keep's authoring.

## Decided (the Builder, provisional)

- **No new win condition.** `win: defeat_boss` plus a boss that arrives by event is the whole objective. An event action `spawn boss <template> x,y group:<g>` places a boss (behavior `boss` by default, or `guard`). A `defeat_boss` map may then have no `B` line.
- **Won only once he has come and gone.** A Defeat Boss map is won when no boss stands and every boss spawn has fired. Before the arrival the map cannot be won by killing everything else.
- **A held tile never stops the boss.** Every other spawn is blocked and spent by a unit on its tile (DESIGN 10). A boss's is not: he lands on the nearest free tile he can stand on, so the hold-then-boss map cannot be won by parking a unit on his road. The announce line and `help` both say so: `the boss, a bandit leader, arrives at 0,6. A unit standing on 0,6 does not stop the boss, who takes the nearest free tile.`
- **A boss spawn needs a turn trigger.** An arrival on an enter or a fall would be a boss the board cannot name a turn for, which the announced-assault rule forbids.
- **His escape is the turn limit.** A turn limit passed with the boss standing reads `Lost because turn L ended and the boss got away.` No new escape rule (an exit tile the boss walks to) is built; the clock is the escape until a play says it needs to be a place.
- **The objective line** reads `Hold until the boss arrives on turn N, then defeat the boss by the end of turn L.` The pronoun-free wording stays until the story names him.

## Found in play (Code 694, warm)

The stand-in (`docs/samples/ironwake_keep_assault.map`: the hunt sample with the boss moved off the board into a turn-6 `spawn boss` at the gate road) lost on turn 6, the phase he arrived. The objective line and the announce read at once; the board said what turn mattered. But six L1 units against sixteen L2 enemies never reached him, and he arrived on the far edge with `behavior: boss`, which waits, so with five turns left the party would have had to march out to him through the field. Hask "leads the assault": the boss of a hold-then-boss map probably wants to come in. That is a reading for the measured slice, not built here.

## Not built here (slice 4 of #692)

Marrit's and Hask's stats and the pair rule's test (neither kills a full-HP unit in a two-unit pair in one enemy phase); `--full` measuring the keep full, depleted and floor with its time and median turns; whether the arriving boss advances.

## Kill / revisit

If a play shows the turn-limit escape reading as a timeout rather than as the boss getting away, the escape becomes a place: an exit tile he walks to once wounded.
