# 0188 — The tier line: a support pair announces the tier it reaches (#77 slice 6)

Date: 2026-10-03. Built by the chain Builder. Implementation of an item 0185 left open ("a battle line for reaching a tier"); no number moves.

## What is built

- **`SupportReached(a, b, tier)`**, a new event. At the end of a player phase, `Rivalry.Accrue` raises it for a support pair whose total now reaches a higher tier than it did before the gain. The tier's name is C, B or A. It comes after that pair's `RapportGained` (and after a `RivalryEnded`, when the same gain ends a rivalry).
- **One event per pair per phase.** A gain that crosses two tiers names only the higher one, so the screen never prints a tier the pair has already left behind.
- **No announcement for a pair that is not a support pair.** That holds even behind the `rivalry:` header, where rapport also accrues between recruits who have no pairing: `Supports.TierOf` returns no tier for them.
- **The console** prints `Wren and Pell reach support C` in the phase-end block, beside the rapport lines.
- **The protocol** writes it as `supportReached` with `a`, `b` and `tier`, documented in PROTOCOL.md. Adding an event does not move the protocol version, the same as every event added before this one.
- **The client** draws nothing new. Its event log shows the line the same way it shows any event it has no card for.

## Why a line and not a scene

The support text waits on WRITING.md (#811) and the voice sheets. Until those exist, the line is the board fact: from this phase on, the pair fights at the tier's numbers. When support conversations are written, they will be played from camp, keyed on the record's rapport, and not triggered by this event.

## Not changed

- The tiers stay at C 16, B 40, A 72.
- The captain's pairs still accrue at the recruit's rate alone (0185).
- Both leans in 0186 and 0187 still wait on the Table.
