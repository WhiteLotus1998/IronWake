# 0146 — The Postern and the Sergeant: the hire who is not what the menu says, as built

Date: 2026-10-01. Issue #691, with #706's Sergeant. The rule is the Table's (Lotus's batch, item 4; round 213; amended by round 216). The shapes below are the Builder's and are provisional.

## Decided (the Table, restated)

- Bet Lowry, the barracks' pikeman, is the one hire with a story. Her menu line stays as dry as the others, and her card follows #690's formula.
- The event fires at the first camp after map 8 at which she stands in the company, never before and never without her. Bet asks for a day and four people (she and three). The Postern is a side map in the trial shape: Bet required, her death permadeath.
- A win pays the Sergeant, a hidden class: lance and sword, Mov 4, no advanced form. Its mastery Unsworn (+10 hit and +10 crit against oath-bound units) is held at once, and she braces on every map.
- Alive at the end, her ending line is her own. The card waits on #656; the mechanic, map and class do not.

## Decided (the Builder, provisional)

- **A hire's quest is a `quests` entry.** It names its member (a hire, allowed only when the entry names `opensAfter`), the main map it `opensAfter`, the hidden class it `promotes` to, and the `ending` that replaces the served line. It opens at every camp after that map while she stands, and the existing quest rules apply: won once, lost reopens after the next map, closed if she falls. In the shipped campaign the camp before the keep is the only such camp, so it fires once.
- **A side map takes one ally per bare slot.** `quest <id> <ally>...` takes as many allies as the board has bare `recruit` slots. Maud's quest still takes one. Each ally is checked as the one ally was, and none may be named twice. The Quests panel prints `<ally>` once per slot, and the heading says "one ally" while every offered board takes one, so older transcripts replay byte for byte.
- **The oath-bound flag is a map header.** `oathbound: <group>, ...` marks enemy groups. It sets `Combatant.Oathbound`, which an ability's `against: { "oathbound": true }` reads. It is the group flag the issue named in place of STORY's flag, and the break does not read it yet (the exemption is story, behind #656).
- **Hold the Gate is an ability.** It has a new effect kind, `brace`, held by the class: `Brace.BracesOnWait` is true on a `brace: on` map or for its holder. Nothing else about the brace changes.
- **Hidden means kept.** `hidden: true` on a class refuses certifying into it (`Sergeant is not certified; it is earned`), refuses a holder certifying out (`bet is a Sergeant, earned and kept`), and leaves it off `classes` for anyone not in it. `CampaignRecord.Promote` changes the class and puts the class's mastery in the unit's abilities with its points full.
- **The Postern** (`content/quests/the_postern.map`): seize, 10x8, limit 10, Recall 2, enemy level 2, `deploy: 4`. The Postern Keeper, a guard boss with a steel lance, stands in the one-tile gate at 7,3, beside the seat at 8,3. An archer and a soldier hold the yard, and the back door at 8,7 is behind the soldier. A lane group of three sleeps outside. Every group is oath-bound. Bet ships as a pikeman, so none of it is Unsworn's board; it is the board she earns it on.
- **Seeds.** A hire's quest seeds past every member quest's block, so adding one moves no member quest's rolls.
- **The art.** The Sergeant's silhouette is the pike crossed with a short sword, in `make_art.py` and the client's `DrawSilhouette`. Its token and clip rows are generated like every class's.
- **Not built here:** the Godot client has no `quest` button (as with Maud's), the Sim never plays a side map in a campaign, and the card is a rules line until #656.

## Kill / revisit

The Sim's gate 1 on the Postern is 74/200, with the cast captain in Bet's slot and 118 timeouts: the heuristic stalls at the cork. As on the Lazar House, a side map's gate is a cold, non-authoring chair at 7+ on tension and choice. If that chair finds the Keeper never leaves his gate, the first lever is the lane group's wake radius, not the boss.
