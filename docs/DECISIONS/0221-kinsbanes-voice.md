# 0221 — Kinsbane's voice, the console slice

Date: 2026-10-03. Issue #804, item 3. Chat's round 249 proposal (the issue body) and WRITING.md (#811, round 295 and 296) are the spec; the shapes and the when-it-speaks rule below are the Builder's and provisional.

## Decided (the issue, restated)

- Kinsbane speaks only to its carrier, one short line per event at most, at most three a map, in three registers: starved, feeding (a new tooth), woken (her name, first time).
- The text is a placeholder; Chat writes the set in Kinsbane's voice sheet (round 296: it names its appetite and hate freely, never the human feelings until woken).

## Decided (the Builder, provisional)

- **The lines are content:** `voice` on the hungering weapon in `weapons.json`, `starved`, `tooth`, `woken`, each `{ id, text }`. Validated on load: only on a hungering weapon, ids unique, one line, at most 12 words (WRITING.md's bark), `{name}` the only token. Stable ids let the voice sheet swap text without renumbering.
- **Core decides when, so a renderer carries no rule:** the event `kinsbaneSpoke` carries the line id and the finished text.
- **When:** a drain speaks only when it starves the blade (every drain would spend the three lines by turn 4); a kill speaks when it grows a tooth, the tooth's number choosing the line; the waking kill speaks the woken line in place of its tooth line; a woken blade is then silent. The starved line is picked by the turn. No roll is drawn, so no seed moves.
- **The cap is per carrier per battle** (`VoiceSpoken` on the unit, board state): a Recall gives lines back with the board.

## Not built here

- The clip and the hound (#535), Harrow Weir's choice screen, the real text.

## Kill / revisit

If a play journals a line as noise, the first lever is fewer moments (the starved line only once a map), then the cap at 2. If a play never hears one, the cap goes to 3 per register.

## Amended (issue 1002, 2026-10-04)

The eight lines are rewritten under `docs/voices/kinsbane.md`, same ids, no shape change. Starved: "Eat.", "Cold." (empty, never weather), "The slowest." Teeth 1 to 4: a weak kill, a chase, the herd thinning with a look ahead, the pen. The woken line is kept. The placeholders broke the sheet three ways: "us" in tooth 2 (the Kin's "we"), a look ahead at the first tooth, and the name promised before the waking.
