# 0397: Fire Emblem's own terms retired from ids, commands and the protocol

Date: 2026-10-09. Issue #1446 (Lotus's shipped-text read). Amends 0153, whose rule was "labels change, ids do not".

## Decided

- **The command is `again <unit> <x,y|stay>`.** It names the displayed concept, Move Again, in one word. The help, the usage error, the "may move again ... : again <unit> <x,y|stay>" prompt, the roster row (`again 3`) and the refusals (`cannot move again to 3,0`, `has no Move Again`, `Move Again is spent this phase`) all read it. The frost's hold reads `no Move Again`.
- **`canto` is still read, never printed.** The CLI, the client's script reader and the protocol accept it as a hidden alias, because 69 journaled scripts, the screenshot scripts and Chat's habits spell it. The help does not list it. RenamePassTests' guard now refuses `canto` and `Canto` on every shown line.
- **Ability ids take their own words:** `sword_sense`, `lance_sense`, `axe_sense`, `bow_sense`, `lore_sense` (was `reasonbreaker`), `faith_sense`, `fist_sense`, `steady_aim` (was `deadeye`), `move_again` (was `canto`). The effect kind is `move_again`. Display names were already these words (0153).
- **Old saves read.** `ProtocolJson.RetiredAbilityIds` maps each retired id to its replacement when a unit's `abilities` are read, so a campaign saved before the rename loads. Nothing writes an old id.
- **Protocol:** the command type is `moveAgain` (`canto` still read), the unit field `moveAgain` (`canto` still read), the event `movedAgain`. PROTOCOL.md says so.
- **Code follows the word.** The C# names (`MoveAgain`, `MoveAgainEffect`, `MovedAgain`, `HasMoveAgain`, `NoMoveAgain`, the test classes) and DESIGN's prose say Move Again and the Senses. The sample `docs/samples/canto_raid.map` keeps its file name: it is an id that transcripts and tests name, and its display name is already Hit and Run Raid.
- **Transcripts.** The 16 replays the change failed were regenerated with `tools/rejournal.py`; every changed line is a `canto` to `again` line. Older transcripts that no test replays keep their old text as history. `tests/parity/campaign/full-campaign-644.script` is what the Sim writes, so its two `canto` lines read `again`.
- **No rule changed, only words.** `--smoke` passes, and every regenerated replay is the same play with the same rolls.

## Left for Lotus (issue 1446)

- The genre terms FE also uses: Seize / Rout / Escape / Defeat Boss, supports, talk-to-recruit, seals and certification, class mastery, Gauntlets. Not changed here.
- The weapon type's id `reason` (displayed Lore) is untouched; the issue named only `reasonbreaker`.
