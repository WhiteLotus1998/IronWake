# IRONWAKE — Design Document

Working title: **Ironwake**. A turn-based tactics game in the Fire Emblem: Three Houses lineage — grid battles, a small cast you grow, class paths, permadeath with a rewind — but its own game, not a clone. No Fire Emblem names, places, characters, or text anywhere in the repo.

This document is the source of truth, and it belongs to both partners (Chat and Code — see CLAUDE.md). Code that disagrees with it is wrong; if the document is wrong, fix the document in the same PR and record why in `docs/DECISIONS/`. Sections 1–12 are the floor. Section 13 is where the game gets interesting, and it is meant to be rewritten as we learn what's fun.

## 1. Identity

**Premise.** Ironwake is a crumbling border keep at the seam of three rival regions. An armistice is fraying. The keep trains *wardens* — and this year, because the crown stopped paying, the three regions' cadets were dumped into one under-funded cohort. The player is the new captain of that cohort. The cast's frictions and friendships are the story; the fraying armistice is the campaign.

**Pillars, in priority order.**
1. *Every turn is a decision.* If a random legal move wins as often as a thoughtful one, the map is broken.
2. *Numbers are honest.* The combat forecast shows exactly what the resolver will do. No hidden modifiers.
3. *Growth you feel.* Units level, certify into classes, and change what they can do on the map.
4. *Loss has weight, rewind has cost.* Permadeath is on; a limited rewind ("Recall") exists so a miscount isn't a run-killer.
5. *Small cast, big personalities.* 10 recruits + captain for the whole campaign. Depth over breadth.

**Out of scope for v1.** Monastery/calendar, romance, cooking, fishing, voice, animation, any graphics beyond the console. Rendering front-end (Godot 4 .NET, DECISIONS/0008) comes after the core is done and the campaign is fun in the console.

## 2. Architecture (non-negotiable)

- **Headless core.** `Ironwake.Core` contains all rules. It has no dependency on Console, System.Random, file IO, or time. Everything it needs is injected (`IRng`, content passed in).
- **Immutable state.** `BattleState` is an immutable record. The only way to change it is `Resolver.Apply(state, command) -> (BattleState next, IReadOnlyList<GameEvent> events)`. Events are the log; the CLI renders from events, never from diffing state.
- **Determinism.** Same seed + same command sequence = same state, byte for byte. RNG draws happen in a fixed, documented order. This is tested.
- **Rewind is free.** Because states are immutable, Recall is "pop the history stack." Replays are "re-apply the command list." Every bug report is a seed + command list.
- **Content is data.** Classes, weapons, terrain, units, and maps are JSON/text files under `/content`, validated on load with clear error messages. No stats in code.

## 3. Units and stats

Nine stats, integers: **HP, Str, Mag, Dex, Spd, Lck, Def, Res, Cha**. Cha exists from day one. Its jobs are the lean of the Table's sixth round, both spikes and both content: the captain's Cha scales Commander's Word (13.2, issue 85), and a recruit's Cha sets the rate rapport accrues (13.1, issue 16). Battalions, the job this section first named, keep their place in section 12 with their shape open. Each unit has base stats, per-stat growth rates (%), a level (1–30), EXP (0–99), a class, an inventory (5 slots), and a set of abilities.

Classes define: movement type (Infantry, Cavalry, Flying, Armored), Mov, stat modifiers, usable weapon types, class growth modifiers, and one mastery ability (Phase 3). Movement types matter for terrain cost and for effective damage.

Level up at 100 EXP. Each stat rolls independently against its growth (unit growth + class modifier). Level cap 30 for v1. Growth rolls are keyed on (unit, new level, stat) and nothing else (issue 31), so a recruit's trajectory is fixed at the campaign seed: no Recall changes what a recruit gets at level 7, and a recruit's level-ups are identical whether or not another recruit is deployed, which gate 4's ablation needs. Keying on map or turn as well would reopen laundering by waiting a turn and break the gate. A single starved stat on one recruit is a character, not a defect; the Sim shows the spread before anyone argues for a change.

## 4. Terrain

Square grid, 4-connected movement. No zone of control. Units may pass through allies, never through enemies, and may not end on an occupied tile.

| Tile | Glyph | Infantry | Cavalry | Flying | Armored | Avoid | Def/Res | Notes |
|---|---|---|---|---|---|---|---|---|
| Plain | `.` | 1 | 1 | 1 | 1 | 0 | 0 | |
| Road | `=` | 1 | 1 | 1 | 1 | 0 | 0 | reserved for later cav bonus |
| Forest | `^` | 2 | 3 | 1 | 2 | +20 | +1 Def | |
| Hill | `n` | 2 | 3 | 1 | 3 | +10 | +1 Def | |
| Mountain | `M` | 3 | — | 1 | — | +30 | +2 Def | |
| Water | `~` | — | — | 1 | — | 0 | 0 | |
| Fort | `F` | 1 | 1 | 1 | 1 | +30 | +2 Def, +2 Res | heals 20% max HP at start of owner's phase |
| Wall | `#` | — | — | — | — | — | — | blocks everything, including flyers |
| Gate/Throne | `T` | 1 | 1 | 1 | 1 | +30 | +3 Def, +3 Res | seize target; heals 20% |

`—` means impassable. Terrain avoid/def do not apply to flyers except on Fort/Throne.

**Reach.** A unit's reachable tiles are every tile whose cheapest entry cost, summed along a 4-connected path, is at most its Mov; the budget is strict, so a tile costing more than what remains is not entered. Tiles an ally stands on may be crossed but not ended on; tiles an enemy stands on are never entered. The unit's own tile is always a destination, since Move is optional (section 7), and it must be a tile the unit's movement type can enter: a query from one it cannot, a footman on a wall or a rider in water, is refused with an error naming the tile, the terrain, and the movement type, never answered, because the loader will not place a unit there and a unit there anyway is a corrupt state (issue 42). Which of two equal-cost paths a unit walks is part of the rules, because renderers show it: tiles settle in (cost, then row-major) order, and a tile's path changes only when a strictly cheaper one is found. Read as a rule about paths: a tile's path arrives through the row-major-first of its neighbours whose cost plus the tile's entry cost equals the tile's cost, so the path to every reachable tile is derivable from the map alone. The order in which a tile's neighbours are enumerated does no work and is not part of the contract (issue 45). `Movement.Reach` is the one function that answers this; the CLI, the AI, and every renderer ask it rather than counting for themselves (DECISIONS/0012).

## 5. Combat

3H-inspired, but these are Ironwake's formulas. All division is integer floor.

```
Burden        = max(0, WeaponWeight - Str / 5)
AttackSpeed   = Spd - Burden
Doubles       if AttackSpeed >= TargetAttackSpeed + 4

Atk           = Str + Mt   (physical)   |  Mag + Mt   (magic)
Effective     = Mt * 3 before adding Str/Mag, when weapon tag matches target movement type
Damage        = max(0, Atk - (Def | Res) - TerrainDefBonus)
Crit damage   = Damage * 3

Hit           = WeaponHit + Dex + Lck / 2
Avoid (phys)  = AttackSpeed + Lck / 2 + TerrainAvoid
Avoid (magic) = (Spd + Lck) / 2 + TerrainAvoid
HitChance     = clamp(Hit - Avoid, 0, 100)

Crit          = WeaponCrit + (Dex + Lck) / 2
CritAvoid     = Lck + modifiers
CritChance    = clamp(Crit - CritAvoid, 0, 100)
```

Only `CritChance` is clamped. `CritAvoid` is not: a modifier (rivalry in 13.1 is the first) may push it below zero, and a crit of 3 against a crit avoid of -5 is 8 percent. A defensive clamp on `CritAvoid` would silently erase every such cost against the low-crit weapons that make up maps 1 and 2, so a test falsifies it.

**Rolls.** Hit uses the average of two rolls (0–99) by default: a strike lands when `floor((A + B) / 2) < HitChance`, so the landing probability is the share of the 10000 ordered pairs whose sum is below `2 * HitChance` (a raw 50 lands 50.5 percent of the time). Under one roll it lands when `A < HitChance`. Crit uses one roll, `C < CritChance`, drawn only when the hit landed. Every roll is derived from the campaign seed and a key, never from a stream position (issue 31): `IRng.Roll(key)` is the engine's only source of chance, and the key tuples are the contract that replaced draw order. A combat roll keys on `(turn, phase, attacker, target, strikeIndex, roll)` with `roll` one of `HitA`, `HitB`, `Crit`; the counter's strikes key with the defender as the attacker, and `strikeIndex` counts that striker's own strikes in the combat from 0. A growth roll keys on `(unit, newLevel, stat)` and nothing else (section 3). The production `IRng` hashes the key's text with the seed (FNV-1a and a 64-bit finalizer, written out in `KeyedRng`, DECISIONS/0013), so the same seed and key give the same roll in every process; the two pinned rolls in `KeyedRngTests` make a change to the hash a visible decision, since it changes every seed's game.

**Display.** The forecast shows the *resolved* probability of whatever roll scheme is in use, rounded to the nearest integer, never the raw hit stat. Under two-roll averaging a raw 75 lands 87.75% of the time and displays as 88; a raw 30 lands 18.3% and displays as 18. Forecast, resolver, and AI scorer all call one hit-probability function; the roll scheme is a switch inside it. One roll versus two was settled by the hit A/B on the cast (Design Table, ninth round; DECISIONS/0023): two-roll averaging, because under it who you brought explains more of the outcome and the dice less (gate 4's median drop as a share of the baseline's wins, 95 against 77 percent on Old Mill Road and 82 against 59 on Saltmarsh Ford, paired over 200 seeds), and fights are shorter. The terrain reason for one roll did not survive the arithmetic: two-roll averaging makes forest's +20 worth less than printed only above raw 85 (11 points at 96 to 76) and more below it (19 at 86 to 66, 33 at 68 to 48), and the cast's median matchup is raw 86. `--scheme one` stays in the Sim and the CLI as the Fun Gate's first dial if the journals on maps 1 to 3 say the turns read as arithmetic rather than risk (issue 15).

**Sequence.** Attacker strikes; defender counters if the attacker is within the defender's weapon range and the defender has a usable weapon; then whoever doubles strikes again. Combat ends early on death. Brave/gauntlet double-strikes are Phase 3.

**Weapon types.** Sword, Lance, Axe, Bow, Reason (black magic, targets Res), Faith (white magic: heal and a few attacks). Gauntlets in Phase 3. There is **no weapon triangle**; Phase 3 adds Breaker abilities (e.g. Lancebreaker: +20 hit/avoid vs lances) as the counterplay layer. Weapons have Mt, Hit, Crit, Wt, Range (min–max), Durability, and optional effective tags.

**Ranges.** Melee 1; Bows 2 (2–3 with Phase 3 abilities); Reason/Faith 1–2. A unit with only a 2-range weapon cannot counter at 1, and vice versa.

**Durability.** Physical weapons have uses and break at 0 (broken weapons stay in inventory, usable at −5 Mt / −10 hit as a "broken" fallback so units are never helpless; repair comes with the between-map screen). Every strike a unit makes spends one use, landed or not, so a swing costs the same whether it hits (issue 9). Spells have uses per battle that fully refresh each map; a spell at 0 is not cast again this battle, and a unit whose only weapon is a spent spell is unarmed until the next map, since the broken fallback is for steel, not for words (DECISIONS/0018). A healer whose attacking spell is spent is conditional rather than idle: the healing spell still casts wherever an ally in range is hurt, which is why the cast rule in section 9 asks for one attacking spell and not for two of anything.

**Items.** A unit's five slots hold weapons, spells, and consumables; the equipped weapon is the first slot holding a usable weapon that is not a healing spell, and an Attack may name another usable slot, which moves to the front so the counter uses it too; choosing costs nothing extra (issue 99). Consumables live in `items.json` (`id`, `name`, `heals`, `uses`); the first is the Field Dressing (heals 10, 3 uses), an original name, since the content standards forbid franchise names. Section 7's Item action uses one slot: a consumable heals its user and needs no target; a healing spell heals one ally within its range. Either spends a use and ends the action; a consumable at 0 leaves the inventory. Healing a unit already at full HP is refused, so no script burns a use on nothing, and the legal-command list offers an item use only where something would heal.

**Healing.** Faith heal = Mag / 2 + 5 + spell base. Healer gains 11 EXP per heal, +5 if the target was below 50%.

## 6. EXP

```
On any strike landed:  Exp = clamp(10 + 2 * (EnemyLevel - UnitLevel), 1, 30)
On a kill (added):     Exp = clamp(20 + 3 * (EnemyLevel - UnitLevel), 5, 70)
Bosses:                kill bonus +20
```

A unit earns EXP once per combat, computed from the best outcome in that combat: the strike line when any of its strikes landed, plus the kill line when the other unit died, plus the boss bonus; nothing landed earns nothing. Only player units earn EXP; enemies are templates that do not outlive the map, and their numbers never change mid-battle behind a forecast the player already read (issue 8, DECISIONS/0017). Level up at 100, carrying the remainder; a combat is worth at most 120, so it can cross two levels and the engine handles that. At level 30 nothing is awarded and no event is emitted; a level-up that reaches 30 discards the remainder. Growth rolls are the section 3 keys, `(unit, new level, stat)`, tested against the effective growth clamped to 0..100, the same growth `AtLevel` scales by, so a rolled level-up equals the deterministic scaling in expectation. An HP gain raises current HP by the same amount. Two events carry it, `ExpGained` and `LeveledUp` (which stats rose), after the combat and before any death. Healer EXP (section 5) is a formula without a caller until issue 9's heal action.

## 7. Turn structure

Player phase → Enemy phase → (Ally phase if any) → turn counter increments. On a unit's turn: **Move** (optional), then one of **Attack / Item / Wait**. Cavalry get Canto (move remaining Mov after acting) in Phase 3, not v1.

**Win conditions** (one per map): Rout, Seize (captain on `T`), Defeat Boss, Survive N turns, Escape (all living units reach exit tiles).
**Loss conditions:** captain dies; or a map-specific protect-target dies (the `protect:` header, section 10).

**Outcome.** `BattleState.Outcome` is computed from the board after every command, never stored, and read in this order: captain dead (lost), protected recruit dead (lost), the win condition met (won), the turn past `turn_limit` (won for Survive, lost for everything else), else ongoing. Seize is the captain standing on a throne tile; Escape is every living player unit standing on an exit tile; Rout is no enemy alive; Defeat Boss is no boss alive. A decided battle refuses every command but Recall, so a loss is the moment Recall is for. Healing terrain (section 4) heals the units of the side whose phase begins, the terrain's percent of max HP with integer floor, capped at max (issue 7).

**Recall.** 3 charges per map on Normal. Rewinds to any previous state in the current map's history. Charges do not refresh mid-map.

**Recall restores the rolls.** The same attack after a Recall gets the same rolls; a Recall lets you choose differently, never reroll. Rolls are keyed (issue 31, section 5), so this holds at the roll level: the same strike on the same turn draws the same numbers whatever was resolved before it, and a resolver test shows it. It becomes a fact of the game once `BattleState` (issue 6) carries only the seed and no stream position; that issue's acceptance holds the three Recall tests.

## 8. Enemy AI

Every enemy belongs to a *group* with one behavior:

- `Aggressive` — moves toward and attacks the best target every turn.
- `Hold` — never moves; attacks anything in range.
- `Guard` — behaves as Hold until the group *wakes*; then the whole group becomes Aggressive. This is the main pacing tool for maps. A group wakes on any of: **proximity** (after any player command, a player unit stands within the wake radius of any member), **noise** (any combat involving a tile within the wake radius plus 2 of any member), or **death** (any member dies, at any distance). The wake radius is one global constant, 4 tiles, Manhattan distance, walls not considered; it lives in content, never in code and never in a map file, and it is identical on every map so the player learns it once and counting tiles is a skill, not a discovery. The check runs after every player command on where units stand, never on tiles passed through, so the player-facing rule is "do not stop close," not "do not get close": a unit with the movement for it may run past a sleeping group and end beyond the radius, which is a real play, countable in advance, and Cavalry's first job that is not arriving early. The dash is free only if nothing fights on the way through; the noise radius is what prices the greedy version. Waking emits an event naming the group and the cause. There are no trigger rectangles. The radius is `wakeRadius` in `content/rules.json` (issue 10), the file for rule constants that belong to no map; the loader refuses a negative one. The check runs inside the resolver after every accepted command on either phase, so a woken group is a fact of the board that a Recall restores with the rest: on the enemy phase only noise and a death can newly fire, since neither player units nor sleeping members move, and a group woken mid enemy phase acts as Aggressive from the next member in id order. One event per group per command, naming the loudest cause in the order death, noise, proximity.
- `Boss` — Hold, plus never leaves its tile.

The AI is a planner, never a mutator: `EnemyAi.Plan(state, content)` returns the enemy phase's command list, ending in `EndPhase` unless the battle was decided on the way, and the caller applies it through the same resolver the planner used on its working copy, so the two agree by construction. A unit's plan is at most a Move then an Attack or a Wait. Hold, Boss, and a sleeping Guard consider only their own tile; a woken Guard is Aggressive.

Target scoring, deterministic (ties broken by lowest unit id):
```
score = 100 if the attack can kill
      + expectedDamageDealt * hitChance/100
      - expectedDamageTaken * counterHitChance/100 * 0.5
      + 10 if the target cannot counter
      + 5  if the target is a healer
```
`expectedDamage` on both lines includes the crit expectation, `Damage * (1 + 2 * CritChance / 100)`, times the strikes that side makes (2 when it doubles), capped at the HP it could remove, using the same functions the forecast uses. The whole score is a double, since the section 5 convention of integer floor would make `2 * CritChance / 100` zero for every crit under 50 and delete the term in the band it exists for. "Can kill" reads deterministic damage times strikes against remaining HP, never the expectation, so an attack lethal only on a crit is never priced as a kill (issue 10, Chat's two hazards, each held by a test). The enemy prices what the player sees, so a modifier that lowers a unit's crit avoid (13.1) makes that unit a better target in the enemy's own arithmetic rather than a cost the enemy is blind to. "Cannot counter" is the target having no weapon that reaches back at that distance; a healer is a unit carrying a healing spell its class can use.

Tile choice: among tiles that allow the best-scoring attack, prefer highest terrain avoid, then fewest player units that can reach the tile next phase, then (cost from the mover, row-major) so the answer is unique. The score is taken per (target, tile), since the mover's terrain changes the counter it takes, and a tie in score goes to the lower target id before any tile key. "Can reach the tile" means the tile is in that player unit's reach set as section 4 defines it, computed once per plan on the board before the mover moves, which is what `ironwake reach` prints, so the count can be checked by hand.

**Approach.** The tile rule above is scoped to tiles that allow an attack, so it says nothing about a turn on which an Aggressive unit can attack nobody, and "moves toward" is not an algorithm. The approach is a rule for the same reason the path tie-break is (DECISIONS/0012): a debug prediction written from this document and the implementation must arrive at the same tile, so the choice cannot be an accident of the code (Design Table, fifth round). When no reachable tile allows any attack:

- **Target:** the player unit whose nearest *attack tile* (a tile the mover can end on, from which its equipped weapon reaches that unit) has the lowest path cost from the mover's own tile, computed with section 4's costs and occupancy and no Mov budget. Ties by lowest unit id, section 8's existing tie-break. A unit with no path to any attack tile is not a target; a mover with no target waits where it stands.
- **Destination:** among the mover's reachable tiles (section 4, own tile included), the one with the lowest remaining path cost to any attack tile on the chosen target. Ties by highest terrain avoid, then fewest player units whose reach set contains the tile, then (cost from the mover's tile, then row-major), DECISIONS/0012's contract as the final key, so the answer is unique. Path cost throughout, never Manhattan, so terrain means the same thing to the approach as to everything else.
- **Order:** enemy units act in ascending unit id, each on the board as the one before it left it. The remaining-cost query is `Movement.DistancesTo`, one multi-source Dijkstra on the reversed graph over the same costs and occupancy as `Reach`, so nothing re-counts; an attack tile is one the mover's movement type can enter, occupied by nobody but the mover, within its equipped weapon's range of the target.

On Old Mill Road as placed, this sends the brigand at 6,5 to 3,6 on enemy phase 1: 3,6, 4,7 and 5,8 all sit at remaining cost 2 to an attack tile on the recruit at 2,8, all Plain, all inside both player units' reach, all at cost 4 from 6,5, and row-major takes y=6. Issue 10 holds that case as a table-driven test named for this rule; a determinism test (the phase replays for the same seed) does not protect it, because a hash-ordered pick among the three replays too.

## 9. Content plan for v1

- **Cast.** Captain (fixed sword unit, Cha-heavy) + 10 recruits, 3–4 from each region, covering every weapon type and movement type at least once. Each recruit has a one-line personality, a home region, and two people they get on with (support hooks for Phase 3). Names are original. A Reason or Faith recruit carries at least two castable spells, at least one of them an attacking spell: a spent spell does not equip, and a healing spell is legal only when an ally in range is hurt, so two healing spells leave the unit with Move and Wait on a healthy turn (issue 113; the loader refuses the entry).
- **Campaign.** 8 maps, escalating: 2 tutorials-in-disguise (small maps, one new idea each), 4 mid maps introducing Guard groups, reinforcements, forts, and a Seize, 2 finale maps. Maps are 12×10 to 20×16.
- **Map authoring constraint.** Armored's job is blocking, and with no zone of control that job is binary: a one-tile corridor with a body in it is an absolute wall, a two-tile one is nothing. Maps that want the wall must draw the corridor. Archers need a back line to protect, which means forests behind a wall of two or a corridor, not a third body.
- **Difficulty.** Normal only in v1. Hard is a data change (enemy stat multipliers, fewer Recall charges) — leave the hook.
- **Between maps.** Minimal: repair, shop with fixed stock, level/class screen. Text only.

## 10. Map file format

A `.map` file is a header of `key: value` lines, then the grid, then a `units:` block. Glyphs per section 4. Coordinates are `x,y` from the top-left, 0-based. Header keys: `name`, `size` (`WxH`), `win` (`rout`, `seize`, `defeat_boss`, `survive`, `escape`), `turn_limit` (required; every map has one, gate 1 counts against it), `recall` (default 3), `enemy_level` (default 1), `exit` (Escape maps only: the exit tiles as `x,y` separated by spaces, at least as many as the map has player slots, so every deployed unit has one to stand on), `protect` (optional: the id of a `recruit:<id>` slot on this map whose death loses the map, section 7), and the optional `cheap_shots` below. A `P` line is `captain`, `recruit:<id>` (this recruit stands here), or bare `recruit` (a deployment slot the roster fills in order). An `E` line needs `group:` and `behavior:` (`aggressive`, `hold`, `guard`); a `B` line is a boss, and its behavior is always `boss`. Example:

```
name: Old Mill Road
size: 12x10
win: rout
turn_limit: 20
recall: 3

............
..^^....n...
..^^..F.nn..
============
..~~........
..~~.....#..
.........#..
..........^^
............
............

units:
P captain 1,8
P recruit:wren 2,8
E soldier 9,1 group:mill behavior:guard
E archer 10,2 group:mill behavior:guard
E brigand 6,5 group:road behavior:aggressive
B bandit_leader 10,1 group:mill behavior:boss
```

Enemy generic units are templates from `/content/units/enemies.json` scaled to the map's `enemy_level`: a template below it is raised to it (deterministically, on the effective growth of section 3 clamped per stat to 0..100, DECISIONS/0005 as amended by issue 84), and a template already at or above it keeps its own level, so a level-3 boss on a level-1 map stays level 3. Guard groups carry no trigger attribute; they wake by the section 8 rule. An optional header `cheap_shots: allowed` (maps 4 and up only) declares that the map waives gate 3 on purpose; the Sim reports the waiver rather than skipping the gate quietly.

The parser validates beyond the grammar (DECISIONS/0011): positions inside the grid, one unit per tile, exactly one captain, player slots on ground infantry can stand on, enemies on ground their class can enter, a throne for `seize`, a `B` line for `defeat_boss`, and no attribute other than `group` and `behavior`. Errors name the file and line.

The game writes this format as well as reads it: experiment 13.5 edits the finale's `.map` between maps. The writer is canonical (fixed header order, defaults written out), so writing a parsed map gives the same text back and every file under `content/maps/` is held to that form by a test. The console view (grid with units drawn over the terrain, plus a legend) is a separate rendering for reading, since a grid with letters on it has lost the terrain under them; the legend names the terrain under each unit instead. Player units are drawn as uppercase letters in placement order, enemies lowercase, bosses `!`, and any letter a terrain draws with (`F`, `M`, `T`, `n` in the starter content) is skipped on both sides, so a unit is never drawn as a tile; the skip is by the content's whole glyph set, not the map's, so a slot keeps its letter from map to map (issue 41).

## 11. Quality bar — what "not janky" means when no human is watching

The Sim harness (`Ironwake.Sim`) exists so quality is measured, not felt. Every campaign map must pass these gates before it is marked `tuned` in STATE.md:

1. **Beatable.** The heuristic AI player wins ≥ 60% of 200 seeded runs within the turn limit. The heuristic player is the Sim's own planner over section 8's score and attack tiles, per unit in id order (issue 12): the best-scoring attack from the best tile with the tile keys mirrored (exposure counted over enemy reach sets), else a heal below half HP, else the approach (section 8's rule toward the nearest enemy on Rout and Defeat Boss, toward the throne on Seize and the nearest exit on Escape, through the nearest enemy when the objective has no open path, hold on Survive). It knows nothing of the wake rule. The captain veto is arithmetic, not categorical (Design Table, seventh round): the exposure query returns two sums for a plan over the whole cycle, the worst-case counter on the attack plus every enemy whose reach-plus-range covers the tile it ends on, once with no crit landing and once with every strike a crit, at the forecast's own numbers, sleepers counted by their reach from where they stand and a unit the attack would probably kill counted too. The one exclusion is exact and constant-free (issue 117, tenth round): a target the named attack kills with certainty, the raw hit at 100 (the resolved probability exactly one under either scheme; a raw 99 prints as 100 under two rolls and still counts) and the first strike's damage at least its current HP, contributes neither its counter nor its enemy-phase term, since both are branches of probability zero and a veto that counted them chose a certain loss over an impossible death. A plan is refused when the no-crit sum reaches the captain's current HP; among plans that pass, one a crit cannot kill on is preferred ahead of the tile keys, so the crit term orders and never forbids. The gate also prints the median and p90 winning turn beside the limit, the instrument issue 13 reads limits from.
2. **Decisions matter.** The random-legal-move player wins ≤ 5% of 200 runs.
3. **No cheap shots.** Running the enemy phase from deployment positions with no player moves kills zero player units. Maps 4 and up may waive this deliberately with `cheap_shots: allowed` in the map header (section 10); the waiver is declared in the file so the Table can see which maps opted in, never exempted in a test.
4. **No dead weight.** Ablation, not kills: kills are a proxy that fails healers and corridor-blockers. For each deployed recruit, replay the same 200 seeds with that recruit benched and compare outcomes paired by seed against the baseline. Benching leaves the recruit's placement empty, named or bare, and never shifts another recruit into it, because a substitution is not an ablation (`BattleState.From` takes the bench; the captain cannot be benched). The threshold is relative: a recruit is dead weight when their paired drop is under half the median drop across the cast. (Threshold provisional, from the Table; tune on the first map that fails it.) A body does not always matter (issue 105, the Tollgate on the synthetic roster: benching any recruit raised the win rate), so the relative rule is guarded by a map-level verdict read first: when the median drop is at or below zero the cast fails as a whole, with a line saying the cast is not earning its deployment because benching the median recruit raises the win rate, the per-recruit rows print as data, and no recruit is labelled, since the relative threshold has no zero point on that side of zero. This verdict is the gate-side instrument for the authoring rule agreed in the seventh round: about one contested place worth standing per two deployed units, or deploy fewer. The per-recruit rule applies only when the median drop is positive. The drop is a sample statistic, so the gate reports it with a standard error and fails a recruit only when `drop + 2 * SE < 0.5 * median`; a gate that flips a borderline recruit between runs gets ignored within a week. At 200 seeds the margin is about the size of the threshold, so the gate catches recruits whose drop is near zero and nothing subtler, which is what dead weight means; the seed count is a Sim flag when more precision is wanted. The pairing is only real once rolls and growth are keyed (issue 31): under a sequential stream, benching one unit reshuffles every later roll and every other recruit's growth, and the two arms decorrelate at the first differing command. With keyed rolls both arms draw the same numbers wherever they share a situation, and every recruit's level-ups are identical in the baseline and in every bench, so the gate measures contribution rather than a growth lottery the bench itself reshuffled. Keying does not reduce the run count (the baseline is gate 1's seed set); it reduces the variance. The output prints three action mixes (attacks made, damage dealt, heals cast, enemy attacks absorbed) next to the drop, over the same set of units on both sides of the bench: the recruit's own baseline mix, the rest of the cast's baseline mix, and the rest of the cast's mix with the recruit benched. The gate measures how well the heuristic player uses the unit, not the unit: a failure on a healer indicts the heuristic first, and a healer with a small drop whose absence makes the others absorb a third more is visible in the row. The captain's baseline mix prints as a last row, unjudged, since he is never benched; on later maps a captain whose attack count is zero is the veto making him a piece to position rather than swing, read off the instrument.
5. **Forecast honesty.** For 10,000 random combats, forecast damage/hit/crit equals resolver behavior (statistically for hit/crit, exactly for damage). Two streams feed one tally: 10,000 combats between the content's units on random terrain resolved directly, and every attack of gate 6's random board stream against the forecast asked before it, so the count covers the formulas and the resolver's use of them; hits and crits must sit within three standard errors of the summed displayed probabilities.
6. **Determinism.** 100 random seeds × 100 random commands replay byte-identical.
7. **Speed.** A full AI-vs-AI map resolves in under 1 second.
8. **Crash-free.** 10,000 random legal command sequences across all maps throw nothing.

Gates 5–8 run on every PR (`Ironwake.Sim --smoke`). Gates 1–4 run when maps change.

## 12. Phases

- **Phase 1 — Core rules.** Sections 2–7 with tests. ASCII renderer. Done when a scripted skirmish resolves end to end.
- **Phase 2 — Playable.** Enemy AI, CLI with forecasts and a command language, Sim harness, first three maps and the cast. Done when all gates pass on three maps.
- **Phase 3 — The 3H layer.** Combat arts (durability cost, bonus stats), weapon skill ranks E–S gained by use, class certification (rank requirements + a certification roll), mastery abilities, Breaker abilities, gauntlets, battalions and gambits driven by Cha (shape open on the Table, sixth round: both partners read the 3H gambit as one obviously correct move and a second riskless per-map charge beside Recall, so battalions are the one system here not in the Phase 3 backlog, and dropping them is a `fork` for Lotus filed with both positions once either of Cha's other two jobs, 13.1 or 13.2, has been played, kept or killed, by the PR that carries that spike's decision record; the between-map screen (issue 74) is where battalions would be bought and carried, so it stays `blocked` until the fork is ruled, and this note is here so the drop cannot happen by omission, through the backlog or through the build order), supports (adjacency tally → C/B/A bonuses), Canto, Recall UI. Full 8-map campaign. Between-map screen.
- **Phase 4 — Presentation.** Front-end in Godot 4 .NET, in this repo, consuming `Ironwake.Core` as a project reference and the event protocol (issue 25) as its only view of the rules. Any other renderer, including an Unreal one Lotus builds himself, is a second consumer of the same protocol (DECISIONS/0008). Not started until Phase 3 gates pass.

## 13. Fun — direction and experiments

The gates in section 11 catch jank. They cannot tell us whether a turn was *worth taking*. This section is the creative direction and a starting list of things to try. Both partners add to it; killed ideas get one line in `docs/DECISIONS/` so we don't circle.

### What we're chasing
- **The tense turn.** The turn where you can see two good moves and one of them is wrong. If a map has none of those, it's a spreadsheet.
- **The story the map tells by itself.** Guard groups that wake up at the wrong time, a fort you didn't take, a rival who remembers. Emergent drama from rules, not cutscenes.
- **Growth with a face.** A recruit who was useless on map 1 and carries map 5 should feel like *your* doing, and the class they ended up in should be a choice you can point to.
- **Rewind you argue with yourself about.** Recall should be something you're slightly ashamed to use.

### Starting experiments (build on `experiment/<name>`, play, keep or kill)

Agreed order on the Table (2026-09-14): map events first, as Phase 2 infrastructure rather than a spike; enemy retreat second; Rapport and Rivalry third. Commander's Word runs at its number, issue 85, after map 8: its keep condition is only readable on a party that spreads (sixth round), so the first round's condition (after maps 1 to 3 and the hit A/B) is superseded, not merely met.

1. **Rapport and Rivalry.** Adjacent allies build rapport (the support base). But recruits from *different* regions start with rivalry, and a rival adjacent gives +10 crit and -5 hit — they show off. Rapport eventually overwrites rivalry. Cohort friction as a mechanic, not a cutscene. This is the only thing in v1 that makes adjacency mean anything (no zone of control, supports and battalions are Phase 3). Watch for: +10 crit at triple damage is worth about +20% expected damage against -5 hit, which is a formation bonus the player will farm, not friction. The spike's question is whether the player clusters rivals on purpose. Lean for the spike, the second arm: a rival adjacent also drops the unit's own crit avoid by 10. Against the starter content the cost is symmetric in probability, both sides move about ten points, which is a better defence of it than the flavour; it needs `CritAvoid` unclamped (section 5). The hole in the second arm: the bonus applies to every attack the pair makes and the cost only to attacks the pair receives, and a clean player phase kills the enemies that would have swung back, so the cost tends to zero as skill rises while the bonus stays whole. The spike therefore also logs exposure: of the player-phase actions taken while rival-adjacent, the fraction where that unit is attacked on the following enemy phase. If exposure comes back low, the third arm ties bonus and cost to one condition: the rivalry bonus applies only on counters, so the unit shows off when someone swings at it and nothing can be collected from the back line. All three arms are data changes. Cha's job here (Table, sixth round, the lean): a recruit's Cha sets the rate rapport accrues while adjacent, printed on the unit screen as the rate itself, never as a bare Cha, so pillar 2 holds. Before supports (issue 77) rapport's only effect is to overwrite rivalry, so Cha's sign follows the arm: under the bonus as written a high-Cha recruit loses the show-off bonus soonest, under the symmetric cost that is relief, under the counter-only arm it ends a conditional bonus. The tier numbers are set with issue 77, and the spike's journal says whether a high-Cha recruit read as a friend or as a unit losing its edge.
2. **Commander's Word.** Once per map the captain calls one order, scaled by Cha: *Hold* (+15 avoid to all allies this enemy phase), *Press* (+1 Mov this phase), *Rally* (heal 15% all). A gambit before battalions exist, and Cha's first job (Table, sixth round; issue 85). Calling the order is the captain's action for the turn, so it costs the captain's swing. Two arms, both content, and arm B is the shape both partners expect to keep. Arm A, as written: magnitude scales with the captain's Cha and the order reaches every ally. Arm B: magnitude fixed, and the order reaches allies within a radius of the captain in tiles set by Cha (`Cha / 4` to start, so the first radius is the wake rule's 4 and the player counts it the same way); a multiplier is honest only after the order is spent, since the forecast shows the resolved number but the player cannot price the order before committing, while a radius is countable at the moment it is a decision, and it prices the charge, since the captain is the loss condition and must stand near the line to hold it. Arm B's failure is Cha going idle once the radius covers the party, so the spike logs the binding fraction (recruits reached over recruits alive, per order, per map) and the captain's exposure on the enemy phase after the order, which separates an order that made the player commit from one that cost them (issue 85). Runs at its number: after issue 66 (Hold is a conditional modifier section 8's scorer must see) and after map 8 (issue 83), because the three sample maps deploy their units packed within Manhattan 2 of the captain and a binding fraction read there is 1 on every order, which says nothing; maps 4 to 8 deploy six to eleven units on grids up to 20x16, where the fraction can bind and the drift can be read on the maps it drifts on. The first round's condition, after maps 1 to 3 and the hit A/B, is superseded by that schedule (Table, sixth round); the hit A/B has run long before, so the order's avoid numbers are still read against one roll scheme. If the journals ever say bosses are walls, a fourth order that stuns is where to look, never an equipment layer.
3. **Recall scars.** Killed 2026-09-14 (DECISIONS/0010). It taxed the recruit's future instead of the charge, and its incentive was to let the recruit die. Replaced by the "Recall restores the rolls" rule in section 7.
4. **Named rivals who level.** A crown cohort — six named enemies — appears on maps 2, 4, 6, and 8 with levels tracking the player's average, carrying grudges: a rival whose ally you killed targets that unit first. Data-driven recurring antagonists.
5. **Ironwake itself.** The finale is the defense of the keep. Between maps the player spends repair points on the keep's `.map` — rebuild a wall, dig a ditch (water), raise a fort — editing the finale's terrain. Your base is a level you've been building all game.
6. **Certification as a puzzle.** Instead of a roll, certifying into a class is a one-turn micro-map: "kill the dummy in one turn with this loadout." Pass and the class is yours. Tiny puzzle maps are cheap to author and genuinely satisfying.
7. **Dusk maps.** Vision radius shrinks by one tile each turn. Not fog of war — a countdown. Pressure without RNG.
8. **Carry the fallen.** Permadeath stays, but an ally who reaches a fallen recruit's tile recovers their weapon, and the weapon keeps their name. Loss with a keepsake.
9. **Map events.** A minimal trigger system: on turn N or when a tile is entered, open a gate, collapse a bridge, spawn reinforcements from an edge. The single most reusable tool for making maps tell stories. Not an experiment: it is the authoring tool maps 1 to 8 are written with, so it is Phase 2 infrastructure (issue 32) and maps 1 to 3 are authored with it rather than rewritten after it.
10. **Enemy retreat and regroup.** Enemies below 30% HP with an escape route fall back to the nearest fort and heal. Enemies that act like they want to live make players commit. Constraint: an enemy retreats only if it can reach a fort or a safe tile *this turn*, and never retreats twice; otherwise it stands and fights. No multi-turn flights, no chases, nothing that eats the turn limit.

### The Fun Gate (part of `tuned`, alongside gates 1–8)
Both partners play the map by hand and independently rate it 1–10 on *tension*, *choice*, and *surprise*, and name their best turn in `docs/PLAYTEST.md`. All three scores ≥ 7 from both, or it goes back. If we disagree by 3+ on any axis, the Design Table gets a thread before anyone touches the map.
