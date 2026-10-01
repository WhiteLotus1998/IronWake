# 0138 — 13.20 rooms and beds as built: the shipped bed count and the seated-cast guard

Date: 2026-10-01. Issue 687, building DESIGN 13.20 and DECISIONS/0137. Code's call, recorded as an implementation choice; provisional, and the Table may move the number.

## The problem

0137 says the keep starts with seven beds: the captain and the five, plus one spare. That reads the levy company of section 14, which is not built yet. Today's campaign opens with ten of the eleven cast on the roster and Maud arriving on The Mill, where she is the protected unit. At seven beds every member would be over the count and Maud would be turned away on the map that must protect her, so The Mill could not begin.

## Decided

- **Shipped beds are the cast plus one spare: 12.** That keeps 0137's shape (everyone the campaign seats without a choice, plus one) on the roster the content has. It becomes 7 when the roster becomes the levy company (section 14, behind #656).
- **The loader refuses `beds` below the cast** (`keep.beds`, naming the minimum). A map's own `arrives` is never a meeting by choice and always has a bed; `no bed free: <name> will not join` is for meetings by choice (the side characters and the branch's return, #633).
- **The rule is built whole anyway.** `CampaignRecord.TurnedAway` and `Present` drop an arrival with no free bed, `Begin` leaves a named slot for one empty (as for a fallen recruit), and the camp screen prints the line before the heading. Tested on content with fewer beds than the loader allows.
- **`build <room>`** buys a room; `build <edit> <x,y>` still builds walls. `keep` before the raid prints the rooms and says the walls wait for it.
- **Fixture copies of the content drop `beds` and `rooms`**, since every journaled transcript predates them.

## What this leaves open

With 12 beds and no meeting by choice in the content, a bunk room buys nothing in today's campaign. The kill clause of 0137 reads the purse at the raid screen and can still be read, but the "kept" clause (a room bought or refused with the walls named) has no reason to fire until a meeting can be refused. The deciding plays wait for #633's meetings or the levy roster.
