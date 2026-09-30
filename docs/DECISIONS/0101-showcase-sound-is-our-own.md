# 0101 — The showcase's sound is our own, synthesised in the repo

Date: 2026-09-30. Issue 516 (showcase slice 6). Rounds 162 to 164 on the Design Table (#547). Builds on DECISIONS/0092 (sound CC0 with a `LICENSES` file, or cut).

## Context

#516 asked for a small CC0 set (hit, miss, crit, fall, a UI click, one ambient bed) fetched from GitHub-hosted sources, since the sandbox cannot reach the usual CC0 libraries, with an ambiguous licence cutting the clip. Every candidate would have been a third-party file whose licence we read second-hand.

## Decision

1. The six clips are synthesised by `docs/sound/make_sounds.py` from sines and a fixed-seed noise generator, 16-bit mono PCM at 22050 Hz, the same bytes on every run. They are the project's own and dedicated CC0 1.0 in `LICENSES`, so there is no licence to guess and the rule that cuts an ambiguous clip never fires. Code's lean, derivable from 0092; reversible.
2. A clip from elsewhere (Lotus's, an artist's) replaces one only under the same file name, with a CC0 licence stated at its source and its own `LICENSES` entry. No code changes.
3. Which cue plays when is the client's (`Ironwake.Client.Sound`), read from the beats: a strike's number sounds as it rises, a death as its beat starts, so a skipped beat is silent and no sound runs ahead of the board. `ClientSoundTests` hold every cue to a readable file named in `LICENSES`.
4. M mutes everything, shown on the title and in the footer. On the campaign's screen M stays the march; the campaign is outside the showcase.
5. Neither partner can hear. The clips are judged by Lotus alone (0092); the partners check the licence file, the mute key, and that the cues fire where the beats say.

## Also in #516, from rounds 163 and 164

- All three turn-1 callouts sit at the foot of the column; the third lights the footer's E key instead of pointing at it, so no callout covers a unit, a lit tile or the legend.
- The Recall card's first line is Chat's round-163 wording, word for word (round 164): two sentences, "in one of your turns", "the dice remember: the same swing rolls the same."
- A Seize map lost on the clock says so on the end card without coordinates ("Turn 10 ran out with the captain short of the gate."), the seize tile named as the legend names it; the log keeps the console's verdict with its coordinates. Every other loss keeps the console's words.
- The objective line's "throne" is #569's.
