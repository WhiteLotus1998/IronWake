# 0014 — BattleState: the roster is an input, enemies are named by template and count, rejections are values

Date: 2026-09-18. Ruled by: Code, while building issue 6. Provisional where marked.

## The roster is passed in, not read from content
`content/units/` holds enemy templates only; the cast is issue 13's. So `BattleState.From(map, content, roster, seed, scheme)` takes the player's units as an ordered list: the first is the captain and fills the captain slot, a `recruit:<id>` slot takes the roster unit with that id, and bare `recruit` slots are filled by the remaining roster units in roster order. Roster units past the map's slots are benched, which is what gate 4's ablation needs. A roster that cannot fill the map, a recruit deployed twice, or a unit that cannot stand on its slot (a rider on a mountain) throws an `ArgumentException` naming the slot: it is a content or programming error, not a player's command, so it is not a `Rejection`.

## Enemy ids are `template-n`
Two soldiers on one map need two ids, and every roll key and event carries the id. An enemy is `<template>-<n>`, counting placements of that template in file order from 1, so Old Mill Road's brigand is `brigand-1`. The `Unit` inside the `BattleUnit` carries that id too (the template is copied with the id replaced), so `Combatant.Id`, the roll keys, and the event stream all name the same body. Section 8's "lowest unit id" tie-break and "ascending unit id" action order are ordinal string order over these ids. Provisional: if a map ever wants named enemies, a `name:` attribute on the `E` line can override this.

## Rejections are values, the resolver never throws for a command
`Resolver.Apply(state, content, command)` returns `ApplyResult(Next, Events, Rejection?)`. A rejected command returns the same state instance, no events, and a `Rejection(reason, message)` with one `RejectionReason` per rule (thirteen, each with a test that shows it firing). DESIGN.md section 2 writes the signature without `content`; content is passed rather than stored because the state is serialised (gate 6, issue 25) and the content is not part of a replay, the seed and the command list are.

## History holds prior states without their own histories
Every accepted command except Recall pushes the state it left, stored with an empty history, so the record is a flat list and the memory is linear in the number of commands. Recall to index `i` restores `History[i]` with `History[0..i)` and one charge fewer than the state being left: the charges spent survive the rewind, so three Recalls are three whatever order they are taken in. Recall itself is not pushed; a rewind is not a move.

## Flags and phases
A `BattleUnit` carries `Moved` and `Acted`: Move is legal once, before acting; Attack and Wait set both. A Move to the unit's own tile is legal and spends the move, since section 4 makes the own tile a destination. `EndPhase` flips the side, increments the turn after the enemy phase, and clears every flag on both sides. Fort healing at the start of a phase (section 4) and the win and loss conditions are issue 7's, which owns the turn loop. `UseItem` is refused with `NotAvailable` and a message naming issue 9 (Table, fourth round: stubs refuse out loud).

## The purity test walks IL
Section 2's "no Console, no System.Random, no IO, no clock" is checked by walking the IL of every method in `Ironwake.Core`, compiler-generated types included, plus every field, property, and parameter type, and refusing `System.Console`, `System.Random`, `System.DateTime`, `System.DateTimeOffset`, `System.Environment`, `Stopwatch`, `Thread`, `Task`, and the `System.IO`, `System.Security.Cryptography`, `System.Net`, and `System.Threading` namespaces. The referenced-assembly list is checked too. The one whitelisted member is `Environment.CurrentManagedThreadId`, which every C# iterator reads to tell a fresh enumerator from a reused one. The walker is falsified against a method in the test assembly that calls Console, reads the clock, and touches a file.
