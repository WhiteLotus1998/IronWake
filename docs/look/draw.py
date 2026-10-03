# Draws docs/look/*.svg (issue 510). Every colour comes from the CSS classes in style(), whose values are LookPalette.Tokens; PaletteTests holds the files to them.
T=dict(plain='7E9470',road='B3AE9C',forest='4F6E54',hill='C8C8A0',mountain='77767C',water='41667F',fort='9FB0C4',wall='23272E',throne='E4E7EA',fire='943C0C')
P='E8A33D'; PD='9A6420'; E='2F3742'; EB='E6E0D0'
M=dict(selected='F6D38A',reach='BFD9EA',threat='EDE6D6',struck='FFFFFF')
U=dict(ink='15181D',panel='1E232A',text='E9ECEF',muted='8C96A3',lost='5A6270')
UIF="Inter, 'DejaVu Sans', sans-serif"; MONO="'JetBrains Mono', 'DejaVu Sans Mono', monospace"
def style():
    s=[]
    for k,v in T.items(): s.append(f".t-{k}{{fill:#{v}}}")
    s+= [f".player{{fill:#{P}}}",f".player-deep{{fill:#{PD}}}",f".enemy{{fill:#{E}}}",f".enemy-bone{{fill:#{EB}}}",
         f".glyph-p{{fill:none;stroke:#{U['ink']};stroke-width:2.6;stroke-linecap:round;stroke-linejoin:round}}",
         f".glyph-pf{{fill:#{U['ink']}}}",
         f".glyph-e{{fill:none;stroke:#{EB};stroke-width:2.6;stroke-linecap:round;stroke-linejoin:round}}",
         f".glyph-ef{{fill:#{EB}}}",
         f".rim-e{{fill:none;stroke:#{EB};stroke-width:0.75}}", f".ring-lost{{fill:none;stroke:#{U['lost']};stroke-width:1.5}}"]
    for k,v in M.items(): s.append(f".m-{k}{{fill:#{v}}} .ms-{k}{{fill:none;stroke:#{v}}}")
    for k,v in U.items(): s.append(f".u-{k}{{fill:#{v}}}")
    s.append(f".ui{{font-family:{UIF};font-variant-numeric:tabular-nums}} .mono{{font-family:{MONO}}}")
    s.append(f".hatch-line{{stroke:#{T['fire']};stroke-width:3}}")
    return "\n    ".join(s)

def glyph(cls, cx, cy, side):
    g='glyph-p' if side=='p' else 'glyph-e'; f='glyph-pf' if side=='p' else 'glyph-ef'
    x,y=cx,cy
    out=[]
    if cls in('cadet','captain'):
        out.append(f'<path class="{g}" d="M{x} {y-11} L{x} {y+6} M{x-6} {y+4} L{x+6} {y+4} M{x} {y+6} L{x} {y+10}"/>')
        out.append(f'<path class="{f}" d="M{x-2} {y-8} L{x} {y-13} L{x+2} {y-8} Z"/>')
    elif cls in('pikeman','warden','outrider'):
        out.append(f'<path class="{g}" d="M{x-9} {y+10} L{x+7} {y-8}"/>')
        out.append(f'<path class="{f}" d="M{x+5} {y-6} L{x+11} {y-12} L{x+9} {y-4} Z"/>')
        if cls=='warden': out.append(f'<path class="{g}" d="M{x+1} {y-2} L{x-3} {y-6} M{x+1} {y-2} L{x+5} {y+2}"/>')
        if cls=='outrider': out.append(f'<path class="{f}" d="M{x-3} {y+4} L{x-10} {y} L{x-5} {y+8} Z"/>')
    elif cls=='bowman':
        out.append(f'<path class="{g}" d="M{x-4} {y-11} Q{x+10} {y} {x-4} {y+11} M{x-4} {y-11} L{x-4} {y+11}"/>')
        out.append(f'<path class="{g}" d="M{x-9} {y} L{x+8} {y}"/>')
    elif cls=='adept':
        out.append(f'<path class="{f}" d="M{x} {y-12} C{x+8} {y-4} {x+7} {y+8} {x} {y+9} C{x-7} {y+8} {x-8} {y-2} {x-2} {y-6} C{x-2} {y-2} {x} {y} {x+1} {y-2} C{x+2} {y-6} {x} {y-9} {x} {y-12} Z"/>')
    elif cls in('reaver','leader'):
        out.append(f'<path class="{g}" d="M{x-6} {y+11} L{x+4} {y-9}"/>')
        out.append(f'<path class="{f}" d="M{x+1} {y-5} C{x+6} {y-12} {x+12} {y-8} {x+11} {y-3} C{x+8} {y-4} {x+6} {y-1} {x+5} {y+2} Z"/>')
        if cls=='leader': out.append(f'<path class="{f}" d="M{x+1} {y-5} C{x-5} {y-10} {x-9} {y-4} {x-8} {y+1} C{x-5} {y-1} {x-3} {y} {x-2} {y+1} Z"/>')
    return "".join(out)

def token(cls, cx, cy, side, hp, hpmax, r=16, crown=False, boss=False):
    o=[]
    if side=='p':
        o.append(f'<ellipse class="player-deep" cx="{cx}" cy="{cy+3}" rx="{r}" ry="{r-1}"/>')
        o.append(f'<circle class="player" cx="{cx}" cy="{cy}" r="{r}"/>')
    else:
        o.append(f'<ellipse class="u-ink" opacity="0.55" cx="{cx}" cy="{cy+3}" rx="{r}" ry="{r-1}"/>')
        o.append(f'<circle class="enemy" cx="{cx}" cy="{cy}" r="{r}"/>')
        o.append(f'<circle class="rim-e" cx="{cx}" cy="{cy}" r="{r-1.5}"/>')
        if boss: o.append(f'<circle class="rim-e" cx="{cx}" cy="{cy}" r="{r+3}" stroke-dasharray="3 2"/>')
    o.append(glyph(cls,cx,cy-1,side))
    if crown:
        o.append(f'<path class="player" d="M{cx-8} {cy-r-1} L{cx-8} {cy-r-9} L{cx-4} {cy-r-4} L{cx} {cy-r-11} L{cx+4} {cy-r-4} L{cx+8} {cy-r-9} L{cx+8} {cy-r-1} Z"/>')
        o.append(f'<path class="glyph-p" style="stroke-width:1.2" d="M{cx-8} {cy-r-1} L{cx-8} {cy-r-9} L{cx-4} {cy-r-4} L{cx} {cy-r-11} L{cx+4} {cy-r-4} L{cx+8} {cy-r-9} L{cx+8} {cy-r-1} Z"/>')
    w=30; fill=round(w*hp/hpmax); bx=cx-w/2; by=cy+r+2
    o.append(f'<rect class="u-ink" x="{bx-1}" y="{by-1}" width="{w+2}" height="6" rx="2"/>')
    o.append(f'<rect class="{"player" if side=="p" else "enemy-bone"}" x="{bx}" y="{by}" width="{fill}" height="4" rx="1.5"/>')
    return "".join(o)

MAP="""MM############
MM#....T.....#
MM#..........#
MM####.#######
......^.......
....n^^^......
.....^........
..........F...
......~~......
.....~~~~.....
..............
..............""".split("\n")
G={'M':'mountain','#':'wall','.':'plain','T':'throne','^':'forest','n':'hill','F':'fort','~':'water','=':'road'}
TS=48; BX=24; BY=64
def tile(x,y): return BX+x*TS, BY+y*TS
def board(reach, hover, selected):
    o=[]
    o.append(f'<rect class="u-ink" x="{BX-6}" y="{BY-6}" width="{14*TS+12}" height="{12*TS+12}" rx="6"/>')
    for y,row in enumerate(MAP):
        for x,c in enumerate(row):
            t=G[c]; px,py=tile(x,y)
            o.append(f'<rect class="t-{t}" x="{px}" y="{py}" width="{TS}" height="{TS}"/>')
    # terrain detail, drawn in the tile's own palette
    for y,row in enumerate(MAP):
        for x,c in enumerate(row):
            px,py=tile(x,y)
            if c=='^':
                for dx,dy in((13,26),(28,20),(22,34)):
                    o.append(f'<path class="u-ink" opacity="0.35" d="M{px+dx} {py+dy-12} L{px+dx+7} {py+dy} L{px+dx-7} {py+dy} Z"/>')
            elif c=='~':
                for dy in(15,29):
                    o.append(f'<path class="ms-reach" opacity="0.35" stroke-width="1.5" d="M{px+8} {py+dy} q5 -4 10 0 t10 0 t10 0"/>')
            elif c=='M':
                o.append(f'<path class="u-ink" opacity="0.3" d="M{px+6} {py+36} L{px+20} {py+12} L{px+28} {py+24} L{px+32} {py+18} L{px+40} {py+36} Z"/>')
            elif c=='n':
                o.append(f'<path class="u-ink" opacity="0.25" d="M{px+6} {py+34} Q{px+22} {py+10} {px+38} {py+34} Z"/>')
            elif c=='#':
                o.append(f'<path class="t-mountain" opacity="0.35" d="M{px} {py+22} H{px+TS} M{px+22} {py} V{py+22} M{px+11} {py+22} V{py+TS}" stroke="#77767C" stroke-width="1"/>')
            elif c=='T':
                o.append(f'<path class="u-ink" opacity="0.35" d="M{px+8} {py+40} V{py+10} H{px+40} V{py+40} H{px+33} V{py+18} H{px+15} V{py+40} Z"/>')
            elif c=='F':
                o.append(f'<path class="u-ink" opacity="0.3" d="M{px+8} {py+36} V{py+14} h6 v5 h5 v-5 h6 v5 h5 v-5 h6 V{py+36} Z"/>')
    # grid
    for i in range(15):
        o.append(f'<line x1="{BX+i*TS}" y1="{BY}" x2="{BX+i*TS}" y2="{BY+12*TS}" stroke="#15181D" stroke-opacity="0.18"/>')
    for j in range(13):
        o.append(f'<line x1="{BX}" y1="{BY+j*TS}" x2="{BX+14*TS}" y2="{BY+j*TS}" stroke="#15181D" stroke-opacity="0.18"/>')
    for (x,y) in reach:
        px,py=tile(x,y)
        o.append(f'<rect class="m-reach" opacity="0.42" x="{px+2}" y="{py+2}" width="{TS-4}" height="{TS-4}" rx="4"/>')
    if hover:
        px,py=tile(*hover)
        o.append(f'<rect class="ms-selected" stroke-width="2" stroke-dasharray="5 3" x="{px+3}" y="{py+3}" width="{TS-6}" height="{TS-6}" rx="5"/>')
    if selected:
        px,py=tile(*selected)
        o.append(f'<rect class="ms-selected" stroke-width="3" x="{px+1.5}" y="{py+1.5}" width="{TS-3}" height="{TS-3}" rx="6"/>')
    return "".join(o)

def text(x,y,s,cls='u-text',size=14,weight=400,anchor='start',extra=''):
    return f'<text class="ui {cls}" x="{x}" y="{y}" font-size="{size}" font-weight="{weight}" text-anchor="{anchor}" {extra}>{s}</text>'

def hpbar(x,y,w,hp,hpmax,after,cls):
    o=[f'<rect class="u-ink" x="{x}" y="{y}" width="{w}" height="14" rx="3"/>']
    keep=w*after/hpmax; cur=w*hp/hpmax
    o.append(f'<rect class="{cls}" x="{x}" y="{y}" width="{keep:.1f}" height="14" rx="3"/>')
    if cur>keep:
        o.append(f'<rect class="{cls}" opacity="0.35" x="{x+keep:.1f}" y="{y}" width="{cur-keep:.1f}" height="14"/>')
        for i in range(int(keep)+4,int(cur),6):
            o.append(f'<line x1="{x+i}" y1="{y+14}" x2="{x+i+6}" y2="{y}" stroke="#15181D" stroke-opacity="0.6" stroke-width="1.5"/>')
    return "".join(o)

def forecast_card(x,y):
    # Real numbers: seed 113, turn 3, Teodor on 7,5 against the Toll Brigand on 6,5 (forest).
    o=[f'<rect class="u-panel" x="{x}" y="{y}" width="540" height="260" rx="10"/>']
    o.append(text(x+20,y+30,"FORECAST",'u-muted',12,700,extra='letter-spacing="2"'))
    o.append(text(x+520,y+30,"Teodor strikes first",'u-muted',12,anchor='end'))
    # left: attacker
    o.append(token('pikeman',x+46,y+78,'p',21,21,r=20))
    o.append(text(x+80,y+70,"Teodor",'u-text',18,700)); o.append(text(x+80,y+90,"Iron Lance  |  Forest 7,5",'u-muted',12))
    o.append(token('reaver',x+494,y+78,'e',23,23,r=20))
    o.append(text(x+460,y+70,"Toll Brigand",'u-text',18,700,'end')); o.append(text(x+460,y+90,"Toll Axe  |  Forest 6,5",'u-muted',12,anchor='end'))
    # hp bars with damage shaded in
    o.append(hpbar(x+20,y+118,240,21,21,12,'player')); o.append(text(x+20,y+150,"21",'u-text',15,700)); o.append(text(x+44,y+150,"&#8594; 12 if countered",'u-muted',12))
    o.append(hpbar(x+280,y+118,240,23,23,12,'enemy-bone')); o.append(text(x+520,y+150,"23 &#8594; 12",'u-text',15,700,'end'))
    # big numerals
    cols=[("HIT","47","21"),("DMG","11","9"),("CRIT","2","0")]
    for i,(lab,a,b) in enumerate(cols):
        cx=x+95+i*175
        o.append(text(cx,y+176,lab,'u-muted',11,700,'middle',extra='letter-spacing="2"'))
        o.append(text(cx-10,y+210,a,'player',38,800,'end'))
        o.append(f'<line x1="{cx}" y1="{y+184}" x2="{cx}" y2="{y+210}" stroke="#5A6270"/>')
        o.append(text(cx+10,y+210,b,'enemy-bone',38,800,'start'))
    o.append(f'<line x1="{x+20}" y1="{y+220}" x2="{x+520}" y2="{y+220}" stroke="#5A6270"/>')
    # strikes: pips
    o.append(text(x+20,y+244,"strikes",'u-muted',12))
    o.append(f'<circle class="player" cx="{x+84}" cy="{y+240}" r="6"/>')
    o.append(f'<circle class="ring-lost" cx="{x+100}" cy="{y+240}" r="5.5"/>')
    o.append(text(x+280,y+244,"counter",'u-muted',12))
    o.append(f'<circle class="enemy-bone" cx="{x+344}" cy="{y+240}" r="6"/>')
    o.append(f'<circle class="ring-lost" cx="{x+360}" cy="{y+240}" r="5.5"/>')
    o.append(text(x+520,y+244,"neither doubles",'u-muted',12,anchor='end'))
    return "".join(o)

def move_card(x,y):
    # Real: seed 113 turn 1, `threat captain from 4,9` prints "no enemy can strike it next phase".
    o=[f'<rect class="u-panel" x="{x}" y="{y}" width="540" height="260" rx="10"/>']
    o.append(text(x+20,y+30,"FORECAST",'u-muted',12,700,extra='letter-spacing="2"'))
    o.append(text(x+520,y+30,"hover a foe in reach to price a strike",'u-muted',12,anchor='end'))
    o.append(token('captain',x+50,y+92,'p',22,22,r=22,crown=True))
    o.append(text(x+88,y+80,"Alder Fenn to 4,9",'u-text',20,700)); o.append(text(x+88,y+102,"Plain  |  4 of 4 move  |  avoid 0",'u-muted',13))
    o.append(f'<rect class="u-ink" x="{x+20}" y="{y+136}" width="500" height="64" rx="8"/>')
    o.append(f'<circle class="m-reach" cx="{x+48}" cy="{y+168}" r="9"/>')
    o.append(text(x+68,y+164,"Safe next phase",'u-text',18,700)); o.append(text(x+68,y+184,"No enemy can strike 4,9.",'u-muted',13))
    o.append(text(x+20,y+236,"Nobody is awake. A group wakes when you stop within 4 of it.",'u-muted',12))
    return "".join(o)

def unit_card(x,y):
    o=[f'<rect class="u-panel" x="{x}" y="{y}" width="540" height="150" rx="10"/>']
    o.append(text(x+20,y+28,"UNIT",'u-muted',12,700,extra='letter-spacing="2"'))
    o.append(text(x+20,y+56,"Alder Fenn",'u-text',18,700)); o.append(text(x+140,y+56,"Captain  |  Cadet L1  |  6,11 Plain",'u-muted',13))
    o.append(hpbar(x+20,y+68,200,22,22,22,'player')); o.append(text(x+230,y+80,"22 / 22",'u-text',13,700))
    stats=[("STR",8),("MAG",0),("DEX",7),("SPD",8),("LCK",6),("DEF",5),("RES",2),("CHA",9),("MOV",4)]
    for i,(k,v) in enumerate(stats):
        cx=x+36+i*56
        o.append(text(cx,y+108,k,'u-muted',10,700,'middle',extra='letter-spacing="1"'))
        o.append(text(cx,y+128,str(v),'u-text',17,700,'middle'))
    o.append(text(x+520,y+28,"Iron Sword  |  mt 5  hit 75  range 1",'u-muted',12,anchor='end'))
    return "".join(o)

def frame():
    W,H=1280,720
    reach=[(4,9),(3,10),(4,10),(5,10),(7,10),(8,10),(9,10),(2,11),(3,11),(4,11),(8,11),(9,11),(10,11)]
    o=[f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">',
       f'<title>Ironwake, the Tollgate, turn 1, seed 113: the look (issue 510)</title>',
       f'<style>\n    {style()}\n  </style>',
       f'<rect class="u-ink" width="{W}" height="{H}"/>']
    # top bar
    o.append(text(24,38,"The Tollgate",'u-text',22,700))
    chips=[("TURN","1 / 10"),("PHASE","Yours"),("GOAL","Seize the gate"),("RECALL","3")]
    cx=200
    for k,v in chips:
        w=40+len(v)*9+len(k)*8
        o.append(f'<rect class="u-panel" x="{cx}" y="16" width="{w}" height="30" rx="15"/>')
        o.append(text(cx+14,36,k,'u-muted',10,700,extra='letter-spacing="1.5"'))
        o.append(text(cx+22+len(k)*8,36,v,'player' if k=="PHASE" else 'u-text',14,700))
        cx+=w+10
    o.append(board(reach,(4,9),(6,11)))
    units=[('captain',6,11,'p',22,22,True,False),('cadet',5,11,'p',20,20,False,False),('pikeman',7,11,'p',21,21,False,False),('adept',6,10,'p',16,16,False,False),
           ('bowman',6,1,'e',17,17,False,False),('warden',6,2,'e',21,21,False,False),('leader',7,2,'e',26,26,False,True),('reaver',6,5,'e',23,23,False,False),('bowman',5,5,'e',17,17,False,False)]
    for cls,x,y,s,hp,mx,crown,boss in units:
        px,py=tile(x,y)
        o.append(token(cls,px+TS/2,py+TS/2-3,s,hp,mx,r=16,crown=crown,boss=boss))
    # the planned path, drawn in the selected mark
    pts=[tile(6,11),tile(5,11),tile(4,11),tile(4,10),tile(4,9)]
    d="M"+" L".join(f"{px+TS/2} {py+TS/2}" for px,py in pts)
    o.append(f'<path class="ms-selected" stroke-width="3" stroke-dasharray="1 7" stroke-linecap="round" d="{d}"/>')
    o.append(move_card(716,64))
    o.append(unit_card(716,338))
    # log toggle
    o.append(f'<rect class="u-panel" x="716" y="502" width="540" height="40" rx="10"/>')
    o.append(text(736,527,"LOG",'u-muted',12,700,extra='letter-spacing="2"'))
    o.append(f'<text class="mono u-muted" x="780" y="527" font-size="12">no events yet</text>')
    o.append(text(1236,527,"L to open",'u-muted',12,anchor='end'))
    # legend
    lg=[("m-reach","can move"),("ms-selected","selected"),("player","yours"),("enemy","theirs")]
    lx=716
    for cls,lab in lg:
        if cls=="ms-selected": o.append(f'<rect class="ms-selected" stroke-width="2.5" x="{lx}" y="560" width="16" height="16" rx="3"/>')
        elif cls=="enemy": o.append(f'<circle class="enemy" cx="{lx+8}" cy="568" r="8"/><circle class="rim-e" cx="{lx+8}" cy="568" r="7"/>')
        elif cls=="player": o.append(f'<circle class="player" cx="{lx+8}" cy="568" r="8"/>')
        else: o.append(f'<rect class="{cls}" opacity="0.8" x="{lx}" y="560" width="16" height="16" rx="3"/>')
        o.append(text(lx+24,573,lab,'u-muted',13)); lx+=24+len(lab)*8+26
    # footer keys
    kx=24
    for k,v in [("click","select, move, strike"),("E","end phase"),("Space","next enemy act"),("C","skip"),("R","recall"),("L","log"),("Esc","clear")]:
        w=len(k)*8+16
        o.append(f'<rect class="u-panel" x="{kx}" y="{H-42}" width="{w}" height="24" rx="5"/>')
        o.append(text(kx+w/2,H-25,k,'u-text',12,700,'middle'))
        o.append(text(kx+w+8,H-25,v,'u-muted',13)); kx+=w+8+len(v)*7+26
    o.append('</svg>')
    return "\n".join(o)

def forecast_svg():
    W,H=580,300
    return "\n".join([f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">',
      '<title>Ironwake forecast, the Tollgate seed 113 turn 3: Teodor against the Toll Brigand (issue 510)</title>',
      f'<style>\n    {style()}\n  </style>', f'<rect class="u-ink" width="{W}" height="{H}"/>', forecast_card(20,20), '</svg>'])

def sheet_svg():
    items=[('captain','p',True,False,'Captain'),('cadet','p',False,False,'Cadet'),('pikeman','p',False,False,'Pikeman'),('adept','p',False,False,'Adept'),('bowman','p',False,False,'Bowman'),
           ('reaver','e',False,False,'Reaver'),('outrider','e',False,False,'Outrider'),('warden','e',False,False,'Toll Warden'),('leader','e',False,True,'Bandit Leader'),('bowman','e',False,False,'Archer')]
    W,H=5*120+40,2*130+40
    o=[f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">','<title>Ironwake silhouettes (issue 510)</title>',
       f'<style>\n    {style()}\n  </style>', f'<rect class="u-ink" width="{W}" height="{H}"/>']
    for i,(c,s,cr,b,lab) in enumerate(items):
        col=i%5; row=i//5; cx=20+col*120+60; cy=20+row*130+54
        o.append(f'<rect class="t-plain" x="{cx-44}" y="{cy-44}" width="88" height="88" rx="6"/>')
        o.append(token(c,cx,cy-4,s,3,4,r=26,crown=cr,boss=b))
        o.append(text(cx,cy+66,lab,'u-text',13,700,'middle'))
    o.append('</svg>'); return "\n".join(o)

if __name__=="__main__":  # usage: python3 docs/look/draw.py docs/look; grounds.py imports the tokens from here
    import sys
    d=sys.argv[1]
    open(f"{d}/the_tollgate-turn1.svg","w").write(frame()+"\n")
    open(f"{d}/forecast.svg","w").write(forecast_svg()+"\n")
    open(f"{d}/silhouettes.svg","w").write(sheet_svg()+"\n")
