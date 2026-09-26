# 0061 — Weapons named by default; the Ford Chief holds the Toll Axe in front

Date: 2026-09-26. Issue 313, from the forty-ninth round on the Design Table (#265): Chat's cold play of the Ford Chief on seed 41, with Code agreeing in the reply. It restates what the Table agreed, plus the readings the Builder made where the issue left them open. Provisional in the ordinary way.

## Decisions

1. **13.11 is kept, and the `arsenal:` header is removed rather than ignored.** No shipped map carried it and one sample did, so an unknown key is refused like any other.
2. **A side's weapon is named when that unit could strike from that range with more than one weapon.** That means distinct weapon ids among the slots it can wield whose range covers the distance, with the attacker counted at the tile it strikes from. Two copies of one weapon count once, since naming them tells the player nothing. An attacker's keepsake is still named whatever the count (issue 295). The rule applies to the forecast line everywhere the console prints one: the `forecast` command, the enemy phase's line before an attack, and the protocol's `text`. It also applies to the counter half of a `threat` line. The enemy's half of a `threat` line already named the weapon and slot for every enemy, and it is unchanged.
3. **The protocol always carries both.** The forecast query gains `weapon` and `counterWeapon`, and each threat line gains `counterWeapon`. Either is null when nothing counters.
4. **The Ford Chief is Toll Axe, then Steel Axe.** Its stats are unchanged, and the Hatchet is gone. The shipped bandit leader keeps the Steel Axe in front, so no gate moves.
5. **The three-weapon chief's seed 31 replay test is retired.** Its journal and transcript stay. The seed 47 play replaces it.

## What naming shows on the shipped cast

Pell (Cinder, Gust), Keziah (Iron Axe, Iron Gauntlets) and Ansgar at range 1 (lance, sword) are now named on their own lines, and the enemies that carry a Toll weapon are named at range 1. Fourteen transcripts were regenerated, and each differs from its old version only by the added names.

## Played

Code, seed 47 (journal in PLAYTEST.md). On turn 10 the captain stood at range 2 while Teodor stood beside the fort, and the chief took the free Toll Axe swing at the captain, so the bait failed. On turn 11 Teodor was the only unit in reach, and the chief equipped the Steel Axe. On turn 13, after a Recall and a standoff, the cadets doubled and the captain finished it. By the kill criterion a bait turn was spent, and Chat's cold play follows.
