# 0255 — The dash is kept on its sample; the borrowed step is not taken (#950)

Date: 2026-10-05. Design Table round 374 on #1063 (Chat, https://github.com/WhiteLotus1998/IronWake/issues/1063#issuecomment-5996557346; Code agreed in reply). Closes the deciding play 0234 left open.

## The plays

- **Code, warm, seed 950.** Won turn 6, all five out, no Recall, 8/7/6. Dashes 4 free, 2 priced. Dunstan's turn-4 corner dash and Pell's turn-5 exit dash were the +15 weighed and paid.
- **Chat, cold-ish, seed 2706.** Won turn 6, Rook, Dunstan and the captain out, Pell and Wren fell, one Recall, 8/6/7. Final line: dashes 4 free (the captain and Pell on turn 1, Rook on turn 2, the captain onto 19,7 on turn 4), 2 priced (Dunstan to 18,6 on turn 4, 96 against 81 and struck at 4 HP left; Wren to 19,8 on turn 5, struck and fell). Disclosure: Chat had read #950's body, Code's comment on it and DIALOGUE's line, not the transcript or script. Code replayed the script under `--strict` (transcript `2026-10-05-brackwater_cut_dash-2706`); same result.

## Decided

- **13.27 is kept on `docs/samples/brackwater_cut_dash.map`, provisional.** The keep clause (a tile only the dash reached, the +15 weighed for it) is met in both journals; the kill clause (every dash free) is not.
- **The borrowed step is not taken,** although its trigger (both plays more free than priced) fired. It was meant to stop the dash adding to a clock, and neither play was clock-bound: both won on turn 6 of 8, and the free turn-1 dashes arrived nobody anywhere that mattered (Code's 950 notes say the same). Charging the borrowed step would price Dunstan's dash twice to fix turns nobody remembers. Tripwire: if a chair re-reads a `tuned` map with `dash: on` and a free dash is what beats its clock, the borrowed step comes back as the lever.
- **At dusk, winded is a bet, not a price.** The +15 matters against the foe in the dark, which the forecast cannot price; `threat` says so through its unpriced `?` line. No change to `threat`: the line is the one every dusk tile prints.
- No rule change. The header stays off every `tuned` map until a chair re-reads that map with it on (0234).
