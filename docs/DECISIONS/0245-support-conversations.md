# 0245: support conversations, the slot

Date: 2026-10-04. Issue #77 slice 8 (scope on the issue, round 343; #1001's format, 0244). Built by the chain Builder.

## Decided

- **A conversation is a scene script.** `content/scenes/<id>.txt` with `plays: support <a> <b> <tier>`: a support pair of `campaign.json` in either order (stored in the file's order) and a tier of `rules.json`'s `supportTiers`. Its lines, conditions, locks and incidentals are 0244's; its `map` is empty. A support conversation holds at most 20 lines, its rules lines aside (WRITING.md's budget), and a beat sheet does not lift it. The loader refuses a second script for one pair at one tier, naming the file.
- **Heard at a camp, once each, lowest tier first.** `support <a> <b>` (ids or names, any case, either order) plays the pair's lowest tier that its rapport on the record has reached, whose conversation is written and not yet seen: a pair at B with C unseen hears C, then B. Both members must be on the living roster. Refused with the reason: no such unit on the roster, no support pair, below C (`have not reached support C`), or nothing waiting. It costs nothing; a conversation is reading, not a camp trade.
- **On screen.** The Roster panel lists what waits, one line, `Conversations: Wren and Pell C (support wren pell)`, after the meeting lines. The command prints the event `Wren and Pell talk (support C)` (logged), then the shown lines under `-- Wren and Pell, support C --` as a scene prints them (screen text, not logged). The client's `SeeSupport` queues the same lines as a card.
- **On the record.** `supportsSeen`, scene ids in the order seen, written only when not empty, read as none when absent; an id that is not a support conversation of the content is refused. The protocol's scene shape carries `a`, `b` and `tier` in place of `map` for a conversation, `point` `support`.
- No authored text ships here. The first conversation is #1004's Wren and Pell C, after its cold read.

## Tests

`SupportConversationTests`: the header parses and writes back, a bad pair, tier or word count fails load naming `plays`, two scripts at one tier fail, the lowest unseen reached tier plays first and each once, a tier not reached waits, every refusal names its reason and leaves the record unchanged, a fallen member's conversations never wait, a conversation over 20 lines fails load, the Roster line, the printed block, the record round trip and the protocol shape. `SupportCommandTests`: from a save, the camp lists the conversation, `support` plays it once into the screen and not the log, and a bare `support` prints its usage.
