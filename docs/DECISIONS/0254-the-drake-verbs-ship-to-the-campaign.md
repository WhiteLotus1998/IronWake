# 0254 — The drake's carry and breath ship to the campaign (#1094)

Date: 2026-10-05. Builds 0252's keep rounds as #1094 leaned (Code, https://github.com/WhiteLotus1998/IronWake/issues/1063#issuecomment-5994950107); Chat agreed the lean in round https://github.com/WhiteLotus1998/IronWake/issues/1063#issuecomment-5995249813 and sharpened the Brackwater journal.

## Decided

- **The carry is Grown's verb on every campaign battle, main map or side map, with no header.** `BattleState.InCampaign` is a main map (`CampaignMap`) or a side map (`SideMap`, new, set by `BeginQuest`; the protocol's `sideMap`). `DrakeCarry.Open` and `Rime.Open` read it, or the sample header.
- **The breath is Unbroken's verb on the same battles,** once a map, as built (0216).
- **`free` is the only setting.** The `carry:` header is `carry: <rider>` and stays for samples; the loader refuses `waited`, `free` or `brace` as a word before the rider, naming the field and saying the setting was dropped. `CarrySetting`, the `carried` event's `setting`, and the unit's `landed` (the brace setting's mark) are gone. Brace (13.14) itself is untouched.
- **Rook's card prints the verbs with the stage:** `Drake: grown; carries an ally (carry).` and `Drake: unbroken; carries an ally (carry), breathes rime once a map (breathe).`
- **The blind spots stay.** The enemy, the planner, `Legal` and the Sim never carry or breathe, and `--full` plays a map outside the campaign, so no gate moves. Brackwater Cut and the field, the two tuned boards a Grown Rook can stand on, are not retuned before play. The next campaign journal that carries on Brackwater says whether a carry put a body on an exit a turn early (Chat's sharpening); if a journal shows the carry paying a tuned map's price for free (68), the lever is the carry's reach on that map, by the Sim first.
- **The Drover's long carry reads the same** with the header gone (tested on a headerless campaign battle).

## The play

Code warm, the Rookery (seed 1132) from the 1132 save with Rook's drake set Grown by hand (the committed saves predate the drake field), Wren the ally. The carry flew Wren over the ravine on turn 2 and she landed free to kill the archer; Rook then spent her own HP on the bridge soldier, and on turn 8 fell to a 51% rider strike on the exit. Lost, Wren out. Transcript `docs/transcripts/2026-10-05-the_rookery-1132-carry`.

## Not done here

Chat's ask for a save with Rook at lance C and her drake Grown, earned in play, so the Drover chair can be sat cold: no committed save or Sim script reaches it (the full-campaign parity script never runs `rook_1`). Filed as #1100 (by the round-373 session).
