# 0128 — Signature items: bound weapons, signature arts, quest 2's payout

Date: 2026-10-01. Issue #635, slice 2. The Builder building the plumbing DESIGN 14 already names (rounds 192 to 194): quest 2 pays the member's signature item, bound to them and lost with them, the default shape a shop weapon plus an art only it declares. Mechanics only, under Lotus's story gate (#656). The shapes below are Code's leans; Chat can argue them on the PR.

## Decided

- `weapons.json` takes `boundTo`, a cast id. A bound weapon carries no `price`, so no shop stocks it and nobody repairs it (the stock validator already refuses an unpriced id).
- An art (`abilities.json`, kind `art`) takes `item`, a weapon id of the art's own type: the signature art, declared only with that weapon. Otherwise it is refused as `<art> is declared only with <item>`, the reason the #611 menu greys its row with.
- A quest takes `pays`, only on part 2 and only a weapon bound to that quest's member. A won quest puts it in the member's pack at full uses, and the result line says so (`wren wins wren_2; wren receives <item>; ...`). A loss pays nothing.
- Room for the payout: a quest that pays is refused while the member carries five, naming the item. The camp gains `drop <unit> <slot>`, which discards a stack with no refund. It is refused for a signature item, an empty slot or a unit not on the roster. The cap stays five; a sixth slot for the item would make it stronger than DESIGN 14's ceiling allows.
- Lost with the owner: a fallen unit's bound weapon is never left as a 13.8 keepsake. Its next unbound weapon is the keepsake instead, or nothing.

## Not decided here

The items themselves, their names and numbers, and the arts (content behind #656). The Sim's 15 percent ceiling, read with and without the item, waits for the first shipped item. Quest 1's second signature. The Godot camp screen.
