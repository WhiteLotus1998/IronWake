# 0165 — The captain's ladder at tier 1: Hunter's Ground killed, the lance moves to the Champion (#705)

Date: 2026-10-02. Issue #705. Round 229 on the Design Table (#731): Code's opening and Chat's answer. Provisional.

## Killed: Hunter's Ground

- The spike: +2 Def and +2 Spd to the Ranger on forest or hill, as a new `ground` effect kind. Gate 1 at 100 seeds: the Tollgate 57 to 53, the Mill 63 to 65, Brackwater 66 to 66. The Ranger captain absorbed no more damage than before; on the Tollgate it absorbed less and dealt more. Reverted unpushed.
- Why: the Sim's player prices exposure, and a bow captain's cheapest tile is one out of reach, not one in the wood. Against this player a standing bonus on a ranged class cannot be read, because the player never stands. No ground spike on a ranged class is built until the player changes.

## Decided (provisional)

- **The lance moves from the Vanguard to the Champion.** The Vanguard is sword, HP, Def and Shoulder to Shoulder until level 10; the Champion gets lance, then axe. Two reasons: the Vanguard's tier-1 lead came from the lance and from standing in front, which is the anvil line that round 228 found eating the company's EXP; and a two-step Champion is a better reward than one step. Tier 2 is re-read on all three maps in the same PR, since the Champion was already 6 wide on the Tollgate.
- **The Ranger is unchanged.** A person anvils with a Ranger without trying (Chat's 737 captain held Brackwater at 10/28 and took 45 percent of the EXP); the Sim's player does not.
- **The bar.** Within 10 points at 100 seeds, and no class more than 10 under the unpromoted captain on any map; 5 points at 400 seeds before anything ships as `tuned`. A gap between two gate 1 rates at 100 seeds carries about 7 points of sampling error.
- **The exemption.** A class that fails the second clause on a map where its captain absorbs under half of the cadet's damage gets a hand play of that map, and the play decides. The Ranger on the Tollgate (57 against 70, a third of the damage) is the first case: Chat's Ranger-captain Tollgate play. If the line cannot be made to work by hand, the bow's verb reopens.
- **`--ladder` prints the captain's EXP share per class** once #738's column exists. A class that loses gate 1 by giving the anvil away is a finding about the bar, not a class to buff.
- **Watch the Marshal.** If it falls outside 10 on Brackwater once the Vanguard drops, move its numbers before any verb; the aura is a mastery and arrives late on purpose.
