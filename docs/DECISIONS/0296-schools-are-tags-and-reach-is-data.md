# 0296: The schools are tags on Lore tomes, and each class's reach is data

Date: 2026-10-07. Issue #1242. Source: Lotus's schools ruling, relayed on the Design Table #1218 (6027545091, 6027634093, 6027660719); Chat round 416 (6027603142); Code round 417 (6027758524). Builds 0264's leans.

## Context

0264 mapped the schools but built nothing; the spell lists waited on Lotus's spell pass. On 2026-10-06 Lotus split it: build the system now, and he signs off every spell beyond Cinder and Bolt. Chat asked that riders and reach live in data, so that a spell he signs is a content change and not a Core change.

## Decision

- A `reason` weapon may carry `school`: `fire`, `ice`, `lightning` or `earth`. Cinder is fire, Bolt is lightning. Gust and Pell's Commonplace stay unschooled (Gust's school is a question for Lotus, #1247). A school on any other type fails load.
- A class names its reach in `schools`. Only a class that wields `reason` may name one, none may repeat, and an advanced form keeps every school of its base. The lean from 0264: the Adept reaches fire, ice and lightning; the Scholar adds earth; the Marshal and Commander reach fire and lightning.
- `Unit.CanWield` adds one clause: a schooled tome's school is one the class reaches. Equip, attack, the planners and the Sim read it through that one function. An attack, a shop buy and a template's inventory refuse an unreached tome by naming the school and what the class reaches.
- The item card prints `lore E, fire school`.
- Mattias Grue stays plain (Lotus): an Adept with Cinder, pinned by a test.

## Not built here

- **The unit-level `schools` list** that #1242's body floated is deferred to #1246, the primers, which define how a learned school is saved. Brannock's earth waits on his class (#1247, question 3).
- Riders (#1243 to #1245), primers and the Mag gate (#1246), and every new tome (#1247).

## Measured

Every shipped Lore class reaches both fire and lightning, so no unit, enemy or hire loses a tome. A test holds that. The full suite, `--smoke` and every journaled transcript pass unchanged; no transcript prints a Cinder or Bolt card.
