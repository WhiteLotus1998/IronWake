# 0185 — Supports in battle, rapport on the record (#77 slice 3)

Date: 2026-10-02. Built by the chain Builder. Provisional: every choice below is a lean on #812's Unsure, which the Table has not answered yet; Chat argues on the PR or the Table.

## What is built

- **The bonus.** A unit beside a support partner (`campaign.json` `supports`) whose rapport reaches a tier fights with that tier's hit, avoid and crit (0184's table). It is added to the unit's `Combatant.Aura`, the on-combat bonus `AbilityRules.Against` already sums, so the forecast, the resolver and both planners read one number. `Supports.Bonus` is the one place it is computed.
- **Best, not sum.** Beside two partners, a unit takes the better tier. Stacking partners would let a cluster outweigh the captain's tier-2 formation bands (0164), and the support is about the pair, not the crowd.
- **Accrual on every main campaign map.** `Rivalry.Accrues` is true behind `rivalry:` and on any board with `CampaignMap` set. Rivalry's combat arm stays behind the header. Off the header only support pairs accrue, so the record carries no rapport that nothing reads. Accrual is still bought under fire: only on a phase an awake enemy could strike one of the pair (0042).
- **The captain's pairs accrue at the recruit's rate alone.** The captain fights every map and stands in ten pairs. At both rates (the captain's Cha 9 gives the top step) every captain pair would outrun every recruit pair, and the captain would be the support hub by arithmetic. A captain beside a recruit who is no partner accrues nothing.
- **The record carries rapport.** `CampaignRecord.Rapport`; `Begin` and `BeginQuest` put it on the board, and `AfterBattle` and `AfterQuest` write the board's back. A lost main map has no record after it. A Recall restores rapport with the board, as before. The save writes `rapport` only when there is some, so older saves read as empty.
- **The roster's line.** The camp roster prints `  Supports: Wren and Pell C, ...` after the members, each living support pair at a tier in file order, and nothing when no pair stands at C.

## What it changed

- Six journaled campaign transcripts gain `Rapport ...` lines on campaign maps and a `rapport` field on the record; no outcome, roll or line otherwise moved. `full-campaign-631`'s parity check now first differs at line 1102 (was 1084), the same order removed, 18 rapport lines earlier.

## Not in this slice

- The writing (WRITING.md, the voice sheets, then support scenes).
- Any battle line naming a tier reached, and the client's drawing of it.
- A read of how fast pairs climb on the campaign: the Sim's `--levels` shape for rapport is the measure the A-at-72 question in 0184 needs.
