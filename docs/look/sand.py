# Draws the outland sand's gate frame (issue 916, rounds 307 to 310): one board on the sand `#A89670` with the ink
# edge on the threat hatch and the player's discs, beside the same board on the old hill `#B89E6C` without it (the
# ground that turned the bone hatch peach, round 166). Each board carries amber tokens, a slate enemy, the awake hatch
# (0.5), the sleeping hatch (0.2) and a road running across the ground (road is sand's nearest terrain, 15.3).
# The old hill is not a token, so the SVG is written outside docs/look (LookPaletteTests holds every docs/look/*.svg
# to the tokens); only the PNG is committed.
# usage: python3 docs/look/sand.py <out_dir>, then
#        NODE_PATH=$(npm root -g) node docs/look/shot-one.js <out_dir>/sand.svg docs/look/sand-916.png 1100 450
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from draw import T, M, U, style, token, text

SAND, OLD_HILL = "A89670", "B89E6C"
GLYPH = {'.': 'plain', '=': 'road', '^': 'forest', 'n': 'hill', '~': 'water', 'M': 'mountain'}
# A small outland board: a road east to west across the sand, scrub to the north, a dry wash to the south.
ROWS = ["..^^.....n..",
        ".^^.......n.",
        "============",
        "...........M",
        "....~~.....M",
        "...~~......."]
UNITS = [("p", "captain", 2, 3), ("p", "cadet", 4, 2), ("p", "bowman", 1, 4),
         ("e", "reaver", 8, 2), ("e", "bowman", 9, 4), ("e", "warden", 10, 0)]
AWAKE = {(x, y) for x in range(5, 8) for y in range(1, 5)} | {(8, 3), (8, 1)}  # the reaver's group, awake
ASLEEP = {(x, y) for x in range(8, 12) for y in range(0, 2)} - {(10, 0)}  # the warden's group, asleep

UID = [0]
def hatch(px, py, ts, alpha, edged):
    UID[0] += 1
    cid = f"c{UID[0]}"
    step = max(6, ts // 5)
    xs = range(-ts, ts, step)
    lines = "".join(f'<line x1="{px+i}" y1="{py+ts}" x2="{px+i+ts}" y2="{py}"/>' for i in xs)
    o = [f'<clipPath id="{cid}"><rect x="{px+1}" y="{py+1}" width="{ts-2}" height="{ts-2}"/></clipPath>', f'<g clip-path="url(#{cid})">']
    if edged:
        # The 1px ink edge: the stroke laid over an ink stroke two pixels wider, at the hatch's own alpha.
        o.append(f'<g stroke="#{U["ink"]}" stroke-opacity="{alpha}" stroke-width="3.5">{lines}</g>')
    o.append(f'<g stroke="#{M["threat"]}" stroke-opacity="{alpha}" stroke-width="1.5">{lines}</g></g>')
    return "".join(o)

def board(ground, bx, by, ts, title, sub, edged):
    o = [text(bx, by - 26, title, 'u-text', 15, 700), text(bx, by - 9, sub, 'u-muted', 11)]
    h, w = len(ROWS), len(ROWS[0])
    o.append(f'<rect class="u-ink" x="{bx-4}" y="{by-4}" width="{w*ts+8}" height="{h*ts+8}" rx="5"/>')
    for y, row in enumerate(ROWS):
        for x, c in enumerate(row):
            t = GLYPH[c]; px, py = bx + x*ts, by + y*ts
            o.append(f'<rect x="{px}" y="{py}" width="{ts}" height="{ts}" fill="#{ground if t == "plain" else T[t]}"/>')
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
    for (x, y) in sorted(AWAKE):
        o.append(hatch(bx + x*ts, by + y*ts, ts, 0.5, edged))
    for (x, y) in sorted(ASLEEP):
        o.append(hatch(bx + x*ts, by + y*ts, ts, 0.2, edged))
    for i in range(w + 1):
        o.append(f'<line x1="{bx+i*ts}" y1="{by}" x2="{bx+i*ts}" y2="{by+h*ts}" stroke="#{U["ink"]}" stroke-opacity="0.18"/>')
    for j in range(h + 1):
        o.append(f'<line x1="{bx}" y1="{by+j*ts}" x2="{bx+w*ts}" y2="{by+j*ts}" stroke="#{U["ink"]}" stroke-opacity="0.18"/>')
    r = 16
    for side, kind, x, y in UNITS:
        cx, cy = bx + x*ts + ts/2, by + y*ts + ts/2 - 2
        o.append(token(kind, cx, cy, side, 1, 1, r=r, crown=kind == "captain"))
        if edged and side == "p":
            o.append(f'<circle cx="{cx}" cy="{cy}" r="{r+0.5}" fill="none" stroke="#{U["ink"]}" stroke-width="1"/>')
    return "".join(o)

def sheet():
    W, H, ts = 1100, 450, 42
    o = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">',
         '<title>Ironwake outland sand gate (issue 916)</title>',
         f'<style>\n    {style()}\n  </style>', f'<rect class="u-ink" width="{W}" height="{H}"/>']
    o.append(text(24, 34, "The outland sand, edged, beside the old hill without the edge (issue 916)", 'u-text', 20, 700))
    o.append(text(24, 56, "Awake hatch at 0.5 (centre), sleeping hatch at 0.2 (top right), a road across the ground. "
                          "Amber is ours, slate theirs. Both partners look before the exception counts as kept.", 'u-muted', 12))
    o.append(board(SAND, 24, 130, ts, "Outland sand #A89670, ink edge on", "the shipping exception: hatch strokes and player discs edged in ui.ink", True))
    o.append(board(OLD_HILL, 24 + 12*ts + 40, 130, ts, "Old hill #B89E6C, no edge", "the ground that turned the bone hatch peach (round 166)", False))
    o.append(text(24, 130 + 6*ts + 40, "If the sleeping hatch on the left still reads peach, the next lever is the hatch drawn in slate on sand (#578's rule, scoped).", 'u-muted', 12))
    o.append('</svg>')
    return "\n".join(o)

if __name__ == "__main__":
    out = sys.argv[1]
    open(os.path.join(out, "sand.svg"), "w").write(sheet() + "\n")
