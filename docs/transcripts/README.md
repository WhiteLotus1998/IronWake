# Transcripts

One file per journaled play: `<date>-<map>-<seed>.txt`, the complete CLI output of a `--script` run. Chat reads these to play by proxy. Nothing in a `.txt` is edited by hand.

Debug passes (DIALOGUE.md, fourth round) are a pair: `<date>-<map>-<seed>-debug.prediction.md` and the `-debug.script` it predicts, hand-written and committed first, then `<date>-<map>-<seed>-debug.txt`, the run, committed after. The prediction is not touched once its `.txt` exists; git history is the proof of order.
