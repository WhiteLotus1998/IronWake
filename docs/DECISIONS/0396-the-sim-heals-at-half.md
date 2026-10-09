# 0396: The Sim heals at half HP or below

Date: 2026-10-09. Issue #1429. From #1418's trace (`docs/measurements/mill-1418.txt`, DECISIONS/0367) and the Table's round 511.

## Decided

- **The heuristic's heal threshold is half max HP or below, not below half** (`HeuristicPlayer.Wounded`). It governs all three places the Sim decides a unit is hurt enough to mend: a consumable on itself, a healing spell on an ally, and the unarmed healer's walk toward a patient. Before, an exact half (11/22) was never healed, and on the Mill 20 of 41 timeouts were the captain parked there with every approach lethal at 11, Field Dressing and Maud's Salve unused to the turn limit.
- **The threshold, not "no approach passes the veto".** The issue offered both. The threshold is one comparison that already existed and was off by one at exactly half; the veto-keyed rule would add a second reason to heal that no map has asked for. The threshold is the honest fix for the fault the trace found.
- **Read** (`docs/measurements/heal-at-half-1429.txt`): no tuned cell moves past noise (the Tollgate 191, Brackwater 133, Harrow Weir 132 all unchanged; the field 103 to 104). The keep's finale 149/135/0 to 150/134/0, ok. Untuned: the Mill 107 to 115 (timeouts 41 to 30; captain deaths 5 to 8; Maud's 47 unchanged), Saltmarsh 50 to 54 (gate 4 0.075 to 0.090, now passing), Sallow 175 to 173.
- **It ships.** The issue's bar was "no cell moves past noise", read on the `tuned` maps; none does. The Mill's +8 is the parked captain unparking, which is what the issue was for. 115 is still under gate 1's 120, so 0394's condition 3 (every screen under the gate) still holds and the Mill stays not `tuned`, waiting on the Critic's cold chair (#1479).

## Corrects

- 0367 read "fixing the park moves no wins" on screen A (103), which also changed the consumable rule to "a real wound". The bare threshold fix moves 8 on the Mill. The trace's other findings stand.
