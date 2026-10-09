# 0401: Forms are the word; the yard teaches them; both sides know their basic form

Date: 2026-10-09. Issue #1461 slice a3. Builds 0386's "every unit starts with one basic form for its weapon type and learns more in the yard from a teacher who knows them; arts are retired as a player word." Follows 0397's pattern for retired words.

## Decided

- **"Form" is the shown word.** The unit card says `Forms:`, the forecast `Form Heavy Cut: ...`, and the refusals say `knows no form`, `is a sword form`, `cannot pay for a form`, `no form is declared with it`, `knows no heal form`. "Technique" (0153's label) and "art" are gone from every shown line, and RenamePassTests' guard refuses both.
- **The keyword is `form <id>`** on `attack`, `forecast` and `item`. `art <id>` is still read, never printed, because journaled scripts and Chat's habits spell it. The help and the usage errors list only `form`. `recall list`'s command text and the Sim's script writer print `form`, and `tests/parity/campaign/psalter-art-644.script` says `form unasked` because the Sim writes it that way.
- **Protocol.** The `attack`, `item` and `forecast` field is `form`, and `art` is still read. The event is `formDeclared` with `form` (the C# record is `FormDeclared`). PROTOCOL.md says so.
- **Basic forms on both sides.** On a `forms: on` map, a player unit also knows the basic form of each weapon type it carries, under a2's enemy rule (`GameContent.FormsOf`, one rule for both sides). The unit card, the attack menu and the Sim's player read it. No shipped map is `forms: on`, so no cell moves. The forms kill-criterion numbers do move, and #1489's screen reads them on this rule.
- **The yard teaches a form.** A won drill also teaches the student the teacher's first form, in the teacher's list order, that is for the drilled weapon, is not known to the student, and is within the student's rank after the drill. A form bound to an item (`item`) or declared once a map (`perMap`) is a character form and is never taught. The camp line adds `; learned Feint`. A drill whose student is at both ceilings is still offered while a form is left to learn. A lost drill teaches nothing.
- **Not renamed:** content's `kind: art`, the C# names (`CombatArtEffect`, `ArtsOf`, `Attack.Art`), and the Sim's internal `art` touch key in its script comments. These are ids, not shown words (the issue: "internal names may move later").

## Transcripts

45 replays were regenerated with `tools/rejournal.py`. Every changed line is `Technique` to `Form` or a drill line gaining `; learned Feint`. The three ambiguous ones (Mill 2061, Sallow 3400 and its `goes_home` twin) were each taken from their own exact-compare test's capture. Thirteen older transcripts that a capture matched on commands but whose play has drifted since (09-25 to 09-29, harrow_weir 1360, the field 1440, drake_warden 644) were left as they were. Their tests do not compare them byte for byte.

## Left open

- A choice of form when a teacher knows two teachable forms in one weapon. Today none does, so the yard takes the first. If content gives a teacher two, `yard <teacher> <student> <weapon> [form]` is the shape.
