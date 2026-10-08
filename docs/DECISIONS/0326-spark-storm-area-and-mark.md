# 0326: Spark Storm's area cast and its mark, engine and fixture

Date: 2026-10-08. Issue #1329 slice 1. Source: Lotus's round-3 spell rulings, settled on the Table in 0322 (rounds 443, 444). Provisional.

## Context

#1329 asks for Spark Storm (Gust renamed, an area setup cast with a lightning mark) and for Lightning Rod's half damage and charge. The cold trace found three slices: the area and the mark (engine), the rod, and the content change. `gust` appears in 165 transcripts, saves and scripts. Making it area-only refuses every journaled `attack ... gust` and moves the Sim's player, which triggers the `--full --all` rule from #971. Content waits on Lotus's signature on #1247 in any case.

## Decision

- **Slices.** Slice 1 (this) is the area cast and the mark, on a fixture. Slice 2 is the rod's x0.5 and its +25% charge, through the same final-damage rule. Slice 3 moves Gust in `content/` to Spark Storm (the rename, the flier rider dropped, area-only) once its numbers are signed.
- **`area: N` on a magic strike** (`Weapon.Area`). The loader refuses it below 1, on a heal, on a physical weapon, and on a tome naming a rider. A chilling area is the whiteout's slice. An area tome is never equipped (`UsableWeaponAt`), so it never attacks or counters. An attack with it is refused with the right command.
- **The cast:** `item <unit> <slot> <unit|x,y>`, the target read the way a sunder reads its target. The tile must be in range and seen at dusk. Every enemy within N of the tile is struck once, in board order, through `CombatResolver`, with no double and no counter. Its rolls are keyed by caster and target, and it is read at the tile's distance, because a unit on the area's edge may stand beyond the tome's reach. The caster's side is never struck. There is no rod catch and no cover swap. The cast is refused (`noSuchTarget`) when no enemy is in the area. Events: `areaCastAt`, then one `combatFought` per unit, deaths as in any combat, then `spellSpent` on the last use.
- **One use, one action, one combat's pay:** EXP from the struck unit that pays most (a kill pays its kill), one rank award (the kill amount if anything died), one mastery. An area is not an EXP farm, which is 0324's reasoning.
- **`marks` on a tome with a school** (`Weapon.Marks`; `BattleUnit.Mark` holds the school). A hit on a unit that survives marks it (`unitMarked`). The **first hit** on a marked unit from any tome of that school, from any caster, by a strike, a counter or a watch shot, deals x1.5 and spends the mark (`markCashed`). The multiple applies to final damage, after Def or Res and after the crit, rounded down once (0322). A miss keeps the mark, and a doubled second hit gets plain damage. A marking hit on a marked unit cashes the old mark and then lays its own. The mark has no clock. Cleanse does not clear it.
- **Shown:** the forecast says ` cashes the mark (x1.5)`, the card says `marked: next lightning x1.5`, and `item <unit> <slot> <unit|x,y> preview` lists every strike. The kill flags, the first-round miss chance and the counter's if-all-land read the marked first hit, the way they read Stoop. The protocol carries `mark` on a unit and `cashesMark` on a forecast side.
- `Legal`, the Sim's player and the enemy planner never cast an area tome until a shipped unit carries one, the same as raise and sunder.

## Unsure

- Cleanse passing over the mark. The mark is a setup rather than an affliction, and the ruling did not name it. If the Table wants light to clear it, that is one clause in `Cleanse.Clear`.
- `threat` doesn't yet flag an enemy lightning caster against a marked unit of ours. That matters only once an enemy carries a marking tome.

## Kill criterion

If a play carrying shipped Spark Storm never once casts it to set up a lightning hit, so the storm is cast only for its chip damage, the mark is too small or too short-lived to plan around. Retune the multiple before cutting it.
