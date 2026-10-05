# 0262: The Sky Captain's passives, Rook's drake frost and Stoop for the rest

Date: 2026-10-05. Lotus's design, relayed on the Design Table (#1112, comments 6001779566 to 6001917373); Chat's numbers and rules (6001937747), agreed by Code (6003215791). Built on #1127. Provisional until play or Lotus says otherwise.

## Context

Lotus wanted the Sky Captain to have a class trick of its own, so that taking it over the Drake Warden is a real sidegrade. After two withdrawn shapes (Strong Bond, a frost strike with uses) he settled on a passive frost where Rook lands, Rook's alone, and asked for a different passive for every other Sky Captain. Chat set the numbers and proposed Stoop.

## Decision

- **Drake Frost** (`drake_frost`, `damage` 1, `rest` 1) on the `skycaptain` class, live only on a rider with a drake. It fires when she ends a Move she flew (at least one tile, not grounded) with an enemy orthogonally beside her. Each such enemy takes 1 and, unless a boss or already held, is held: Mov at most 1 and no Canto through its side's next phase, on the chill's clock (`frosted`). No friendly fire. A landing beside no enemy never spends it. Order: move, frost, then her action. Fired on turn N, it is ready on N + 2.
- **Stoop** (`stoop`, `flight` 4, `damage` 2) on the same class, live only on a rider without a drake. When she attacks after flying 4 or more tiles this phase (take-off tile to striking tile), her first strike adds 2 when it hits, after any crit. Never on a counter.
- Both are class abilities, so the enemy Wing Captain (the Rookery) stoops too.

## Built (#1127), and the calls made in building

- **The frost never kills.** Its damage floors at 1 HP, as burn and the rock do. Chat had allowed that 1 could finish a unit at 1 HP; a kill would need the whole death path (EXP, keepsakes, grudges, the bound, the hunger) for a passive that is meant to be the lock, not the damage. Reversible: a later round can let it kill.
- **What counts as a flown landing:** a `move` only. A carry, a Canto, a dash, a shove, a Fall back and a grounded walk never fire it.
- **"Does not stack":** a unit already held takes the 1 but is not held again or refreshed; the hold reads Mov at most 1 after a chill, so a chilled and held unit is at 1.
- **Previews:** `move ... preview` and `threat from` print `Frost: N enemies, 1 damage, held to 1 tile (...: no hold, boss)`; `threat from` prices the coming phase on the frosted board (`DrakeFrost.Strike`). The forecast prints `; Stoop +2 on the first strike` and the protocol carries `stoop` on a side, `frosted`, `frostTurn` and `flewFrom` on a unit, and a `unitFrosted` event.
- **The enemy planner does not price Stoop** (`EnemyAi.Score` builds its own combatant); the resolver applies it. Only the Rookery's Wing Captain has it, and changing the scorer reruns `--full --all` (#971), so it waits for a reason.
- The Rookery's journaled transcript is regenerated: the Wing Captain's dive takes Rook to 2 where it took her to 4, the same play.
- `--drover`'s price ceiling is unchanged (player median 0.719, enemy 0.727; passes): it is a static combat measure and reads neither passive.
- The `frost_landing` effect is listed on #621 for the clip batch; it is not drawn here.

## Kill clause and levers

- Frost: if a journaled play on the keep or an escape map shows a hold stopping a whole enemy push for free, the rest goes to two turns before anything else changes.
- Stoop: if a hired Sky Captain on the Sim is not clearly ahead of her as a Skyrider, the lever is +3, never a wider trigger.
