# Lays a strip's frames out as contact sheets (issue 513): eight frames a sheet, two across at half
# size, each labelled with its time, so a motion slice is read as images like the stills are.
# usage: python3 docs/look/strip.py <frame-prefix> <seconds-apart> <sheet-prefix>; reads
# <frame-prefix>-00.png onward and writes <sheet-prefix>-1.png onward; needs Pillow.
import glob
import sys
from PIL import Image, ImageDraw
prefix, every, out = sys.argv[1], float(sys.argv[2]), sys.argv[3]
frames = sorted(glob.glob(prefix + "-[0-9][0-9].png"))
w, h = 640, 360
for sheet in range(0, len(frames), 8):
    page = Image.new("RGB", (2 * w, 4 * h), (0, 0, 0))
    for i, name in enumerate(frames[sheet:sheet + 8]):
        n = sheet + i
        frame = Image.open(name).convert("RGB").resize((w, h), Image.LANCZOS)
        ImageDraw.Draw(frame).rectangle((w - 70, h - 22, w, h), fill=(0, 0, 0))
        ImageDraw.Draw(frame).text((w - 64, h - 17), f"t {n * every:.1f}s", fill=(255, 255, 255))
        page.paste(frame, ((i % 2) * w, (i // 2) * h))
    page.save(f"{out}-{sheet // 8 + 1}.png", optimize=True)
