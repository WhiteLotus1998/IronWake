# 0130 — 13.23 Kinsbane: the hungering scythe's spike

Date: 2026-10-01. Issue #645. The rules are the issue body's: Chat's round 193, Code's round 194 amendments, and the cap's true state from round 197. The comment of 2026-10-01 relays Lotus's story gate (#656): mechanics only, with no voice lines and no lore. Where the issue left an implementation choice, the Builder's leans are below; Chat can argue them on the PR. Provisional, like every spike.

## Decided

- **The rule belongs to the weapon, not to a map.** `weapons.json` takes `hungers: true`. A hungering weapon is physical and has no price, so it is never sold and never repaired; the validator refuses a price, a spell or a heal. The state lives on the stack (`fed`, `starved`) and on the unit (`hasFed`, set by a feed and cleared at each phase start of the unit's side). All of it is board state, so Recall restores it.
- **The issuing is a map header.** `kinsbane: <recruit>` puts the weapon, `kinsbane` in `weapons.json`, at the front of that named recruit's pack at full uses. If the pack is full, the last stack makes room. The map validator refuses a bearer who is not placed by name. The sample is `docs/samples/the_gleaning_kinsbane.map` (Keziah).
- **The drain** comes at each phase start of the carrier's side, after heal and burn. It skips turn 1, the side's first phase. It costs 5 HP if the carrier fed the weapon nothing since its last phase start. It drains whether the weapon is equipped or not, and never below 1. A drain that would take the carrier to 1 or below sets the carrier at 1 and starves the weapon. A starved carrier already at 1 gets no further line.
- **The feed** is a kill made with the weapon equipped, and a counter-kill counts. It heals 10, up to max HP. It ends the starved form, restores full uses, and sets `hasFed`. Every 3 kills give +1 Mt, up to +5.
- **The starved form** has half Mt, rounded down after the growth, and uses held at 1. Any hit it lands that kills nothing ends the form, restores the uses and heals 5.
- **The uses floor is general.** A hungering weapon never spends below 1, starved or not, so it never breaks. The issue gave the floor only for the starved form. Since the weapon is never repaired, a break would end it for good, which the feed's "uses restored" already rules out.
- **The cap.** At fed 15 (the kill that gives +5) the weapon wakes: no drain, no starved form, no feed heal. Kills still count and still restore uses.
- **On screen.** The forecast shows `kill: <unit> +10 HP, to max N (Kinsbane feeds, fed n)` for the striker, and for the target when it counters. In the starved form it adds `hit: <unit> +5 HP, the starved form ends`. `show` prints `Kinsbane: fed n. Mt +m.` followed by the weapon's state: `Hungry: -5 HP at the next phase start[, and it starves].`, `Fed this phase.`, `Starved: ...` or `Woken: no drain.` The events are `hungerDrained`, `hungerFed` and `hungerEased`. Protocol stacks carry `fed` and `starved`, and units carry `hasFed`.
- **Rank E for the spike.** The issue says "reads axe at D", but a level 1 Keziah has rank E, and DIALOGUE has her carrying it from arrival. So the sample weapon is an E axe at Mt 9, hit 70, wt 10 and 20 uses, between iron and steel. Whether the D is the weapon's rank or the rank it counts toward is asked under Unsure.

## Not built here

Bound once fed (no trade, drop or unequip): the camp's `drop` exists but no campaign carries the scythe yet, so the rule waits until the scythe has a campaign home. Also not built: the chest it is found in (#649), Keziah's arrival with it, the god and its voice (#656), and the Sim re-reading the 15 percent ceiling at the cap. The Godot client draws none of this.

## Keep test (from the issue; no kill criterion, as it is Lotus's wish)

A journal must show the hunger changing a decision: a kill taken that the forecast's best line would have left, or a starved unit forced into the open. If no journal shows it, the numbers are wrong, not the scythe, and the levers are the drain (5) and the heal (10). Code's warm play on seed 645 shows both clauses (PLAYTEST). Chat's cold play decides.
