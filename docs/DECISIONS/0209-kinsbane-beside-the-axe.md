# 0209 — Kinsbane beside the axe, bound on issue, the hunt only to kill (#851; amends 0207)

Date: 2026-10-03. Built by the chain Builder. The shape is the Table's (rounds 275 to 277): lever 1 of three, no number moved. The scythe is issued beside her iron axe and bound to her, and the Sim's hunt swings it only to kill. The bar and the lever order were agreed before the read.

## What is built

- **Beside, not in place.** `CampaignRecord.Kitted` puts an issued weapon in front of the pack at full uses and keeps the cast pack behind it. A full pack drops its last stack to make room. Keziah joins with Kinsbane, her iron axe and her gauntlets. The drain runs for whoever carries the scythe, equipped or not, so carrying it is the cost.
- **Bound on issue.** `weapons.json` gives Kinsbane `boundTo: keziah`. The camp's `drop` refuses it (`Kinsbane is bound to keziah and is never dropped`), a chest may not hold it, and it is never left as a keepsake. The signature ceiling leaves out a bound hungering weapon (`SignatureCeiling.Items`). It is no quest's payout, and 13.23 judges its numbers, the same way the forge already refuses to Refine it. The smoke row still reads 4 items.
- **The hunt, only to kill.** In `HeuristicPlayer.PlanUnit`, an attack with a hungering weapon not yet woken whose forecast kills on one plain hit (`KillsOnHit`) beats any other attack. One that does not kill loses to any attack with another weapon, so she swings the best other weapon by the usual score and uses the scythe only when nothing else reaches.
- **`--kinsbane` counts her attacks** on each winning try, and how many used the scythe (round 277's share). The `--axe` control now only takes the scythe out, since the axe is already behind it.

## The reading (200 runs each, same seeds, `docs/measurements/kinsbane-851-beside.txt`)

| Map | Beside won | Axe won | Beside fed p50 (teeth) | Her attacks (scythe) |
|---|---|---|---|---|
| 6 the raid | 82 | 82 | 3 (1) | 250 (214) |
| 7 Sallow Grange | 34 | 49 | 5 (2) | 126 (69) |
| 8 Brackwater | 33 | 49 | 6 (3) | 33 (26) |
| 9 the field | 30 | 49 | 11 (4) | 164 (135) |
| 10 the keep | 7 | 7 | 13 (5) | 44 (22) |

The control arm reproduces 0207's row for row, so the comparison is clean.

**The bar misses on two of its three clauses.**
- Sallow is 34 of 82, up from 26 but short of 40.
- The field's feed median is 11, which is four teeth and not woken. Under replace it was 13.
- The keep is no worse: 7 of 30 reachers (23%) against the axe's 7 of 49 (14%).

The scythe share is not the dull pass round 277 warned about: she uses the scythe on 55 percent of her Sallow attacks and on 82 percent at the field. What it shows instead is that iron saves fewer runs than expected. On Sallow she still starves (in 18 of 34 wins, against 12 of 26 under replace), because the drain runs while she swings the axe. That is the walking tax round 276 named.

## What follows

Per #851 and round 276, the next lever is the first fallback, one per read, on the same seeds against the same axe arm: the drain skips a phase start with no enemy in her reach. Beside and the bind stay. It is filed as its own issue. Drain 5, the heal at 10 and the schedule stay as built, provisional.

No hand play was made for this slice. The bar sends Sallow to Chat's cold chair only once a read passes, and this one did not.
