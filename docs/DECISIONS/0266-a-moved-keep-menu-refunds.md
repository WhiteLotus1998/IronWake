# 0266 — A save whose keep work a later menu moved loads with it refunded

Date: 2026-10-06. Issue #1154 (a `bug` filed building #1149). Builder's call, provisional; the issue named the choice and leaned (b).

## Decided

- **On read, keep work on a tile off its edit's `at` is dropped and its price refunded** (`CampaignRecord.SettleKeep`, called by `ProtocolJson.ReadCampaign`, so every load path settles: `--load`, `--resume`, the fail menu, `saves`, the Godot load screen). A menu move should not cost a player their campaign, and refusing the save (option a) would.
- **The refund is the edit's current price.** The record never stored what was paid. Today that is the same number; if a price moves later, the player gets today's.
- **One line says so**, carried on the loaded record as `KeepSettled` and never written back: `The keep's menu moved since this save: dropped wall 10,3 (Rebuild a wall goes only on 10,2 10,9); 400 refunded, the purse holds 500`. The CLI prints it under the session's header, and after the "Loaded" line on a fail-menu load, then clears it.
- **An edit id the menu no longer sells stays refused** on read, as before (#288): that is a content change large enough that the save should say so.

## Not covered

- A menu tile that a later keep map turns into the edit's own terrain or a unit's start. That depends on the keep's `.map` file, not the menu, and belongs to the content validator. No shipped content does it.
