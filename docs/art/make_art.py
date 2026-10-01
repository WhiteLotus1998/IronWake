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

Slice 2: every class and boss clip row of docs/ART_SPEC.md's names block, one-row sheets of
256 x 256 frames with a <name>.json sidecar, drawn in three neutral greys the client tints with
the side's colour (0105), plus docs/art/contact-clips.png.

Slice 3: every effect row (fx_*), one-row sheets of 256 x 256 frames with a sidecar whose pivot
is the point the scene lays the effect on (128, 128), drawn in LOOK.md's own values and never
tinted, ember only where fire is, plus docs/art/contact-effects.png.

Slice 4: the variant tokens (token_<class>_enemy_<tell>, ArtSpec.TokenVariants), a weapon's tell
a shipped enemy carries on its token: the hooked pike, the boss's double bit. They sit in a third
row of the tokens contact sheet.

Run from the repository root: python3 docs/art/make_art.py
"""
import json
import math
import os
import struct
import zlib

OUT = os.path.join("src", "Ironwake.Godot", "assets", "art")
SHEET = os.path.join("docs", "art", "contact-tokens.png")
CLIP_SHEET = os.path.join("docs", "art", "contact-clips.png")
FX_SHEET = os.path.join("docs", "art", "contact-effects.png")
FRAME = 96  # ART_SPEC: 48 px tokens and tiles, delivered at 2x


def hex_rgb(value):
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


# docs/LOOK.md's palette table; LookPalette holds the same values.
TERRAIN = {
    "plain": "7E9470", "road": "B3AE9C", "forest": "4F6E54", "hill": "C8C8A0",
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
    """An RGBA raster; shapes are tested at pixel centres, and only columns inside `clip` are painted."""

    def __init__(self, width, height, fill=(0, 0, 0, 0)):
        self.width, self.height = width, height
        self.px = bytearray(bytes(fill) * (width * height))
        self.clip = (0, width)
        self.ground = height

    def put(self, x, y, rgba):
        if self.clip[0] <= x < self.clip[1] and 0 <= y < self.ground:
            i = 4 * (y * self.width + x)
            self.px[i:i + 4] = bytes(rgba)

    def get(self, x, y):
        i = 4 * (y * self.width + x)
        return tuple(self.px[i:i + 4])

    def paint(self, inside, rgba, box=None):
        x0, y0, x1, y1 = box or (0, 0, self.width, self.height)
        for y in range(max(0, int(y0)), min(self.ground, int(math.ceil(y1)) + 1)):
            for x in range(max(self.clip[0], int(x0)), min(self.clip[1], int(math.ceil(x1)) + 1)):
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
        stride = 4 * self.width
        raw = b"".join(b"\x00" + bytes(self.px[y * stride:(y + 1) * stride]) for y in range(self.height))

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


def silhouette(canvas, class_id, cx, cy, k, rgba, tell=None):
    """The class silhouette of LOOK.md (the shapes Main.Look.cs draws), on a disc of radius 16 k.

    A tell (ArtSpec.TokenTell) adds the variant's mark: "hooked" the Toll Spear's crossbar on the
    pike, "double" the second bit on a boss reaver's axe.
    """
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
    elif class_id == "outrider":
        # Cavalry reads apart from the pike at 32 px (round 170): the lance couched flatter, its
        # pennant under the head, and a horseshoe at the foot.
        stroke(P(-11, 3), P(7, -5))
        fill(P(6.2, -6.8), P(12.5, -7), P(7.8, -3.2))
        fill(P(-1, -0.4), P(-7, 2.2), P(-5, 5))
        stroke(*[P(4.5 * math.cos(math.pi * i / 8), 7.5 + 4.5 * math.sin(math.pi * i / 8)) for i in range(9)])
    elif class_id == "pikeman":
        stroke(P(-9, 10), P(7, -8))
        fill(P(5, -6), P(11, -12), P(9, -4))
        if tell == "hooked":
            stroke(P(1, -2), P(-3, -6))
            stroke(P(1, -2), P(5, 2))
    elif class_id == "sergeant":
        # The pike crossed with a short sword, the two weapons the class carries (issue 691).
        stroke(P(-9, 10), P(7, -8))
        fill(P(5, -6), P(11, -12), P(9, -4))
        stroke(P(8, 9), P(-5, -4))
        stroke(P(7.5, 3.5), P(2.5, 8.5))
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
        if tell == "double":
            fill(*(bezier(P(1, -5), P(-5, -10), P(-9, -4), P(-8, 1)) + bezier(P(-8, 1), P(-5, -1), P(-3, 0), P(-2, 1))))
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
    if tell is not None and (class_id, tell) not in (("pikeman", "hooked"), ("reaver", "double")):
        raise SystemExit(f"make_art: no '{tell}' tell for class '{class_id}'; add one here and in Main.Look.cs")


def token(class_id, side, tell=None):
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
    silhouette(c, class_id, cx, cy - 2, r / 16, ink, tell)
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


# Slice 2: the battle clips. A sheet is per class, not per side, so the figures are drawn in three
# neutral greys (a base, its shade, and the dark of steel, hair and boots) and the client tints a
# sheet with the side's colour when it plays it, as it mirrors one for facing.
CLIP = 256  # ART_SPEC: the battle-scene frame, pivot at the feet's centre
PIVOT = (128, 232)
LIGHT = (0xD6, 0xD6, 0xD6, 255)
SHADE = (0x9A, 0x9A, 0x9A, 255)
DARK = (0x4E, 0x4E, 0x4E, 255)
GREYS = (LIGHT, SHADE, DARK)

# ArtSpec.Clips: frames and the contact frame of each clip, in the spec's order.
CLIPS = {"idle": (8, None), "advance": (6, None), "strike": (8, 5), "strike_crit": (12, 8),
         "miss_recover": (8, 5), "dodge": (6, None), "hit_react": (4, 1), "fall": (10, None)}


def rotate(p, about, angle):
    """`p` turned by `angle` about `about`; a positive angle turns clockwise on the screen."""
    c, s = math.cos(angle), math.sin(angle)
    x, y = p[0] - about[0], p[1] - about[1]
    return (about[0] + x * c - y * s, about[1] + x * s + y * c)


def along(p, angle, length):
    return (p[0] + math.cos(angle) * length, p[1] + math.sin(angle) * length)


def pose(clip, i):
    """The pose of frame `i`: body offset, lean, crouch, swing (-1 wound up, 0 on guard, 1 the blow), stride, fall."""
    n, contact = CLIPS[clip]
    t = i / n
    p = {"dx": 0.0, "dy": 0.0, "lean": 0.0, "crouch": 0.0, "swing": 0.0, "stride": 0.0, "fall": 0.0}
    if clip == "idle":
        p["crouch"] = 1.5 - 1.5 * math.cos(2 * math.pi * t)
        p["swing"] = 0.06 * math.sin(2 * math.pi * t)
    elif clip == "advance":
        p["dx"] = -40 * (1 - (i + 1) / n)
        p["stride"] = math.sin(2 * math.pi * (i + 1) / n * 1.5)
        p["dy"] = -2 * abs(math.sin(2 * math.pi * (i + 1) / n * 1.5))
    elif clip in ("strike", "miss_recover"):
        p["swing"] = [0, -0.4, -0.8, -1, -1, 1, 0.6, 0.2][i]
        p["dx"] = [0, 0, -2, -4, -4, 14, 8, 2][i]
        p["lean"] = 0.12 * p["swing"]
        if clip == "miss_recover":
            p["swing"] = [0, -0.4, -0.8, -1, -1, 1.3, 0.8, 0.3][i]
            p["dx"] = [0, 0, -2, -4, -4, 22, 16, 6][i]
            p["lean"] = [0, -0.05, -0.1, -0.12, -0.12, 0.38, 0.22, 0.06][i]
            p["stride"] = [0, 0, 0, 0, 0, 1, 0.7, 0.2][i]
    elif clip == "strike_crit":
        p["swing"] = [0, -0.3, -0.6, -0.9, -1.1, -1.2, -1.25, -1.25, 1.15, 0.9, 0.5, 0.2][i]
        p["dx"] = [0, -2, -4, -6, -8, -8, -8, -8, 26, 18, 10, 4][i]
        p["crouch"] = [0, 2, 4, 6, 8, 9, 10, 10, 4, 3, 1, 0][i]
        p["lean"] = 0.14 * p["swing"]
        p["stride"] = [0, 0, 0, 0, 0, 0, 0, 0, 1, 0.8, 0.5, 0.2][i]
    elif clip == "dodge":
        p["dx"] = [0, -10, -22, -26, -16, -4][i]
        p["lean"] = [0, -0.12, -0.25, -0.25, -0.12, 0][i]
        p["crouch"] = [0, 4, 7, 7, 4, 1][i]
    elif clip == "hit_react":
        p["dx"] = [0, -9, -6, -2][i]
        p["lean"] = [0, -0.22, -0.12, -0.04][i]
        p["swing"] = [0, -0.2, -0.1, 0][i]
    elif clip == "fall":
        k = min(1.0, i / 7)
        p["fall"] = k * k * (3 - 2 * k)
        p["crouch"] = 16 * min(1.0, i / 3)
        p["swing"] = 0.4 * min(1.0, i / 3)
    return p


LANCE_BACK = 56  # how far the lance sits back along its shaft from where it first drew


class Figure:
    """One combatant facing right, drawn in local coordinates (the feet's centre at 0,0) and placed on a frame."""

    def __init__(self, canvas, class_id, kind, weapon_id, boss):
        self.c = canvas
        self.class_id, self.kind, self.weapon_id, self.boss = class_id, kind, weapon_id, boss
        self.mount = {"outrider": "horse", "skyrider": "wings"}.get(class_id)
        self.bulk = 1.18 if boss or class_id == "bulwark" else 1.0

    def draw(self, p, origin):
        c = self.c
        ox, oy = origin[0] + p["dx"], origin[1] + p["dy"]
        seat = 0.0
        if self.mount:
            seat = self.draw_mount(p, ox, oy)
        hip_y = -62 + p["crouch"] if not self.mount else seat
        hip = (0.0, hip_y)
        fall = p["fall"]

        def place(q):
            """Local to frame: a mounted rider slumps forward on the neck, one on foot goes over backwards."""
            if self.mount:
                q = rotate(q, hip, 1.0 * fall)
                return (ox + q[0], oy + q[1] + 30 * fall)
            q = rotate(q, hip, -math.pi / 2 * fall)
            return (ox + q[0], oy + q[1] + fall * (-hip_y - 13))

        def upper(q):
            return place(rotate((q[0], q[1] + hip_y), hip, p["lean"]))

        w = self.bulk
        # Legs: the far one in the shade, the near one in the base, boots dark.
        if not self.mount:
            stride = p["stride"] * 14
            for foot_x, colour in ((-10 - stride, SHADE), (10 + stride, LIGHT)):
                foot = (foot_x, -4.0)
                knee = ((hip[0] + foot_x) / 2 + 6 + p["crouch"] * 0.8, (hip_y - 4) / 2)
                c.stroke([place(hip), place(knee), place(foot)], 11 * w, colour)
                c.stroke([place((foot_x - 2, -4)), place((foot_x + 8, -4))], 8, DARK)
        else:
            c.stroke([place(hip), place((10, hip_y + 18)), place((6, hip_y + 34))], 10, LIGHT)
        shoulder = (3.0, -46.0)
        swing = p["swing"]
        hand, angle, grip = self.hand(shoulder, swing)
        # The far arm and anything carried behind the body.
        if self.class_id == "bulwark" or (self.boss and self.kind == "lance"):
            shield = [upper((-2, -44)), upper((18, -44)), upper((18, -14)), upper((8, -2)), upper((-2, -14))]
            c.polygon(shield, DARK)
        c.stroke([upper(shoulder), upper(((shoulder[0] + grip[0]) / 2 - 6, (shoulder[1] + grip[1]) / 2 + 8)), upper(grip)], 8, SHADE)
        # Torso and head.
        c.polygon([upper((-11 * w, 0)), upper((11 * w, 0)), upper((14 * w, -50)), upper((-13 * w, -50))], LIGHT)
        c.polygon([upper((-11 * w, 0)), upper((11 * w, 0)), upper((12 * w, -8)), upper((-12 * w, -8))], DARK)
        head = upper((2, -66))
        c.ellipse(head[0], head[1], 12, 12, LIGHT)
        top = upper((2, -71))
        c.paint(lambda x, y: (x - head[0]) ** 2 + (y - head[1]) ** 2 <= 144 and (x - head[0]) * (top[0] - head[0]) + (y - head[1]) * (top[1] - head[1]) > 0,
                DARK, (head[0] - 13, head[1] - 13, head[0] + 13, head[1] + 13))
        # The weapon, then the near arm over its grip.
        self.weapon(upper, hand, angle, grip, swing)
        elbow = ((shoulder[0] + hand[0]) / 2 + 2, (shoulder[1] + hand[1]) / 2 + 9)
        c.stroke([upper(shoulder), upper(elbow), upper(hand)], 9 * w, LIGHT)
        if self.kind == "gauntlet":
            f = [upper((hand[0] + dx, hand[1] + dy)) for dx, dy in ((-7, -8), (9, -8), (9, 8), (-7, 8))]
            c.polygon(f, DARK)

    def hand(self, shoulder, swing):
        """Where the near hand is, which way the weapon points from it, and where the far hand grips."""
        kind = self.kind
        if kind in ("sword", "axe"):
            # Overhead: wound up behind the head, the blow ends forward and low.
            arm = -0.7 + (1.05 * swing if swing > 0 else 0.85 * swing)
            hand = along(shoulder, arm, 34)
            angle = arm - 0.25
            return hand, angle, along(hand, angle + math.pi, 10)
        if kind == "lance":
            reach = 30 + 16 * swing
            hand = along(shoulder, 0.55 - 0.1 * swing, reach)
            angle = -0.12 - 0.05 * swing
            return hand, angle, along(hand, angle + math.pi, 26)
        if kind == "bow":
            hand = along(shoulder, 0.05, 40)
            return hand, 0.0, hand
        if kind == "reason":
            hand = along(shoulder, 0.1 - 0.5 * max(0.0, -swing), 30 + 10 * max(0.0, swing))
            return hand, 0.0, along(shoulder, 1.2, 22)
        if kind == "faith":
            hand = along(shoulder, 0.5 - 0.4 * swing, 30)
            return hand, -math.pi / 2 + 0.6 * max(0.0, swing), along(shoulder, 1.3, 22)
        # gauntlet: drawn back, then the punch.
        hand = along(shoulder, 0.45 - 0.25 * swing, 28 + 14 * swing)
        return hand, 0.0, along(shoulder, 0.9 - 0.4 * max(0.0, -swing), 20)

    def weapon(self, upper, hand, angle, grip, swing):
        c = self.c
        kind, wid = self.kind, self.weapon_id

        def at(length, side=0.0):
            q = along(hand, angle, length)
            return upper(along(q, angle + math.pi / 2, side))
        if kind == "sword":
            c.stroke([at(-8), at(52)], 6, DARK)
            c.polygon([at(50, -3), at(60), at(50, 3)], DARK)
            c.stroke([at(0, -9), at(0, 9)], 5, DARK)
        elif kind == "axe":
            c.stroke([at(-14), at(56)], 5, DARK)
            bits = (1, -1) if wid == "toll_axe" else (1,)
            for side in bits:
                c.polygon([at(40, 2 * side), at(36, 14 * side), at(46, 20 * side), at(58, 16 * side), at(54, 2 * side)], DARK)
        elif kind == "lance":
            # Held back along the shaft (round 170): the tip at full reach, the crit's lunge
            # included, stays inside the 256 px frame with a margin instead of being cropped.
            b = LANCE_BACK
            c.stroke([at(-40 - b), at(78 - b)], 5, DARK)
            c.polygon([at(74 - b, -4), at(86 - b, -5), at(98 - b), at(86 - b, 5), at(74 - b, 4)], DARK)
            if wid == "toll_spear":
                c.stroke([at(70 - b, -12), at(70 - b, 12)], 4, DARK)
                c.stroke([at(70 - b, -12), at(76 - b, -16)], 4, DARK)
        elif kind == "bow":
            draw = max(0.0, -swing) if swing <= 0 else 0.0
            tip_top, tip_bottom = upper((hand[0] - 8, hand[1] - 42)), upper((hand[0] - 8, hand[1] + 42))
            curve = [upper(quad((hand[0] - 8, hand[1] - 42), (hand[0] + 22, hand[1]), (hand[0] - 8, hand[1] + 42), k / 12)) for k in range(13)]
            c.stroke(curve, 5, DARK)
            nock = upper((hand[0] - 8 - 30 * draw, hand[1]))
            c.stroke([tip_top, nock, tip_bottom], 2, SHADE)
            if swing <= 0:
                c.stroke([nock, upper((hand[0] + 14, hand[1]))], 3, DARK)
            elif swing >= 0.9:
                # Loosed: the arrow already on its way out of the frame.
                c.stroke([upper((hand[0] + 40, hand[1] - 2)), upper((hand[0] + 76, hand[1] - 2))], 3, DARK)
        elif kind == "reason":
            size = 0.7 + 0.5 * max(0.0, -swing) + 0.3 * max(0.0, swing)
            cx, cy = hand[0] + 14 + 44 * max(0.0, swing), hand[1] - 6

            def F(x, y):
                return upper((cx + x * size, cy + y * size))
            c.polygon(bezier(F(0, -12), F(8, -4), F(7, 8), F(0, 9)) + bezier(F(0, 9), F(-7, 8), F(-8, -2), F(-2, -6))
                      + bezier(F(-2, -6), F(-2, -2), F(0, 0), F(1, -2)) + bezier(F(1, -2), F(2, -6), F(0, -9), F(0, -12)), DARK)
        elif kind == "faith":
            c.stroke([at(-52), at(54)], 5, DARK)
            ring = at(62)
            c.ring(ring[0], ring[1], 8, 4, DARK)

    def draw_mount(self, p, ox, oy):
        """The outrider's horse or the skyrider's winged mount; returns the rider's seat height."""
        c = self.c
        fall = p["fall"]
        stride = p["stride"]
        sink = 36 * fall
        if self.mount == "horse":
            body_y = -64 + sink
            for x, phase, colour in ((-30, 1, SHADE), (26, -1, SHADE), (-24, -1, LIGHT), (32, 1, LIGHT)):
                swing = 10 * stride * phase
                knee_y = body_y + 26 + (sink * 0.4)
                foot_y = -4 if fall < 0.5 else -4 - 8 * (fall - 0.5)
                c.stroke([(ox + x, oy + body_y + 6), (ox + x + swing * 0.5 - 16 * fall, oy + knee_y), (ox + x + swing - 22 * fall, oy + foot_y)], 8, colour)
            c.ellipse(ox, oy + body_y, 46, 18, SHADE)
            neck = [(ox + 30, oy + body_y - 8), (ox + 46, oy + body_y - 40 + sink * 0.3), (ox + 58, oy + body_y - 38 + sink * 0.3),
                    (ox + 48, oy + body_y + 2)]
            c.polygon(neck, SHADE)
            c.polygon([(ox + 46, oy + body_y - 42 + sink * 0.3), (ox + 70, oy + body_y - 30 + sink * 0.3), (ox + 66, oy + body_y - 24 + sink * 0.3),
                       (ox + 50, oy + body_y - 28 + sink * 0.3)], SHADE)
            c.stroke([(ox - 44, oy + body_y - 6), (ox - 56, oy + body_y + 18)], 6, DARK)
            return body_y - 16
        # The winged mount: a heavy-bodied bird of the high moors, wings up, legs tucked.
        body_y = -62 + p["crouch"] + sink
        flap = 0.35 * stride + 0.1 * p["swing"] if fall == 0 else -0.9 * fall
        root = (ox - 6, oy + body_y - 10)
        wing = [root, along(root, -2.2 + flap, 70), along(root, -1.8 + flap, 86), along(root, -1.3 + flap, 60), (ox + 18, oy + body_y - 10)]
        c.polygon(wing, DARK)
        for x in (-6, 10):
            c.stroke([(ox + x, oy + body_y + 14), (ox + x + 6 - 20 * fall, oy - 20 - 6 * sink / 36), (ox + x + 2 - 24 * fall, oy - 3)], 6, SHADE)
        c.ellipse(ox, oy + body_y, 40, 18, SHADE)
        c.polygon([(ox - 38, oy + body_y - 4), (ox - 64, oy + body_y - 10), (ox - 60, oy + body_y + 6)], SHADE)
        c.ellipse(ox + 40, oy + body_y - 18, 12, 11, SHADE)
        c.polygon([(ox + 50, oy + body_y - 22), (ox + 66, oy + body_y - 14), (ox + 50, oy + body_y - 12)], DARK)
        return body_y - 14


def spec_names():
    """The rows of docs/ART_SPEC.md's fenced names block, the contract the generator writes to."""
    with open(os.path.join("docs", "ART_SPEC.md")) as f:
        lines = f.read().split("\n")
    start = lines.index("```names")
    out = []
    for line in lines[start + 1:]:
        if line == "```":
            return out
        out.append(line)
    raise SystemExit("make_art: ART_SPEC.md's names block is not closed")


def clip_sheet(class_id, kind, weapon_id, boss, clip):
    frames, contact = CLIPS[clip]
    sheet = Canvas(CLIP * frames, CLIP)
    sheet.ground = PIVOT[1]  # nothing below the feet: a fallen weapon lies on the ground, not through it
    figure = Figure(sheet, class_id, kind, weapon_id, boss)
    for i in range(frames):
        sheet.clip = (i * CLIP, (i + 1) * CLIP)
        figure.draw(pose(clip, i), (i * CLIP + PIVOT[0], PIVOT[1]))
    sheet.clip = (0, sheet.width)
    sidecar = {"frame": [CLIP, CLIP], "frames": frames, "pivot": list(PIVOT), "contact": contact}
    return sheet, sidecar


def clips(classes):
    """Every class clip and boss clip row of the spec, as (name, sheet, sidecar)."""
    with open(os.path.join("content", "weapons.json")) as f:
        weapons = {w["id"]: w["type"] for w in json.load(f)["weapons"]}
    with open(os.path.join("content", "units", "enemies.json")) as f:
        units = {u["id"]: u["class"] for u in json.load(f)["units"]}
    out = []
    for name in spec_names():
        clip = next((c for c in sorted(CLIPS, key=len, reverse=True) if name.endswith("_" + c)), None)
        if clip is None or name.startswith(("token_", "tile_", "fx_")):
            continue
        stem = name[:-len(clip) - 1]
        if stem.startswith("boss_"):
            unit, weapon_id = next((u, stem[len("boss_" + u) + 1:]) for u in units if stem.startswith("boss_" + u + "_"))
            class_id, kind, boss = units[unit], weapons[weapon_id], True
        else:
            class_id, kind = next((c, stem[len(c) + 1:]) for c in classes if stem.startswith(c + "_"))
            weapon_id, boss = None, False
        sheet, sidecar = clip_sheet(class_id, kind, weapon_id, boss, clip)
        out.append((name, sheet, sidecar))
    return out


# Slice 3: the effects. One sheet per effect, not per side, drawn in LOOK.md's own values, opaque
# and hard-edged: white (mark.struck), text (ui.text), frost (mark.reach), salt grey (terrain.road)
# and ember (terrain.fire), the last only where fire is.
FX_PIVOT = (128, 128)
WHITE = (0xFF, 0xFF, 0xFF, 255)
TEXT = hex_rgb("E9ECEF") + (255,)
FROST = REACH + (255,)
SALT = hex_rgb(TERRAIN["road"]) + (255,)
EMBER = hex_rgb(TERRAIN["fire"]) + (255,)

# ArtSpec.FixedEffects and ArtSpec.SpellFrames: the frames of each effect.
FX_FRAMES = {"hit_spark": 5, "slash_arc": 5, "crit_flash": 6, "heal": 10, "dust": 6, "embers": 12}
SPELL_FRAMES = 8


def diamond(c, x, y, r, rgba):
    c.polygon([(x, y - r), (x + r, y), (x, y + r), (x - r, y)], rgba)


def star(c, x, y, long, short, turn, rgba):
    """A four-point star: long points on the axes turned by `turn`, short ones between."""
    points = []
    for k in range(8):
        a = turn + k * math.pi / 4
        r = long if k % 2 == 0 else short
        points.append((x + math.cos(a) * r, y + math.sin(a) * r))
    c.polygon(points, rgba)


def crescent(c, x, y, radius, a0, a1, thick, rgba):
    """A crescent along the circle of `radius` from angle a0 to a1, `thick` at its widest, pointed at both ends."""
    steps = 16
    outer = [along((x, y), a0 + (a1 - a0) * k / steps, radius) for k in range(steps + 1)]
    inner = [along((x, y), a0 + (a1 - a0) * k / steps, radius - thick * math.sin(math.pi * k / steps)) for k in range(steps + 1)]
    c.polygon(outer + inner[::-1], rgba)


def fx_frame(c, name, i, n, cx, cy):
    """Frame `i` of `n` of effect `name`, laid on the pivot (cx, cy)."""
    t = i / (n - 1)
    rays = [k * math.pi / 4 + (0.2 if k % 2 else 0.0) for k in range(8)]
    if name == "hit_spark":
        if i == 0:
            diamond(c, cx, cy, 14, WHITE)
        elif i < n - 1:
            for k, a in enumerate(rays):
                reach = (1.0 if k % 2 == 0 else 0.65) * (30 + 22 * i)
                c.stroke([along((cx, cy), a, 10 + 14 * i), along((cx, cy), a, reach)], 9 - 2 * i, WHITE)
        else:
            for a in rays[::2]:
                x, y = along((cx, cy), a, 96)
                diamond(c, x, y, 4, WHITE)
    elif name == "slash_arc":
        head = -2.3 + 2.9 * (i + 1) / n
        tail = max(-2.3, head - 1.9)
        crescent(c, cx - 30, cy + 10, 96, tail, head, 26 * (1 - 0.6 * t), TEXT)
    elif name == "crit_flash":
        if i < 4:
            size = [120, 100, 70, 36][i]
            star(c, cx, cy, size, size * 0.18, math.pi / 4 * 0.0, WHITE)
        if i > 0:
            c.ring(cx, cy, 28 + 17 * i, max(3, 14 - 2.4 * i), TEXT)
    elif name == "heal":
        for k in range(7):
            phase = (k * 0.37) % 1.0
            rise = (t + phase) % 1.0
            x = cx - 48 + k * 16 + 6 * math.sin(2 * math.pi * (rise + k * 0.2))
            y = cy + 70 - 150 * rise
            size = 10 * math.sin(math.pi * rise)
            if size >= 2:
                diamond(c, x, y, size, FROST if k % 3 else WHITE)
    elif name == "dust":
        for side in (-1, 1):
            for k in range(3):
                spread = 14 + 11 * i + 10 * k
                r = (16 - 3 * k) * math.sin(math.pi * (i + 1) / (n + 1)) + 4
                c.ellipse(cx + side * spread, cy - 4 - 5 * i - 6 * k, r * 1.3, r, SALT)
    elif name == "embers":
        for k in range(9):
            rise = ((k * 0.29) + i / n) % 1.0
            x = cx - 60 + k * 15 + 8 * math.sin(2 * math.pi * (rise + k * 0.13))
            y = cy + 90 - 180 * rise
            size = 6 * (1 - rise) + 2
            diamond(c, x, y, size, EMBER)
    elif name == "spell_cinder":
        if i < 4:
            fx, fy, size = cx - 96 + 32 * i, cy - 6, 1.6 + 0.3 * i

            def F(x, y):
                return (fx + y * size, fy - x * size)  # the adept's flame laid on its side, point trailing
            c.polygon(bezier(F(0, -12), F(8, -4), F(7, 8), F(0, 9)) + bezier(F(0, 9), F(-7, 8), F(-8, -2), F(-2, -6))
                      + bezier(F(-2, -6), F(-2, -2), F(0, 0), F(1, -2)) + bezier(F(1, -2), F(2, -6), F(0, -9), F(0, -12)), EMBER)
        else:
            k = i - 4
            for j in range(6):
                a = -math.pi / 2 + (j - 2.5) * 0.45
                base = along((cx, cy + 30), a, 10 + 8 * k)
                tip = along((cx, cy + 30), a, 50 + 22 * k - 10 * k * k / 3)
                side = along(base, a + math.pi / 2, 10 - 2 * k)
                other = along(base, a - math.pi / 2, 10 - 2 * k)
                c.polygon([side, tip, other], EMBER)
            if k >= 2:
                for j in (-1, 1):
                    c.ellipse(cx + j * 24, cy - 40 - 12 * k, 10 + 3 * k, 8 + 2 * k, SALT)
    elif name == "spell_gust":
        for j in range(3):
            y = cy - 40 + 40 * j
            head = -110 + 38 * i + 18 * j
            points = [(cx + x, y + 10 * math.sin((x - head) / 22)) for x in range(int(head - 90), int(head) + 1, 6)
                      if -128 <= x <= 127]
            if len(points) > 1:
                c.stroke(points, 6 if j != 1 else 9, TEXT if j == 1 else FROST)
    elif name == "spell_bolt":
        if i < 2:
            for j in range(4):
                x, y = along((cx, 26), j * math.pi / 2 + i * 0.4, 30 - 12 * i)
                diamond(c, x, y, 5, FROST)
        elif i < 5:
            path = [(cx - 4, 0), (cx + 18, 40), (cx - 14, 64), (cx + 12, 96), (cx, cy)]
            c.stroke(path, 16 - 3 * (i - 2), FROST)
            c.stroke(path, 5, WHITE)
            if i == 4:
                for a in rays[::2]:
                    c.stroke([(cx, cy), along((cx, cy), a + 0.4, 34)], 5, FROST)
        else:
            k = i - 5
            for a in rays[1::2]:
                c.stroke([along((cx, cy), a, 20 + 18 * k), along((cx, cy), a, 34 + 20 * k)], 6 - 2 * k, FROST)
    elif name == "spell_radiance":
        spread = [0.2, 0.5, 0.85, 1.0, 1.0, 0.8, 0.5, 0.2][i]
        source = (cx, -30)
        for j in range(-2, 3):
            a = math.pi / 2 + j * 0.16 * spread
            half = 0.03 + 0.03 * spread
            c.polygon([source, along(source, a - half, 200), along(source, a + half, 200)], TEXT)
        if 2 <= i <= 5:
            c.stroke([(cx, 0), (cx, cy + 40)], 4, WHITE)
    else:
        raise SystemExit(f"make_art: effect '{name}' has no drawing; add one here")


def effect_sheet(name):
    frames = SPELL_FRAMES if name.startswith("spell_") else FX_FRAMES[name]
    sheet = Canvas(CLIP * frames, CLIP)
    for i in range(frames):
        sheet.clip = (i * CLIP, (i + 1) * CLIP)
        fx_frame(sheet, name, i, frames, i * CLIP + FX_PIVOT[0], FX_PIVOT[1])
    sheet.clip = (0, sheet.width)
    sidecar = {"frame": [CLIP, CLIP], "frames": frames, "pivot": list(FX_PIVOT), "contact": None}
    return sheet, sidecar


def effects():
    """Every effect row of the spec, as (name, sheet, sidecar)."""
    return [(name,) + effect_sheet(name[len("fx_"):]) for name in spec_names() if name.startswith("fx_")]


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
    variants = []
    for name in spec_names():
        parts = name.split("_")
        if name.startswith("token_") and len(parts) == 4 and parts[2] == "enemy":
            art[name] = token(parts[1], "enemy", parts[3])
            variants.append(name)
    for terrain in terrains:
        art[f"tile_{terrain}"] = tile(terrain)
    clip_rows = clips(classes)
    fx_rows = effects()
    for name, canvas in art.items():
        canvas.png(os.path.join(OUT, name + ".png"))
    for name, sheet, sidecar in clip_rows + fx_rows:
        sheet.png(os.path.join(OUT, name + ".png"))
        with open(os.path.join(OUT, name + ".json"), "w", newline="\n") as f:
            f.write(json.dumps(sidecar) + "\n")
    with open(os.path.join(OUT, "generated.txt"), "w", newline="\n") as f:
        f.write("# Written by docs/art/make_art.py; a file an artist replaces comes off this list.\n")
        f.write("# A clip or effect sheet's sidecar <name>.json goes with it.\n")
        f.write("".join(name + ".png\n" for name in list(art) + [row[0] for row in clip_rows + fx_rows]))

    # The contact sheet: every file at 2x, and under it at 1x, on the panel colour.
    names = list(art)
    columns = len(classes) * 2 if len(classes) * 2 >= len(terrains) else len(terrains)
    cell = FRAME + 8
    rows = [names[:len(classes) * 2], names[len(classes) * 2 + len(variants):], variants]
    sheet = Canvas(columns * cell + 8, len(rows) * (cell + FRAME // 2 + 8) + 8, PANEL + (255,))
    for r, row in enumerate(rows):
        top = 8 + r * (cell + FRAME // 2 + 8)
        for i, name in enumerate(row):
            sheet.blit(art[name], 8 + i * cell, top)
            sheet.blit(art[name].half(), 8 + i * cell + FRAME // 4, top + FRAME + 4)
    sheet.png(SHEET)

    # The clip sheet: one row per (class or boss, weapon), one frame per clip (the contact frame,
    # else the middle one), at half size, on the panel colour.
    sets = []
    for name, strip, sidecar in clip_rows:
        clip = next(c for c in sorted(CLIPS, key=len, reverse=True) if name.endswith("_" + c))
        stem = name[:-len(clip) - 1]
        if not sets or sets[-1][0] != stem:
            sets.append((stem, []))
        frame = sidecar["contact"] if sidecar["contact"] is not None else sidecar["frames"] // 2
        cut = Canvas(CLIP, CLIP)
        cut.px = bytearray(b"".join(bytes(strip.px[4 * (y * strip.width + frame * CLIP):4 * (y * strip.width + (frame + 1) * CLIP)])
                                    for y in range(CLIP)))
        sets[-1][1].append(cut.half())
    half = CLIP // 2
    board = Canvas(len(CLIPS) * (half + 4) + 4, len(sets) * (half + 4) + 4, PANEL + (255,))
    for r, (stem, cuts) in enumerate(sets):
        for i, cut in enumerate(cuts):
            board.blit(cut, 4 + i * (half + 4), 4 + r * (half + 4))
    board.png(CLIP_SHEET)

    # The effects sheet: one row per effect, every frame at half size, on the panel colour.
    widest = max(sidecar["frames"] for _, _, sidecar in fx_rows)
    board = Canvas(widest * (half + 4) + 4, len(fx_rows) * (half + 4) + 4, PANEL + (255,))
    for r, (name, strip, sidecar) in enumerate(fx_rows):
        for i in range(sidecar["frames"]):
            cut = Canvas(CLIP, CLIP)
            cut.px = bytearray(b"".join(bytes(strip.px[4 * (y * strip.width + i * CLIP):4 * (y * strip.width + (i + 1) * CLIP)])
                                        for y in range(CLIP)))
            board.blit(cut.half(), 4 + i * (half + 4), 4 + r * (half + 4))
    board.png(FX_SHEET)
    print(f"make_art: {len(art) + len(clip_rows) + len(fx_rows)} files under {OUT}, sheets {SHEET}, {CLIP_SHEET} and {FX_SHEET}")


if __name__ == "__main__":
    main()
