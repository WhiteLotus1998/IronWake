# Cuts a region out of a rendered frame and scales it 4x with no smoothing, so a pixel stays a pixel (issue 511).
# usage: python3 docs/look/crop.py <in.png> <out.png> <x> <y> <width> <height>; needs Pillow.
import sys
from PIL import Image
src, out, x, y, w, h = sys.argv[1], sys.argv[2], *map(int, sys.argv[3:7])
Image.open(src).crop((x, y, x + w, y + h)).resize((w * 4, h * 4), Image.NEAREST).save(out)
