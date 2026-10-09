# 0398: Grit is built behind `forms: on`, the shipped arts re-costed; the Sim counts the kill criterion

Date: 2026-10-09. Issue #1461, forms slice (a), first PR (a1). Builds on 0386 (Lotus's Forms direction, rounds 544 and 545).

## Decided

- **The header.** `forms: on` (MapDefinition.FormsEnabled) turns Grit on for a map. Off it nothing changes: an art costs uses as before, no unit carries Grit, and no tuned cell or transcript moves. The header lets the kill criterion run before any map pays for Grit, and lets a killed Grit leave with one header.
- **Grit, as 0386 screened it:** 0 at the start; +1 as the unit's side's phase begins, the player side's first phase included (so the company opens at 1 and the enemy at 0); +1 for each hit landed on the unit in a fought combat, a counter's included; at most 3; nothing for a kill. A unit placed later starts at 0.
- **The price.** A declared art (a form) costs its `grit`, hit or miss, and is refused below it (`<form> costs 2 Grit and <unit> has 1`); its weapon pays only for its strikes. The event `artDeclared` carries `cost` 0 and `grit`; the forecast's technique line prints `costs N of M Grit`; every unit's row prints `Grit n/3`; the protocol state carries `grit`.
- **Re-costed, no new effects:** every shipped payoff art at 2 (Feint, Heavy Cut, Long Thrust, Cleave, Aimed Shot, Overcast, Read Ahead, Turn the Key, Paid in Full). Full Measure at 0: its once-a-map is its price until #1464 makes it Exhausted. Nothing costs 1 until a reposition form exists (#1462), nor 3 until Defend (#1463). `grit` is optional in content: absent, 2, or 0 on a per-map art.
- **The Sim.** On a forms map the heuristic player declares a form when it kills on its hit from the planned tile and the plain strike does not (the #1453 rule; a form that costs the next phase stays 0380's finisher's). Every such lethal form is counted as an offer whatever the Grit, and `--full <map>|--all --forms` prints per map and in total the offers, the unaffordable ones, and the 2-cost ones affordable, with the kill verdict (over a third unaffordable, or over 80 % of 2-cost affordable).

## First reading (player side only, so not the verdict)

`--full the_tollgate --forms`, 200 seeds: 97 lethal form offers, 0 unaffordable, 97 of 97 two-cost offers affordable. On the Tollgate, Grit is a free button: by the time a form would kill, the unit has had a phase start and usually a hit. The all-maps reading is in the PR. 0386's criterion is read with forms on both sides, so the verdict waits for a2.

## Not decided here

- Enemies carrying their weapon's basic form, `threat` and the forecast pricing an enemy's affordable form (a2); the hand play (a2); the yard teaching forms and "art" leaving the player's words (a3).
- What replaces 2 if the both-sides reading still calls it a free button: a higher start cost, a slower gain, or cooldowns (0386's fallback). That is a Table call on a2's numbers.
