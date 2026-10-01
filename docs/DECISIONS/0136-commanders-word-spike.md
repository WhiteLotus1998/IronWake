# 0136 — 13.2 Commander's Word: spike, arm B with Fall back

Date: 2026-10-01. Issue 85, spec agreed on the Design Table in rounds 206 to 209 (#665). Provisional, like every spike; Chat can argue any of it on the PR.

## Why

Cha has had one job in play (rapport, 13.1). The order is its second: a once-a-map charge the captain pays for with his own swing and with where he stands, so Cha buys reach the player counts before committing.

## Decided

- **Arm B only.** Fixed magnitudes, radius `2 + Cha / 4` on effective Cha (4 at Cha 9), Manhattan, walls ignored, the captain never counted. Arm A (magnitude by Cha, reaching everyone) retires unbuilt: a multiplier is priced only after it is spent.
- **Three orders.** Press (+1 Mov this phase to the reached allies who have not moved, `BattleUnit.Pressed`, read by `ReachOf`), Rally (15 percent of max HP, at least 1, never past max; `UnitHealed` lines), Fall back (the reached allies who have acted may each take one `fallback <unit> <x,y|stay>` move of up to 2; `BattleUnit.FallingBack`). Both flags lapse when the phase ends.
- **Hold is out, not excepted (rounds 208, 209).** Round 155 says defence comes from tiles, never a unit verb, and an exception we grant ourselves against Lotus's ruling is not ours to grant. Fall back gets people to the forest; the forest protects.
- **The order is the captain's action.** After his Move or without one, refused once he has acted, once a map (`BattleState.OrderCalled`), restored by a Recall with the board. No Canto follows.
- **A fall back that wakes is refused, naming the group**, by section 8's proximity check on the board after the move (`WakeCheck.Run` with no noise and no deaths), so the refusal is the same rule the Guard reads, not a new one. `Resolver.Legal` lists only the fall backs that wake no one.
- **Where it is open.** `orders: on` on a standalone board; every campaign map from the second (`BattleState.OrdersOpen` reads `CampaignMap >= 2`), so no campaign map file changes and quests (no campaign map number) carry none. The captain's `show` card prints the order, its radius and whether it is spent.
- **Nothing new is priced.** None of the three changes an enemy's numbers, so the forecast, `threat` and the scorer read the board the order leaves; the old "the scorer must see Hold" dependency on issue 66 is moot.
- **The spike's numbers ride the event.** `orderCalled` carries the reached allies, `inRadius`, `alive` and the captain's no-crit `Exposure.Of` on his tile, so binding fraction and exposure are read off any transcript or protocol log.
- **The Sim's heuristic never calls one.** The random player of gates 2 and 8 can, since it draws from the legal list.
- **Not built:** the client's menu and panel (the console and protocol carry it), a protocol query for the preview, the per-origin variant (#648).

## Keep test

On `docs/samples/harrow_weir_orders.map`, one play from each chair. Kept if a third or more of the orders called reach fewer than all living allies, a journal names a turn the captain moved for the order, and a journal names a turn where calling it took a captain's swing the forecast's best line wanted (0099). Killed: the quirk leaves map 2's card. Code's warm 85 (PLAYTEST) shows the second clause and not the third; its one order reached 5 of 5 because the captain moved until it did. Chat's cold play decides.
