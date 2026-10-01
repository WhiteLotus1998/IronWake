# 0130 — Kinsbane (13.23): the hungering scythe, a mechanics spike

Date: 2026-10-01. Issue 645. The rules are the issue body (Chat's round 193 with Code's round 194 amendments) and DIALOGUE's 13.23 line (rounds 195 to 197, 201). Built mechanics only, behind the story gate (#656): no voice lines, no lore, a placeholder description.

## What is built

- `weapons.json` takes `hungers: true`; one entry, `kinsbane` (axe, rank D, Mt 8, hit 70, crit 5, Wt 9, 30 uses, never priced, in no stock). The rules ride on the weapon, not a header (`Kinsbane`).
- The scythe's state lives on its `ItemStack`: `Fed` (lifetime kills), `Starved`, `Ate` (fed since its wielder's last phase start). Recall rewinds it with the board; the protocol carries it on the inventory (`fed`, `starved`, `ate`).
- Drain, feed, starve, ease, growth and the awake state are as DESIGN 13.23 states them, with the events `HungerDrained`, `HungerFed`, `HungerEased`.
- On screen: a forecast line `kill: +10 HP to max, fed 3, Mt +1` (for a counter too), and a unit card line `Kinsbane: fed 2, Mt +0, hungry: -5 HP at the next phase start unless it kills`.
- The sample header `kinsbane: <cast id>` puts the scythe at the front of that recruit's pack (dropping the last stack if the pack is full) and lifts the carrier's axe to D. The map validator refuses the header without a `P recruit:<id>` line for the carrier. Sample: `docs/samples/hollow_road_kinsbane.map`.

## Code's leans (provisional; the Table may move any of them)

- **Uses never fall below 1, in either form**, not only when starved. A weapon that is never repaired should never break, and "a strike at 1 spends nothing" then holds in both forms.
- **Binding is by usability.** While `fed` is above 0, every other weapon the wielder carries is unusable (`UsableWeaponAt` returns null for it), so menus, forecasts, the planners and counters all follow one guard. An Attack naming another weapon is refused with `Kinsbane has fed and will not be put down`.
- **Feed and ease read the combat the scythe fought**, strike or counter. A kill in a combat it did not fight (a windup blow, an overwatch shot with another weapon) feeds nothing.
- **The forecast prints the heal as +10 to max**, not a number capped at the HP before the combat, since the combat's own damage comes first.
- **Rank D on the sample carrier.** DIALOGUE puts the scythe in Keziah's pack from her arrival, but she arrives at axe E. On the sample the header lifts her to D. In the campaign, either she arrives at D or the scythe reads at E: a Table question (on the PR).

## Not built

The campaign (no shipped carrier, and the campaign record does not save the stack's state yet), the chest it is found in (#649), the Sim's re-read of the 15 percent ceiling at the cap (#635), barks, and the god.

## Keep test

Lotus's wish, so there is no kill criterion: a journal shows the hunger changing a decision. If none does, the numbers move (the drain, 5, and the heal, 10), not the scythe. Code's warm play 709 shows both clauses: a kill taken for the hunger, and a fed unit left in the open (PLAYTEST). Chat's cold play decides on a board that gives the loud route more than one fight tile; the sample's balance is not the rule's.
