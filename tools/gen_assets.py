#!/usr/bin/env python3
"""Generates the original placeholder pixel-art set (FF6-inspired look, no
copied assets): terrain atlas, unit sprite sheets, and UI panel.

Pure stdlib (zlib PNG writer) — no Pillow required.
Run: python3 tools/gen_assets.py
"""
import struct
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "assets"

# ---------------------------------------------------------------- png writer

def write_png(path: Path, width: int, height: int, pixels):
    """pixels: list of rows; each row a list of (r,g,b,a)."""
    raw = b"".join(
        b"\x00" + b"".join(struct.pack("4B", *px) for px in row) for row in pixels
    )

    def chunk(tag: bytes, data: bytes) -> bytes:
        return (
            struct.pack(">I", len(data))
            + tag
            + data
            + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
        )

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(raw, 9))
        + chunk(b"IEND", b"")
    )
    print(f"wrote {path}")


class Canvas:
    def __init__(self, w, h, fill=(0, 0, 0, 0)):
        self.w, self.h = w, h
        self.p = [[fill] * w for _ in range(h)]

    def px(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.p[y][x] = c

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.px(x, y, c)

    def save(self, path):
        write_png(path, self.w, self.h, self.p)


def shade(c, f):
    return (max(0, min(255, int(c[0] * f))), max(0, min(255, int(c[1] * f))),
            max(0, min(255, int(c[2] * f))), c[3])


def noise(x, y, mod=7):
    return (x * 31 + y * 17 + (x * y) % 13) % mod


# ---------------------------------------------------------------- tiles

T = 16
GRASS = (74, 143, 74, 255)
DARKG = (47, 107, 58, 255)
WATER = (42, 75, 160, 255)
ROAD = (176, 154, 104, 255)
COBBLE = (138, 138, 146, 255)
WALLC = (90, 90, 102, 255)
MINEF = (107, 95, 82, 255)
ROCKC = (74, 70, 64, 255)
SANDC = (203, 184, 126, 255)
OUT = (24, 26, 32, 255)


def tile_base(c, color, mod=8, dark=0.88, light=1.1):
    for y in range(T):
        for x in range(T):
            n = noise(x, y, mod)
            col = color
            if n == 0:
                col = shade(color, dark)
            elif n == 1:
                col = shade(color, light)
            c.px(x, y, col)


def draw_tile(idx):
    c = Canvas(T, T)
    if idx == 0:  # grass
        tile_base(c, GRASS)
        for x, y in ((3, 5), (11, 9), (7, 13)):
            c.px(x, y, shade(GRASS, 1.25))
            c.px(x, y - 1, shade(GRASS, 1.25))
    elif idx == 1:  # tree on grass
        tile_base(c, GRASS)
        trunk = (94, 66, 40, 255)
        canopy = (36, 92, 44, 255)
        c.rect(7, 11, 8, 14, trunk)
        for y in range(1, 11):
            half = 6 - abs(y - 6)
            c.rect(8 - half, y, 7 + half, y, canopy if noise(1, y, 3) else shade(canopy, 1.15))
        c.rect(4, 3, 6, 4, shade(canopy, 1.3))
    elif idx == 2:  # water
        tile_base(c, WATER, mod=6, dark=0.9, light=1.12)
        for x, y in ((2, 4), (3, 4), (9, 10), (10, 10), (12, 3)):
            c.px(x, y, shade(WATER, 1.45))
    elif idx == 3:  # road
        tile_base(c, ROAD, mod=5)
        for x, y in ((4, 3), (12, 7), (6, 12)):
            c.px(x, y, shade(ROAD, 0.75))
    elif idx == 4:  # town cobble
        tile_base(c, COBBLE, mod=9)
        for y in (7, 15):
            for x in range(T):
                c.px(x, y, shade(COBBLE, 0.8))
        for x in (3, 11):
            for y in range(0, 8):
                c.px(x, y, shade(COBBLE, 0.8))
        for x in (7, 15):
            for y in range(8, 16):
                c.px(x, y, shade(COBBLE, 0.8))
    elif idx == 5:  # wall bricks
        tile_base(c, WALLC, mod=11)
        for y in (0, 5, 10, 15):
            for x in range(T):
                c.px(x, y, shade(WALLC, 0.65))
        for row, off in ((1, 4), (6, 9), (11, 4)):
            for x in (off % T, (off + 8) % T):
                for y in range(row, min(row + 4, T)):
                    c.px(x, y, shade(WALLC, 0.65))
        for x in range(T):
            c.px(x, 1, shade(WALLC, 1.2))
    elif idx == 6:  # forest grass
        tile_base(c, DARKG)
        for x, y in ((5, 4), (12, 11)):
            c.px(x, y, shade(DARKG, 0.7))
    elif idx == 7:  # mine floor
        tile_base(c, MINEF, mod=6)
        for x, y in ((3, 9), (10, 4), (13, 13)):
            c.px(x, y, shade(MINEF, 0.7))
    elif idx == 8:  # rock
        tile_base(c, ROCKC, mod=5, dark=0.8, light=1.2)
        for i in range(4):
            x, y = (i * 5 + 2) % T, (i * 7 + 3) % T
            c.px(x, y, shade(ROCKC, 0.55))
            c.px(x + 1, y + 1, shade(ROCKC, 0.55))
    elif idx == 9:  # respawn crystal on cobble
        tile_base(c, COBBLE, mod=9)
        cry = (110, 220, 230, 255)
        for y in range(2, 13):
            half = (5 - abs(y - 7)) // 2 + 1
            c.rect(8 - half, y, 7 + half, y, cry if y % 3 else shade(cry, 1.25))
        c.px(6, 5, (240, 255, 255, 255))
        c.rect(5, 13, 10, 14, shade(COBBLE, 0.7))
    elif idx == 10:  # sand
        tile_base(c, SANDC, mod=5)
    elif idx == 11:  # flowers
        tile_base(c, GRASS)
        for (x, y), col in (((4, 4), (232, 90, 110, 255)), ((11, 6), (240, 220, 90, 255)),
                            ((7, 12), (150, 130, 240, 255))):
            c.px(x, y, col)
            c.px(x + 1, y, col)
            c.px(x, y + 1, shade(col, 0.8))
    return c


def gen_tiles():
    n = 12
    atlas = Canvas(T * n, T)
    for i in range(n):
        t = draw_tile(i)
        for y in range(T):
            for x in range(T):
                atlas.px(i * T + x, y, t.p[y][x])
    atlas.save(ROOT / "tiles.png")


# ---------------------------------------------------------------- units
# Sheets are 48x96: 3 walk frames (columns) x 4 directions (rows: down/up/left/right),
# one 16x24 cell per frame. Animation plays 0,1,2,1.

FW, FH = 16, 24
SKIN = (232, 190, 152, 255)


def draw_humanoid(c, ox, oy, direction, frame, hair, tunic, trim, legs):
    # legs (frame 0: left fwd, 1: stand, 2: right fwd)
    ly = oy + 19
    if frame == 1:
        c.rect(ox + 5, ly, ox + 6, ly + 3, legs)
        c.rect(ox + 9, ly, ox + 10, ly + 3, legs)
    elif frame == 0:
        c.rect(ox + 4, ly, ox + 5, ly + 3, legs)
        c.rect(ox + 9, ly + 1, ox + 10, ly + 3, legs)
    else:
        c.rect(ox + 5, ly + 1, ox + 6, ly + 3, legs)
        c.rect(ox + 10, ly, ox + 11, ly + 3, legs)
    # body
    c.rect(ox + 4, oy + 12, ox + 11, oy + 18, tunic)
    c.rect(ox + 4, oy + 12, ox + 11, oy + 13, shade(tunic, 1.2))
    c.rect(ox + 4, oy + 17, ox + 11, oy + 18, shade(tunic, 0.75))
    c.rect(ox + 7, oy + 14, ox + 8, oy + 16, trim)
    # arms
    if direction in (0, 1):
        c.rect(ox + 3, oy + 13, ox + 3, oy + 16, tunic)
        c.rect(ox + 12, oy + 13, ox + 12, oy + 16, tunic)
        c.px(ox + 3, oy + 17, SKIN)
        c.px(ox + 12, oy + 17, SKIN)
    # head block
    hx0, hx1 = ox + 4, ox + 11
    c.rect(hx0, oy + 4, hx1, oy + 11, SKIN)
    # hair cap
    c.rect(hx0, oy + 3, hx1, oy + 6, hair)
    c.px(hx0, oy + 7, hair)
    c.px(hx1, oy + 7, hair)
    if direction == 1:  # facing up: hair covers all
        c.rect(hx0, oy + 4, hx1, oy + 10, hair)
        c.rect(hx0 + 1, oy + 10, hx1 - 1, oy + 10, shade(hair, 0.8))
    elif direction == 0:  # down: two eyes
        eye = (30, 30, 44, 255)
        c.px(ox + 6, oy + 8, eye)
        c.px(ox + 9, oy + 8, eye)
        c.px(ox + 6, oy + 10, (200, 130, 120, 255))
        c.px(ox + 9, oy + 10, (200, 130, 120, 255))
    else:  # side: one eye toward facing
        eye = (30, 30, 44, 255)
        ex = ox + 5 if direction == 2 else ox + 10
        c.px(ex, oy + 8, eye)
        c.rect(hx0 if direction == 3 else ox + 9, oy + 4, hx1 if direction == 2 else hx0 + 2, oy + 9, hair) \
            if False else None
        # hair sweeps to the back of the head
        if direction == 2:
            c.rect(ox + 9, oy + 4, hx1, oy + 9, hair)
        else:
            c.rect(hx0, oy + 4, ox + 6, oy + 9, hair)
    # outline pass (cheap: darken border pixels already colored)


def gen_class_sheet(name, hair, tunic, trim, legs, hat=None):
    c = Canvas(FW * 3, FH * 4)
    for direction in range(4):
        for frame in range(3):
            ox, oy = frame * FW, direction * FH
            draw_humanoid(c, ox, oy, direction, frame, hair, tunic, trim, legs)
            if hat:  # mage's wide-brim hat
                c.rect(ox + 3, oy + 3, ox + 12, oy + 4, hat)
                c.rect(ox + 5, oy + 1, ox + 10, oy + 3, shade(hat, 1.15))
    c.save(ROOT / "units" / f"{name}.png")


def gen_wisp():
    c = Canvas(FW * 3, FH * 4)
    body = (120, 200, 96, 255)
    for direction in range(4):
        for frame in range(3):
            ox, oy = frame * FW, direction * FH
            bob = (0, 1, 0)[frame]
            cy = oy + 14 + bob
            for y in range(-4, 5):
                half = 5 - abs(y) // 2
                c.rect(ox + 8 - half, cy + y, ox + 7 + half, cy + y,
                       body if (y + bob) % 3 else shade(body, 1.15))
            leaf = (60, 140, 60, 255)
            c.rect(ox + 7, cy - 7, ox + 8, cy - 5, leaf)
            c.px(ox + 9, cy - 6, leaf)
            eye = (30, 30, 44, 255)
            if direction != 1:
                exl = ox + 6 if direction != 3 else ox + 8
                exr = ox + 10 if direction != 2 else ox + 8
                c.px(exl, cy - 1, eye)
                c.px(exr, cy - 1, eye)
    c.save(ROOT / "units" / "leaf_wisp.png")


def gen_wolf():
    c = Canvas(FW * 3, FH * 4)
    fur = (122, 88, 58, 255)
    dark = shade(fur, 0.7)
    for direction in range(4):
        for frame in range(3):
            ox, oy = frame * FW, direction * FH
            flip = direction == 2  # left
            def X(x):
                return ox + (15 - x if flip else x)
            by = oy + 13
            for x in range(2, 13):
                c.rect(X(x), by, X(x), by + 4, fur)
            c.rect(X(11), by - 3, X(14), by + 1, fur)      # head
            c.px(X(12), by - 4, dark)                       # ear
            c.px(X(14), by - 2, (30, 30, 44, 255))          # eye
            c.px(X(15), by, dark)                           # snout
            c.rect(X(2), by - 1, X(3), by + 1, dark)        # tail
            lo = (0, 1, 0)[frame]
            c.rect(X(4), by + 5, X(4), by + 7 + lo, dark)   # legs
            c.rect(X(7), by + 5, X(7), by + 7 - lo, dark)
            c.rect(X(10), by + 5, X(10), by + 7 + lo, dark)
            c.rect(X(12), by + 5, X(12), by + 7 - lo, dark)
    c.save(ROOT / "units" / "bramble_wolf.png")


def gen_golem():
    c = Canvas(FW * 3, FH * 4)
    stone = (128, 116, 104, 255)
    rust = (168, 92, 48, 255)
    for direction in range(4):
        for frame in range(3):
            ox, oy = frame * FW, direction * FH
            sway = (-1, 0, 1)[frame]
            c.rect(ox + 4, oy + 2, ox + 11, oy + 7, stone)            # head
            c.rect(ox + 2, oy + 8, ox + 13, oy + 17, stone)           # torso
            c.rect(ox + 2, oy + 8, ox + 13, oy + 9, shade(stone, 1.2))
            c.rect(ox + 5, oy + 11, ox + 10, oy + 14, rust)           # rusted core
            c.rect(ox + 1 + sway, oy + 9, ox + 2 + sway, oy + 16, shade(stone, 0.8))   # arms
            c.rect(ox + 13 - sway, oy + 9, ox + 14 - sway, oy + 16, shade(stone, 0.8))
            c.rect(ox + 4, oy + 18, ox + 6, oy + 22, shade(stone, 0.7))                # legs
            c.rect(ox + 9, oy + 18, ox + 11, oy + 22, shade(stone, 0.7))
            if direction != 1:
                glow = (255, 210, 80, 255)
                c.px(ox + 6, oy + 4, glow)
                c.px(ox + 9, oy + 4, glow)
    c.save(ROOT / "units" / "rust_golem.png")


# ---------------------------------------------------------------- ui

def gen_bat():
    c = Canvas(FW * 3, FH * 4)
    body = (90, 74, 110, 255)
    wing = (60, 48, 78, 255)
    for direction in range(4):
        for frame in range(3):
            ox, oy = frame * FW, direction * FH
            flap = (2, 0, 2)[frame]
            cy = oy + 12
            c.rect(ox + 6, cy - 2, ox + 9, cy + 2, body)          # body
            c.rect(ox + 2, cy - flap, ox + 5, cy + 1 - flap, wing)  # wings
            c.rect(ox + 10, cy - flap, ox + 13, cy + 1 - flap, wing)
            c.px(ox + 6, cy - 3, body)                             # ears
            c.px(ox + 9, cy - 3, body)
            if direction != 1:
                eye = (255, 90, 90, 255)
                c.px(ox + 6 if direction != 3 else ox + 8, cy - 1, eye)
                c.px(ox + 9 if direction != 2 else ox + 7, cy - 1, eye)
    c.save(ROOT / "units" / "cave_bat.png")


def gen_slime():
    c = Canvas(FW * 3, FH * 4)
    goo = (110, 128, 150, 255)
    ore = (196, 160, 80, 255)
    for direction in range(4):
        for frame in range(3):
            ox, oy = frame * FW, direction * FH
            squish = (1, 0, 1)[frame]
            top = oy + 10 + squish
            for y in range(top, oy + 20):
                spread = min(6, (y - top) + 3 + squish)
                c.rect(ox + 8 - spread, y, ox + 7 + spread, y,
                       goo if y % 3 else shade(goo, 1.15))
            c.px(ox + 5, oy + 14, ore)                             # ore flecks
            c.px(ox + 10, oy + 16, ore)
            c.px(ox + 8, oy + 12, shade(ore, 1.2))
            if direction != 1:
                eye = (30, 30, 44, 255)
                c.px(ox + 6, oy + 13, eye)
                c.px(ox + 9, oy + 13, eye)
    c.save(ROOT / "units" / "ore_slime.png")


def gen_warden():
    # The boss reuses the golem silhouette, bigger presence via palette:
    # gunmetal plate, magitek-teal core lines, ember eyes.
    c = Canvas(FW * 3, FH * 4)
    plate = (96, 104, 122, 255)
    core = (80, 220, 200, 255)
    for direction in range(4):
        for frame in range(3):
            ox, oy = frame * FW, direction * FH
            sway = (-1, 0, 1)[frame]
            c.rect(ox + 3, oy + 1, ox + 12, oy + 7, plate)                  # head
            c.rect(ox + 1, oy + 8, ox + 14, oy + 17, plate)                 # torso
            c.rect(ox + 1, oy + 8, ox + 14, oy + 9, shade(plate, 1.25))
            c.rect(ox + 6, oy + 10, ox + 9, oy + 15, shade(plate, 0.7))
            c.rect(ox + 7, oy + 10, ox + 8, oy + 15, core)                  # core column
            c.rect(ox + 0 + sway, oy + 9, ox + 1 + sway, oy + 17, shade(plate, 0.8))
            c.rect(ox + 14 - sway, oy + 9, ox + 15 - sway, oy + 17, shade(plate, 0.8))
            c.rect(ox + 3, oy + 18, ox + 6, oy + 23, shade(plate, 0.65))
            c.rect(ox + 9, oy + 18, ox + 12, oy + 23, shade(plate, 0.65))
            if direction != 1:
                ember = (255, 140, 60, 255)
                c.px(ox + 5, oy + 4, ember)
                c.px(ox + 10, oy + 4, ember)
    c.save(ROOT / "units" / "magitek_warden.png")


def gen_npcs():
    # Vendor: warm apron colors. Elder: grey hair, deep-green robe.
    gen_class_sheet("npc_vendor", (60, 40, 28, 255), (150, 92, 60, 255),
                    (232, 220, 200, 255), (80, 60, 50, 255))
    gen_class_sheet("npc_elder", (208, 208, 208, 255), (44, 84, 60, 255),
                    (200, 180, 110, 255), (40, 56, 48, 255))


def gen_panel():
    # FF6-style window: near-black outer line, white double border,
    # vertical navy gradient fill. 24x24, use as 9-patch with 4px margins.
    s = 24
    c = Canvas(s, s)
    top = (48, 58, 112, 255)
    bottom = (16, 22, 56, 255)
    for y in range(s):
        f = y / (s - 1)
        col = tuple(int(top[i] + (bottom[i] - top[i]) * f) for i in range(3)) + (255,)
        c.rect(0, y, s - 1, y, col)
    white = (238, 238, 230, 255)
    for i in (1, 2):
        c.rect(i, i, s - 1 - i, i, white)
        c.rect(i, s - 1 - i, s - 1 - i, s - 1 - i, white)
        c.rect(i, i, i, s - 1 - i, white)
        c.rect(s - 1 - i, i, s - 1 - i, s - 1 - i, white)
    c.rect(0, 0, s - 1, 0, OUT)
    c.rect(0, s - 1, s - 1, s - 1, OUT)
    c.rect(0, 0, 0, s - 1, OUT)
    c.rect(s - 1, 0, s - 1, s - 1, OUT)
    c.save(ROOT / "ui" / "panel.png")


if __name__ == "__main__":
    gen_tiles()
    for args in (
        ("squire", (196, 150, 70, 255), (58, 90, 168, 255), (216, 180, 90, 255), (70, 62, 58, 255), None),
        ("mage", (120, 80, 160, 255), (106, 58, 154, 255), (230, 210, 120, 255), (50, 40, 70, 255), (74, 40, 110, 255)),
        ("scout", (90, 60, 36, 255), (58, 122, 74, 255), (150, 110, 70, 255), (60, 70, 50, 255), None),
    ):
        gen_class_sheet(*args)
    gen_wisp()
    gen_wolf()
    gen_golem()
    gen_bat()
    gen_slime()
    gen_warden()
    gen_npcs()
    gen_panel()
