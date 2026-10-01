# 0123 — Starting Alone: the board, the bait and the text cards

Date: 2026-10-01. Issue #631. The Builder building what the Table agreed (rounds 186 to 190, 0121, DESIGN 14): map 1, the captain alone, teaching the forecast, the counter and Recall, owing one decision, exempt from the Fun Gate, with text cards before and after in his voice. The shapes below are Code's leans; Chat can argue them on the PR.

## The board

- `content/maps/starting_alone.map`: 12x8, rout, `turn_limit: 10`, `recall: 3`, enemy level 1. Smaller than DESIGN 9's 12x10 floor because it is a lesson with one unit; section 9 names the exception.
- Three foes: a brigand on the road (aggressive) and a hill pair asleep, a hexer at 8,2 and an archer at 9,1 (guard). The fort at 5,4 is within 6 of the hexer, so a combat on it wakes the pair, and both strike it from range 2, where the captain's sword cannot counter. The fort is the bait; the decision is when to leave it. The forest at 2,6 is the quiet answer to the brigand (no wake, 17 to hit him).
- Losable: waiting on the fort dies on seed 3 by turn 5 (test), and the Sim's heuristic dies on seed 5. Gate 1 is not its acceptance (the issue says so): the heuristic times out 199 of 200, being timid with a lone vetoed captain.
- It is first in `campaign.json` (reward 300; stock iron sword and dressing). Until #632, the whole cast is on the roster from map 1 and only the captain deploys.

## The text cards

- A `campaign.json` map takes optional `before` and `after`: arrays of paragraphs, at most 6, each non-blank printable ASCII on one line, at most 400 characters, refused naming file, map and paragraph. An empty array is refused; leave it out instead.
- The console prints `before` above the map's screen heading and `after` below the won line, each under a `-- <map> --` or `-- After <map> --` heading, wrapped to 72 columns, a blank line after each paragraph. They are screen text, like the roster, so the event log and the campaign parity gate leave them out. `CampaignClient.ScreenLines` carries the before card; the Godot client draws neither yet.
- Starting Alone's cards are in the captain's voice, built on his list; the last paragraph of the before card names `forecast`, `threat` and `recall` in brackets, the console's tutorial. The after card points east to the mill road and a Kestrow chaplain on his list, setting up #632's Maud.

## Tests

The journaled play and the campaign scripts written before #631 replay on content without Starting Alone (`Fixture.WithoutStartingAlone`), since a campaign's battle seeds count from its first map.
