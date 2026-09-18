# 0018: Items, durability, and the spent spell

Date: 2026-09-18. Issue 9. Builder-level calls, argued on the PR if they read wrong.

## Decided

1. **Durability is per strike made, landed or not.** A swing costs the same whether it hits, and the count is the strike list of `CombatFought`, so a replay can audit every use. Both sides pay: a counter spends the defender's weapon too.
2. **A broken weapon is a flag on the combatant, not a mutated weapon.** `Combatant.Broken` makes `Combat.Mt` read `max(0, Mt - 5)` and `Combat.Hit` read `Hit - 10`; the forecast, the resolver, and the enemy scorer share those functions, so the fallback numbers are what the player sees and what the enemy prices. The weapon stays in the inventory at 0 uses and stays equipped.
3. **A spent spell is not cast again this battle, and it does not equip.** Section 5's fallback is for steel: a spell with no uses is not a worse spell, it is no spell. A unit whose only weapon is a spent spell is unarmed until the next map, where every Reason and Faith weapon refreshes to its durability at `BattleState.From`. Physical weapons carry what the roster carries between maps.
4. **The first consumable is the Field Dressing** (heals 10, 3 uses), not the name the issue body used, which belongs to a franchise. Consumables are their own table, `items.json`; an inventory entry may name a weapon or an item and the loader refuses anything else by name, and refuses an item whose id is already a weapon's.
5. **Item use is refused when nothing would heal.** `NothingToHeal` fires for a consumable at full HP and for a spell on an ally at full HP, so a script never burns a use on a no-op and the legal-command list (gates 6 and 8) offers a use only where it heals.
6. **A healing spell needs a named target and reaches only allies in its range;** a consumable heals its user and refuses a target that is not the user. The rejections are `EmptySlot`, `NotUsable`, `NoTarget`, `NotAnAlly`, `OutOfRange` (reused), `NothingToHeal`, each with a test that shows it firing. `NotAvailable` is gone with the stub it served.
7. **The healer's EXP goes through the combat level-up path.** `Experience.ForHeal` (11, 16 below half) is awarded to a player healer through `GainExp`, so a chaplain levels like everyone else and its events read the same.
8. **Weapon choice is deferred.** The equipped weapon is still the first usable slot. An `Attack` that names a slot was in the review's lean and is not in this PR: nothing in the roster carries two weapons yet, the bandit leader is the only unit that does, and the enemy planner would keep its first slot anyway. It is a small follow-up once issue 13's cast makes a second weapon a real choice.

## Not decided

- Whether the enemy AI should ever use items. The acolyte carries a salve; the planner ignores it. Issue 13's journals will say whether an enemy healer that never heals reads as broken.
