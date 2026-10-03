# 0196 — Maud's Psalter and Unasked, the first heal art (#635 slice 4)

Date: 2026-10-03. Built by the chain Builder. The shape is the Table's: round 260 (Chat: a heal item, Beacon's numbers plus an art built from her flaw, rank D unless the rank report says C, the ceiling's heal arm, Beacon stocked after map 5, the slot table to follow STORY) and round 261 (Code: the ally unmoved as well as unacted, its phase ending in place and bracing where brace applies, the comparator "at or below its rank"), agreed in round 263. The effect kind's name, the zero use cost and the console line are Code's lean.

## What is built

- **`heal_art`** (`HealArtEffect`): weapon type, rank, `factor` (at least 2, else refused at load), optional `item` (a healing spell of that type, else refused naming it). Declared with the Item action: `item <unit> <slot> <ally> art <id>` on the console and the Godot client's script, `art` on the protocol's `item` command. It is refused with a consumable, when the healer does not know it, the spell is another type or not its item, the healer's rank is short, the target is the healer, or the ally has moved, acted or been shoved this phase. It heals `factor` times the plain cast (Steady Hands' double is in the plain cast, so 4x on the Surgeon), capped at the ally's max HP, and spends one use. The ally then waits where it stands: moved and acted, braced if `Brace.BracesOnWait` says so, no Canto. Events: `artDeclared` (cost 0), `itemUsed`, `unitHealed`, the ally's `unitWaited`. The console prints `Unasked: <ally> holds <x,y>; phase ends` before them.
- **Why no use cost.** A combat art costs uses because one free on a miss would always beat the plain attack. A heal never misses, and Unasked already costs the ally's phase and position; a use on top would make the art a durability tax (round 263's argument against raising the ledger).
- **Content.** `maud_psalter` (Maud's Psalter: faith, range 1 to 2, 4 uses, healBase 0, rank D, bound to Maud, unpriced, placeholder line) and `unasked` (faith, D, factor 2, item the Psalter), which Maud knows from the start. No board pays the Psalter yet; Maud 2 is the next slice.
- **Rank D.** Maud starts at Faith D (`cast.json`), so the Psalter works the day it is paid. The campaign rank report has no per-unit row; `levels-763` counts chaplain faith at 154 attacks and 97 counters over 82 won campaigns, far short of C (80 points) by map 5. D stands.
- **The ceiling's heal arm** (`SignatureCeiling.ReadHeal`): HP per cast at the owner's cast card, the target uncapped, times uses, against the best stocked unbound priced heal of the item's type at or below its rank; at most 1.15. A heal art passes by construction (it costs the ally's phase) and play judges it (0099). The Psalter reads 0.50 of Salve (4 x 7 against 8 x 7). Range is not priced: the item's edge is its reach and its art.
- **Beacon** is stocked on every screen from the one after map 5 (before the raid).
- **DESIGN 14's slot table** follows STORY draft 6: Pell's quests after 4 and 6 where Wren's were; Wren has none.

## The warm play

Code, The Mill, seed 635, on a scratch content copy with the Psalter in Maud's pack (no shipped path issues it yet): Unasked twice on the captain, won turn 9. PLAYTEST has the entry; the transcript is `docs/transcripts/2026-10-03-the_mill-635.txt`. The cost bit once: a held captain could not screen Maud, and the soldier went for her.

## Not in this slice

The Maud 2 board that pays it, the Godot client's heal-art row, the Sim's player declaring Unasked (it never does, like every art), gate 4's ablation with the item.
