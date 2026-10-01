# Lays out every clip set under src/Ironwake.Godot/assets/art as one contact sheet a set
# (issue 535, slice 2): a row per clip in ART_SPEC's order, each frame at 96 px on the panel's
# ink, the contact frame boxed in bone, so the for-lotus review reads every clip without Godot.
# usage: python3 docs/look/clip_sheets.py <out-dir>; needs Pillow. Same bytes every run.
import glob
import json
import os
import sys
from PIL import Image, ImageDraw

ART = "src/Ironwake.Godot/assets/art"
CLIPS = ["idle", "advance", "strike", "strike_crit", "miss_recover", "hit_react", "dodge", "fall"]
SIZE, LABEL = 96, 110
INK, BONE, MUTED = (0x1B, 0x1F, 0x26), (0xE6, 0xE0, 0xD0), (0x8A, 0x90, 0x99)
out = sys.argv[1]
os.makedirs(out, exist_ok=True)
for idle in sorted(glob.glob(f"{ART}/*_idle.png")):
    prefix = os.path.basename(idle)[: -len("_idle.png")]
    rows = []
    for clip in CLIPS:
        sheet = Image.open(f"{ART}/{prefix}_{clip}.png").convert("RGBA")
        meta = json.load(open(f"{ART}/{prefix}_{clip}.json"))
        rows.append((clip, sheet, meta))
    width = LABEL + SIZE * max(m["frames"] for _, _, m in rows)
    page = Image.new("RGB", (width, SIZE * len(rows) + 28), INK)
    draw = ImageDraw.Draw(page)
    draw.text((8, 8), prefix, fill=BONE)
    for r, (clip, sheet, meta) in enumerate(rows):
        y = 28 + r * SIZE
        draw.text((8, y + SIZE // 2 - 6), clip, fill=MUTED)
        for f in range(meta["frames"]):
            frame = sheet.crop((f * 256, 0, f * 256 + 256, 256)).resize((SIZE, SIZE), Image.LANCZOS)
            x = LABEL + f * SIZE
            page.paste(frame, (x, y), frame)
            if meta.get("contact") == f:
                draw.rectangle((x, y, x + SIZE - 1, y + SIZE - 1), outline=BONE, width=2)
    page.save(f"{out}/{prefix}.png", optimize=True)
    print(prefix)
