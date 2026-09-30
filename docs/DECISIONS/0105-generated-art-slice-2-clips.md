# 0105 — Generated art, slice 2: the battle clips in three greys

Date: 2026-09-30. Issue 564, slice 2. Implementation and reversible; one lean (the greys) is provisional and waits on Chat's word on the PR.

## Decided

- **The clips come before the loading slice.** Loading waits on Code's final showcase score, because it changes scored frames. The clips change no frame: nothing plays them until the battle scene (#535). So they go now, not with #535 as 0104 had it.
- **Every class and boss clip row of ART_SPEC's names block is written**, 152 sheets in all: 14 (class, weapon kind) pairs and 5 boss weapon sets, 8 clips each. Each is a one-row sheet of 256 x 256 frames with a `<name>.json` sidecar (`frame`, `frames`, `pivot` (128, 232), `contact`), the delivery shape ART_SPEC offers. The generator reads the names block itself, so it writes the contract and cannot drift from it.
- **Figures in three neutral greys** (`#D6D6D6` base, `#9A9A9A` shade, `#4E4E4E` steel, hair and boots), and the client tints a sheet with the side's colour when it plays it. The reasoning: a sheet is per class, not per side, and LOOK carries the side by value. The client already mirrors one sheet for facing, so tinting it for the side is the same move. Provisional: an artist may deliver per-side sheets instead, and the scene decides with #535.
- **One figure rig, posed per clip.** Overhead swings for sword and axe, a thrust for the lance, a draw and loose for the bow, a thrown flame for reason, a staff for faith, and a punch for the gauntlet. The outrider rides a horse. The skyrider rides a heavy moor bird with its wings up, which is grounded, drawn to avoid any winged-horse near-miss. The bulwark and every boss are broader and carry a shield. A fall goes over backwards on foot, and forward onto the neck when mounted. Nothing is drawn below the pivot's row, so a dropped weapon lies on the ground.
- **The boss tells match the tokens.** The Toll Axe is double-bitted and the steel axe single, so the bandit leader's chosen axe reads at a glance. The Toll Spear carries the hooked crossbar.
- **`ArtGeneratedTests`** holds the clips to this: every row is written; each sheet is its frame count times 256 wide; the sidecar matches `ArtSpec.Clips`; only the three greys or clear appear; nothing sits below row 232; and the first frame's feet rest within four rows of the pivot. `docs/art/contact-clips.png` shows one frame of every clip (the contact frame, else the middle one), at half size.

## Next slices

Loading the tokens and tiles in the client, after Code's final score, with the frames re-rendered. Then token rows for the Tollgate's two boss variants. Then the effects, which have no rows in the spec yet. Then the `for-lotus` sheet.
