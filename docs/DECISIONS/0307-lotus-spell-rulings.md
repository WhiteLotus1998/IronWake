# 0307 — Lotus's spell rulings: what is decided, what is filed, what goes back to him

Date: 2026-10-07. Issue #1247 (revised). Design Table #1251: Lotus's rulings relayed from the desktop session (6032995734), his name picks (6033061250), his enemy-caster ruling (6033129482). Restates his rulings; Code's leans are provisional.

## Context

0296 to 0301 built the schools and left every spell past Cinder and Bolt to Lotus (#1247). He answered the list on 2026-10-07 and asked for the build work filed as `ready` and #1247 revised into the next list.

## Decision (Lotus's)

- Tomes are the normal spells, grimoires the big ones; both drop from enemies and turn up rarely in chests. Uses stay per battle; big spells get few, some once a map.
- **Fire:** Thatchlight is replaced by **Smolder**, a burn on an enemy, scaled by its Res, the tick in the forecast. Burn must build (reverses 0264's "refreshes, never stacks"), so Last Ember can cash it out; one more fire DoT spell.
- **Ice:** the first spell is **Frost Arrow** (hits and chills). Still Water needs a second use; one more ice spell. The storm-warden learns ice as well as lightning.
- **Lightning:** Arc stays; Knell is **Arc Flash** (paralysis, a little damage, once a map); Catchrod becomes **Lightning Rod**, a passive on lightning only. The storm-warden's signature is **White Lightning**, then **White Lightning Storm** at his final class.
- **Earth:** Brannock is a fighter who casts earth only. Cairn is **Rampart**, a flat +5 Def. **Earth Armor** (about +10 Def, a Mov cost) and **Obsidian Armor** above it. **Sunder** also grounds a flier and stuns it for a turn.
- **Dark:** a school with three spells, two drains and a raise dead; the book is **the Grave Ledger**; the raised are **Hollows**.
- **Enemy casters:** at least one per school, met before the player wields it; the Ledger drops from a dark-mage mini-boss whose Hollows crumble at his death; casters of big spells drop grimoires. Enemy spells go on his sign-off list.
- Marginalia stays. Final class names and the storm-warden's name are placeholders. Gust is still open.

## Filed `ready`

#1279 burn builds (stacks, Res-scaled tick, cash-out), #1280 Lightning Rod, #1281 Sunder, #1282 Rampart and the armor ladder, #1283 the dark school and drain, #1284 raise dead, #1285 chests with tomes, #1286 enemy casters. Each builds engine and fixtures; no shipped tome, class or map changes until Lotus signs the revised #1247.

## Code's leans (provisional; the Table argues them on the PRs)

- Burn caps at 4 stacks; the tick is `max(1, stacks * amount - Res / 2)`.
- Lightning Rod redirects an enemy lightning spell aimed at an ally within 2 to its holder; the reading goes back to Lotus.
- Raise dead takes enemies only, never a fallen ally (permadeath); a Hollow crumbles after three phases or at its raiser's death; once a map.
- Rampart's Def replaces the tile's; Earth Armor +10 Def, Mov -2, two phases.
