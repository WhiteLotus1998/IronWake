# PROTOCOL

The presentation protocol (issue 25, DECISIONS/0046): how a renderer, a replay, or a bug report talks to the rules without carrying any. Protocol version **1** (`ProtocolVersion.Current` in `Ironwake.Core`). The serializer is `Ironwake.Content.Protocol.ProtocolJson`; the line session is `ironwake play <map> --protocol`.

## Rules of the protocol

- **Versioning.** Consumers ignore fields they do not know, so adding a field is not a change. Removing a field or changing what one means increments `ProtocolVersion`. A full state carries `protocolVersion` and a reader refuses any other.
- **Names.** Field names are camelCase and written by hand, never reflected from the C# records; a golden test per event type holds every shape below. Enum values are the C# name in camelCase (`player`, `twoRollAverage`, `proximity`).
- **Canonical.** Compact JSON, every field present in the order listed here, `null` where a value is absent. The same state writes the same bytes.
- **Coordinates** are `{"x":..,"y":..}`, 0-based from the top-left, like the map format.
- **Slots are 0-based**, as in the core and the events. The console counts slots from 1 (issue 101) because it is read by people; the protocol is read by programs.
- **The seed is a string** (`"seed":"163"`), since a 64-bit seed does not survive a JSON number in every consumer.
- **No rules in the renderer.** Reach, targets, forecasts and threats are queries; a consumer never computes them. Every event carries `text`, the console's own line for it, so a renderer's event log prints what the CLI prints and never falls behind the rules.

## The line session: `ironwake play <map> --protocol [--seed N] [--script file] [--content dir] [--scheme one|two]`

One JSON object per line in, one per line out. Input comes from `--script` (a `.jsonl` command list, which is how a bug report replays) or standard input. `--strict` is refused: every line is answered `ok: true` or `ok: false` and the session goes on. The exit code is 0 on a win and 1 otherwise, as for `play`. Blank lines are skipped.

The first line out is `{"ok":true,"protocolVersion":1,"rulesVersion":1,"state":<full state>}`.

A **command** answers `{"ok":true,"events":[<event>...],"state":<board state>}`. `end` plays the enemy phase through `EnemyAi.Plan` and the resolver exactly as `--script` does, so its events are the player's end of phase, the whole enemy phase, and the start of the next player phase.

A **refusal** answers `{"ok":false,"error":{"reason":<reason>,"message":<text>}}`. `reason` is a `RejectionReason` in camelCase (`outOfReach`, `noSuchUnit`, `alreadyActed`, ...) for anything the core refused, and `badRequest` for a line that is not a request (bad JSON, an unknown type or query, a missing or mistyped field, named in the message).

## Commands

| `type` | fields | core record |
|---|---|---|
| `move` | `unit`, `to` | `Move` |
| `attack` | `unit`, `target`, `slot` (0-based or null for the equipped weapon), `art` (optional: a combat art the unit knows, issue 68) | `Attack` |
| `item` | `unit`, `slot` (0-based), `target` (the ally for a healing spell, else null) | `UseItem` |
| `wait` | `unit` | `Wait` |
| `canto` | `unit`, `to` (the unit's own tile declines it, issue 71) | `Canto` |
| `exit` | `unit` (on an Escape map, from an exit tile, as the unit's action; issue 269) | `Exit` |
| `end` | none | `EndPhase` |
| `recall` | `toIndex` (a history index; the `state` query's `history` lists them) | `Recall` |
| `retreat` | `unit`, `to` | `Retreat` (the AI's; a player's is refused by the core) |

## Queries

A request with a `query` field. Queries change nothing. Each answers `{"ok":true,"query":<name>, ...}` or a refusal.

| `query` | fields in | answer fields |
|---|---|---|
| `state` | none | `state`: the full state |
| `reachable` | `unit` (a unit that has acted and is owed a Canto answers with the Canto's reach, issue 71) | `unit`, `reach`: `origin`, `movement`, `mov`, `tiles`: each `x`, `y`, `cost`, `canEnd`, `path` (the tiles walked after the origin, DECISIONS/0012's tie-break), in the order the core settled them |
| `targets` | `unit` | `unit`, `targets`: enemy ids the equipped weapon reaches from where the unit stands, in id order |
| `forecast` | `unit`, `target`, `slot` (optional, 0-based), `from` (optional, a tile the unit can still move to, issue 151), `art` (optional, issue 68) | `unit`, `target`, `from`, `forecast` (below), `text`: the console's forecast line and, when they apply, its art, rivalry and pending-retreat lines, joined by `\n` |
| `threat` | `unit` (a player unit), `from` (optional) | `unit`, `from`, `threats`: each `enemy`, `from`, `arrives` (only for an enemy an announced event brings at the start of that enemy phase: the tile it arrives on; an unannounced spawn is never listed, issue 248), `slot` (0-based), `weapon` (id), `ifAllLand`, `forecast`; then `ifAllLand` (the sum), `asleep` (each Guard group still asleep that could strike the tile if woken: `group`, `members` as unit ids; no numbers, issue 248) and `text`: the console's `threat` block |

A **forecast** is `{"attacker":<side>,"defender":<side>,"scheme":..}`, each side `strikes`, `damage`, `hitChance` (the raw number the resolver rolls against), `displayedHit` (the only hit a renderer may print), `critChance`, `doubles`, `strikesPerRound` (the strikes each of the side's turns makes, 2 for a gauntlet and 1 otherwise, issue 70; a reader takes 1 when it is absent); a forecast of a combat art also carries `artCost`, the extra uses the art spends hit or miss (issue 68), and its sides are the art's numbers. Gate 5 counts every forecast after a write and read through this shape, and fails on any it changes.

## State

`protocolVersion`, `rulesVersion`, `mapName`, `map` (full only: the map's canonical `.map` text, `MapFormat.Write`, whose `exit:` header carries an Escape map's exit tiles for a renderer to draw, issue 267), `turn`, `phase`, `seed`, `scheme`, `recallCharges`, `units`, `escaped` (the player units that have left an Escape map through an exit, in the order they left, in the unit shape; issue 269; a state without it reads as none), `awakeGroups`, `wakeRadius` and `noiseRadius` (the content's Guard wake and noise radii, DESIGN section 8, so a renderer can print the wake legend, issue 260), `fired` (map events spent), `flags`, `rapport` (each `a`, `b`, `points`), `outcome` (`result`: `ongoing`, `won`, `lost`; `reason`; `cause`: `none`, `captain`, `protected`, `timeout`), `historyCount`, `history` (full only: every prior state in this same shape, each with an empty history of its own).

A **unit** is `id`, `name`, `side`, `at`, `hp`, `maxHp`, `moved`, `acted`, `group`, `behavior` (null for a player unit), `isBoss`, `isCaptain`, `placementIndex` (the map placement it filled, which decides its letter), `retreated`, `canto` (the Mov a Canto unit has left this phase, issue 71; null when none is owed, and a state without it reads as null), `class`, `level`, `exp`, `stats`, `growths` (each `hp str mag dex spd lck def res cha`, the unit's own numbers before its class), `inventory` (each `item`, `uses`), `abilities`, `region`, `personality`, `hooks`, `weaponPoints` (rank points per weapon type, `sword` to `faith`, issue 67, then `gauntlet`, issue 70; a state without it reads as 0 in every type), `masteryPoints` (mastery points by class id, only classes with points, issue 69; a state without it reads as none).

The **full** state (the first line out and the `state` query) reads back to an equal `BattleState` through `ProtocolJson.ReadState`, history and all. The **board** state a command answers with leaves out `map` and `history`, which would make every answer grow with the battle; a renderer reads the map once and follows `terrainChanged`. `outcome`, `maxHp`, `historyCount`, `wakeRadius` and `noiseRadius` are derived and never read back.

## Events

Every event is `{"type":<type>, <fields>, "text":<the console's line>}`, in the order the resolver emitted them.

| `type` | fields |
|---|---|
| `unitMoved` | `unit`, `from`, `to`, `path` |
| `combatFought` | `attacker`, `target`, `turn`, `phase`, `strikes` (each `index`, `attacker`, `target`, `hit`, `crit`, `damage`, `targetHpAfter`), `attackerHpAfter`, `targetHpAfter` |
| `unitDied` | `unit`, `side`, `at` |
| `expGained` | `unit`, `amount`, `expAfter` |
| `leveledUp` | `unit`, `newLevel`, `gains` (1 for each stat that rose) |
| `rankRaised` | `unit`, `weaponType`, `rank` (the new rank, `e` to `s`; issue 67) |
| `masteryEarned` | `unit`, `class`, `ability` (the class's mastery ability, now in the unit's `abilities`; issue 69) |
| `unitWaited` | `unit` |
| `unitExited` | `unit`, `at` (the exit it left through; issue 269) |
| `unitLeftBehind` | `unit`, `at` (on the board when the captain exited; counts as fallen) |
| `cantoed` | `unit`, `from`, `to`, `path` (from equal to to and an empty path: the Canto declined) |
| `unitRetreated` | `unit`, `from`, `to` |
| `rapportGained` | `a`, `b`, `amount`, `total`, `outOf` (the overwrite threshold when the pair were rivals before the gain, else null) |
| `rivalryEnded` | `a`, `b` |
| `phaseEnded` | `side`, `turn` |
| `phaseBegan` | `side`, `turn` |
| `unitHealed` | `unit`, `amount`, `hpAfter` |
| `recalled` | `toIndex`, `chargesLeft` |
| `itemUsed` | `unit`, `item`, `target`, `usesLeft` |
| `weaponEquipped` | `unit`, `item` |
| `artDeclared` | `unit`, `art`, `item` (the weapon it strikes with), `cost` (extra uses, spent hit or miss; issue 68); precedes the `combatFought` |
| `weaponBroke` | `unit`, `item` |
| `spellSpent` | `unit`, `item` |
| `groupWoke` | `group`, `cause` (`death`, `noise`, `proximity`) |
| `mapEventFired` | `name`, `blocked` |
| `terrainChanged` | `at`, `terrain` |
| `unitSpawned` | `unit`, `at`, `group`, `behavior` |
| `flagSet` | `flag` |

## Campaign record

A campaign between maps (issue 74) is one JSON object, written by `ProtocolJson.Campaign` and read back equal by `ProtocolJson.ReadCampaign`, and printed by the `record` command on the campaign screen: `protocolVersion`, `seed` (a string, as in a state), `difficulty`, `purse`, `mapIndex` (the next map, 0-based, in `campaign.json` order), `roster` (in roster order, the captain first; each unit is `id`, `name` and the unit fields of a state from `class` on, without the battle fields `side` to `canto`), `fallen` and `benched` (unit ids). A record from another protocol version is refused.

## A bug report

A seed, a map, and a `.jsonl` command list: `ironwake play the_tollgate --seed 163 --protocol --script report.jsonl`. The answers replay byte for byte on the same build; the first line's state carries both version numbers, so a report from another build is recognisable as one.
