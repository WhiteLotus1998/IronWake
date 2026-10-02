# 0181 — A thinned company fights short-handed (issue 795)

Date: 2026-10-02. A bug fix; the rule is the lean written on #795 when it was filed, and it extends the `deploy: all` rule already built (issue 689).

## Why

`CampaignRecord.Begin` assumed the living roster fills every bare slot. Permadeath can thin a company below a map's slots (the heuristic's seed 631 had eight fallen by Harrow Weir), and `march` then threw out of `BattleState.Fill`: exit 134 in the console, an uncaught throw in the client. A rule of the game crashed the game.

## The rule

- In the campaign a bare `recruit` slot the living company cannot fill stays empty, as past the company on `deploy: all`. The map is fought short-handed. A named slot whose recruit has fallen already stood empty; that is unchanged.
- The camp's deploy line reads `(deploy 3 of 3; 3 slots stand empty)` in place of the old `ERROR:` line. The count is every player slot left empty, named slots of the fallen included, so a fallen named recruit now shows as `1 slot stands empty` (the barracks transcript's Sallow Grange line).
- `march` never throws: `CampaignRecord.MarchRefusal` names any refusal left, printed as an `ERROR:` camp line in the console and as the status in the client.
- Outside the campaign (`BattleState.From` without `shortHanded`, as the Sim's fixtures and single-map play call it), a roster too short for its map is still a content error and throws.

## Not done

- No warning before a map is marched short-handed beyond the deploy line; the camp already prints it on every screen.
