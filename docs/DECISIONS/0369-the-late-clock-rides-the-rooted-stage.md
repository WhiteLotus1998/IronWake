# 0369: the late clock rides the rooted stage 2 on the Warden sample; the tripwire is not met

Date: 2026-10-09. Issue #1423, from the Design Table's rounds 507 (Code) and 508 (Chat). Every Hask number stays provisional on #1247 and goes to Lotus on his Hask list.

## Decided

- **(b) is built on (c).** The `swallow` block takes an optional `late`. A late stage holds Frozen Iron's first landing for one of his phase starts: the swallow sets the dose and a hold (`BattleState.FrozenIronHeld`, `frozenIronHeld` in the protocol), the next phase start of his side lifts the hold and lands nothing, and the clock runs from the one after (2, 4, 6, ...). The Kin's heal is not held. The exposure sum counts no landing while it is held (`Swallow.NextLanding`). `hask_warden` carries it; his card says the clock starts at the second phase start, and once swallowed, that it lands at the phase start after next. The campaign keep keeps the stand-in, unchanged.
- **The read: 115 / 59** (`docs/measurements/keep-1423-late.txt`), as 0366's scratch screen said, against rooted alone 120 / 53. Clock deaths of 2+ fall from 85 to 55 of 165 (full) and from 62 to 37 of 110 (depleted). The gate (at most 1) still fails on both.
- **Round 508's tripwire is not met.** Full's share of stage-2 damage on him from tiles he cannot reach is 33 % (31 % rooted alone), depleted's 43 % (43 %). Under 40 %, so (b) stays.
- **The depleted pre-swallow falls, by where the hunt stood** (a new `--finale` line). Of 29: hunted while he held his front alone 8 (the Sworn Hunter 5), hunted and paired 1, hunted behind the fronts 10 (Hask 7), not hunted behind the fronts 9 (Hask 8), his own phase 1. 15 of Hask's 16 come with the captain behind the fronts, 5 of them down the line. So the hunter's weakest-front pick finding a lone captain is a minority (8); the larger share is stage-1 Hask reaching a captain who has fallen back inside the walls.

## Open, for the Table

- Depleted is 61 under gate 1. The bar stays untouched until the Table reads the falls line above; the read names Hask's stage-1 reach inside the walls, not the hunter, as the larger share.
- The clock-death gate fails on every arm built so far.
- Chat's cold L8 chair on the rooted sample, and Code's owed play, now read this fight.
