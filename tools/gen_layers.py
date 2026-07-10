#!/usr/bin/env python3
"""FF6-fidelity character sprite LAYER generator.

Emits grayscale-ramp layer sheets (composited + tinted at runtime by the
game's SpriteComposer, or by tools/preview_sprites.py for inspection):

  assets/layers/body.png            one body, tinted per skin ramp
  assets/layers/hair_NN.png         12 hair styles, tinted per hair ramp
  assets/layers/outfit_NN_a.png     10 outfits, primary-color regions
  assets/layers/outfit_NN_b.png     10 outfits, secondary-color regions
  assets/layers/acc_NN.png          3 accessories (01..03; 00 = none)
  data/appearance.json              ramps, style counts, preset gallery

Sheet layout: 3 walk frames (cols) x 4 directions (rows: down/up/left/right),
16x24 per cell. Gray indices encode the 4-tone ramp:
  G0=40 outline · G1=110 dark · G2=175 mid · G3=235 light
Runtime maps G(i) -> ramp[i] of the chosen color.

Sprite conventions (FF6-era): 1px outline, chibi head ~10px, 2px eyes,
hair shine band, 3-frame walk (0/1/2; play 0,1,2,1). Left row is a mirror
of right. All art is original.

Run: python3 tools/gen_layers.py
"""
import json
import struct
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FW, FH = 16, 24
GRAY = (40, 110, 175, 235)  # ramp indices 0..3

DOWN, UP, LEFT, RIGHT = 0, 1, 2, 3


def write_png(path, width, height, pixels):
    raw = b"".join(b"\x00" + b"".join(struct.pack("4B", *px) for px in row) for row in pixels)

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(b"\x89PNG\r\n\x1a\n"
                     + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))
    print(f"wrote {path}")


class Cell:
    """One 16x24 frame. Values: None (transparent) or gray index 0..3."""

    def __init__(self):
        self.p = [[None] * FW for _ in range(FH)]

    def px(self, x, y, g):
        if 0 <= x < FW and 0 <= y < FH:
            self.p[y][x] = g

    def rect(self, x0, y0, x1, y1, g):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.px(x, y, g)

    def hline(self, x0, x1, y, g):
        self.rect(x0, y, x1, y, g)

    def outline(self):
        """1px G0 outline around every opaque region (drawn into transparency)."""
        grown = []
        for y in range(FH):
            for x in range(FW):
                if self.p[y][x] is None:
                    if any(0 <= x + dx < FW and 0 <= y + dy < FH and self.p[y + dy][x + dx] is not None
                           and self.p[y + dy][x + dx] != 0
                           for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                        grown.append((x, y))
        for x, y in grown:
            self.p[y][x] = 0

    def mirrored(self):
        c = Cell()
        for y in range(FH):
            for x in range(FW):
                c.p[y][FW - 1 - x] = self.p[y][x]
        return c

    def over(self, other):
        """Paste other's opaque pixels over self."""
        for y in range(FH):
            for x in range(FW):
                if other.p[y][x] is not None:
                    self.p[y][x] = other.p[y][x]


def sheet_from_cells(cells):
    """cells[dir][frame] -> grayscale RGBA rows for the full sheet."""
    rows = [[(0, 0, 0, 0)] * (FW * 3) for _ in range(FH * 4)]
    for d in range(4):
        for f in range(3):
            cell = cells[d][f]
            for y in range(FH):
                for x in range(FW):
                    g = cell.p[y][x]
                    if g is not None:
                        v = GRAY[g]
                        rows[d * FH + y][f * FW + x] = (v, v, v, 255)
    return rows


def save_layer(name, cells):
    write_png(ROOT / "assets" / "layers" / f"{name}.png", FW * 3, FH * 4, sheet_from_cells(cells))


# ---------------------------------------------------------------------------
# Pose: shared skeleton parameters so every layer stays aligned per frame.
# ---------------------------------------------------------------------------

class Pose:
    def __init__(self, d, f):
        self.d = d
        self.f = f
        # Contact frames sink the whole figure 1px (classic walk bounce).
        self.dy = 1 if f in (0, 2) else 0
        # Leg stride: -1 left-forward, 0 neutral, +1 right-forward.
        self.stride = (-1, 0, 1)[f]
        # Arm swing opposes stride.
        self.swing = -self.stride

    # Anchors (all already include dy). Layout (down view, dy=0):
    #   y1-2 hair crown · y3-11 face (fringe to y6, eyes y7-8, chin y11)
    #   y12-16 torso · y17-21 legs · y21-22 feet
    @property
    def head_top(self):
        return 3 + self.dy      # top of the SKIN head (hair sits above/over)

    @property
    def eye_y(self):
        return 7 + self.dy

    @property
    def chin_y(self):
        return 11 + self.dy

    @property
    def torso_top(self):
        return 12 + self.dy

    @property
    def torso_bot(self):
        return 16 + self.dy

    @property
    def legs_top(self):
        return 17 + self.dy


# ---------------------------------------------------------------------------
# BODY layer (skin): head, face, arms, hands, legs.
# ---------------------------------------------------------------------------

def draw_head(c, p: Pose, face: bool):
    """Chibi head with rounded silhouette: crown y=ht, jaw taper, chin at chin_y."""
    ht, ey, ch = p.head_top, p.eye_y, p.chin_y
    c.rect(4, ht + 1, 11, ey + 1, 2)             # main mass
    c.hline(5, 10, ht, 2)                        # rounded crown
    c.rect(5, ey + 2, 10, ch - 1, 2)             # jaw (tapered 1px each side)
    c.hline(6, 9, ch, 2)                         # chin
    c.px(5, ey + 2, 1); c.px(10, ey + 2, 1)      # jaw corner shade
    c.hline(6, 9, ch, 1)                         # underchin shadow tone
    if face:
        c.hline(5, 7, ht + 1, 3)                 # forehead sheen
        # FF6-style eyes: 1px wide, 2 tall, catchlight above.
        for ex in (6, 9):
            c.px(ex, ey, 0)
            c.px(ex, ey + 1, 0)
            c.px(ex, ey - 1, 3)
        c.px(7, ey + 1, 1)                       # nose hint
        c.hline(7, 8, ch - 1, 1)                 # mouth shadow


def draw_body(p: Pose) -> Cell:
    c = Cell()
    d = p.d
    ey, tt, tb, lt = p.eye_y, p.torso_top, p.torso_bot, p.legs_top

    if d in (DOWN, UP):
        draw_head(c, p, face=(d == DOWN))
        # Torso: narrower than the head (chibi taper).
        c.rect(5, tt, 10, tb, 2)
        # Arms: distinct 1px columns just outside the torso; hands swing.
        for side, ax in ((0, 4), (1, 11)):
            sw = p.swing if side == 0 else -p.swing
            c.rect(ax, tt, ax, tb - 1, 2)
            c.px(ax, tb + (1 if sw > 0 else 0), 3)   # hand
        # Legs: 2px each with a 2px gap; stride lifts alternate foot.
        left_lift = 1 if p.stride < 0 else 0
        right_lift = 1 if p.stride > 0 else 0
        c.rect(5, lt, 6, 20 - left_lift, 2)
        c.rect(9, lt, 10, 20 - right_lift, 2)
        c.px(5, lt, 1); c.px(10, lt, 1)          # hip shade
        c.hline(5, 6, 21 - left_lift, 1)         # feet
        c.hline(9, 10, 21 - right_lift, 1)
    else:
        # RIGHT-facing profile (LEFT mirrors at build).
        draw_head(c, p, face=False)
        ht, ch = p.head_top, p.chin_y
        # Profile face: one eye near the leading edge + nose bump.
        c.px(9, ey, 0)
        c.px(9, ey + 1, 0)
        c.px(9, ey - 1, 3)
        c.px(11, ey + 1, 2)                      # nose sticks out 1px
        c.px(11, ey + 2, 1)
        # Torso.
        c.rect(6, tt, 10, tb, 2)
        # Near arm swings across the torso.
        ax = 8 + p.swing
        c.rect(ax, tt, ax, tb - 1, 2)
        c.px(ax, tb, 3)
        # Legs stride front/back; far leg darker.
        front = 8 + p.stride
        back = 7 - p.stride
        c.rect(back, lt, back + 1, 20, 1)
        c.rect(front, lt, front + 1, 20, 2)
        c.hline(back, back + 1, 21, 1)
        c.hline(front, front + 1, 21, 1)

    c.outline()
    return c


# ---------------------------------------------------------------------------
# HAIR styles. Each returns a Cell layered OVER body (covers scalp).
# Gray use: 1 shadow, 2 base, 3 shine.
# ---------------------------------------------------------------------------

def hair_cap(c, p, fringe=1, ragged=False):
    """Crown + fringe. fringe = rows below the crown (1 = brow-length)."""
    ht = p.head_top
    c.hline(5, 10, ht - 2, 2)                    # crown curve
    c.rect(4, ht - 1, 11, ht + fringe, 2)
    c.hline(5, 9, ht - 1, 3)                     # shine band
    c.px(4, ht - 1, 1); c.px(11, ht - 1, 1)      # crown edge shade
    if ragged:                                   # notched fringe tips
        for sx in (5, 8, 10):
            c.px(sx, ht + fringe + 1, 2)


def hair_temples(c, p, depth):
    """Side coverage hugging the head outline, above the eye line only."""
    ht, ey = p.head_top, p.eye_y
    c.rect(4, ht, 4, min(ht + depth, ey + 1), 2)
    c.rect(11, ht, 11, min(ht + depth, ey + 1), 1)


def hair_back_fill(c, p, bottom):
    """UP view: hair covers the whole back of the head."""
    ht = p.head_top
    c.hline(5, 10, ht - 2, 2)
    c.rect(4, ht - 1, 11, bottom, 2)
    c.hline(5, 9, ht - 1, 3)
    c.hline(4, 11, bottom, 1)


def draw_hair(style, p: Pose) -> Cell:
    c = Cell()
    d = p.d
    ht, ey, ch = p.head_top, p.eye_y, p.chin_y
    back = d == UP

    if back:
        # Back of head: full fill; long styles extend below.
        bottoms = {0: ey + 1, 1: ch - 1, 2: ey, 3: p.torso_bot - 1, 4: ey + 1,
                   5: ht + 2, 6: ey + 1, 7: ch - 1, 8: ey + 1, 9: ch - 2,
                   10: ey + 1, 11: ey}
        hair_back_fill(c, p, bottoms[style])
        if style == 2:                                   # spiky silhouette
            for sx in (4, 6, 9, 11):
                c.px(sx, ht - 3, 2)
        elif style == 3:                                 # long sheet sways
            c.rect(4 + p.stride, p.torso_bot, 11 + p.stride, p.torso_bot, 1)
        elif style == 4:                                 # ponytail
            c.rect(7, ey, 8, p.torso_top + 2 + p.dy, 2)
            c.px(7 + p.stride, p.torso_top + 3 + p.dy, 1)
            c.px(7, ey, 3)
        elif style == 5:                                 # bandana knot
            c.px(7, ht + 3, 1); c.px(8, ht + 4, 1)
        elif style == 6:                                 # braid weave
            for i, yy in enumerate(range(ey + 1, p.torso_bot)):
                c.px(7 + (i % 2), yy, 1 if i % 2 else 2)
        elif style == 7:                                 # puff outline bumps
            c.px(3, ht, 2); c.px(12, ht, 2); c.px(3, ey, 2); c.px(12, ey, 2)
        elif style == 10:                                # twin tails
            for tx, sw in ((3, p.stride), (12, -p.stride)):
                c.rect(tx, ht + 1, tx, p.torso_top + 1 + sw, 2)
                c.px(tx, ht + 1, 3)
        elif style == 11:                                # topknot bun
            c.rect(7, ht - 4, 8, ht - 2, 2); c.px(7, ht - 4, 3)
        c.outline()
        return c

    # ---- front/side views ----
    if style == 0:      # short crop
        hair_cap(c, p, fringe=1)
        hair_temples(c, p, 3)
    elif style == 1:    # bob
        hair_cap(c, p, fringe=2)
        hair_temples(c, p, 5)
        c.px(6, ht + 3, 1); c.px(9, ht + 3, 1)          # fringe notches
    elif style == 2:    # spiky
        hair_cap(c, p, fringe=1)
        for sx in (4, 6, 9, 11):
            c.px(sx, ht - 3, 2)
            c.px(sx, ht - 2, 2)
        c.px(6, ht - 3, 3)
        hair_temples(c, p, 2)
    elif style == 3:    # long straight — strands OUTSIDE the face columns
        hair_cap(c, p, fringe=2)
        hair_temples(c, p, 5)
        for sx, g in ((3, 2), (12, 1)):
            c.rect(sx, ht + 1, sx, p.torso_bot - 1, g)
            c.px(sx, p.torso_bot, 1)
        c.px(3, ht + 1, 3)
    elif style == 4:    # ponytail (tail peeks on side views)
        hair_cap(c, p, fringe=1)
        hair_temples(c, p, 3)
        if d in (LEFT, RIGHT):
            c.rect(3, ht + 1, 4, ht + 2, 2)              # gathered knot
            c.rect(3, ht + 3, 3, p.torso_top + 2, 2)     # tail
            c.px(3, p.torso_top + 3, 1)
            c.px(3, ht + 1, 3)
    elif style == 5:    # bandana with front tuft
        c.rect(4, ht - 1, 11, ht + 1, 2)                 # band
        c.hline(5, 9, ht - 1, 3)
        c.px(6, ht - 2, 2); c.px(7, ht - 2, 2)           # tuft above band
        if d in (LEFT, RIGHT):
            c.px(3, ht, 2); c.px(3, ht + 1, 1)           # knot tails
    elif style == 6:    # braid (over-shoulder on side views)
        hair_cap(c, p, fringe=2)
        hair_temples(c, p, 4)
        if d in (LEFT, RIGHT):
            for i, yy in enumerate(range(ey + 2, p.torso_bot + 1)):
                c.px(3 + (i % 2 == 0), yy, 1 if i % 2 else 2)
    elif style == 7:    # curly puff
        hair_cap(c, p, fringe=1)
        hair_temples(c, p, 4)
        for xx, yy in ((3, ht), (12, ht), (4, ht - 2), (11, ht - 2), (7, ht - 3)):
            c.px(xx, yy, 2)
        c.px(5, ht - 2, 3)
    elif style == 8:    # slicked back (high forehead)
        c.hline(5, 10, ht - 2, 2)
        c.rect(4, ht - 1, 11, ht, 2)
        c.hline(6, 10, ht - 1, 3)
        hair_temples(c, p, 2)
    elif style == 9:    # messy shag
        hair_cap(c, p, fringe=2, ragged=True)
        hair_temples(c, p, 4)
        c.px(5, ht - 2, 2); c.px(9, ht - 2, 2)
    elif style == 10:   # twin tails
        hair_cap(c, p, fringe=1)
        hair_temples(c, p, 3)
        for tx, sw in ((2, p.stride), (13, -p.stride)):
            if d in (LEFT, RIGHT) and tx == 13:
                continue                                  # far tail hidden in profile
            c.rect(tx, ht + 1, tx, p.torso_top + sw, 2)
            c.px(tx, ht + 1, 3)
            c.px(tx, p.torso_top + 1 + sw, 1)
    elif style == 11:   # topknot
        hair_cap(c, p, fringe=1)
        hair_temples(c, p, 2)
        c.rect(7, ht - 4, 8, ht - 2, 2)
        c.px(7, ht - 4, 3)

    c.outline()
    return c


# ---------------------------------------------------------------------------
# OUTFITS. Return (primary Cell, secondary Cell).
# ---------------------------------------------------------------------------

def torso_block(c, p, g=2, sleeves=True):
    """Matches draw_body: torso x5-10 (down/up) or x6-10 (side), arms 1px."""
    tt, tb = p.torso_top, p.torso_bot
    if p.d in (DOWN, UP):
        c.rect(5, tt, 10, tb, g)
        c.hline(5, 10, tt, 3)                    # shoulder light
        c.hline(5, 10, tb, 1)                    # hem shadow
        if sleeves:
            for side, ax in ((0, 4), (1, 11)):
                c.rect(ax, tt, ax, tt + 2, g)    # short sleeve over the arm
                c.px(ax, tt, 3)
    else:
        c.rect(6, tt, 10, tb, g)
        c.hline(6, 10, tt, 3)
        c.hline(6, 10, tb, 1)
        if sleeves:
            ax = 8 + p.swing
            c.rect(ax, tt, ax, tt + 2, g)


def legs_cover(c, p, g=1):
    """Trousers over the body's legs (respects stride lift)."""
    lt = p.legs_top
    if p.d in (DOWN, UP):
        left_lift = 1 if p.stride < 0 else 0
        right_lift = 1 if p.stride > 0 else 0
        c.rect(5, lt, 6, 19 - left_lift, g)
        c.rect(9, lt, 10, 19 - right_lift, g)
    else:
        front = 8 + p.stride
        back = 7 - p.stride
        c.rect(back, lt, back + 1, 19, 0 if g == 1 else 1)
        c.rect(front, lt, front + 1, 19, g)


def skirt(c, p, hem_y, g=2):
    """Flared skirt from below the torso to hem_y; feet stay visible."""
    tt, tb = p.torso_top, p.torso_bot
    if p.d in (DOWN, UP):
        c.rect(5, tb, 10, hem_y - 1, g)
        c.rect(4, hem_y - 1, 11, hem_y, g)       # flare
        c.hline(4, 11, hem_y, 1)
        c.px(7, tb + 1, 1)                       # fold
    else:
        c.rect(6, tb, 10, hem_y - 1, g)
        c.rect(5 + p.stride, hem_y - 1, 11, hem_y, g)
        c.hline(5, 11, hem_y, 1)


def draw_outfit(style, p: Pose):
    a, b = Cell(), Cell()
    d = p.d
    tt, tb = p.torso_top, p.torso_bot
    side = d in (LEFT, RIGHT)
    x0 = 6 if side else 5                        # torso left edge

    if style == 0:  # tunic + belt + trousers
        torso_block(a, p)
        legs_cover(a, p)
        b.hline(x0, 10, tb - 1, 2)               # belt
        b.px(7, tb - 1, 3)                       # buckle
    elif style == 1:  # vest over shirt
        torso_block(b, p)                        # shirt = secondary
        a.rect(x0, tt, x0 + 1, tb, 2)            # vest panels
        a.rect(9, tt, 10, tb, 2)
        a.px(x0, tt, 3); a.px(10, tt, 3)
        legs_cover(a, p)
    elif style == 2:  # robe (skirt to ankles, feet peek)
        torso_block(a, p)
        skirt(a, p, 20)
        b.rect(7, tt, 8, tb + 2, 2)              # front trim stripe
        b.px(7, tt, 3)
    elif style == 3:  # suit + cape
        torso_block(a, p)
        legs_cover(a, p)
        if d == UP:                              # cape covers the back
            b.rect(4, tt, 11, 19, 2)
            b.hline(4, 11, 19, 1)
            b.hline(5, 10, tt, 3)
        elif side:
            b.rect(4, tt, 5, 19, 2)              # cape trails behind
            b.px(4, 19, 1)
        else:
            b.px(x0, tt, 3); b.px(10, tt, 3)     # clasps
    elif style == 4:  # dress
        torso_block(a, p)
        skirt(a, p, 19)
        b.hline(x0, 10, tt + 1, 2)               # bodice trim
        b.px(7, tb, 2)                           # waist ribbon
    elif style == 5:  # plate armor
        torso_block(a, p)
        a.hline(x0 + 1, 9, tt + 2, 3)            # chest ridge
        b.rect(4, tt - 1, 4, tt + 1, 2)          # pauldrons
        b.rect(11, tt - 1, 11, tt + 1, 2)
        b.px(4, tt - 1, 3); b.px(11, tt - 1, 3)
        b.hline(x0, 10, tb, 2)                   # faulds trim
        legs_cover(a, p)
    elif style == 6:  # sleeveless gi + sash
        torso_block(a, p, sleeves=False)
        legs_cover(a, p)
        b.hline(x0, 10, tb - 1, 2)               # sash
        b.px(10, tb, 2)                          # sash tail
    elif style == 7:  # long coat over inner shirt
        torso_block(a, p)
        b.rect(7, tt, 8, tb, 2)                  # inner shirt front
        a.rect(x0, tb + 1, x0 + 1, 19, 2)        # coat skirts (open front)
        a.rect(9, tb + 1, 10, 19, 2)
        a.px(x0, 19, 1); a.px(10, 19, 1)
        legs_cover(a, p)
    elif style == 8:  # overalls
        b.rect(x0, tt, 10, tt + 1, 2)            # shirt shoulders
        a.rect(x0, tt + 2, 10, tb, 2)            # bib
        a.px(x0 + 1, tt + 1, 2); a.px(9, tt + 1, 2)  # straps
        a.hline(x0, 10, tb, 1)
        legs_cover(a, p, g=2)
    elif style == 9:  # scale hauberk (dragoon)
        torso_block(a, p)
        for yy in range(tt + 1, tb, 2):          # scale rows
            for xx in range(x0 + 1, 10, 2):
                a.px(xx, yy, 1)
        b.rect(4, tt - 1, 4, tt + 2, 2)          # tall pauldrons
        b.rect(11, tt - 1, 11, tt + 2, 2)
        b.px(4, tt - 1, 3); b.px(11, tt - 1, 3)
        legs_cover(a, p)

    a.outline()
    b.outline()
    return a, b


# ---------------------------------------------------------------------------
# ACCESSORIES (tinted with secondary outfit color).
# ---------------------------------------------------------------------------

def draw_race(race, p: Pose) -> Cell:
    """Racial features drawn over hair/body. 1=elf ears, 2=dwarf beard, 3=gnome ears+nose.
    Elf/gnome layers tint with the SKIN ramp, dwarf with the HAIR ramp."""
    c = Cell()
    ht, ey, ch = p.head_top, p.eye_y, p.chin_y
    side = p.d in (LEFT, RIGHT)
    back = p.d == UP
    if race == 1:  # elf: long pointed ears sweeping up-out
        if not back:
            ears = ((3, ey), (2, ey - 1), (12, ey), (13, ey - 1)) if not side \
                else ((3, ey), (2, ey - 1),)
            for x, y in ears:
                c.px(x, y, 2)
            c.px(2 if not side else 2, ey - 2, 3)
        else:
            c.px(3, ey, 2); c.px(12, ey, 2)
    elif race == 2:  # dwarf: broad braided beard over jaw and chest
        if not back:
            c.rect(5, ey + 2, 10, ch + 1, 2)
            c.hline(6, 9, ch + 2, 2)
            c.px(6, ey + 3, 1); c.px(9, ey + 3, 1)     # braid shadows
            c.px(7, ch + 3, 2); c.px(8, ch + 3, 1)     # beard tip
            c.hline(6, 9, ey + 2, 3)                   # mustache sheen
    elif race == 3:  # gnome: big round ears + button nose
        if not back:
            for x in (3, 12) if not side else (3,):
                c.px(x, ey, 2)
                c.px(x, ey + 1, 2)
                c.px(x, ey - 1, 3)
            c.px(7, ey + 1, 3)                          # button nose highlight
            c.px(8, ey + 1, 2)
        else:
            c.px(3, ey, 2); c.px(3, ey + 1, 2)
            c.px(12, ey, 2); c.px(12, ey + 1, 2)
    c.outline()
    return c


def draw_acc(style, p: Pose) -> Cell:
    c = Cell()
    ht = p.head_top
    if style == 1:  # headband
        c.hline(4, 11, ht, 2)
        c.px(5, ht, 3)
    elif style == 2:  # circlet with gem
        c.hline(4, 11, ht - 1, 2)
        c.px(7, ht - 1, 3); c.px(8, ht - 1, 3)
    elif style == 3:  # feather
        c.px(11, ht - 2, 2); c.px(12, ht - 3, 2); c.px(12, ht - 4, 3)
    c.outline()
    return c


# ---------------------------------------------------------------------------
# Build sheets: rows down/up/left/right; left mirrors right.
# ---------------------------------------------------------------------------

def build(fn):
    cells = []
    for d in (DOWN, UP, LEFT, RIGHT):
        row = []
        src = RIGHT if d == LEFT else d
        for f in range(3):
            cell = fn(Pose(src, f))
            if d == LEFT:
                cell = cell.mirrored()
            row.append(cell)
        cells.append(row)
    return cells


def build_pair(fn):
    ca, cb = [], []
    for d in (DOWN, UP, LEFT, RIGHT):
        ra, rb = [], []
        src = RIGHT if d == LEFT else d
        for f in range(3):
            a, b = fn(Pose(src, f))
            if d == LEFT:
                a, b = a.mirrored(), b.mirrored()
            ra.append(a)
            rb.append(b)
        ca.append(ra)
        cb.append(rb)
    return ca, cb


# ---------------------------------------------------------------------------
# Ramps + appearance manifest.
# ---------------------------------------------------------------------------

def ramp(base, outline_mix=0.35):
    r, g, b = base
    def clamp(v):
        return max(0, min(255, int(v)))
    light = (clamp(r * 1.3 + 20), clamp(g * 1.3 + 20), clamp(b * 1.3 + 20))
    dark = (clamp(r * 0.62), clamp(g * 0.62), clamp(b * 0.62))
    outline = (clamp(r * outline_mix + 18), clamp(g * outline_mix + 10), clamp(b * outline_mix + 24))
    return [list(outline), list(dark), [r, g, b], list(light)]


SKINS = [ramp(c) for c in [
    (244, 212, 180), (232, 190, 152), (216, 168, 130), (196, 144, 104),
    (168, 116, 80), (136, 90, 62), (104, 68, 48), (188, 196, 176),  # last: ashen/fey
]]
HAIR_COLORS = [ramp(c) for c in [
    (40, 38, 44), (74, 52, 34), (110, 76, 44), (140, 96, 52),
    (150, 74, 38), (196, 60, 44), (222, 120, 40), (228, 190, 90),
    (232, 224, 190), (168, 168, 176), (238, 238, 240), (96, 190, 110),
    (70, 170, 160), (74, 110, 200), (140, 90, 190), (226, 130, 170),
]]
OUTFIT_COLORS = [ramp(c) for c in [
    (46, 66, 130), (70, 110, 200), (120, 170, 220), (60, 140, 130),
    (74, 140, 74), (110, 124, 60), (210, 190, 80), (188, 148, 62),
    (208, 120, 50), (160, 84, 48), (188, 58, 58), (190, 80, 140),
    (120, 80, 170), (114, 82, 56), (226, 216, 190), (74, 74, 86),
]]

PRESETS = [
    {"name": "Emerald Adept",   "race": 0, "skin": 1, "hairStyle": 4,  "hairColor": 11, "outfit": 4, "outfitA": 10, "outfitB": 6,  "acc": 2},
    {"name": "Treasure Hunter", "race": 0, "skin": 1, "hairStyle": 5,  "hairColor": 7,  "outfit": 1, "outfitA": 0,  "outfitB": 14, "acc": 0},
    {"name": "White General",   "race": 0, "skin": 0, "hairStyle": 3,  "hairColor": 8,  "outfit": 5, "outfitA": 14, "outfitB": 6,  "acc": 0},
    {"name": "Azure Dragoon",   "race": 0, "skin": 2, "hairStyle": 2,  "hairColor": 0,  "outfit": 9, "outfitA": 1,  "outfitB": 15, "acc": 0},
    {"name": "Machinist King",  "race": 0, "skin": 0, "hairStyle": 8,  "hairColor": 7,  "outfit": 3, "outfitA": 0,  "outfitB": 10, "acc": 2},
    {"name": "Sylvan Warden",   "race": 1, "skin": 0, "hairStyle": 3,  "hairColor": 11, "outfit": 1, "outfitA": 4,  "outfitB": 13, "acc": 3},
    {"name": "Forge Thane",     "race": 2, "skin": 2, "hairStyle": 2,  "hairColor": 5,  "outfit": 5, "outfitA": 15, "outfitB": 7,  "acc": 0},
    {"name": "Cog Tinker",      "race": 3, "skin": 1, "hairStyle": 7,  "hairColor": 14, "outfit": 8, "outfitA": 5,  "outfitB": 6,  "acc": 2},
]


def main():
    save_layer("body", build(draw_body))
    for s in range(12):
        save_layer(f"hair_{s:02d}", build(lambda p, s=s: draw_hair(s, p)))
    for s in range(10):
        ca, cb = build_pair(lambda p, s=s: draw_outfit(s, p))
        save_layer(f"outfit_{s:02d}_a", ca)
        save_layer(f"outfit_{s:02d}_b", cb)
    for s in range(1, 4):
        save_layer(f"acc_{s:02d}", build(lambda p, s=s: draw_acc(s, p)))
    for r in range(1, 4):
        save_layer(f"race_{r:02d}", build(lambda p, r=r: draw_race(r, p)))

    manifest = {
        "gray": list(GRAY),
        "skins": SKINS,
        "hairStyles": 12,
        "hairColors": HAIR_COLORS,
        "outfits": 10,
        "outfitColors": OUTFIT_COLORS,
        "accessories": 4,
        "races": ["Human", "Elf", "Dwarf", "Gnome"],
        "presets": PRESETS,
    }
    dest = ROOT / "data" / "appearance.json"
    dest.write_text(json.dumps(manifest, separators=(",", ":")) + "\n")
    print(f"wrote {dest}")


if __name__ == "__main__":
    main()
