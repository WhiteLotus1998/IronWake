# 0392: the hill after the keep, and the Reseal or Fight card

Date: 2026-10-09. Issue #1386, slice 3d. Builds on 0385 (the secret path's flag, the coma), 0390 (the Kin begins swallowed) and 0391 (the hill's board). The shape is STORY's Under the Hill: "one extra map after the keep, and the choice is on its card"; Reseal needs a rite-keeper alive, and with none the card offers only Fight.

## Decided

- **The hill is reached by the coma.** A keep won with a boss left in the coma (`CampaignRecord.Coma` not empty) stands at the hill instead of finishing. The coma happens only when the secret race ran (0385: three conditions read before the battle, Marrit held read in it) and the shard was taken, so it is the one fact that says all four held and the race was won. A lost race (he swallows) finishes at the keep, as before.
- **The hill is not one of `maps`.** It is the keep's `secret.hill` in `campaign.json` (`map`, `enemyLevel`, `keepers`, a `before` card), read as `UnderTheHill.Hill` and `CampaignRules.Hill`. Trial and quest seeds read the main maps' count, so appending the hill there would have moved every campaign script's rolls. `NextMap` returns it at `MapIndex == Maps.Count`; it plays on `Seed + 10`, and a won hill takes `MapIndex` past the count. `IsFinished` holds off while the hill waits and the card is not answered with Reseal.
- **The map lives in `content/hill/`**, a directory of its own, so `--full --all`, the art spec and `validate`'s map count still see only the main line while its boss is the Warden stand-in. `validate` loads it and refuses a missing file. It is 0391's board unchanged; the sample stays where 0391 left it.
- **The card is two camp commands, `reseal` and `fight`** (Code's lean, provisional), answered once; the march is refused until one is chosen, the way the branch's `pick` refuses it. `reseal` needs a keeper standing in the company (`keepers: maud, pell`, read as STORY's "Maud, or Pell reading": either alive) and ends the campaign at the camp, won. With neither standing, the card offers only `fight` and says why. A lost hill is a lost campaign: the fail menu, as on any main map (STORY's worst ending; its card is #634's).
- **The camp at the hill** keeps the shop, repairs and duties; the keep's menu closes ("the keep is fought") and no side map is offered. The lines say "after the keep" where they said "map N of 10".
- **The save and the ending:** the record's `hillChose` (`reseal` or `fight`, written once answered); `ending.json` goes to version 3 with `hill` (`reseal`, `fight` and won, or null). The ending's word stays `pending` until #634.
- **Marrit leads them down on the card's text**, placeholder until Lotus's story pass: STORY's second half of her free line. Whether the keep's take frees her on the board stays as built (bound until the keep is won).
- **The Sim's campaigns still end at the keep** (`SimPick.Marching`): a run that leaves Hask in the coma stops at the card unanswered, so no campaign read moves. The hill is read on its own with `--finale`.
- **The client** answers the card through `CampaignClient.Hill` and the script's `reseal`/`fight` lines; its buttons ride #1469.

## Read

- Code 1552, warm, the whole company at L8 from a save past the keep (`docs/transcripts/2026-10-09-under_the_hill-1552.*`): the card, `fight`, the march, the shard broken on turn 2 by Brannock, the Kin never reached, lost on turn 5 to Frozen Iron 9. 7/7/5.

## Not decided here

- Whether breaking the shard should touch Frozen Iron (0390's question).
- The endings' text and the worst ending's card (#634), and Marrit's place on the board.
- Tuning the hill, after #1453 and the Kin's real numbers.

## Next

- #1386: tuning, a cold chair on the race, and the Sim's gates; then close.
