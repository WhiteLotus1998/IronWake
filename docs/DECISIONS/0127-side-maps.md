# 0127 — Side maps, and The Lazar House as Maud's quest 1

Date: 2026-10-01. Issue #635, slice 1. The Builder building what the Table agreed (rounds 192 to 194; DESIGN 14, amended by 0126): both of a member's quests are side maps in the trial shape, quest 1 after the member's second map, quest 2 two maps after quest 1, at most two side maps an interlude, the price permadeath. The shapes below are Code's leans; Chat can argue them on the PR.

## The plumbing

- `campaign.json` takes an optional `quests` array: `id`, `member` (in the cast, never the captain), `part` (1 or 2; one of each per member; part 2 only after that member's part 1 in the file), `map` (under `content/quests/`), and optional `before` and `after` cards. `validate` loads `content/quests/` and refuses a quest whose board is missing or not side-map shaped.
- The board is a trial shape without the certification header: its `captain` slot is the member's, so the member's death loses it (the rule a trial already uses), and exactly one bare `recruit` slot is the ally's. No recruit placed by name.
- Opening: part 1 at the member's arrival index plus 2; part 2 at the index quest 1 was won at plus 2. The interlude offers the open quests earliest-opening first, then in file order, two seats less any won this interlude; the rest wait. A quest fought and lost here keeps its seat, closed until the next map.
- `quest <id> <ally>` on the camp screen. The ally is anyone on the roster except the captain and the member. The captain is refused because a side map is the member's story, and the captain's death there would end the campaign on a board the main line never asked for.
- After: whoever fell is fallen for good and leaves the roster, and whoever stands comes back as the battle left them (EXP, levels, uses; spells refresh). No purse reward. A loss never ends the campaign. If the member fell, the quest closes for good; otherwise (a timeout) it opens again after the next map. The record carries `questsWon` (with the map index, which times quest 2) and `questsTried` (cleared by the next map), both optional in the protocol.
- The seed: past every map's and trial's seed, one per quest per interlude.
- The objective and verdict name a unit in the captain slot plainly when it is not the cast's captain ("Maud must survive", "Lost because Maud fell"), so a trial no longer says "Captain Wren" either.
- The Sim's `--full` takes `--lead <id>`, repeatable, which puts those cast units first so a side map is measured with its member in the captain slot and its ally in the bare slot, and finds a map under `content/quests/`.

## The Lazar House

- 12x8 survive, limit 6, Recall 2, `announce: on`. Maud's order's pest house: the fort at 3,2 with walls either side and a door below, open to the north. Wren (or whoever comes) starts on the road below it. A yard pair (brigand, archer) is already inside. Then three lanes bring announced waves: north (8,0), east (11,4), and the ford south (6,7). The twist: a unit that ends a Move on a lane's bar (8,1 north, 10,4 east) turns the lane's mouth to wall, so every later spawn there is refused. The ford cannot be barred. The card prints the bars, and `announce` prints the waves and the bar events.
- The decision is the ally: hold the door beside the fort and take every wave, or walk out to bar a lane, which leaves the healer alone and the runner out of the Salve's reach.
- The second signature (the softened flaw) is not built. The after card says it in prose: Maud has started asking whether they want the prayer before she says it. Its printed board fact is the next slice and needs its line agreed.

## Measured and played

- Sim, 200 seeds, `--lead maud --lead wren`, level-1 cast: gate 1 4/200 (196 Maud), gate 4 fails with it (drop 0.015), gates 2, 3, 5 to 8 ok. The heuristic holds on survive only when it has no strike, so it walks the pair out of the house to swing and loses Maud. The keep is read the same way (acceptance is play, not gate 1; 0059, 0060). Levers tried: dropping the turn-5 ford brigand (4/200), dropping the north hexer (7/200), starting the ally on the door (4/200). None reads the board, so the map ships as drawn. The side-map gate is the cold chair's play (round 194).
- Code's warm plays, both with level-1 units on seed 701: holding the door lost Wren on turn 5; the bar line won and lost Wren on the last enemy phase (PLAYTEST). In the campaign the pair arrives a level or two higher.

## Not in this slice

Quest 1's signature and quest 2's item per member; the other nine boards; the captain's quest; the Godot client's camp screen (console only); the blocked-spawn line naming terrain (#655).
