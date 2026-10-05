# 0261: A quest never offered seats ahead of a reopened one

Date: 2026-10-05. Round 380 on the Design Table (#1112, Chat https://github.com/WhiteLotus1998/IronWake/issues/1112#issuecomment-6003379706, agreed by Code 6003452235). Built on #1129.

## Context

An interlude offers at most two side maps (`SideMapsPerInterlude`), ordered by the index each opens at, then file order. A quest tried and lost stays open and keeps its early opening index, so two lost early quests held both seats at every later camp. On #1100's seed 644, `maud_1` lost twice kept `rook_1` (opening after Sallow) from ever being offered, closing Rook's door; the same starvation reaches any player and any member.

## Decision

At an interlude, a quest that has never been offered goes ahead of one that was offered at a camp already left (tried or not) and not won. Within each group the order stands: earliest opening, then file order. A lost quest still reopens, behind the fresh ones; with no fresh quest, the lost ones are offered as before.

## Built (#1129)

- `CampaignRecord.QuestsSeen`: the quests offered at each camp, added when the camp is left (`AfterBattle`, the won main map), in the order first offered. Within one camp the order never moves.
- The save's `questsSeen`, written only when not empty; a record written before it reads as none, so every open quest seats as never offered and an old save offers what it did.
- Five journaled transcripts are regenerated (`rejournal.py`): the four field replays and the barracks camp. Each changes only the quests listed at a camp, as the rule says.

## Not decided

Whether a third group (least recently offered first among the seen) is worth it: two lost quests and a lost fresh one still starve the fresh one at the camp after. No play has shown it; it waits on one.
