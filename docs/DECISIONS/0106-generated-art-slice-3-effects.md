# 0106 — Generated art, slice 3: the effects in LOOK's own values

Date: 2026-09-30. Issue 564, slice 3. This is implementation and reversible. The colour lean is provisional and waits on Chat's word on the PR.

## Decided

- **The effects come before the boss token variants.** 0105 had the order the other way round. The issue names the effects outright. The boss tokens still need a naming question settled first: per boss or per boss weapon, and whether the toll warden counts. Like the clips, the effects change no scored frame, since nothing plays them until #535.
- **The effects have rows in ART_SPEC, derived from content.** There are six fixed rows: `fx_hit_spark` (5 frames), `fx_slash_arc` (5), `fx_crit_flash` (6), `fx_heal` (10), `fx_dust` (6) and `fx_embers` (12, loops). Each Reason or Faith weapon that strikes also gets an `fx_spell_<weapon>` of 8 frames, today `bolt`, `cinder`, `gust` and `radiance`. A new spell shows up as a missing row. Salve and Beacon share `fx_heal`. `ArtSpec.Effects` holds the list, and `ArtSpecTests` holds the spec's table to it.
- **An effect is delivered the way a clip is.** It is a one-row sheet of 256 x 256 frames with a sidecar. The sidecar's `pivot` is (128, 128), the point the scene lays the effect on: the struck body's centre, or for dust the ground under the feet. Its `contact` is `null`.
- **The effects are drawn in LOOK.md's own values, opaque, and never tinted.** They are the lean. An effect is not per side, so the three-grey tint of 0105 has nothing to carry. The values are: white (`mark.struck`) for sparks and the crit flash, `ui.text` for the slash arc and Radiance, frost (`mark.reach`) for Gust, Bolt and the heal, salt grey (`terrain.road`) for dust and Cinder's smoke, and ember (`terrain.fire`) only for Cinder and the embers. Fire stays the one warm thing, and no effect borrows the player's amber. The tone rule holds: a heal is motes rising, not a halo, and Radiance is a hard-edged fan of rays.
- **`ArtGeneratedTests`** holds the effects to these rules. Every row is written. Each sheet has its frame count times 256 across, and its sidecar matches. Every pixel is clear or opaque in one of that effect's values, ember appears only in the fire effects, and no frame is empty. A new effect fails until it is given its values. `docs/art/contact-effects.png` shows every frame of every effect at half size.

## Next slices

Next comes loading the tokens and tiles in the client, after Code's final score, with the frames re-rendered. Then the token rows for the Tollgate's boss variants, and then the `for-lotus` sheet.
