# 0317 — Lotus's second spell rulings: dark rare, Gust is storm, light's ladder, no middle armor

Date: 2026-10-07. Issue #1247 (revised again). Design Table #1287: Lotus's answers relayed from the desktop session (6046566109, 6046597230). Restates his rulings; the pitches at the end are Code's, for the Table and then his list.

## Context

0307 filed the engine work from his first rulings and left Gust, names, Lightning Rod's reading and the armor ladder's middle rung with him. On the evening of 2026-10-07 he answered most of that and asked for more on the list.

## Decision (Lotus's)

1. **Dark stays rare.** Among enemies only the dark-mage mini-boss and his followers use dark. In the company, whoever takes up the Grave Ledger and can wield it gets a **special dark class**, unique like a signature class: one unit, opened by the Ledger. Its shape is ours to pitch; it goes on his list.
2. **Gust folds into lightning as storm.** Closes #1247's open question 1.
3. **The second fire DoT stays**, not under the name Kindle.
4. **Fire gets an end-game spell**: big damage plus a burn (a meteor strike or a flame storm, "something like that"), fire's grimoire top.
5. **Names wait for a naming pass**: every unnamed spell, the two drains, the mini-boss, the enemy casters and the final classes keep placeholders. No build waits on a name.
6. **No middle armor rung.** Earth Armor and Obsidian Armor are the only Def-raising spells. Rampart is ground, not a spell on a unit, and stays (confirmed, 6046597230).
7. **Earth needs a late-game spell** that is not another Def one.
8. **Light gets a ladder**: a cleanse that clears burn, chill and stun; a strike effective against Hollows; a big late heal around the caster. All three go on his list.

Lightning Rod's reading (#1280) is still unconfirmed and stays as built.

## Filed and unblocked

- #1319 Gust takes the lightning school (`ready`).
- #1320 the second burn: a tome whose hit lays two stacks, engine and fixture (`ready`).
- #1321 light's ladder, engine and fixtures (`ready`).
- #1286 unblocked for its enemy classes (placeholder names; dark on the mini-boss and his followers only) and `drops:`. Placement still waits on STORY (#1144) and a Table round.

Nothing new ships to a shop, chest, unit or map until he signs the revised #1247; #1319 changes a shipped tome because his ruling is the change.

## Code's pitches (provisional; argued on the Table, then on his list)

- **Fire's top:** a meteor called onto a tile 3 to 5 away. It marks the tile and its ring, and lands at the start of the caster's next phase: full damage on the tile, half on the ring, two burn stacks on each survivor, friend or foe. Once a map. `threat` prints the mark, so it is a zone the other side must leave, which is why it may hit hard.
- **Earth's late spell:** a fault line. Up to four tiles straight out from the caster split for two phases: walkers cannot cross, fliers can; a unit standing on one takes a hit and is shoved to the nearest open side. Earth reshapes the board instead of armoring it.
- **The Ledger's class:** opened at a camp by the unit holding the Ledger who can wield dark, once a campaign, one unit. Drains heal in full and the raise is theirs; the price is that staves heal them at half (the Ledger collects). Kill criterion: no campaign play takes it.
