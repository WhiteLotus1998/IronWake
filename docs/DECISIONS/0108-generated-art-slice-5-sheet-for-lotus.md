# 0108 — Generated art, slice 5: the labelled sheet for Lotus

Date: 2026-09-30. Issue 564, slice 5. Implementation; reversible.

## Decided

- **The sheet goes before loading.** Issue 564 asks for a `for-lotus` issue "when the first full set is in", with a contact sheet of every unit and a capture of the battle clips. Slices 1 to 4 finished the set. Loading changes no file the sheet shows, so the sheet does not wait on it.
- **A labelled page, not a fourth contact sheet.** The contact sheets `make_art.py` writes are for the partners: no labels, one frame per clip. `docs/art/for_lotus.py` (stdlib) writes `docs/art/for-lotus.html` from `generated.txt`, the sidecars and content, and names every file, so Lotus or an artist can say what to fix by the name of the file it lives in. It shows the cast by name with their tokens (the captain's slot empty, his to render), every enemy token and each tell with who carries it, every tile by display name, all eight clips of the cadet's sword frame by frame, the strike of every clip set frame by frame with the contact frame ringed, every effect frame by frame, and an index of all 192 files. The page slices the sheets with CSS; nothing is copied.
- **`docs/art/for_lotus.js` rasterises it** with the preinstalled Chromium to `docs/art/for-lotus.png`, the image the `for-lotus` issue shows.
- **The clips are shown in the delivered greys**, with a line saying the game tints them by side (0105, provisional), so the files are seen as they are.
- **`ArtGeneratedTests`** holds the page to `generated.txt`: every generated file is named on it, with a test that shows a missing name failing.
