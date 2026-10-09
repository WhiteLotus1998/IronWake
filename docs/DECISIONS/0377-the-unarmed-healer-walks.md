# 0377: the Sim's unarmed healer walks with the company, onto tiles no enemy reaches

Date: 2026-10-09. Issue #1395, from the Design Table's rounds 524 and 525 (Code found the hold, 0376; Chat agreed it is a fix and gave the walk's shape). A change to the Sim's player on every map; every Hask number stays provisional on #1247.

## Decided

- **The walk** (`Players.cs`, `HealerWalk`): a unit with a heal it can wield and no weapon it can strike with, with no heal to give, on a Rout or Defeat Boss map (Seize and Escape already walk it to the objective), walks toward the nearest ally below half HP (the heal's own threshold) that has a path, by the fewest steps left to a tile its heal reaches that ally from. With no one hurt, it walks behind the ally standing nearest a seen enemy (any enemy when none is seen): a tile from which that ally is within its Move and its heal's range next phase first, then the fewest steps left.
- **Only tiles at exposure 0.** A tile counts only when it closes on the ally, no enemy's no-crit strike reaches it, and it is not left free for a corked captain. When none does, the healer waits, and the idle line counts that wait apart (`an unarmed healer's N`).
- **This departs from round 525 by one step, on the screen.** Round 525 agreed a fallback to the least exposed closing tile, never lethal, and named the exposure-0 preference as the knob if her deaths rose. They rose, so the knob was turned to its end. On the Warden sample (`keep-1395-walk.txt`), wins full / depleted / floor: no walk 91 / 66 / 1; round 525's rule 91 / 67 / 0, her stage-1 falls on depleted 19 to 101; exposure 0 with no one hurt only 91 / 68 / 0; **exposure 0 always 91 / 84 / 0**, her falls 83. Provisional: the Table can reopen it.
- **The reads round 525 asked for** print beside the race: `company at N % HP` at the swallow (stage 2's line), and `unarmed healers fallen in stage 1` (the idle line). Depleted's company arrives at 60 % (61 % before); the gain is more swallows (110 to 128) and fewer stage-1 timeouts (61 to 42), not a healthier arrival.
- **The re-runs (#971):** `--full --all`, every gate cell unchanged. `--finale content/keep/ironwake_keep.map`: 148 / 114 / 0 to **149 / 142 / 0, finale: ok**. The campaign keep's depleted company passes gate 1 (120). The Warden sample stays FAILED (91 / 84; 120 is the bar).
- `tests/parity/campaign/full-campaign-644.script` is rewritten by `--campaign-script 644`: Maud walks on the keep.

## Open, for the Table

- #1395's title (the campaign keep fails gate 1 on main) is met on main by this fix. Whether #1395 closes, or stays open for the Warden sample's stage-1 lever (wave entry, round 523's lean), is the Table's call.
- Floor drops from 1 win to 0 on the sample (its timeouts rise); data, a cold chair decides.
- Her stage-1 falls rise on every walk. Where they are taken is not yet traced.
