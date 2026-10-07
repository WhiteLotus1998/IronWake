#!/usr/bin/env python3
"""Writes docs/art/for-lotus.html, the labelled sheet of the generated art (issue 564, slice 5).

The contact sheets make_art.py writes carry no labels; this page names every file, so Lotus or an
artist can say what to fix by the name of the file it lives in. It reads generated.txt, the clip
and effect sidecars and the content, and slices the sheets with CSS, so no image is copied: the
cast by name with their tokens, every enemy token and each tell with who carries it, every tile
by its display name, every frame of every clip for one class, the strike of every clip set frame
by frame with the contact frame marked, every effect frame by frame, and an index of every file.
docs/art/for_lotus.js rasterises the page to docs/art/for-lotus.png.

Run from the repository root: python3 docs/art/for_lotus.py
"""
import html
import json
import os

ART = os.path.join("src", "Ironwake.Godot", "assets", "art")
PAGE = os.path.join("docs", "art", "for-lotus.html")
REL = "../../src/Ironwake.Godot/assets/art/"
CLIPS = ["idle", "advance", "strike", "strike_crit", "miss_recover", "dodge", "hit_react", "fall"]
FULL_SET = "cadet_sword"  # the set shown clip by clip, every frame
SCALE = 0.3125  # a 256 px clip frame drawn at 80 px, so twelve fit a row

# Who carries each tell on the shipped maps (DECISIONS/0107; ArtSpec.TokenVariants derives the rows).
CARRIERS = {"hooked": "the toll warden, the Grange Reeve", "double": "the Bandit Leader, the Weir Foreman"}


def load(path):
    with open(path) as f:
        return json.load(f)


def sidecar(name):
    return load(os.path.join(ART, name + ".json"))


def esc(text):
    return html.escape(text, quote=True)


def token(name, caption):
    return (f'<figure><img src="{REL}{name}.png" width="96" height="96" alt="{esc(name)}">'
            f'<figcaption>{esc(caption)}<br><code>{esc(name)}</code></figcaption></figure>')


def frames(name, marked=True):
    car = sidecar(name)
    size = int(car["frame"][0] * SCALE)
    width = int(car["frame"][0] * car["frames"] * SCALE)
    cells = []
    for i in range(car["frames"]):
        hit = " hit" if marked and car["contact"] == i else ""
        cells.append(f'<span class="f{hit}" style="width:{size}px;height:{size}px;'
                     f"background-image:url('{REL}{name}.png');background-size:{width}px {size}px;"
                     f'background-position:-{i * size}px 0"><i>{i}</i></span>')
    return "".join(cells)


def row(label, name, marked=True):
    return (f'<div class="row"><div class="label">{esc(label)}<br><code>{esc(name)}</code></div>'
            f'<div class="strip">{frames(name, marked)}</div></div>')


def main():
    generated = [line.strip()[:-4] for line in open(os.path.join(ART, "generated.txt"))
                 if line.strip() and not line.startswith("#")]
    cast = load(os.path.join("content", "units", "cast.json"))["units"]
    classes = {c["id"]: c["name"] for c in load(os.path.join("content", "classes.json"))["classes"]}
    terrain = {t["id"]: t["name"] for t in load(os.path.join("content", "terrain.json"))["terrain"]}

    tokens = [n for n in generated if n.startswith("token_")]
    tiles = [n for n in generated if n.startswith("tile_")]
    effects = [n for n in generated if n.startswith("fx_")]
    clips = [n for n in generated if n not in tokens and n not in tiles and n not in effects]
    stems = []
    for name in clips:
        clip = next(c for c in sorted(CLIPS, key=len, reverse=True) if name.endswith("_" + c))
        stem = name[:-len(clip) - 1]
        if stem not in stems:
            stems.append(stem)

    out = []
    out.append("<h2>The cast</h2><p>Ours are amber. The captain, Alder Fenn, is yours to model and render; "
               "his slot is empty on purpose, and his files go in under <code>token_captain_player</code> and the clip names.</p><div class=\"grid\">")
    out.append('<figure><div class="empty">yours</div><figcaption>Alder Fenn, the captain<br><code>token_captain_player</code></figcaption></figure>')
    for unit in cast:
        if unit["id"] == "captain":
            continue
        out.append(token(f"token_{unit['class']}_player", f"{unit['name']}, {classes[unit['class']].lower()}"))
    out.append("</div>")

    out.append("<h2>The enemy</h2><p>Theirs are slate and bone, one token per class. The last two are the tells a "
               "shipped enemy carries on his token: the hooked pike and the boss's double bit.</p><div class=\"grid\">")
    for name in tokens:
        # A class id may hold an underscore (frost_caster): split on the side word, not on every underscore.
        class_id, side, tell = name[len("token_"):].partition("_enemy")
        if not side:
            continue
        if tell:
            tell = tell[1:]
            out.append(token(name, f"{classes[class_id]}, {tell}: {CARRIERS.get(tell, 'no carrier named')}"))
        else:
            out.append(token(name, classes[class_id]))
    out.append("</div>")

    out.append("<h2>The ground</h2><p>A cold world. Fire is the only warm thing in it.</p><div class=\"grid\">")
    for name in tiles:
        out.append(token(name, terrain[name[len("tile_"):]]))
    out.append("</div>")

    out.append(f"<h2>One class, every clip</h2><p>The cadet with a sword, every frame of all eight clips. "
               f"A ringed frame is where the blow lands. The figures are drawn in three greys; the game tints them "
               f"amber for us and slate for them when it plays them.</p>")
    for clip in CLIPS:
        out.append(row(clip.replace("_", " "), f"{FULL_SET}_{clip}"))

    out.append("<h2>Every weapon, its strike</h2><p>One row per class and weapon, and per boss weapon. "
               "The other seven clips of each are in the index below.</p>")
    for stem in stems:
        out.append(row(stem.replace("_", " "), f"{stem}_strike"))

    out.append("<h2>Effects</h2><p>Laid over the struck body. Never tinted; ember only where there is fire.</p>")
    for name in effects:
        out.append(row(name[len("fx_"):].replace("_", " "), name, marked=False))

    out.append(f"<h2>Every file</h2><p>{len(generated)} files under <code>src/Ironwake.Godot/assets/art/</code>. "
               "A file dropped in under the same name replaces the generated one with no code change; "
               "take its name off <code>generated.txt</code> when you do.</p><p class=\"index\">")
    out.append(" ".join(f"<code>{esc(n)}</code>" for n in generated))
    out.append("</p>")

    page = f"""<!doctype html>
<html><head><meta charset="utf-8"><title>Ironwake art sheet</title>
<style>
body {{ background: #1E232A; color: #E9ECEF; font: 15px/1.4 sans-serif; margin: 24px; width: 1400px; }}
h1 {{ color: #E8A33D; margin: 0 0 4px; }}
h2 {{ border-bottom: 1px solid #3A424D; padding-bottom: 4px; margin-top: 32px; }}
p {{ color: #B8BEC6; max-width: 1100px; }}
code {{ color: #8C949E; font-size: 12px; overflow-wrap: anywhere; }}
.grid {{ display: flex; flex-wrap: wrap; gap: 12px; }}
img {{ image-rendering: pixelated; }}
figure {{ margin: 0; width: 140px; text-align: center; }}
figcaption {{ font-size: 13px; }}
.empty {{ width: 96px; height: 96px; margin: 0 auto; border: 2px dashed #E8A33D; border-radius: 48px;
  box-sizing: border-box; display: flex; align-items: center; justify-content: center; color: #E8A33D; }}
.row {{ display: flex; align-items: center; margin: 4px 0; }}
.label {{ width: 250px; flex: none; }}
.strip {{ display: flex; gap: 2px; flex-wrap: wrap; }}
.f {{ image-rendering: pixelated; position: relative; background-color: #262C34; background-repeat: no-repeat; }}
.f i {{ position: absolute; left: 3px; top: 1px; font: 10px monospace; color: #6B737D; }}
.hit {{ outline: 2px solid #E8A33D; outline-offset: -2px; }}
.index code {{ display: inline-block; margin-right: 8px; }}
</style></head><body>
<h1>Ironwake: the generated art</h1>
<p>Every sprite below is drawn by <code>docs/art/make_art.py</code> from coded shapes, no hand and no download. It is a
placeholder set: consistent and readable, not final. Mark what needs fixing by its file name.</p>
{"".join(out)}
</body></html>
"""
    with open(PAGE, "w", newline="\n") as f:
        f.write(page)
    print(f"for_lotus: {PAGE}, {len(generated)} files named")


if __name__ == "__main__":
    main()
