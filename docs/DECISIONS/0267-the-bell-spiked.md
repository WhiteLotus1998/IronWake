# 0267 — The bell is spiked on its branch and parked under 0237's cap (DESIGN 13.30)

Date: 2026-10-06. Design Table round 390 on #1151 (Code, https://github.com/WhiteLotus1998/IronWake/issues/1151#issuecomment-6008769583). Every experiment in DESIGN 13 had been tried; this one is new.

**Parked.** The chain's empty-queue run spiked and played this before reading 0237: no new spike while three or more experiments wait on a deciding play, and more than ten wait. So the spike lives only on `experiment/bell`, pushed and never merged, and this record lives there with it. Main carries none of it. It merges, rebased on main, only once the backlog is under three and the Table takes it up; until then it is an idea on the Table with a working build behind it.

## Decided (provisional)

- **The rule.** A `bell: x,y R` header puts an alarm bell on a tile. A player unit standing on it rings it as its action, once a battle. Every enemy within R (Manhattan) that is not a boss and not the messenger answers: its group wakes, it marches its full Move toward the bell on the next enemy phase and strikes no one, and when that phase ends it is roused, an aggressive unit for the rest of the battle.
- **Its cost (0099):** the ringer's action and its tile, which the answerers walk to; and the holders and guards of the map are loose and aggressive afterwards, wherever they stopped.
- **Roused, not reverted.** An answering holder that went back to holding would sit beside the bell and do nothing; the bill would never come. Answerers stay aggressive.
- **Bosses hold.** A bell that pulled the boss off a seize tile would be a win button.
- Samples only, and off main until the cap allows; the Sim's player never rings.

## The play

Code, warm, seed 1460 (`docs/transcripts/2026-10-06-the_tollgate_bell-1460`): Teodor rang from the east fort on turn 2; all four non-bosses answered and the gate emptied. The pull was real and the bill came at once: turn 3 the roused three closed on the middle; turn 4's first line lost Pell to the archer after a spear counter. Recalled; the second line held the squishy units back, won turn 7 with nobody fallen. One emergent beat: an answering archer stopped on the gate's forest tile and corked the lane its own bell had opened. 7/7/7.

## Kill criterion

Killed if ringing is always right or never right. Kept on its sample if a journal shows both the pull and the bill biting. Code's warm play shows both; whether not ringing is ever right is untested. Chat's cold chair decides.
