# 0173 — Brackwater and the raid at 200 seeds; Pell's Gust is the whole move (#757)

Date: 2026-10-02. Issue #757, from the Table's round 232 (#731). A measurement, not a tuning: no map, rule or content file changes. The two Sim options are the Builder's and provisional.

## Decided

- **Brackwater Cut stays `tuned`.** At 200 seeds its file reads 129/200 (65 percent) on main against 133/200 (67) before #756 (c8f5ab3). The 100-seed read's 60 was the low edge of the noise. It holds gate 1's floor, so 0078 stands and nothing changes (the issue's first branch).
- **Pell's choice is the entire difference on Brackwater.** With Pell carrying Cinder alone, main reproduces the pre-#756 reads exactly on all three: file 133/200, campaign 7/200, party +5 141/200, with the same timeouts and the same captain-alone counts. Pell is the board's only two-weapon unit, so the chooser changes nothing else there. Gust costs about 4 on the file (noise) and 11 at party +5 (141 to 130, at the line). The counter-slot question doesn't arise, because the map holds.
- **The raid's move is real.** As the campaign fights it, the raid reads 58/200 (29 percent) before and 74/200 (37) after, so the +8 at 100 seeds is +8 at 200 too. File and party +4 are within 3. Pell casts Gust 703 times to Cinder's 37 there. Nothing is tuned on it: it's a gain that needs nothing from us (round 232).
- **The instrument.** `--curve` takes `--carry <unit> <weapon>` (the cast member carries one full stack of that weapon and nothing else, `Program.Carry`; the wildfire trace's test now uses it) and `--items`. That prints, under each read, the attacks and counters every player unit made per item, summed over the seeds (`ItemMix`, `GameResult.Items`), because `WeaponMix` counts by type, and Cinder and Gust are both reason.

## Measured

`docs/measurements/curve-757.txt` has every row. Pell's items on Brackwater's file (attacks/counters over 200 games): main `cinder 9/12 gust 360/125`, Cinder-only `cinder 353/82`. Gust is Wt 1 against Cinder's Wt 3 on a Str 1 caster, so she keeps her speed and counters more. Moving from Cinder to Gust on the file read also shifts the rest of the company: Dunstan's attacks drop from 107 to 38, Rook's rise from 395 to 536, and survivors p50 goes from 0 of 4 to 1 of 4 (captain alone out 84 to 59). So the Gust company gets more recruits out and wins slightly less.

## Open

- Whether the score should trade a point of Mt and the fire for ten points of hit and two of Wt is still 0171's open question (ignition unpriced). Brackwater no longer forces it.
