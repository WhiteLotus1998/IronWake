# Transcripts

One file per journaled play: `<date>-<map>-<seed>.txt`, the complete CLI output of a `--script` run. Chat reads these to play by proxy. Nothing in a `.txt` is edited by hand.

Debug passes (DIALOGUE.md, fourth round) are a pair: `<date>-<map>-<seed>-debug.prediction.md` and the `-debug.script` it predicts, hand-written and committed first, then `<date>-<map>-<seed>-debug.txt`, the run, committed after. The prediction is not touched once its `.txt` exists; git history is the proof of order.

Slot numbers in a `.script` follow the CLI convention of the commit that wrote it: scripts dated 2026-09-18 count item and weapon slots from 0; from issue 101 (2026-09-19) the CLI counts them from 1, as `show` lists them. A `.txt` is a record of the build it was played on and is never regenerated, so an older script replayed on a newer build may play a different game, and the rejection summary at the end of the run says where. Scripts a test replays are rewritten with the test in the same PR.

Roster: transcripts dated 2026-09-18 were played on the synthetic five-cadet roster (captain, wren, recruit-2 to recruit-4, zero growths) and open with its notice line; from issue 13's first PR (2026-09-19, DECISIONS/0022) the roster is `content/units/cast.json`, the notice is gone, and level-ups raise stats. The captain and Wren keep their synthetic base stats, so the committed winning script still wins.

Sample content: a transcript whose name carries a suffix after the seed was played on content other than `content/`, and its PLAYTEST.md entry says which. `2026-09-25-old_mill_road-11-cleave` is the shipped content with `docs/samples/arts/abilities.json` as `abilities.json` and the captain's `abilities` set to `["cleave"]` (issue 68), and, since issue 69 shipped the class masteries after it was played, no class naming a mastery; `CombatArtCliTests` builds that content and replays it. `2026-09-25-old_mill_road_keziah-7` is the shipped content on `docs/samples/old_mill_road_keziah.map`, Old Mill Road with Keziah deployed in Wren's place (issue 70's gauntlet play). `2026-10-02-saltmarsh_ford-772-vanguard` is the shipped content with the cast captain's `class` set to `vanguard` at level 1 and his cadet numbers (issue 772's Opening play), not the ladder's raised board.

Saves: a `<date>-<map>-<seed>.saves/` directory beside a transcript holds the save its run loaded. Replay from a copy of it (`campaign --load <name> --saves <copy>`), since the run writes autosaves into its saves directory.
