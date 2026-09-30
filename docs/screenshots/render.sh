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
STILLS=(13 17 19)
strip the_tollgate-113-enemy-strip 32 0.3 --map the_tollgate --seed 113 --script "$PWD/$out/the_tollgate-113-enemy.script" --enemy-steps 0
