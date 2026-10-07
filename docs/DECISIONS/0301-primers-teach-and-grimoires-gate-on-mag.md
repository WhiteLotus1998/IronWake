# 0301: Primers teach a school, a learned rider is gated on Mag over Res, grimoires gate on Mag, and maps drop tomes

Date: 2026-10-07. Issue #1246. Source: Lotus's learning line (rounds 383, 384) and his follow-up on #1218 (points 1 and 2), Code round 417 (the gate reads Mag directly; lean), 0296 to 0300. Provisional.

## Context

Lotus ruled that power in magic is found, not given: a school's first spell is plain, grimoires drop and are gated on the magic stat, and a Lore unit can learn a school its class does not reach from a primer, but a learned school bites only past a stat check. #1246 asked for the four pieces as a system, with no content until Lotus signs his spell list (#1247).

## Decision

- **A primer is an item.** `items.json` takes `teaches: <school>` in place of `heals`, one use. Fire, ice and lightning only; the loader refuses earth (Brannock's, Lotus), a primer that also heals, and a primer of more than one use.
- **Read at camp.** `read <unit> <slot>` (`CampaignRecord.Read`) spends the primer and adds the school to the unit's own `Unit.Learned`. Refused, with the reason, for a class that does not wield Lore, a school the unit already reaches, and anything but a primer. In battle `item` refuses a primer ("read at camp, not in battle"), and neither `Legal` nor the Sim offers it. The verb is `read` because a primer is not used up the way a dressing is: it changes who the unit is.
- **The save carries it.** A roster unit writes `learned` (school words, in order) only when it has one, and a unit without it has learned none. PROTOCOL.md says adding a field is not a version change, so `protocolVersion` stays 1.
- **Reach.** A unit wields a tome of a school its class reaches or it learned (`Unit.Reaches`). The refusal names the class's schools and then the learned ones.
- **The learned rider is gated.** `LearnedGate`: a rider from a school the caster reaches only by a primer fires only when the caster's Mag is above the target's Res plus the rider's `gate` (`rules.json`, default 0; `null` is no gate). The numbers are the card's (unit, class, passives); a tile's Res is not counted, so the gate reads who the two are, not where they stand. A caster whose class names the school is never gated. It binds burn and chill; the stun is class-gated already and earth is never taught. The forecast prints the reading either way: ` burns 2 for two phases: Mag 6 over Res 4`, or ` no burn: Mag 4, Res 4` in place of the rider's words. A margin prints as `Res 4+2`. The enemy planner prices no burn the gate holds back.
- **Grimoires gate on Mag.** A Lore tome may carry `minMag`. A unit whose Mag (unit and class) is below it cannot wield it: the attack and cast refusals print `needs Mag 8; pell has 6`, the rank requirement still applies, and the card prints `needs Mag 8` beside the rank. The gate reads Mag directly, not a Lore rank (Code round 417's lean), because Lotus named the stat and the rank already gates by rank. An enemy template is held to it at the level it is written at.
- **Drops.** A map's `drops: <x,y> ...` names placed enemies by tile, each carrying a Lore tome. When one dies, by any death, every Lore tome in its pack goes to the battle's wagon (`tomeDropped`; `<name> drops Cinder: to the wagon, kept only if the map is won`). This reuses the chest's wagon, which the record collects only on a win, so a lost map loses the drop.
- **Content.** None: no primer, grimoire or `drops:` ships. The tests use fixtures (`test_primer_fire`, `test_grimoire_mag8`). Nothing on a shipped board moves, so no transcript or Sim number changes.

## Levers (content changes, recorded in DIALOGUE)

The gate's margin is `gate` on the rider: Res + 2 is `"gate": 2`, no gate is `"gate": null`. "A cap at rank E" (a learned school's tomes held to rank E) is not built; it would be a loader rule on `minMag`-style data, and waits until a play asks for it.

## Kill criterion

If a campaign play journals a learned rider that never fires against the maps it is fought on, or always fires, the gate is wrong: the first lever is the margin, then `null`.
