#!/usr/bin/env bash
# Renders the Godot client's review screenshots (issue 349): each shipped map at turn 1 with the
# captain selected and a tile hovered, and mid enemy phase with one event marked; then the
# slice screenshots CI and the earlier issues keep. Needs GODOT set to a Godot 4.3 .NET binary
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
