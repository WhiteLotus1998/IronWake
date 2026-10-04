# 0244: scene scripts, their format, locks and campaign points

Date: 2026-10-04. Issue #1001 (WRITING.md process step 9, rounds 254 to 257). Built by the chain Builder.

## Decided

- **The file.** One plain-text script per scene, `content/scenes/<id>.txt`, read only by `Ironwake.Content` (`SceneFormat`). A header (`scene:` the file's name, `plays:` the point and map, optional `beat:` and `retired:`), then lines `<id> <speaker> [(if <condition>)] [(human:<hash>)]: <text>`. `if <condition>` alone opens a block and `end` closes it; blocks do not nest, and a line in one needs both conditions. `#` is a comment.
- **Conditions** are a closed set of record facts joined by `and`, each optionally after `not`: `fallen`, `met`, `pick`, `returned <fate>`, `drake [<stage>]`, `oath <side>`, `quest <id>`, `support <a> <b> <tier>` (that tier or higher) and `freed-fell`. The words for fates, stages and oath sides are the campaign record's own (PROTOCOL.md). There is no `or`: two lines say it. A fact naming a unit, quest, pair or tier the content lacks fails load.
- **Validation** names file, line id (or line number) and field: plain ASCII; a spoken line at most 25 words, narration uncapped; at most 40 lines unless `beat:` names the beat sheet; ids unique and never a retired one.
- **Locks.** `human:<hash>` is 4 lowercase hex: FNV-1a 32 over the trimmed text's bytes, folded to 16 bits (`SceneScripts.LockHash`, in Core so `--story-stamp` shares it). WRITING.md named no hash; this one is BCL-only and stable across runtimes. A locked line whose text no longer hashes to its stamp fails load.
- **Points.** `camp <map>` prints when that map's camp opens, after its before card; `before <map>` prints after `march` and the map line, just before the battle (not on a resume); `after <map>` prints after the after card on a win, shown against the record the win wrote. Main-line maps only. Each scene prints under the heading its point's card uses, a spoken line as `Name: text`, wrapped to 72; screen text, so the event log leaves it out. The Godot client queues the same lines as cards.
- **Protocol.** The campaign has no event stream, so a scene is its own shape, `ProtocolJson.Scene`: `scene`, `point`, `map`, `lines` (each `id`, `speaker`, `text`), the lines shown. Adding a shape is not a version change under PROTOCOL.md's own rule, so `ProtocolVersion` stays 1; a bump would refuse every save for nothing.
- `campaign.json`'s `before`/`after` cards are untouched and keep printing until a scene replaces each.

## Tests

`SceneScriptTests`: the fixture scene (`tests/Ironwake.Core.Tests/Content/Scenes/fixture_alone.txt`) loads its speakers, block, conditions and lock; each fact reads its own record field; each guard fires (23 cases); the writer reads back equal; the loader reads `content/scenes` and the serializer writes it back; the console prints each point in place and the log leaves it out; the protocol carries the shown lines.
