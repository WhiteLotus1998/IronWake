# 0126 — DESIGN 1 and 14 amended for Lotus's round 192: quests are side maps, neutral captain, romance lifted, Promotion

Date: 2026-10-01. Issue #644. This record restates what the Table agreed in rounds 192 to 194 (#592: Lotus's wishes relayed by Code, Chat's round 193, Code's filing in round 194, Lotus's acceptance in round 195). Nothing here is a new lean except where marked.

## Section 1

- The premise reads "the captain gathering it", never "him": the captain's gender is the player's pick (#648).
- Romance is lifted from "out of scope for v1" (Lotus, item 7); it lives in supports (#77), pairs typed by kind before a word is written.
- Pillar 3 says units "are promoted into classes".

## Section 14

- **Quests.** Round 188's deed for quest 2 is reversed: both of a main member's quests are side maps in the trial shape, quest 2 one size larger and paying the signature item. Quest 1 opens after the member's second map, quest 2 two maps after quest 1, at most two side maps an interlude. The slot table is round 193's, worked from the arrival table. The price is permadeath and nothing else.
- **Side maps' gate:** one cold chair 7+ on tension and choice plus the Sim, the chair never Code's, since Code authors them (round 194).
- **Deeds** remain only for one or two side characters, with round 189's floor (never on the meeting map, at most two a map) and 0099's keep-round clause moved onto them.
- **Signature items:** the best shop weapon of its rank plus an art only it declares (0099 on the art), or a little better with no art, at most 15 percent over it in damage per combat. Replaces "loses to a shop weapon somewhere".
- **The branch:** the extra seat goes to the outlands to shame the regions, the outland Keziah against Kestrow's Rook; the passed Keziah rides home with outland raiders, the passed Rook with Kestrow's levy.
- **Names:** Promotion on screen for a class change, Refine for the smith; ids kept. Rook locked, Keziah an outlander, everything else a placeholder.
- **Ascension is not built** (round 193), with the reason in DESIGN 14.

## The strings (Code's implementation, reversible)

Eight player-facing lines say Promotion instead of certify: the trial banner (`Promotion trial: ...`), the trial result (`Promotion: <unit> earned <class>`), the classes screen (`may be promoted`), the shop (`a seal for promotion costs`), the refusals (`cannot be promoted to <class>`, `promote with a seal`, `does not promote to`) and the campaign help. The `certify` command, the `certification` map header, `certificationPrice` and the code's type names are ids and stay, so every script still replays. The eight saved transcripts that print these lines are replay goldens (the CLI tests compare them byte for byte), so they are updated with the strings; no command in them changed.

## Not in this record

Kinsbane's carrier and voice: round 194 withdrew the last sheaf, and round 195's re-pitch (Keziah carries it) is not yet agreed. DESIGN 14 points at #645 only.
