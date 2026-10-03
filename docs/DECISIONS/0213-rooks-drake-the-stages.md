# 0213 — Rook's drake: three stages on the rider (#805 slice 1)

Date: 2026-10-03. Built by the chain Builder. The stages and their triggers are STORY draft 6's (Rook's section) and #805's: Half-grown from her arrival, Grown after her quest 1 "and enough maps flown, deployed and alive at the end", Unbroken after her quest 2, a passed Rook returning Grown. "Enough" was the Builder's to propose.

## What is built

- **On the rider, never a weapon.** `Unit.Drake` (`DrakeState`: stage and main maps flown), null for everyone else. It changes only between maps, so Recall never touches it.
- **The rule is content.** `campaign.json` `drake: { member, grownAfter, grownFlown, unbrokenAfter }`, Rook's `rook_1`, 2, `rook_2`. The loader refuses a member not in the cast and a named quest it lists that is not the member's quest of that part (1 for `grownAfter`, 2 for `unbrokenAfter`). A named quest the file does not list leaves the drake short of that stage.
- **Where it changes.** Joining (`Kitted`): Half-grown, flown 0. A won main map: a rider deployed and standing at the end flies one more map, then the stage is read again. A won side map: the stage is read again, and its win line says `Rook's drake is unbroken now`. `Returning`: Grown at least. Stages never go back.
- **On screen.** The card: `Drake: half-grown; grows once Rook's first quest is won and she has flown 2 maps (n so far).`, then `Drake: grown.` or `Drake: unbroken.` The camp prints `Rook's drake is grown.` after the map that grows it.
- **The save and the protocol** carry `drake` (`stage`, `flown`) on a unit that has one, so #807 can read the stage and whether Rook lived.

## Choices made here (provisional)

- **Two maps flown.** A picked Rook sits out map 6 and `rook_1` opens after map 7, so flying 7 and 8 and winning quest 1 makes her Grown for map 9, the map a passed Rook returns on at Grown. The two sides of the branch meet at the same stage on the same map.
- **Side maps never count as flown.** "Maps flown" reads as the campaign's maps; a quest is already the other half of the trigger.
- **Unbroken needs only quest 2 won.** Quest 2 opens only two maps after quest 1 is won, so the flown count cannot hold it back in any campaign the file allows.

## Not in this slice

Grown's carry (the spike with both costs), Unbroken's rime breath, their forecast and `threat` lines, and the ending line for a fallen Rook. Until they land, Grown and Unbroken change nothing on the board, and the card says only the stage.
