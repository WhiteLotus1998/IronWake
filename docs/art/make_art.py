#!/usr/bin/env python3
"""Writes the generated art (issue 564) under src/Ironwake.Godot/assets/art/.

Every sprite is drawn here from coded shapes (discs, polygons, strokes, arcs) rasterised at pixel
centres with hard edges, no anti-aliasing and no noise, so each pixel is a palette value, a
LOOK.md mix of one, or clear. The output is the same bytes on every run. Files carry their
docs/ART_SPEC.md names, so a hand-fixed or artist-made file of the same name replaces one with no
code change; generated.txt lists the files this script owns, and ArtGeneratedTests holds them to
the look's rules.

Slice 1: the map tokens (every class, both sides; the captain's is Lotus's own) and the tiles
(every terrain), at 2x (96 x 96), plus docs/art/contact-tokens.png, a contact sheet at 2x and at
the 1x the client draws.

Run from the repository root: python3 docs/art/make_art.py
"""
import json
import math
import os
import struct
import zlib

OUT = os.path.join("src", "Ironwake.Godot", "assets", "art")
SHEET = os.path.join("docs", "art", "contact-tokens.png")
FRAME = 96  # ART_SPEC: 48 px tokens and tiles, delivered at 2x


def hex_rgb(value):
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


# docs/LOOK.md's palette table; LookPalette holds the same values.
TERRAIN = {
    "plain": "7E9470", "road": "B3AE9C", "forest": "4F6E54", "hill": "B89E6C",
    "mountain": "77767C", "water": "41667F", "fort": "9FB0C4", "wall": "23272E",
    "throne": "E4E7EA", "fire": "943C0C",
}
PLAYER = hex_rgb("E8A33D")
PLAYER_DEEP = hex_rgb("9A6420")
ENEMY = hex_rgb("2F3742")
BONE = hex_rgb("E6E0D0")
INK = hex_rgb("15181D")
REACH = hex_rgb("BFD9EA")
PANEL = hex_rgb("1E232A")
SHADOW_ALPHA = 140  # the enemy disc's ink shadow at 0.55, as the client draws it


def mix(base, over, alpha):
    """The colour of `over` laid at `alpha` on `base`, rounded per channel."""
    return tuple(int(round(b + (o - b) * alpha)) for b, o in zip(base, over))


class Canvas:
    """An RGBA raster; shapes are tested at pixel centres."""

    def __init__(self, width, height, fill=(0, 0, 0, 0)):
        self.width, self.height = width, height
        self.px = [list(fill) for _ in range(width * height)]

    def put(self, x, y, rgba):
        if 0 <= x < self.width and 0 <= y < self.height:
            self.px[y * self.width + x] = list(rgba)

    def get(self, x, y):
        return tuple(self.px[y * self.width + x])

    def paint(self, inside, rgba, box=None):
        x0, y0, x1, y1 = box or (0, 0, self.width, self.height)
        for y in range(max(0, int(y0)), min(self.height, int(math.ceil(y1)) + 1)):
            for x in range(max(0, int(x0)), min(self.width, int(math.ceil(x1)) + 1)):
                if inside(x + 0.5, y + 0.5):
                    self.put(x, y, rgba)

    def ellipse(self, cx, cy, rx, ry, rgba):
        self.paint(lambda x, y: ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1, rgba,
                   (cx - rx - 1, cy - ry - 1, cx + rx + 1, cy + ry + 1))

    def polygon(self, points, rgba):
        def inside(x, y):
            hit = False
            j = len(points) - 1
            for i in range(len(points)):
                (xi, yi), (xj, yj) = points[i], points[j]
                if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
                    hit = not hit
                j = i
            return hit
        xs = [p[0] for p in points]
        ys = [p[1] for p in points]
        self.paint(inside, rgba, (min(xs) - 1, min(ys) - 1, max(xs) + 1, max(ys) + 1))

    def stroke(self, points, width, rgba):
        """A polyline of round-capped segments `width` wide."""
        half = width / 2
        for (ax, ay), (bx, by) in zip(points, points[1:]):
            dx, dy = bx - ax, by - ay
            length2 = dx * dx + dy * dy or 1e-9

            def inside(x, y, ax=ax, ay=ay, dx=dx, dy=dy, length2=length2):
                t = max(0.0, min(1.0, ((x - ax) * dx + (y - ay) * dy) / length2))
                return (x - ax - t * dx) ** 2 + (y - ay - t * dy) ** 2 <= half * half
            self.paint(inside, rgba, (min(ax, bx) - half - 1, min(ay, by) - half - 1,
                                      max(ax, bx) + half + 1, max(ay, by) + half + 1))

    def ring(self, cx, cy, radius, width, rgba):
        half = width / 2
        self.paint(lambda x, y: abs(math.hypot(x - cx, y - cy) - radius) <= half, rgba,
                   (cx - radius - half - 1, cy - radius - half - 1, cx + radius + half + 1, cy + radius + half + 1))

    def blit(self, other, ox, oy):
        for y in range(other.height):
            for x in range(other.width):
                rgba = other.get(x, y)
                if rgba[3] == 0:
                    continue
                if rgba[3] < 255:
                    under = self.get(ox + x, oy + y)
                    rgba = mix(under[:3], rgba[:3], rgba[3] / 255) + (255,)
                self.put(ox + x, oy + y, rgba)

    def half(self):
        """A 2x2 box downscale, the 1x the client draws at 1280x720."""
        out = Canvas(self.width // 2, self.height // 2)
        for y in range(out.height):
            for x in range(out.width):
                quad = [self.get(2 * x + i, 2 * y + j) for i in (0, 1) for j in (0, 1)]
                alpha = sum(q[3] for q in quad)
                if alpha == 0:
                    continue
                rgb = tuple(int(round(sum(q[c] * q[3] for q in quad) / alpha)) for c in range(3))
                out.put(x, y, rgb + (int(round(alpha / 4)),))
        return out

    def png(self, path):
        raw = b"".join(b"\x00" + bytes(v for p in self.px[y * self.width:(y + 1) * self.width] for v in p)
                       for y in range(self.height))

        def chunk(kind, data):
            return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
        head = struct.pack(">IIBBBBB", self.width, self.height, 8, 6, 0, 0, 0)
        with open(path, "wb") as f:
            f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", head) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def bezier(a, b, c, d, steps=10):
    out = []
    for i in range(steps):
        t = i / steps
        u = 1 - t
        out.append(tuple(u * u * u * a[k] + 3 * u * u * t * b[k] + 3 * u * t * t * c[k] + t * t * t * d[k] for k in (0, 1)))
    return out


def quad(a, b, c, t):
    ab = tuple(a[k] + (b[k] - a[k]) * t for k in (0, 1))
    bc = tuple(b[k] + (c[k] - b[k]) * t for k in (0, 1))
    return tuple(ab[k] + (bc[k] - ab[k]) * t for k in (0, 1))


def silhouette(canvas, class_id, cx, cy, k, rgba):
    """The class silhouette of LOOK.md (the shapes Main.Look.cs draws), on a disc of radius 16 k."""
    def P(x, y):
        return (cx + x * k, cy + y * k)
    width = 2.6 * k

    def stroke(*points):
        canvas.stroke(list(points), width, rgba)

    def fill(*points):
        canvas.polygon(list(points), rgba)

    if class_id == "cadet":
        stroke(P(0, -11), P(0, 10))
        stroke(P(-6, 4), P(6, 4))
        fill(P(-2, -8), P(0, -13), P(2, -8))
    elif class_id in ("pikeman", "outrider"):
        stroke(P(-9, 10), P(7, -8))
        fill(P(5, -6), P(11, -12), P(9, -4))
        if class_id == "outrider":
            fill(P(-3, 4), P(-10, 0), P(-5, 8))
    elif class_id == "bowman":
        stroke(*[quad(P(-4, -11), P(10, 0), P(-4, 11), i / 12) for i in range(13)])
        stroke(P(-4, -11), P(-4, 11))
        stroke(P(-9, 0), P(8, 0))
    elif class_id == "adept":
        fill(*(bezier(P(0, -12), P(8, -4), P(7, 8), P(0, 9)) + bezier(P(0, 9), P(-7, 8), P(-8, -2), P(-2, -6))
               + bezier(P(-2, -6), P(-2, -2), P(0, 0), P(1, -2)) + bezier(P(1, -2), P(2, -6), P(0, -9), P(0, -12))))
    elif class_id == "reaver":
        stroke(P(-6, 11), P(4, -9))
        fill(*(bezier(P(1, -5), P(6, -12), P(12, -8), P(11, -3)) + bezier(P(11, -3), P(8, -4), P(6, -1), P(5, 2))))
    elif class_id == "chaplain":
        stroke(P(0, -6), P(0, 11))
        canvas.ring(*P(0, -9), 3.5 * k, width, rgba)
    elif class_id == "skyrider":
        stroke(P(-10, 2), P(-4, -6), P(0, 0), P(4, -6), P(10, 2))
        stroke(P(0, 0), P(0, 10))
    elif class_id == "bulwark":
        fill(P(-8, -9), P(8, -9), P(8, 1), P(0, 10), P(-8, 1))
    else:
        raise SystemExit(f"make_art: class '{class_id}' has no silhouette; add one here and in Main.Look.cs")


def token(class_id, side):
    """ART_SPEC's token: a 64 px disc (32 at 1x) centred 6 px above the frame's centre, the bottom 16 px clear."""
    c = Canvas(FRAME, FRAME)
    cx, cy, r = FRAME / 2, FRAME / 2 - 6, 32
    if side == "player":
        c.ellipse(cx, cy + 6, r, r - 1, PLAYER_DEEP + (255,))
        c.ellipse(cx, cy, r, r, PLAYER + (255,))
        ink = INK + (255,)
    else:
        c.ellipse(cx, cy + 6, r, r - 1, INK + (SHADOW_ALPHA,))
        c.ellipse(cx, cy, r, r, ENEMY + (255,))
        ink = BONE + (255,)
    silhouette(c, class_id, cx, cy - 2, r / 16, ink)
    return c


def tile(terrain):
    """A 96 px tile: the terrain's flat colour, its detail a LOOK.md mix over it; the client draws the grid and the shadows."""
    s = FRAME / 48

    def P(x, y):
        return (x * s, y * s)
    if terrain == "fire":
        # Fire is never a fill: an ember hatch on clear ground, laid over whatever burns.
        c = Canvas(FRAME, FRAME)
        ember = hex_rgb(TERRAIN["fire"]) + (255,)
        for i in range(-FRAME, FRAME, 12):
            c.stroke([(i, FRAME), (i + FRAME, 0)], 3, ember)
        return c
    base = hex_rgb(TERRAIN[terrain])
    c = Canvas(FRAME, FRAME, base + (255,))

    def over(colour, alpha):
        return mix(base, colour, alpha) + (255,)
    if terrain == "forest":
        for dx, dy in ((13, 26), (28, 20), (22, 34)):
            c.polygon([P(dx, dy - 12), P(dx + 7, dy), P(dx - 7, dy)], over(INK, 0.35))
    elif terrain == "water":
        for dy in (15, 29):
            c.stroke([P(8 + i * 2.5, dy - 2 * math.sin(i * math.pi / 2)) for i in range(13)], 1.5 * s, over(REACH, 0.35))
    elif terrain == "mountain":
        c.polygon([P(6, 36), P(20, 12), P(28, 24), P(32, 18), P(40, 36)], over(INK, 0.3))
    elif terrain == "hill":
        c.polygon([P(6 + i * 32 / 12, 34 - 24 * math.sin(i * math.pi / 12)) for i in range(13)], over(INK, 0.25))
    elif terrain == "wall":
        coursing = over(hex_rgb(TERRAIN["mountain"]), 0.35)
        c.stroke([P(0, 22), P(48, 22)], s, coursing)
        c.stroke([P(22, 0), P(22, 22)], s, coursing)
        c.stroke([P(11, 22), P(11, 48)], s, coursing)
    elif terrain == "throne":
        c.polygon([P(8, 40), P(8, 10), P(40, 10), P(40, 40), P(33, 40), P(33, 18), P(15, 18), P(15, 40)], over(INK, 0.35))
    elif terrain == "fort":
        battlement = [P(8, 36), P(8, 14), P(14, 14), P(14, 19), P(19, 19), P(19, 14), P(25, 14), P(25, 19),
                      P(30, 19), P(30, 14), P(36, 14), P(36, 19), P(40, 19), P(40, 36)]
        c.polygon([(x, y + 2 * s) for x, y in battlement], over(INK, 0.25))
        c.polygon(battlement, over(INK, 0.3))
    return c


def main():
    with open(os.path.join("content", "classes.json")) as f:
        classes = sorted(c["id"] for c in json.load(f)["classes"])
    with open(os.path.join("content", "terrain.json")) as f:
        terrains = sorted(t["id"] for t in json.load(f)["terrain"])
    os.makedirs(OUT, exist_ok=True)
    art = {}
    for class_id in classes:
        for side in ("player", "enemy"):
            art[f"token_{class_id}_{side}"] = token(class_id, side)
    for terrain in terrains:
        art[f"tile_{terrain}"] = tile(terrain)
    for name, canvas in art.items():
        canvas.png(os.path.join(OUT, name + ".png"))
    with open(os.path.join(OUT, "generated.txt"), "w", newline="\n") as f:
        f.write("# Written by docs/art/make_art.py; a file an artist replaces comes off this list.\n")
        f.write("".join(name + ".png\n" for name in art))

    # The contact sheet: every file at 2x, and under it at 1x, on the panel colour.
    names = list(art)
    columns = len(classes) * 2 if len(classes) * 2 >= len(terrains) else len(terrains)
    cell = FRAME + 8
    rows = [names[:len(classes) * 2], names[len(classes) * 2:]]
    sheet = Canvas(columns * cell + 8, len(rows) * (cell + FRAME // 2 + 8) + 8, PANEL + (255,))
    for r, row in enumerate(rows):
        top = 8 + r * (cell + FRAME // 2 + 8)
        for i, name in enumerate(row):
            sheet.blit(art[name], 8 + i * cell, top)
            sheet.blit(art[name].half(), 8 + i * cell + FRAME // 4, top + FRAME + 4)
    sheet.png(SHEET)
    print(f"make_art: {len(art)} files under {OUT}, sheet {SHEET}")


if __name__ == "__main__":
    main()
