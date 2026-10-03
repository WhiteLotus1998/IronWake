# 0220 — The ending save for a sequel

Date: 2026-10-03. Issue #807, slice 1. Lotus's note in `docs/FUTURE.md` (2026-10-02: "The sequel will carry over your save file") is the spine; the shapes below are the Builder's and are provisional.

## Decided (the issue, restated)

- A won campaign's save carries a versioned `ending` block a later game can read without this one's code. Nothing in this game reads it back.
- Slice 1 writes what the record already holds and `ending: pending` until the endings are built (#634).

## Decided (the Builder, provisional)

- **Derived, not stored.** `CampaignEnding.Of(record, content)` computes the block from a finished record; the record gains no field, so no two places can disagree. It refuses a campaign still marching.
- **Its own save, `ending.json`,** beside the others, written by the CLI and the client the moment the last map is won (the camps' autosaves skip a finished record, so nothing else held it). The latest win replaces it. It is not listed with the saves and no player save may take the name.
- **One fixed shape per `version`:** every field written, an absent fact as `null`, so a sequel never guesses whether a missing key means "no" or "older file". The fields are in PROTOCOL.md ("The ending block").
- **Beyond the issue's slice-1 list:** the drake's stage and whether Rook lived (`DrakeFlew` landed with #805 slice 4, so the record holds it now), and the seed, difficulty and permadeath, which a sequel needs to say what kind of run it inherits.
- **Kinsbane is read off the pack:** the first living member carrying a hungering weapon gives `bearer`, `fed`, `teeth`, `woken`; null when nobody does (Rook's runs, or Keziah fallen).

## Not built here

- The ending itself and its texture (held or hand-locked; secret reseal, fight won, fight lost; bad): slice 2, with #634.
- A lost campaign writes nothing: a lost battle ends the campaign with no record after it (the bad ending is a lost map). Slice 2 decides whether the bad ending leaves a save.

## Kill / revisit

If the sequel wants every finished run kept rather than the latest, the reserved name becomes a prefix (`ending-<seed>`), with the same block.
