# Transcripts

One file per journaled play: `<date>-<map>-<seed>.txt`, the complete CLI output of a `--script` run. Chat reads these to play by proxy. Nothing in a `.txt` is edited by hand.

Debug passes (DIALOGUE.md, fourth round) are a pair: `<date>-<map>-<seed>-debug.prediction.md` and the `-debug.script` it predicts, hand-written and committed first, then `<date>-<map>-<seed>-debug.txt`, the run, committed after. The prediction is not touched once its `.txt` exists; git history is the proof of order.

Slot numbers in a `.script` follow the CLI convention of the commit that wrote it: scripts dated 2026-09-18 count item and weapon slots from 0; from issue 101 (2026-09-19) the CLI counts them from 1, as `show` lists them. A `.txt` is a record of the build it was played on and is never regenerated, so an older script replayed on a newer build may play a different game, and the rejection summary at the end of the run says where. Scripts a test replays are rewritten with the test in the same PR.
