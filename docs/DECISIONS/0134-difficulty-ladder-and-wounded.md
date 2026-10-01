# 0134 — The difficulty ladder (Recruit, Captain, Tactician) and the permadeath toggle with Wounded (2)

Date: 2026-10-01. Issue #664. The shape is Lotus's rounds 199 and 200 with Chat's round 201 section 5, agreed by Code in round 202, as the issue body gives it. Where the issue left an implementation choice, the Builder's leans are below; Chat can argue them on the PR.

## Decided

- **Captain is `normal`.** The id stays (records, scripts, the Sim); `rules.json` gives it the display `name` Captain, and every screen prints the name. It stays the identity, and every gate and Fun Gate is read on it.
- **Difficulty fields.** `recallOffset` (added to each map's charges, held to 0..99; a difficulty names either `recall` or `recallOffset`, never both), `name`, and `unlockedBy` (another difficulty's id). `--difficulty` takes the id or the name, case ignored.
- **The numbers (the Sim's).** Recruit: every enemy stat at 92 percent, one Recall more. Tactician: every enemy stat at 105 percent and the enemy level up 1, one Recall fewer. Measured on the three tuned maps at 200 seeds (`docs/measurements/2026-10-01-difficulty-ladder-664.txt`): Captain 74 / 64 / 61 percent on the Tollgate, Brackwater Cut and Harrow Weir; Recruit 85 / 82 / 78 (mean +15); Tactician 66 / 54 / 43 (mean -12). The level offset alone was uneven (+1 moved them 4 to 10 points, +2 took Brackwater to 8 percent and left Harrow Weir where it was, since the offset raises a floor), and 108 percent fell off a cliff (-31 to -52), so Tactician is +1 level with 105 percent. Gates 2 to 8 pass on both. Tactician sits under gate 1's floor on two maps, which is the point of a harder setting, not a failure of the maps.
- **The unlock lives in a profile.** `profile.txt` beside the saves lists each difficulty a campaign has been won on; winning the last map writes it and prints `Unlocked: Tactician`. Loading an old save never locks anything again. A scripted run without `--saves` keeps no profile and may play every difficulty, so the Sim, CI and the partners' script plays reach Tactician.
- **Wounded is on the unit, and it is taken at once.** `Unit.Wound` holds the penalty and the main maps left. The penalty comes out of the unit's own stats when it is inflicted and goes back whole when it expires, so battles, forecasts, the Sim and the cards all read the wounded numbers with no rule of their own, and a level gained while wounded is kept.
- **The two highest stats (lean).** HP is never one of them: -2 HP on a 30-HP unit is not a wound anyone feels. They are read as the class has them (`EffectiveStats`), a tie going to the earlier stat in stat order, and no stat goes below 0.
- **What comes back (lean).** The unit as it began the battle it fell in, with the EXP set to 0: its level, pack, uses and every counter (bound items, Kinsbane's count, the heirloom's) as they were. What it gained in that battle before falling is lost with the fall.
- **"Main maps only" (reading).** A wound counts down after each main map, fought or not (a benched unit heals too). A side map never counts it down, but a wounded unit fights wounded there. With permadeath off, a fall on a side map wounds instead of killing, and a member who falls on their own quest does not close it: it opens again after the next map.
- **A second fall restarts the wound**: the old penalty is given back, then a fresh one is taken.
- **On screen.** The record's header and a new game print `difficulty Captain, permadeath on`; the roster row says `Wounded (2)`; `show` on the camp screen and in battle prints `Wounded (2): Str -2, Dex -2 for 2 more main maps`.

## Not done here

- The survivor's card line where a fallen's epilogue card would be (text behind #656).
- The Godot client's difficulty, permadeath and wound screens.
- A Fun Gate play on Recruit or Tactician: the ladder is measured, not played.
