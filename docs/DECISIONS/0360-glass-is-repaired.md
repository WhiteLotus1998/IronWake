# 0360 — Glass is repaired at half rate, never Refined (slice 1 of #1403)

Date: 2026-10-08. Issue #1403, slice 1. Amends DECISIONS/0154 (glass never repaired) on Lotus's ruling of 2026-10-08 (Table #1368, "obsidian numbers": low durability, repaired after nearly every fight, that is their price) and Chat's round 500 (repair opens, Refine stays refused, lean: half rate).

## Built

- `CampaignRules.RepairPricePerUse` prices glass at `max(1, price / durability / 2)`: 1800 / 8 / 2 = 112 a use, a full repair of 8 for 896 against 1800 for a new one. Other weapons are unchanged.
- `CampaignRecord.Repair` no longer refuses glass; the camp's repair offer reads the same price, so it lists a worn obsidian weapon.
- Refine still refuses glass (`Forge.MaterialFor`), now with a plain placeholder, `<name> is glass; the forge cannot raise its edge`. `Weapon.GlassRefusal` ("You don't mend glass. You buy another.") is gone: the smith now mends it. His real line is Lotus's or the Fable pass's to write.
- 8 uses, iron's weight, Crit 25 and the broken fallback (-5 Mt, -10 Hit) are unchanged.

## Open

- A campaign play reads whether 8 uses lasts about a map for a carrier who leans on it, and whether 896 is a real cost against the middle maps' rewards. Uses are the first lever (10 or 12), price the second (round 500).
- Slice 2 of #1403 is the one-hit Obsidian Armor shell; no armor tome ships yet.
