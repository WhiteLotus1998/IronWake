# Draws the ground candidates for Lotus to pick (issue 892): per-region frames and the Tollgate once per
# candidate, on the real map files, with amber tokens, slate enemies, the captain's reach and the bone threat hatch.
# The SVG uses candidate colours that are not tokens, so it is written outside docs/look (LookPaletteTests holds
# every docs/look/*.svg to the tokens); only the PNG is committed.
# usage: python3 docs/look/grounds.py <out_dir>, then
#        NODE_PATH=$(npm root -g) node docs/look/shot-one.js <out_dir>/grounds.svg docs/look/grounds-892.png 1440 1060
import os, sys, heapq
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from draw import T, M, U, style, token, text

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
GLYPH = {'.': 'plain', '=': 'road', '^': 'forest', 'n': 'hill', 'M': 'mountain', '~': 'water', 'F': 'fort', '#': 'wall',
         'T': 'throne', '%': 'fire', '+': 'planks', ':': 'split_planks', '&': 'rime'}
# Infantry costs from content/terrain.json; None is impassable.
COST = {'plain': 1, 'road': 1, 'forest': 2, 'hill': 2, 'mountain': 3, 'water': None, 'fort': 2, 'wall': None, 'throne': 1}

CANDIDATES = [  # (label, hex, the region it is proposed for)
    ("Peat (shipping, control)", "7E9470", "Aldmere, temperate"),
    ("A. Cold moss", "649470", "Sallow, marsh"),
    ("B. Heather", "8E82A0", "Kestrow quests, reserve"),
    ("C. Frost, snow as detail", "949A9A", "the seam, tundra"),
]

def read_map(name):
    rows, units, part = [], [], "header"
    for line in open(os.path.join(ROOT, "content", "maps", name + ".map")):
        line = line.rstrip("\n")
        if line.startswith("size:"): part = "pre"; continue
        if line == "units:": part = "units"; continue
        if line == "events:": part = "events"; continue
        if part == "pre" and line and ":" not in line: part = "grid"
        if part == "grid":
            if not line: part = "post"; continue
            rows.append(line)
        elif part == "units" and line:
            side, kind, at = line.split()[:3]
            group = next((w[6:] for w in line.split() if w.startswith("group:")), "")
            x, y = map(int, at.split(","))
            units.append((side, kind.split(":")[0], x, y, group))
    return rows, units

def shape(kind):
    if kind == "captain": return "captain"
    if kind == "recruit": return "cadet"
    if "archer" in kind or "bow" in kind: return "bowman"
    if "leader" in kind or "foreman" in kind: return "leader"
    if "rider" in kind: return "outrider"
    if "warden" in kind or "soldier" in kind or "shield" in kind: return "warden"
    if "hexer" in kind or "adept" in kind: return "adept"
    return "reaver"

def reach(rows, start, move, rng, blocked=()):
    """Tiles a unit at start reaches on foot with move, then strikes at 1 to rng: an approximation for the mock, not the priced threat."""
    h, w = len(rows), len(rows[0])
    best = {start: 0}; q = [(0, start)]
    while q:
        d, (x, y) = heapq.heappop(q)
        if d > best[(x, y)]: continue
        for nx, ny in ((x+1, y), (x-1, y), (x, y+1), (x, y-1)):
            if not (0 <= nx < w and 0 <= ny < h) or (nx, ny) in blocked: continue
            c = COST.get(GLYPH[rows[ny][nx]])
            if c is None or d + c > move: continue
            if d + c < best.get((nx, ny), 99): best[(nx, ny)] = d + c; heapq.heappush(q, (d + c, (nx, ny)))
    stand = set(best)
    strike = {(x+dx, y+dy) for x, y in stand for dx in range(-rng, rng+1) for dy in range(-rng, rng+1)
              if 1 <= abs(dx) + abs(dy) <= rng and 0 <= x+dx < w and 0 <= y+dy < h}
    return stand, strike

def hatch(px, py, ts, cid):
    clip = f'<clipPath id="{cid}"><rect x="{px+1}" y="{py+1}" width="{ts-2}" height="{ts-2}"/></clipPath>'
    step = max(6, ts // 5)
    lines = "".join(f'<line x1="{px+i}" y1="{py+ts}" x2="{px+i+ts}" y2="{py}"/>' for i in range(-ts, ts, step))
    return clip + f'<g clip-path="url(#{cid})" stroke="#{M["threat"]}" stroke-opacity="0.5" stroke-width="1.5">{lines}</g>'

def snow(px, py, ts, x, y):
    # Frost-white patches over the frost ground: two to three soft drifts, placed by the tile's own coordinates.
    k = (x * 7 + y * 13) % 5
    if k == 4: return ""
    o = []
    for i in range(2 + (k % 2)):
        ox = ((x * 31 + y * 17 + i * 11) % 60) / 100 * ts + 0.15 * ts
        oy = ((x * 19 + y * 29 + i * 23) % 60) / 100 * ts + 0.2 * ts
        o.append(f'<ellipse cx="{px+ox:.1f}" cy="{py+oy:.1f}" rx="{ts*0.16:.1f}" ry="{ts*0.07:.1f}" fill="#{T["throne"]}" opacity="0.38"/>')
    return "".join(o)

UID = [0]
def board(name, ground, ts, bx, by, title, sub, frost=False, threat_group=None):
    rows, units = read_map(name)
    h, w = len(rows), len(rows[0])
    o = [text(bx, by - 26, title, 'u-text', 15, 700), text(bx, by - 9, sub, 'u-muted', 11)]
    o.append(f'<rect class="u-ink" x="{bx-4}" y="{by-4}" width="{w*ts+8}" height="{h*ts+8}" rx="5"/>')
    for y, row in enumerate(rows):
        for x, c in enumerate(row):
            t = GLYPH[c]; px, py = bx + x*ts, by + y*ts
            fill = ground if t == 'plain' else T[t]
            o.append(f'<rect x="{px}" y="{py}" width="{ts}" height="{ts}" fill="#{fill}"/>')
            s = ts / 48
            if c == '^':
                for dx, dy in ((13, 26), (28, 20), (22, 34)):
                    o.append(f'<path class="u-ink" opacity="0.35" d="M{px+dx*s:.1f} {py+(dy-12)*s:.1f} L{px+(dx+7)*s:.1f} {py+dy*s:.1f} L{px+(dx-7)*s:.1f} {py+dy*s:.1f} Z"/>')
            elif c == '~':
                for dy in (15, 29):
                    o.append(f'<path fill="none" stroke="#{M["reach"]}" opacity="0.35" stroke-width="1.2" d="M{px+8*s:.1f} {py+dy*s:.1f} q{5*s:.1f} {-4*s:.1f} {10*s:.1f} 0 t{10*s:.1f} 0 t{10*s:.1f} 0"/>')
            elif c == 'n':
                o.append(f'<path class="u-ink" opacity="0.25" d="M{px+6*s:.1f} {py+34*s:.1f} Q{px+22*s:.1f} {py+10*s:.1f} {px+38*s:.1f} {py+34*s:.1f} Z"/>')
            elif c == 'M':
                o.append(f'<path class="u-ink" opacity="0.3" d="M{px+6*s:.1f} {py+36*s:.1f} L{px+20*s:.1f} {py+12*s:.1f} L{px+28*s:.1f} {py+24*s:.1f} L{px+32*s:.1f} {py+18*s:.1f} L{px+40*s:.1f} {py+36*s:.1f} Z"/>')
            elif c == 'F':
                o.append(f'<path class="u-ink" opacity="0.3" d="M{px+8*s:.1f} {py+36*s:.1f} V{py+14*s:.1f} h{6*s:.1f} v{5*s:.1f} h{5*s:.1f} v{-5*s:.1f} h{6*s:.1f} v{5*s:.1f} h{5*s:.1f} v{-5*s:.1f} h{6*s:.1f} V{py+36*s:.1f} Z"/>')
            elif c == 'T':
                o.append(f'<path class="u-ink" opacity="0.35" d="M{px+8*s:.1f} {py+40*s:.1f} V{py+10*s:.1f} H{px+40*s:.1f} V{py+40*s:.1f} H{px+33*s:.1f} V{py+18*s:.1f} H{px+15*s:.1f} V{py+40*s:.1f} Z"/>')
            if frost and t == 'plain': o.append(snow(px, py, ts, x, y))
    occupied = {(x, y) for _, _, x, y, _ in units}
    # The threat hatch: the named group's reach on foot at Mov 4, striking 1 to 2.
    hatched = set()
    for side, kind, x, y, group in units:
        if side in "EB" and group == threat_group:
            _, strike = reach(rows, (x, y), 4, 2 if shape(kind) == "bowman" else 1)
            hatched |= strike
    for (x, y) in sorted(hatched):
        if rows[y][x] == '#': continue
        UID[0] += 1
        o.append(hatch(bx + x*ts, by + y*ts, ts, f"h{UID[0]}"))
    # The captain's reach at Mov 4, through allies, never onto one.
    cap = next((x, y) for side, kind, x, y, _ in units if kind == "captain")
    stand, _ = reach(rows, cap, 4, 1)
    for (x, y) in stand - occupied:
        o.append(f'<rect class="m-reach" opacity="0.42" x="{bx+x*ts+2}" y="{by+y*ts+2}" width="{ts-4}" height="{ts-4}" rx="3"/>')
    for i in range(w + 1):
        o.append(f'<line x1="{bx+i*ts}" y1="{by}" x2="{bx+i*ts}" y2="{by+h*ts}" stroke="#{U["ink"]}" stroke-opacity="0.18"/>')
    for j in range(h + 1):
        o.append(f'<line x1="{bx}" y1="{by+j*ts}" x2="{bx+w*ts}" y2="{by+j*ts}" stroke="#{U["ink"]}" stroke-opacity="0.18"/>')
    r = ts * 0.34
    for side, kind, x, y, group in units:
        cx, cy = bx + x*ts + ts/2, by + y*ts + ts/2 - 2
        sc = r / 16  # draw.py's glyphs are sized for r 16
        o.append(f'<g transform="translate({cx:.1f} {cy:.1f}) scale({sc:.3f}) translate({-cx:.1f} {-cy:.1f})">{token(shape(kind), cx, cy, "p" if side == "P" else "e", 1, 1, r=16, crown=kind == "captain", boss=side == "B")}</g>')
    return "".join(o)

def fen(bx, by, ts, ground, title, sub):
    """A fen swatch, not a map: ground, water and forest at the density a fen board would use."""
    pat = ["~.~..^", ".~~.~.", "..~.^~", "~..~.."]
    o = [text(bx, by - 26, title, 'u-text', 15, 700), text(bx, by - 9, sub, 'u-muted', 11)]
    for y, row in enumerate(pat):
        for x, c in enumerate(row):
            t = GLYPH[c]; px, py = bx + x*ts, by + y*ts
            o.append(f'<rect x="{px}" y="{py}" width="{ts}" height="{ts}" fill="#{ground if t == "plain" else T[t]}"/>')
            if c == '~':
                for dy in (0.31, 0.6):
                    o.append(f'<path fill="none" stroke="#{M["reach"]}" opacity="0.35" stroke-width="1.2" d="M{px+ts*0.17:.1f} {py+ts*dy:.1f} q{ts*0.1:.1f} {-ts*0.08:.1f} {ts*0.2:.1f} 0 t{ts*0.2:.1f} 0 t{ts*0.2:.1f} 0"/>')
    return "".join(o)

def sheet():
    W, H = 1440, 1060
    o = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">',
         '<title>Ironwake ground candidates (issue 892)</title>',
         f'<style>\n    {style()}\n  </style>', f'<rect class="u-ink" width="{W}" height="{H}"/>']
    o.append(text(24, 34, "The ground, per region (issue 892)", 'u-text', 22, 700))
    o.append(text(24, 56, "Every candidate passes LookPaletteTests' colour checks in place of plain. Forest, road and every other token are unchanged. "
                          "Amber is ours, slate theirs; pale blue is the captain's reach, the bone hatch an enemy group's reach.", 'u-muted', 12))
    # Row 1: each region on its own map.
    ts = 30
    o.append(board("the_tollgate", "949A9A", ts, 24, 120, "The seam: the Tollgate on C, frost", "maps 1, 2, 4, 6, 9, 10; snow drawn as detail on the ground", frost=True, threat_group="woods"))
    o.append(board("saltmarsh_ford", "649470", ts, 480, 120, "Sallow: Saltmarsh Ford on A, cold moss", "maps 3, 7, 8", threat_group="fort"))
    o.append(fen(480, 480, 30, "649470", "A beside water at fen density", "a swatch, not a map"))
    o.append(board("harrow_weir", "7E9470", ts, 936, 120, "Aldmere: Harrow Weir on peat", "map 5, the one summer ground", threat_group="weir"))
    # Row 2: the Tollgate once per candidate, for one ground everywhere.
    o.append(text(24, 632, "Or one ground for everything: the Tollgate on each", 'u-text', 17, 700))
    ts2 = 24
    for i, (label, hexv, region) in enumerate(CANDIDATES):
        o.append(board("the_tollgate", hexv, ts2, 24 + i * 352, 690, label, f"#{hexv}, proposed for {region}", frost=hexv == "949A9A", threat_group="woods"))
    o.append('</svg>')
    return "\n".join(o)

if __name__ == "__main__":
    out = sys.argv[1]
    open(os.path.join(out, "grounds.svg"), "w").write(sheet() + "\n")
