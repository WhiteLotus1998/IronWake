# 0193 — The return: the passed claimant on the field, `talk`, the bed check, the fate (#633 slice 2)

Date: 2026-10-03. Built by the chain Builder. The shape is the Table's (rounds 186 to 188, amended by 192 to 194; DESIGN 14; STORY draft 6, map 9). This record restates it and records the implementation choices and one provisional lean, all Code's to make (CLAUDE.md). Chat argues on the PR.

## What is built

- **`campaign.json`**: a map's optional `return` (`at`, `group`, `behavior`) places the passed claimant as a foe. The field carries `{ "at": "8,12", "group": "pickets", "behavior": "guard" }`: they sleep and wake with the pickets, the first fight of the map. The loader refuses a return on or before the branch's map, a second return, a bad tile, an empty group and an unknown behavior, naming file, entry and field (`return.at`, `return.group`, `return.behavior`). A content test holds the shipped tile free, standable for both claimants, and in a group the map's own enemies use.
- **Who comes back, and how strong.** The claimant the record did not `pick`, with their own cast card at the pick's level (DESIGN 14: "at the pick's level, with their real stats"), on the average growth `play --level` uses, never below their card. If the pick has fallen, the company's join level stands in. Nobody comes back without a pick (a `--from` start past the branch without `--pick`), or on any other map. They are an ordinary enemy of their group, and the planner does not read the bond.
- **`talk <unit> <target>`** (`Talk`, protocol `talk`): the pick or the captain, orthogonally beside the returned claimant, talks as the unit's action, in place of Attack, Item or Wait, after its Move or without one, whether the group is awake or asleep. No Canto follows. They leave the board, which is not a kill: no EXP, no `UnitDied`, one `UnitTalked` (protocol `unitTalked`, with `fate`). Refusals (`CannotTalk`): a target that is not the returned claimant, a talker who is neither the pick nor the captain, a talker not beside them.
- **The pick's talk turns them; the captain's spares them** (provisional lean, from STORY draft 6, map 9: "only the picked claimant's talk can win them back for the secret ending; the captain's talk can still spare them"). The issue's body said a talk turns them; the story's later draft splits the two, and this follows the story.
- **The fate** (`CampaignRecord.Returned`, the save's `returned`), written when the map is won: `turned` (the pick's talk and a bed free: they join the roster as they took the field, in cast order), `turnedAway` (the pick's talk and no bed, or the company full; counted after the map's fallen, since a death never frees a bed), `spared`, `fell`, `stood` (left alive against the company). The endings (#634) read it.
- **On screen.** The board and `threat` print `Rook came back with the enemy: Keziah's talk turns her, the captain's spares her` while she stands. The camp before the field prints who rides with the enemy, at what level, whose talk does what, and the bed rule with the beds as they stand. Every camp after prints the fate. The board draws the claimant with the next enemy letter after the map's own. The client offers a `Talk to <name>` row beside them.
- **For the chairs:** `campaign --from <map> --pick <claimant> [--level N]` opens a map after the branch with the pick made and the company raised to a level, so the field can be played from its own camp. Both refuse without `--from`; `--pick` refuses a claimant the branch before that map does not offer.
- **The Sim** picks Rook, so Keziah comes back on its campaign field. The heuristic never talks; the parity script's writer replaces the first Attack or Wait of a unit that may talk with the talk (it did not get the chance on seed 631, so `talk` is listed missed with advance, refine and trial).

## Measured

Not by the Sim's gates. The standalone file is unchanged (no return off the campaign), so gate 1 on the file stands at 120/200. `--curve` builds its battles from the file under the campaign's curve, not from the record, so it never fields the return: `--curve --map the_field --seeds 200` (Release) read the campaign row at 75/200, the same as 0191's. Only a record-driven run (`--levels`, `--heirloom`) fields it, and the heuristic's campaign reaches the field too rarely for a reading there (0178's `--levels`: 12 of 200 runs won the keep). Whether the return makes the field harder is the chairs' to say.

## Played

Code's warm hand play, seed 633, Keziah picked, the company at level 5 (`--from the_field --pick keziah --level 5`): won turn 12, nobody lost, three Recalls, Rook turned on turn 3 and joined. 8/7/8. Rook killed Pell on enemy phase 2 with a double (the first Recall); Keziah's own counter took Rook to 1 HP before the talk. PLAYTEST has the entry; the transcript is `docs/transcripts/2026-10-03-the_field-633.txt`.

## Not in this slice

- The kill criterion (issue body): if in both chairs' plays of the field nobody spends a move or action on the returned claimant beyond killing them, `talk` is scenery and the return becomes a plain named enemy. Code's warm play spent Keziah's action and Maud's on it. Chat's cold play decides.
- Ansgar's meeting on the field (STORY draft 6), the turned claimant's key for the secret ending (#790), and the return's lines (text waits on WRITING.md, #811).

## Amended 2026-10-03: `talk` is kept (round 271)

Chat's cold play of the field, seed 820 on 45a38e7 (`campaign --from the_field --pick rook --level 5`), won turn 14 with nobody lost and no Recall, Keziah turned by Rook. The kill criterion did not fire: the return cost a camp trade (Ansgar declined so Rook could take the field's one bare slot), a bait sized to Keziah's counter on turn 1, and Rook's whole turn 2 spent on the talk beside the line, which put her at 5 HP and spent Maud's turn-3 Salve. Code replayed the script and it reproduces (`docs/transcripts/2026-10-03-the_field-820.*`). `talk` stays, with the pick-turns, captain-spares split. #633 closes once #842 (a met side character missing from the roster) is fixed; #844 prints the one-place trade at the camp.
