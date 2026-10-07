# 0323: The whiteout's freeze is priced by its lock, not its damage

Date: 2026-10-07. Rounds 444 and 445 on the Design Table (#1334). Prices the ice top (0322) before it goes on #1247's revision for Lotus.

## Decided

- **The freeze spends the chill.** A unit the whiteout freezes comes out of it unchilled; locking it again takes a fresh chill and a fresh whiteout. Two casts a lock, and no lock chains from one phase into the next.
- **The freeze is the stun already built (0299)**: the unit skips its side's next phase, bosses spared (a boss in the area is chilled, never frozen), light's cleanse clears it. Two rules are new: **a hit on a frozen unit ends the freeze, and a frozen unit does not counter** (Chat on #1336), so the hit that breaks it is a free swing. Striking it trades the bought phase for clean damage; leaving it keeps the phase. A stun still counters (0299).
- **Area and counters as Spark Storm:** enemies only, no counter. The stun rider's once-a-map-per-caster limit does not bound it; the cast's uses and the spent chill do.
- **Damage one tier below the meteor**, close to zero. The meteor carries the big number, the whiteout the stop.
- If Lotus picks the plain blizzard instead, the hit-break and the boss exemption still bind anything else that freezes.

## Why

Low damage does not stop a 3x3 lock from deleting a formation's phase; spending the chill and breaking on a hit do. With a counter, breaking a freeze would be strictly worse than leaving it, so nobody would; without one it is a real trade, and the spent chill caps it at one free swing per two casts. Reusing the stun keeps the engine cost to two conditions and keeps held-guard bosses watchable.

## Open

The first whiteout playtest checks the free swing; if it reads wrong, the counter is the line to change. Every number goes to Lotus on #1247 before `content/`.
