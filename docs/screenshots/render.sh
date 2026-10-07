#!/usr/bin/env bash
# Renders the Godot client's review screenshots (issue 349): each shipped map at turn 1 with the
# captain selected and a tile hovered, and mid enemy phase with one event marked; then the
# slice screenshots CI and the earlier issues keep, and the showcase's review frames. Needs GODOT set to a Godot 4.3 .NET binary
# and a display (xvfb-run supplies one). Run from the repository root.
set -euo pipefail
: "${GODOT:?set GODOT to the Godot 4.3 .NET binary}"
out=docs/screenshots
shot() {
  local name=$1; shift
  timeout 180 xvfb-run -a -s "-screen 0 1280x720x24" "$GODOT" --rendering-driver opengl3 --path src/Ironwake.Godot -- "$@" --screenshot "$PWD/$out/$name.png" > /dev/null 2>&1
  test -s "$out/$name.png"
  echo "$name"
}
dotnet build src/Ironwake.Godot/Ironwake.Godot.csproj > /dev/null
# map seed captain hover-tile enemy-prefix-script enemy-steps
while read -r map seed captain hover prefix steps; do
  shot "$map-$seed-turn1" --map "$map" --seed "$seed" --select "$captain" --hover "$hover"
  args=(--map "$map" --seed "$seed" --enemy-steps "$steps")
  if [ "$prefix" != "-" ]; then args+=(--script "$PWD/$out/$prefix"); fi
  shot "$map-$seed-enemy" "${args[@]}"
done <<'SHOTS'
old_mill_road 7 1,8 2,6 - 2
saltmarsh_ford 7 6,8 6,6 - 3
the_tollgate 113 6,11 6,9 the_tollgate-113-enemy.script 5
harrow_weir 7 1,6 3,6 - 4
sallow_grange 61 1,6 3,6 sallow_grange-61-enemy.script 9
brackwater_cut 7 8,4 9,2 - 5
SHOTS
shot brackwater_cut-53-turn3 --map brackwater_cut --seed 53 --script "$PWD/$out/brackwater_cut-53-turn3.script" --select 11,3 --hover 11,3
shot sallow_grange-61-recall --map sallow_grange --seed 61 --script "$PWD/$out/sallow_grange-61-recall.script" --recall
shot the_tollgate-113-camp --campaign --from the_tollgate --seed 113
# Showcase slice 1 (issue 511): seed 113 turn 3 with Teodor on 8,7 hovering 7,5, the forecast
# priced against the Toll Brigand; the enemy frame again with the threat hatch on; then the
# 4x crops (a player token, an enemy token on forest, the forecast), which need Pillow.
shot the_tollgate-113-turn3 --map the_tollgate --seed 113 --script "$PWD/$out/the_tollgate-113-turn3.script" --select 8,7 --hover 7,5
shot the_tollgate-113-threat --map the_tollgate --seed 113 --script "$PWD/$out/the_tollgate-113-enemy.script" --enemy-steps 5 --threat
# Showcase slice 2 (issue 512): the block now sits centred between the top bar and the footer,
# so the Tollgate's board starts at y 62 and Brackwater's at 110. Seed 113 turn 4 with Teodor on
# 7,5 is the wounded forecast (both sides already hurt); the enemy frame again with the log open
# on Tab; Brackwater seed 53 turn 3 at dusk is the rim frame, a rider lit on 8,3 beside the dark.
shot the_tollgate-113-turn4 --map the_tollgate --seed 113 --script "$PWD/$out/the_tollgate-113-turn4.script" --select 7,5 --hover 7,5
shot the_tollgate-113-log --map the_tollgate --seed 113 --script "$PWD/$out/the_tollgate-113-enemy.script" --enemy-steps 5 --log-open
# Crops at the layout of slice 3 (issue 513): the Tollgate's tile is 45 with its board at 49,56
# and the column at 719; Brackwater's tile is 34 with its board at 24,122.
python3 docs/look/crop.py "$out/the_tollgate-113-turn3.png" "$out/crop-player-4x.png" 409 371 45 45
python3 docs/look/crop.py "$out/the_tollgate-113-turn3.png" "$out/crop-enemy-forest-4x.png" 319 281 45 45
python3 docs/look/crop.py "$out/the_tollgate-113-turn3.png" "$out/crop-forecast-4x.png" 707 110 524 238
python3 docs/look/crop.py "$out/the_tollgate-113-turn4.png" "$out/crop-forecast-wounded-4x.png" 707 110 524 238
python3 docs/look/crop.py "$out/brackwater_cut-53-turn3.png" "$out/crop-dusk-edge-4x.png" 262 190 102 68
# Showcase slice 3 (issue 513): the Tollgate's turn-4 enemy phase on seed 113 played by itself at
# normal speed, a frame every 0.3 s on a fixed clock (the archer on Wren, the leader on Teodor, the
# rider's arrival and its kill, the mark he leaves), laid out as contact sheets; then three stills
# of it at full size: a number in the air, the fade, and the mark with the act card.
strip() {
  local name=$1 count=$2 every=$3; shift 3
  local tmp; tmp=$(mktemp -d)
  timeout 300 xvfb-run -a -s "-screen 0 1280x720x24" "$GODOT" --rendering-driver opengl3 --path src/Ironwake.Godot -- "$@" --strip "$tmp/f" "$count" "$every" > /dev/null 2>&1
  python3 docs/look/strip.py "$tmp/f" "$every" "$out/$name"
  for keep in "${STILLS[@]}"; do cp "$tmp/f-$keep.png" "$out/$name-$keep.png"; done
  rm -rf "$tmp"
  echo "$name"
}
# Slice 3b (issue 544): the death holds, so the phase runs longer; the stills are the kill with
# its headline card, the fade, and the mark in its side's ring during the hold, then a 4x crop of it.
STILLS=(12 15 18)
strip the_tollgate-113-enemy-strip 36 0.3 --map the_tollgate --seed 113 --script "$PWD/$out/the_tollgate-113-enemy.script" --enemy-steps 0
python3 docs/look/crop.py "$out/the_tollgate-113-enemy-strip-18.png" "$out/crop-fallen-mark-4x.png" 300 190 160 110
# Showcase slice 4 (issue 514): the same turn-4 enemy phase, now with the lethal number held
# through the death beat and the RECALL chip pulsing on the hold, the act card clearing on the
# flip to turn 5, then a Recall to state 49 (turn 4, before Teodor walked to 7,4) scrubbing the
# board back. The stills are the kill with its number, the pulse on the hold, the first
# player-phase frame after it and the rewind mid-scrub; then the scrub alone at 0.1 s a frame.
STILLS=(12 18 27 31)
strip the_tollgate-113-recall-strip 40 0.3 --map the_tollgate --seed 113 --script "$PWD/$out/the_tollgate-113-enemy.script" --enemy-steps 0 --recall-after 49
# Slice 5 (issue 515) weights it: a step that brings a unit back holds 0.3 s, the rest blur by in
# 40 ms each, and the log dims every line the Recall undid. The stills are the rewind's start with
# the log dimmed, Teodor rising, the hold, and the board after it.
STILLS=(12 14 16 18)
strip the_tollgate-113-scrub-strip 30 0.1 --map the_tollgate --seed 113 --script "$PWD/$out/the_tollgate-113-enemy.script" --enemy-steps 200 --recall-after 49
python3 docs/look/crop.py "$out/the_tollgate-113-recall-strip-18.png" "$out/crop-lethal-pulse-4x.png" 320 0 320 290
# Showcase slice 5 (issue 515): the title and how-to-play screens; the three turn-1 callouts on
# seed 113 (nothing selected, the captain selected, then a tile pointed at; since slice 6, issue 516,
# all three at the foot of the column, the third lighting E); the end card won on the seed-113
# transcript's play and lost on ten empty phases.
shot title --screen title
shot how-to-play --screen howto
# Issue 677 slice 2: the campaign's title with no save and with one (Continue lit), New game,
# Load, Options, and the end-turn confirm with all four of the Tollgate's units unmoved.
saves=$(mktemp -d)
shot campaign-title --campaign --screen title --saves "$saves"
printf 'help\n' > "$saves/camp.script"
dotnet run --project src/Ironwake.Cli -- campaign --script "$saves/camp.script" --saves "$saves" > /dev/null 2>&1
shot campaign-title-continue --campaign --screen title --saves "$saves"
shot campaign-new-game --campaign --screen new-game
shot campaign-load --campaign --screen load --saves "$saves"
shot options --campaign --screen options
shot the_tollgate-113-confirm --map the_tollgate --seed 113 --confirm
rm -rf "$saves"
shot the_tollgate-113-callout-1 --map the_tollgate --seed 113 --callouts
shot the_tollgate-113-callout-2 --map the_tollgate --seed 113 --callouts --select 6,11
shot the_tollgate-113-callout-3 --map the_tollgate --seed 113 --callouts --select 6,11 --hover 4,11
shot the_tollgate-113-won --map the_tollgate --seed 113 --script "$PWD/docs/transcripts/2026-09-27-the_tollgate-113.script"
shot the_tollgate-113-lost --map the_tollgate --seed 113 --script "$PWD/$out/the_tollgate-113-lost.script"
# Issue 533: the enemy reach overlay, one enemy's reach picked out by a click (the archer on 5,5)
# and every seen reach on T, at the Tollgate's turn 3 and at Brackwater's dusk on seed 53; then the
# unit card's EXP bar and a forecast that crosses a level: seed 113's transcript to turn 6, Pell
# (68 EXP) selected on 6,4 pointing at 7,6 against the rider; on Saltmarsh seed 7 the fort group
# asleep, its reach faint and its wake ring dashed.
shot the_tollgate-113-reach-one --map the_tollgate --seed 113 --script "$PWD/$out/the_tollgate-113-turn3.script" --select 5,5
shot the_tollgate-113-reach-all --map the_tollgate --seed 113 --script "$PWD/$out/the_tollgate-113-turn3.script" --threat
shot brackwater_cut-53-reach-all --map brackwater_cut --seed 53 --script "$PWD/$out/brackwater_cut-53-turn3.script" --threat
shot the_tollgate-113-levelup --map the_tollgate --seed 113 --script "$PWD/$out/the_tollgate-113-levelup.script" --select 6,4 --hover 7,6
shot saltmarsh_ford-7-reach-asleep --map saltmarsh_ford --seed 7 --threat
# Issue 601: the captain's gold ring and crown beside Dunstan's plain cadet disc on Brackwater
# seed 53 turn 3, and the threat hatch laid over the dark at dusk (columns 3 to 8).
python3 docs/look/crop.py "$out/brackwater_cut-53-reach-all.png" "$out/crop-captain-4x.png" 390 222 102 40
python3 docs/look/crop.py "$out/brackwater_cut-53-reach-all.png" "$out/crop-dusk-hatch-4x.png" 126 190 136 102
# Issue 535 slice 2: the battle scene's procedural backdrop. The Tollgate's turn-4 phase at the
# key-moments default (the rider's kill on Teodor, on plain), and Brackwater seed 53's turn-3
# phase with every combat a scene (Rook in the ford under fog, the dusk over both halves).
STILLS=(21)
strip the_tollgate-113-scene 40 0.25 --map the_tollgate --seed 113 --script "$PWD/$out/the_tollgate-113-enemy.script" --enemy-steps 0
STILLS=(22)
strip brackwater_cut-53-scene 40 0.25 --map brackwater_cut --seed 53 --script "$PWD/$out/brackwater_cut-53-turn3.script" --enemy-steps 0 --scenes all
# Issue 698: UI scale. The Tollgate's turn 1 with the captain selected, the turn-3 forecast, the
# pause menu and Options at 125 and 150: the canvas narrows to 1024 and 853, the column wraps,
# the top bar and the key strip take a second row, and the board's tile shrinks.
for scale in 125 150; do
  shot the_tollgate-113-turn1-scale$scale --map the_tollgate --seed 113 --select 6,11 --hover 6,9 --ui-scale $scale
  shot the_tollgate-113-turn3-scale$scale --map the_tollgate --seed 113 --script "$PWD/$out/the_tollgate-113-turn3.script" --select 8,7 --hover 7,5 --ui-scale $scale
  shot the_tollgate-113-paused-scale$scale --map the_tollgate --seed 113 --paused --ui-scale $scale
  shot options-scale$scale --campaign --screen options --ui-scale $scale
done
# Issue 1308: Maud's Salve row armed as a target pick with the captain hovered, and the captain's exit row naming who is left behind.
shot the_mill-635-item-pick --map the_mill --seed 635 --script "$PWD/$out/the_mill-635-item.script" --select 5,7 --action Item --hover 5,6
shot brackwater_cut-53-exit-captain --map brackwater_cut --seed 53 --script "$PWD/$out/brackwater_cut-53-exit.script" --select 19,3
