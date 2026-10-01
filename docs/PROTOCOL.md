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

## The line session: `ironwake play <map> --protocol [--omniscient] [--seed N] [--script file] [--content dir] [--scheme one|two]`

One JSON object per line in, one per line out. Input comes from `--script` (a `.jsonl` command list, which is how a bug report replays) or standard input. `--strict` is refused: every line is answered `ok: true` or `ok: false` and the session goes on. The exit code is 0 on a win and 1 otherwise, as for `play`. Blank lines are skipped.

The first line out is `{"ok":true,"protocolVersion":1,"rulesVersion":1,"state":<full state>}`.

A **command** answers `{"ok":true,"events":[<event>...],"state":<board state>}`. `end` plays the enemy phase through `EnemyAi.Plan` and the resolver exactly as `--script` does, so its events are the player's end of phase, the whole enemy phase, and the start of the next player phase.

A **refusal** answers `{"ok":false,"error":{"reason":<reason>,"message":<text>}}`. `reason` is a `RejectionReason` in camelCase (`outOfReach`, `noSuchUnit`, `alreadyActed`, ...) for anything the core refused, and `badRequest` for a line that is not a request (bad JSON, an unknown type or query, a missing or mistyped field, named in the message). `message` is for a reader (issue 615): unit ids read as names, an id quoted as typed stays as typed, sentence case; a client branches on `reason`, never on `message`.

## Commands

| `type` | fields | core record |
|---|---|---|
| `move` | `unit`, `to` | `Move` |
| `attack` | `unit`, `target`, `slot` (0-based or null for the equipped weapon), `art` (optional: a combat art the unit knows, issue 68) | `Attack` |
| `item` | `unit`, `slot` (0-based), `target` (the ally for a healing spell, else null) | `UseItem` |
| `wait` | `unit` | `Wait` |
| `watch` | `unit` (on an `overwatch: on` map, a unit whose equipped weapon reaches range 2 watches the tiles two steps away until its side's next phase, as its action; DESIGN 13.17, experiment) | `Watch` |
| `cover` | `unit`, `ally` (on a `cover: on` map, a player unit beside an ally covers it as its action: the first Attack aimed at the ally while the two stand side by side swaps them and strikes the coverer; DESIGN 13.19, experiment) | `Cover` |
| `canto` | `unit`, `to` (the unit's own tile declines it, issue 71) | `Canto` |
| `exit` | `unit` (on an Escape map, from an exit tile, as the unit's action; issue 269) | `Exit` |
| `recover` | `unit` (on a `keepsakes: on` map, from a keepsake's tile, as the unit's action; DESIGN 13.8, experiment) | `Recover` |
| `open` | `unit`, `at` (the chest's tile; from that tile or orthogonally beside it, as the unit's action, every item into its pack; issue 649) | `Open` |
| `order` | `kind` (`press`, `rally` or `fallBack`; Commander's Word, once a map, as the captain's action, on an `orders: on` map or a campaign map from the second; DESIGN 13.2, issue 85) | `Order` |
| `fallBack` | `unit`, `to` (after a `fallBack` order, an ally who had acted moves up to 2, refused if it would wake a group; its own tile declines it; issue 85) | `FallBack` |
| `shove` | `unit`, `target` (on a `shove: on` map, an orthogonally adjacent ally pushed one tile away, as the unit's action; an enemy target is refused; DESIGN 13.12, experiment) | `Shove` |
| `end` | none; the response also carries `lethal` (read before the phase ends: each player unit the coming enemy phase kills if every strike `threat` prices on it lands, in deployment order, as `unit`, `total`, `hp` and `strikers`, each `enemy` and `damage`, the strikers the total sums; the console's `Lethal if all land:` lines, issue 558; empty when none) | `EndPhase` |
| `recall` | `toIndex` (a history index; the `state` query's `history` lists them) | `Recall` |
| `retreat` | `unit`, `to` | `Retreat` (the AI's; a player's is refused by the core) |

## Queries

A request with a `query` field. Queries change nothing. Each answers `{"ok":true,"query":<name>, ...}` or a refusal.

| `query` | fields in | answer fields |
|---|---|---|
| `state` | none | `state`: the full state |
| `reachable` | `unit` (a unit that has acted and is owed a Canto answers with the Canto's reach, issue 71) | `unit`, `reach`: `origin`, `movement`, `mov`, `tiles`: each `x`, `y`, `cost`, `canEnd`, `path` (the tiles walked after the origin, DECISIONS/0012's tie-break), in the order the core settled them |
| `targets` | `unit` | `unit`, `targets`: enemy ids the equipped weapon reaches from where the unit stands, in id order |
| `forecast` | `unit`, `target`, `slot` (optional, 0-based), `from` (optional, a tile the unit can still move to, issue 151), `art` (optional, issue 68) | `unit`, `target`, `from`, `forecast` (below), `weapon` (the item id the unit strikes with), `counterWeapon` (the item id the target counters with, its equipped weapon, or null when it does not counter; both always present, issue 313), `counterLethal` (true when the counter kills the unit if every counter strike lands, no crit counted, and the unit's first round is not a certain kill; the console's `Counter: lethal to` line, issue 539), `text`: the console's forecast line and, when they apply, its art, rivalry and pending-retreat lines, joined by `\n` |
| `threat` | `unit` (a player unit), `from` (optional) | `unit`, `from`, `threats`: each `enemy`, `from`, `arrives` (only for an enemy an announced event brings at the start of that enemy phase: the tile it arrives on; an unannounced spawn is never listed, issue 248), `slot` (0-based), `weapon` (id), `ifAllLand` (the damage if every hit lands, over the strikes the enemy lives to make; issue 315; 0 for a raise), `raises` (only on a `windup: on` map, true when the line is a windup weapon raising a blow over the tile, which deals nothing this phase; issue 444), `forecast`, `counterWeapon` (the item id the unit counters with, or null; issue 313); then `blow` (only when a blow is already raised over the tile: `wielder` and the certain `damage` it lands at the enemy phase start; issue 444), `ifAllLand` (the total: the worst case with at most one enemy per strike tile, so it can be less than the lines' sum, issue 253, plus a raised blow's damage and never a raise's), `asleep` (each Guard group still asleep that could strike the tile if woken: `group`, `members` as unit ids, only those that could strike the tile, issue 454; no numbers, issue 248), `cannotSee` (only on a dusk map: the enemies the player sees that would strike the tile in daylight but do not know where the unit is or cannot see it, priced at 0; issue 302), `anvils` (only on a `pincer: on` map: the planner's anvil plans against the unit on the phase-start board, each `anvil` (unit id), `tile` it could end on beside the unit, `follower` (unit id) and `from`, the tile across the unit it would strike from pinned; unpriced, not in `ifAllLand`, not binding; issue 457), `refusals` (the bosses under the veto that could strike the unit from a reachable tile but whose veto refuses every such tile, on the phase-start board: each `boss` (unit id), `tile` (the refused striking tile nearest the boss, by movement cost) and `ends` (the tile the boss's plan ends on, its own when it holds); unpriced, not in `ifAllLand`; a boss the player does not see is left out; issue 565), `wakes` (the sleeping groups stopping on the tile would wake, in the wake check's order: `group`, `cause` (`proximity` or `call`), `by` (only for `call`: the group that calls it); empty on the unit's own tile; unpriced; issue 458), `wins` (true when the unit ending its move on the tile wins the map there and then, as the captain onto a Seize throne; no enemy phase follows, and `text` is the one line `... : this move wins the map`; issue 356) and `text`: the console's `threat` block |
| `terrain` | `terrain` (an id, a glyph or a name, case ignored; issue 610) | `terrain` (the id), `text`: what the ground does for a unit on it on this map, the console's `terrain` card without its glyph (`TerrainCard.Text`); an unknown terrain is `badRequest` |
| `about` | `item` (a weapon, spell or item, by id or name, case ignored; issue 650) | `item` (the id), `text`: the console's `about` card, the item's numbers from its record then its one-line `description` (`ItemCard.Text`); an unknown item is `badRequest` |

A **forecast** is `{"attacker":<side>,"defender":<side>,"scheme":..}`, each side `strikes`, `damage`, `hitChance` (the raw number the resolver rolls against), `displayedHit` (the only hit a renderer may print), `critChance`, `doubles`, `strikesPerRound` (the strikes each of the side's turns makes, 2 for a gauntlet and 1 otherwise, issue 70; a reader takes 1 when it is absent); a forecast of a combat art also carries `artCost`, the extra uses the art spends hit or miss (issue 68), and its sides are the art's numbers. Gate 5 counts every forecast after a write and read through this shape, and fails on any it changes.

## State

`protocolVersion`, `rulesVersion`, `mapName`, `map` (full only: the map's canonical `.map` text, `MapFormat.Write`, whose `exit:` header carries an Escape map's exit tiles for a renderer to draw, issue 267), `seizeName` (only on a Seize map: the seize tile as the objective names it, the throne terrain's display name lowercased, `gate` with the shipped content; issue 569), `turn`, `phase`, `seed`, `scheme`, `recallCharges`, `campaignMap` (only on a campaign battle: the campaign map it is, counted from 1, which an heirloom's floor reads; issue 646; a state without it reads as outside the campaign), `order` (only once the captain has called Commander's Word this map: its `kind`; issue 85; a state without it reads as unspent), `units`, `escaped` (the player units that have left an Escape map through an exit, in the order they left, in the unit shape; issue 269; a state without it reads as none), `chests` (only on a map with a `chests:` block: each `at`, `items` (ids, in file order) and `open`, every chest the map authors; issue 649; a state without it reads as none open), `keepsakes` (only on a `keepsakes: on` map: each `at`, `fallen`, `item`, `uses`, the weapons fallen player units left, in the order they fell; DESIGN 13.8, experiment; a state without it reads as none), `awakeGroups`, `wakeRadius` and `noiseRadius` (the content's Guard wake and noise radii, DESIGN section 8, so a renderer can print the wake legend, issue 260), `fired` (map events spent), `flags`, `rapport` (each `a`, `b`, `points`), `outcome` (`result`: `ongoing`, `won`, `lost`; `reason`; `cause`: `none`, `captain`, `protected`, `timeout`), `historyCount`, `history` (full only: every prior state in this same shape, each with an empty history of its own).

A **unit** is `id`, `name`, `side`, `at`, `hp`, `maxHp`, `moved`, `acted`, `group`, `behavior` (null for a player unit), `isBoss`, `isCaptain`, `placementIndex` (the map placement it filled, which decides its letter), `retreated`, `canto` (the Mov a Canto unit has left this phase, issue 71; null when none is owed, and a state without it reads as null), `grudge` (only on an enemy sworn against a player unit on a `grudges: on` map, that unit's id; DESIGN 13.4, experiment; a state without it reads as none), `shoved` (only on a unit an ally shoved this phase, `true`; such a unit may not exit this phase, DESIGN 13.12, issue 396; a state without it reads as false), `braced` (only on a unit that waited without moving on a `brace: on` map, `true`, until its side's next phase; strikes against it are at 15 less hit, DESIGN 13.14, experiment; a state without it reads as false), `burning` (derived, only on a unit standing on fire, `true`; DESIGN 13.15, experiment; never read), `windupAt` (only on a unit with a raised blow on a `windup: on` map, the tile it lands on at the unit's side's next phase start, `{x, y}`; DESIGN 13.16, experiment; a state without it reads as none), `watching` (only on a unit watching on an `overwatch: on` map, `true`, until the watch fires, the unit is struck, or its side's next phase; DESIGN 13.17, experiment; a state without it reads as false), `coveredBy` (only on a unit an ally covers on a `cover: on` map, that ally's id, until the cover fires or the unit's side's next phase; DESIGN 13.19, experiment; a state without it reads as none), `spent` (only on a unit paying an art that costs its next phase, issue 636: `1` from the attack until its side's next phase, `2` through that phase, which it begins moved and acted; a state without it reads as 0), `pressed` (only on an ally a Press order reached, `true`, +1 Mov until the phase ends; issue 85), `fallingBack` (only on an ally a Fall back order owes a move, `true`, until it takes it or the phase ends; issue 85), `hasFed` (only on a unit whose hungering weapon fed on a kill since its side's last phase start, `true`; DESIGN 13.23, experiment; a state without it reads as false), `artsDeclared` (only on a unit that has declared an art with a per-map cap this battle, each such art's id once per declaration; a state without it reads as none), `class`, `level`, `exp`, `stats`, `growths` (each `hp str mag dex spd lck def res cha`, the unit's own numbers before its class), `inventory` (each `item`, `uses`, and `keepsake`, the fallen unit's id, only on a recovered keepsake; DESIGN 13.8, experiment; `fed`, the kills a hungering weapon has fed on, and `starved`, `true` in its starved form, each only when set; DESIGN 13.23, experiment; `combats`, the combats an heirloom has been fought with, and `stage`, the stage it has reached from 0, each only when set; issue 646), `abilities`, `region`, `personality`, `hooks`, `weaponPoints` (rank points per weapon type, `sword` to `faith`, issue 67, then `gauntlet`, issue 70; a state without it reads as 0 in every type), `masteryPoints` (mastery points by class id, only classes with points, issue 69; a state without it reads as none), `wound` (issue 664, only on a unit wounded with permadeath off: `penalty`, the stats it took, already out of `stats`, and `mapsLeft`, 1 or 2; a unit without it is unwounded, and another `mapsLeft` is refused).

**Dusk maps** (DESIGN 13.7, experiment, issue 302). What the player can see is a rule, so on a map with the `dusk:` header the session hides what the console hides. Every state carries `view`: `player` by default, in which `units` leaves out each enemy no player unit sees and `unseen` lists their tiles (each `{x, y}`, row-major) with no ids, names or numbers, every prior state in `history` written the same way; or `omniscient` under `play --protocol --omniscient`, the flag for bug reports, which writes every unit and puts `"omniscient":true` on the first line so a report made with it says so. An enemy's move or wait that no player unit sees before and after answers as one `{"type":"unseenActs","text":"enemy: something in the dark acts"}` event in place of its own (one per command, even where the console prints a run of them as one counted line, issue 415), and a query naming an enemy no player unit sees is refused as `noSuchUnit` or `noSuchTarget`, except a `forecast` with `from`: an enemy the unit would see from that tile is named, the unit's present tile not counting and the rest of its side counting where it stands now (issue 309). A player-view state is a view, not a save, and `ReadState` refuses it. The `map` text is the map file as launched, so it still carries the file's placements; the live board is what the view hides. A daylight state carries neither `view` nor `unseen`. **Wildfire** (DESIGN 13.15, experiment, issue 435). On a map with the `wildfire: on` header every state carries `nextFront`, the forest tiles the next player phase start sets alight (each `{x, y}`, row-major, empty when nothing burns); it is derived and never read. Fire itself is terrain (`fire`), so the map text and `terrainChanged` carry it and a Recall restores it. A group whose lamps are lit (issue 382) is listed in `litGroups` until the enemy phase that follows its wake ends, and its members are in `units` in the player view wherever they stand; the field is omitted when no group is lit, and a state without it reads as none.

The **full** state (the first line out and the `state` query) reads back to an equal `BattleState` through `ProtocolJson.ReadState`, history and all. The **board** state a command answers with leaves out `map` and `history`, which would make every answer grow with the battle; a renderer reads the map once and follows `terrainChanged`. `outcome`, `maxHp`, `historyCount`, `wakeRadius`, `noiseRadius` and `seizeName` are derived and never read back.

## Events

Every event is `{"type":<type>, <fields>, "text":<the console's line>}`, in the order the resolver emitted them. The fields carry unit ids; the `text` names units by display name, numbered only where two on the map share one (`Archer 1`), in sentence case (issue 609). The `text` of the `forecast` and `threat` answers and the strikers in the console's `Lethal if all land:` line do the same (issue 615); their fields stay ids.

| `type` | fields |
|---|---|
| `unitMoved` | `unit`, `from`, `to`, `path` |
| `combatFought` | `attacker`, `target`, `turn`, `phase`, `strikes` (each `index`, `attacker`, `target`, `hit`, `crit`, `damage`, `targetHpAfter`), `attackerHpAfter`, `targetHpAfter` |
| `unitDied` | `unit`, `side`, `at` |
| `expGained` | `unit`, `amount`, `expAfter` |
| `leveledUp` | `unit`, `newLevel`, `gains` (1 for each stat that rose) |
| `rankRaised` | `unit`, `weaponType`, `rank` (the new rank, `e` to `s`; issue 67) |
| `masteryEarned` | `unit`, `class`, `ability` (the class's mastery ability, now in the unit's `abilities`; issue 69) |
| `unitWaited` | `unit`, `braced` (only when the wait braced the unit, `true`; DESIGN 13.14, experiment) |
| `unitExited` | `unit`, `at` (the exit it left through; issue 269) |
| `unitLeftBehind` | `unit`, `at` (on the board when the captain exited; counts as fallen) |
| `grudgeSworn` | `unit`, `against` (an enemy swore against the player unit that killed one of its group, on a `grudges: on` map; DESIGN 13.4, experiment) |
| `keepsakeLeft` | `fallen`, `item`, `at` (a fallen player unit's weapon, or a keepsake a dying unit carried, stays on its tile on a `keepsakes: on` map; DESIGN 13.8, issue 295) |
| `keepsakeRecovered` | `unit`, `fallen`, `item` (the newest keepsake on the tile goes to the end of the unit's inventory under the fallen's name) |
| `keepsakeTaken` | `unit`, `fallen`, `item` (an enemy ended a move on a keepsake and carries it, past the inventory cap, until it dies; issue 295) |
| `chestOpened` | `unit`, `at`, `items` (a player unit opened the chest at `at` and took every item in it, in file order; issue 649) |
| `keepsakeLost` | `fallen`, `item`, `at`, optional `carrier` (the battle ended with the keepsake unrecovered: lying at `at`, or carried off by `carrier` from `at`; issue 295) |
| `orderCalled` | `unit` (the captain), `kind`, `radius`, `reached` (the allies it acts on, in id order), `inRadius`, `alive` (the living allies), `exposure` (the captain's no-crit exposure on the captain's tile; DESIGN 13.2, issue 85) |
| `fellBack` | `unit`, `from`, `to`, `path` (a Fall back order's move; from equal to to and an empty path: declined; issue 85) |
| `cantoed` | `unit`, `from`, `to`, `path` (from equal to to and an empty path: the Canto declined) |
| `shoved` | `unit`, `target`, `from`, `to` (the target's tiles; DESIGN 13.12, experiment) |
| `unitRetreated` | `unit`, `from`, `to` |
| `unitBroke` | `unit`, `at`, `hp` (a unit whose boss fell while it stood at or below half HP left the board on a `break: on` map; not a death; DESIGN 13.22, experiment) |
| `messengerEscaped` | `unit`, `at` (the map's messenger reached its road at `at` and left the board, not a death; its `messenger` events fire next; DESIGN 13.24, experiment) |
| `rapportGained` | `a`, `b`, `amount`, `total`, `outOf` (the overwrite threshold when the pair were rivals before the gain, else null) |
| `rivalryEnded` | `a`, `b` |
| `phaseEnded` | `side`, `turn` |
| `phaseBegan` | `side`, `turn` |
| `unitHealed` | `unit`, `amount`, `hpAfter` |
| `unitRested` | `unit` (begins its side's phase moved and acted, paying an art that cost it this phase; issue 636) |
| `unitBurned` | `unit`, `amount`, `hpAfter` (standing on burning terrain at its side's phase start; never below 1; DESIGN 13.15, experiment) |
| `hungerDrained` | `unit`, `item`, `amount`, `hpAfter`, `starved` (a hungering weapon unfed since its carrier's last phase start drains it, never below 1; `starved` when the drain put the weapon in its starved form; DESIGN 13.23, experiment) |
| `hungerFed` | `unit`, `item`, `fed`, `healed`, `hpAfter`, `mtBonus`, `woke` (a kill fed the weapon: its count, the carrier's heal, its Mt growth, and `woke` on the kill that reaches the cap; DESIGN 13.23) |
| `hungerEased` | `unit`, `item`, `healed`, `hpAfter` (a starved weapon's hit that killed nothing ends the starved form; DESIGN 13.23) |
| `heirloomTurned` | `unit`, `item`, `stage`, `stageId` (an heirloom turned to its next stage after a combat it struck in; `stage` counts from 0, `stageId` names it; issue 646) |
| `blowRaised` | `unit`, `target`, `at` (a windup weapon's attack on a `windup: on` map: no combat, a blow raised over the target's tile; DESIGN 13.16, experiment) |
| `blowLanded` | `unit`, `target`, `at`, `damage`, `targetHpAfter` (the raised blow at the wielder's side's phase start, on whoever stands on its tile: a sure hit, no crit, no counter) |
| `blowFell` | `unit`, `at` (the raised blow on an empty tile; nobody harmed) |
| `blowBroken` | `unit`, `at` (a hit on the wielder broke its raised blow) |
| `watchTaken` | `unit`, `at`, `passesUp` and `passesUpHit` (only when the unit had a legal strike: the target of the best one and its displayed hit; round 115); on an `overwatch: hold` map `holdsInsteadOf` (the tile the move given up reaches) or `noMoveCloser: true` (DESIGN 13.17, 13.17b, experiment) |
| `watchFired` | `unit` (the watcher), `target`, `at` (where the target ended its move), `hit`, `crit`, `damage`, `targetHpAfter` (one strike, no counter, before the target acts) |
| `watchHeld` | `unit` (the watcher), `target`, `at`, `hit` (the displayed hit its signature refused, DESIGN 13.18; the watch stays) |
| `watchEnded` | `unit` (a strike on the watcher ended its watch unfired) |
| `coverTaken` | `unit`, `ally`, `allyLandsOn` (the tile the ally lands on if the swap fires), `passesUp` and `passesUpHit` (only when the unit had a legal strike) (DESIGN 13.19, experiment) |
| `coverFired` | `unit` (the coverer), `ally`, `attacker`, `at` (the tile the coverer now stands on, the ally's), `allyTo` (the ally's new tile), `wouldHaveKilled` (the strike, every hit landing and no crit, would have killed the ally), `counters` (the coverer can answer from there) (DESIGN 13.19, experiment) |
| `recalled` | `toIndex`, `chargesLeft` |
| `itemUsed` | `unit`, `item`, `target`, `usesLeft` |
| `weaponEquipped` | `unit`, `item` |
| `artDeclared` | `unit`, `art`, `item` (the weapon it strikes with), `cost` (extra uses, spent hit or miss; issue 68); precedes the `combatFought` |
| `weaponBroke` | `unit`, `item` |
| `spellSpent` | `unit`, `item` |
| `groupWoke` | `group`, `cause` (`death`, `noise`, `proximity`, `call`), with `by`, the calling group, when the cause is `call` (the map's `wake_links:` header, issue 393), and at dusk, on a wake a player-phase command caused, `lamps`: the lit members row-major by tile, each `{unit, at}` (issue 382; omitted otherwise) |
| `mapEventFired` | `name`, `blocked`, `terrain` (only on a spawn barred by its tile's terrain, issue 655) |
| `terrainChanged` | `at`, `terrain` |
| `unitSpawned` | `unit`, `at`, `group`, `behavior` |
| `flagSet` | `flag` |

## Campaign record

A campaign between maps (issue 74) is one JSON object, written by `ProtocolJson.Campaign` and read back equal by `ProtocolJson.ReadCampaign`, and printed by the `record` command on the campaign screen: `protocolVersion`, `seed` (a string, as in a state), `difficulty` (the id; `normal` prints as Captain), `permadeath` (issue 664: written only as `false`, when the toggle chosen at the start is off; a record without it reads as on), `purse`, `mapIndex` (the next map, 0-based, in `campaign.json` order), `roster` (in roster order, the captain first; each unit is `id`, `name` and the unit fields of a state from `class` on, without the battle fields `side` to `canto`), `fallen` and `benched` (unit ids), and `trialsTried` (issue 252: the certification trials tried since the last map, each `unit` and `class`; absent in a record written before it, which reads as none tried), and `keep` (issue 288: the edits bought for the keep in the order they were made, each `edit`, the menu entry's id, and `at`, an `{x, y}` tile; absent in a record written before it, which reads as nothing built; the keep the finale is fought on is the content's keep with these made), and `questsWon` and `questsTried` (issue 635: the side maps won, each `quest` and the `mapIndex` it was won before, and the ids fought since the last map; each written only when not empty and read as empty when absent). A record from another protocol version is refused. A save (issue 663) is this object on one line in `<name>.json`; a suspend is `suspend.txt`, this object on its first line and the battle's typed lines after it. The profile (issue 664) is `profile.txt` in the same directory, outside every record: one difficulty id a line, each a difficulty a campaign has been won on, which opens any difficulty whose `unlockedBy` names it.

## A bug report

A seed, a map, and a `.jsonl` command list: `ironwake play the_tollgate --seed 163 --protocol --script report.jsonl`. The answers replay byte for byte on the same build; the first line's state carries both version numbers, so a report from another build is recognisable as one.
