# 0400: Enemies carry their basic forms; Grit's both-sides reading fails its kill criterion

Date: 2026-10-09. Issue #1461, forms slice (a), second PR (a2). Builds on 0386 and 0398.

## Decided

- **Basic forms.** An art may carry `basic: true`; at most one per weapon type, refused on load naming the second. Heavy Cut (sword; Feint stays the captain's own), Long Thrust, Cleave, Aimed Shot, and Overcast are basic. On a `forms: on` map every enemy knows the basic form of each weapon type it carries (`GameContent.FormsOf`), under the same Grit rule as the player side. Off the header nothing changes.
- **The enemy's choice is the player's rule.** On the attack it has already picked, the planner declares the affordable lethal form with the best hit when that form kills on its hit and the plain strike does not (`EnemyAi.LethalForms`, `EnemyAi.Form`). A boss under the veto declares none, since the veto prices plain weapons only (0251). The planner's choice of target and tile does not price forms. That is left for a later slice, if Grit survives.
- **`threat` prices it.** The planner is asked on the phase-start board, where the enemy's Grit already counts its phase start. The line names the form (`... (slot 1) as Cleave (2 of its 3 Grit): acc 25% dmg 20 ...`), and the total and `end`'s lethal ask carry it. The protocol's threat line carries `form`. The enemy phase's forecast line prints the declared form's numbers. Before this fix the printed line read the plain strike while the resolver struck with the form.
- **The Sim** counts both sides' lethal form offers: the player's as in 0398, and the enemy's read before each enemy attack resolves.

## The reading (`--full --all --forms`, 200 seeds)

| | offers | unaffordable | 2-cost affordable |
|---|---|---|---|
| player | 514 | 15 (3 %) | 499 of 514 (97 %) |
| enemy | 2789 | 464 (17 %) | 2325 of 2789 (83 %) |
| both | 3303 | 479 (15 %) | 2824 of 3303 (85 %) |

**Grit at 2 fails its kill criterion on both sides:** 85 % against the 80 % line. Most of the enemy's unaffordable offers sit on the fixture and the lesson (old_mill_road 150, starting_alone 105), where an enemy meets the company on its first phase at Grit 1. On the campaign maps the enemy is at 90 %.

The hand play fails it too (PLAYTEST, Code 1461 on `docs/samples/the_tollgate_forms.map`). By turn 3 every unit on both sides sat at 3/3, and the column was never a reason for a choice.

Gate 1 under forms, against main's cells: the Tollgate 194 (191), Brackwater 134 (133), Harrow Weir 153 (132), Sallow 177 (173), Saltmarsh 63 (54), the field 130 (104), the Mill 115 (115). Forms on both sides make the maps easier for the Sim's player, not harder. The enemy's lethal-on-hit rule trades a likely plain hit for an unlikely kill: Cleave is -20 hit. The hand play saw the same thing, a 25 % Cleave where the plain swing was 45 %.

## Not decided here

- What replaces Grit at 2. The criterion says it falls back to cooldowns (0386). The Table can amend the shape once before that lands (round 559 has Code's lean). No `forms: on` map ships either way.
- Whether an enemy should weigh a form's hit against the plain strike's. That only matters if Grit, or its replacement, keeps enemy forms.
- a3: the yard teaching forms, and "art" leaving the player's words.
