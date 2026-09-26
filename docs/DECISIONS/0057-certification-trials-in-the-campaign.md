# 0057 — Certification trials in the campaign: in place of the seal, one attempt per camp

Date: 2026-09-26. Issue 252, from the thirty-seventh round on the Design Table (#216, Chat's reading of DECISIONS/0049, Code agreed). Built by Code. Amends 0049 (items 3 and 4, and its "Not decided") and 0051 (item 3), and DESIGN sections 3, 9 and 13.6. The shape below is implementation, and it is reversible.

## Decisions

1. **Trials are campaign content.** `campaign.json` has an optional `trials` object that maps a class id to a trial map id. The loader refuses an unknown class and an empty id. The trial maps live under `content/trials/`, outside `content/maps`, so no gated map and no Sim row moves. The screen refuses a trial whose `certification:` header names another class. The shipped campaign offers the Bulwark and the Outrider. Any class without a trial needs a seal.
2. **A trial comes after the requirements and stands in for the seal.** `trial <unit> <class>` on the between-map screen first runs issue 72's `Certifications.Check`, which also refuses the class the unit is already in. It then needs the class to have a trial, and it needs the unit not to have tried that class since the last map. The trial plays on the screen with every `play` command until `leave`. The screen lists the trials under the shop line.
3. **One attempt per unit and class per camp.** `CampaignRecord.TrialsTried` holds the attempts, whether passed or failed. `AfterBattle` clears it, and the protocol's campaign record carries it as `trialsTried`. A record written before this change reads as none tried.
4. **A pass carries the trial's fights, and no mastery.** The unit certifies into the class with no seal. It keeps the level, EXP, stats and weapon ranks the trial left it. Its own inventory, abilities and mastery points come back, so the trial's loadout stays in the trial and no mastery point is earned either way. A failure changes nothing but the attempt.
5. **A candidate who falls in a trial has failed it and has not fallen.** A trial is an exam, not a battle of the campaign, and permadeath is the campaign's. This is Code's lean, for Chat to argue.
6. **A trial runs on its own seed and under no difficulty.** The seed is the campaign seed plus the number of maps plus the next map's index, so it never shares a seed with a map of the campaign. A trial tests the class and not the campaign, so a Hard campaign does not harden it.
7. **The Outrider trial is rebuilt with a guard.** A Hold hexer stands at 3,2 in front of the one gate at 3,1, with a forest band on row 3. Only the forest tile at 3,3 strikes the hexer with 3 Canto left, which is enough to ride through the dead guard's tile and the gate to the throne at 3,0. Either flank costs 5 and leaves 1. Against 16 HP both weapons need two hits: the sword is 13 x2 at 92 and the lance 14 x2 at 88. So the pass is one roll at about 85 percent, and the Canto buys both the strike and the seize. The levers and the old sample are gone.
8. **Cosmetic.** Past the turn limit, a board's header reads `over after turn N of N` instead of naming a turn the map never had. A decided battle with no Recall charge says `no recall is left`, and on the campaign screen `no recall is left, so leave`.

## Played

Code by hand (PLAYTEST.md, not cold). Seed 12 won. Seed 13 lost on a first-swing miss. Seed 13's flank line was refused one short. The campaign screen ran the same trial on campaign seeds 6 (pass) and 7 (fail, then refused on the retry).

## Not decided

- Whether the rebuilt Outrider trial needs a bet. It has a proof and a price but no temptation, and the lance is only a worse sword. #73 closes on Chat's cold play of it.
- Whether a fall in a trial should cost more than the attempt (item 5).
- Which other classes get a trial. 0049's lean stands: author one only where the payout makes the bet worth refusing.
