# 0148 — The hunt: Marrit's rule and `threat` by front, as built

Date: 2026-10-02. Issue #692 slice 2 (Chat's round 214: "Marrit hunts the weakest front ... and `threat` shows her next front"). The rule's shape is the Table's; the readings below are the Builder's and are provisional.

## Decided (the Table, restated)

- The sworn elite targets the front with the lowest summed HP of player units, recomputed each enemy phase, and `threat` shows her next front.
- It is a header, not keep-only code, built on a `docs/samples/` stand-in until #656 opens the keep's authoring.

## Decided (the Builder, provisional)

- **`hunter: <x,y>`** names the enemy placed on that tile, as `messenger:` does. The map needs `fronts:`. A boss never hunts (the veto and the hunt would both pick its tile), nor does the messenger. A spawned unit cannot be the hunter yet; Marrit is placed.
- **Who defends what.** A player unit defends the front nearest it by Manhattan distance to the front's nearest tile, within 3, the earlier front in file order on a tie. A unit farther out defends nothing. This is geometry only, so a front with nobody near it sums to 0 and is the first hunted: an empty breach is the weakest point.
- **Which front.** The standing front with the lowest summed defender HP, the earlier on a tie. It is chosen once as the enemy phase begins and held for the phase (`BattleState.Hunting`), so wounds dealt earlier in the phase do not move it and the front `threat` named on the player phase is the front that comes. Fallen fronts are never hunted, and with all fallen the hunter is an ordinary enemy of its behavior.
- **What hunting means.** The hunter's targets are narrowed to the hunted front's defenders, for its strike and its approach alike. With no defender to approach it marches on the front's tiles (the party left out of the field, as Drift does), so it walks through an empty breach and fells it. It still counters anyone who strikes it. One narrowing feeds the planner, `threat` (`StrikeOn`), the veto refusal, the grudge and the anvil, so they agree by construction.
- **`threat` by front.** On a map with fronts every `threat` names the unit's front (`Front: defends the gate`, or none past 3 tiles), and on a map with a hunter it adds the hunt line, read with the unit moved to the asked tile. The board's fronts line adds each standing front's defenders and HP and marks the hunted one. A whole-board threat grouped by front is not built; the per-unit rows plus the fronts line were enough to read twelve units in play (see the journal).
- **The planner and the Sim's veto stay blind** to the hunt for every other enemy. The Sim's exposure sum still counts the hunter against any unit (the conservative worst case).

## Found in play (Code 693, warm)

The stand-in hunter is an ordinary L2 rider. The hunt read at once: the board said `south ... (hunted)` before turn 1 ended, and the rider went for Teodor, the south's lone defender, exactly as printed. But a hunter that goes where the board says is a hunter you can bait. Teodor, a pikeman with a 76 percent counter, killed it on turn 3. The strength of slice 3 (Marrit among the two strongest units, held by the pair rule) is what makes the hunt a threat rather than a lure. The bait is the counterplay the readable rule invites; keep it.

## Not built here (slice 3 of #692)

The hold-then-Defeat-Boss objective with Hask's escape as a loss; Marrit's and Hask's stats and the pair rule's test; `--full` measuring the keep full, depleted and floor with its time and median turns.

## Kill / revisit

If plays show the hunted front never changes once set, because the player always leaves one front light on purpose, the hunt is a lure and the lever is the radius or a minimum-defender clause. If a cold chair cannot predict where she goes from the board alone, the rule is too clever; the fronts line is the place to fix it.
