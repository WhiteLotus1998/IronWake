# 0325: The curse is dark's school rider; drain and hollow borrow it

Date: 2026-10-07. Issue #1328, building 0322 (rounds 443, 444) and Lotus's round-3 spell ruling.

## Decided

- **`curse` is a school rider kind**, with `amount`, `phases`, `blind` and the usual `gate`, and only dark may carry it (the loader refuses it on any other school). A school has one rider, so a dark tome may still name `drain` (Drain Life) or `hollow` on a curse school by borrowing it. Shipped dark stays `drain` until Lotus signs the numbers on #1247.
- **The tick** runs at the cursed unit's side's phase start, after heal and burn, `max(1, amount - Res / 2)`, never below 1 HP. Each tick heals the last caster by what it took, up to max HP. These heals are paid once every unit has ticked. A tick on a unit at 1 HP takes nothing and heals nothing. A caster off the board heals no one, and the tick still lands (`curseTicked`, `caster` absent).
- **One curse a unit**, never a burn stack and outside burn's cap. A new curse resets the count and pays its caster.
- **Blind** is the cursed unit's own term in the striker's hit slot (`Brace.StrikeHit`), so strikes and counters, the forecast, `threat` and the planners all read it.
- **A Hollow is never the caster:** a Hollow's hits lay no curse.
- **Cleanse clears it** (`unitCleansed` gains `curse`, written only when true).

## Why

Keeping the numbers on the school matches burn, and tomes still never invent their own. The borrow means content can switch dark's rider to curse in one line without breaking Drain Life or the Hollow.

## Open

The planners don't price the curse yet, because no shipped unit carries it. They price it when #1286's dark caster gets it. Lotus still has to sign the numbers on #1247.
