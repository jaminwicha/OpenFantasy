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

# ---------------------------------------------------------------- constants

FW, FH = 16, 24
T = 16
OUT = (24, 26, 32, 255)


def auto_outline(c, color=(36, 30, 44, 255)):
    """1px outline drawn into transparent pixels adjacent to opaque ones."""
    edges = []
    for y in range(c.h):
        for x in range(c.w):
            if c.p[y][x][3] == 0:
                if any(0 <= x + dx < c.w and 0 <= y + dy < c.h and c.p[y + dy][x + dx][3] != 0
                       and c.p[y + dy][x + dx] != color
                       for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    edges.append((x, y))
    for x, y in edges:
        c.p[y][x] = color


# ---------------------------------------------------------------- tiles v2
# Atlas: 16 cols x 4 rows of 16px tiles.
# Row 0: grass x3, tree, water x3, road x3, cobble x2, wall, forest x3
# Row 1: mine x3, rock x2, crystal, sand x3, flowers x2, forge
# Row 2: water-foam edge masks 0..15 (bit: 1=N 2=E 4=S 8=W, land on that side)
# Row 3: grass-fringe edge masks 0..15 (grass hangs over this tile's edges)

GRASS = (92, 158, 82)
DARKG = (56, 116, 66)
WATER = (46, 82, 168)
ROAD = (188, 164, 116)
COBBLE = (146, 146, 156)
WALLC = (98, 96, 110)
MINEF = (118, 102, 84)
ROCKC = (86, 82, 78)
SANDC = (216, 196, 138)


def rgba(c):
    return (c[0], c[1], c[2], 255)


def cluster(x, y, salt=0):
    """Blobby deterministic noise 0..8 (adjacent cells correlate)."""
    h = (x * 3 + salt * 17) // 2 * 7 + (y * 3 + salt * 5) // 2 * 13
    return (h ^ (h >> 3)) % 9


def ground(tc, base, variant, blades=None, litter=None):
    """3-tone clustered ground fill."""
    lo, mid, hi = shade(rgba(base), 0.82), rgba(base), shade(rgba(base), 1.16)
    for y in range(T):
        for x in range(T):
            n = cluster(x, y, salt=variant * 31)
            tc.px(x, y, lo if n < 2 else hi if n > 6 else mid)
    if blades:
        for i in range(4):
            bx = (variant * 5 + i * 7 + 3) % (T - 2) + 1
            by = (variant * 11 + i * 5 + 2) % (T - 3) + 1
            tc.px(bx, by, blades)
            tc.px(bx, by - 1, blades)
            tc.px(bx + 1, by, shade(blades, 0.8))
    if litter:
        for i in range(3):
            bx = (variant * 7 + i * 11 + 5) % (T - 2) + 1
            by = (variant * 13 + i * 3 + 4) % (T - 2) + 1
            tc.px(bx, by, litter)
    return tc


def tile_grass(v):
    c = Canvas(T, T)
    ground(c, GRASS, v, blades=shade(rgba(GRASS), 1.35))
    return c


def tile_forest(v):
    c = Canvas(T, T)
    ground(c, DARKG, v, blades=shade(rgba(DARKG), 1.3), litter=shade(rgba(DARKG), 0.62))
    return c


def tile_sand(v):
    c = Canvas(T, T)
    ground(c, SANDC, v)
    # ripple lines
    for ry in ((3 + v * 4) % 10 + 2, (9 + v * 4) % 10 + 4):
        for x in range(1 + v, T - 1, 4):
            c.px(x, ry, shade(rgba(SANDC), 0.85))
            c.px(x + 1, ry, shade(rgba(SANDC), 0.85))
    return c


def tile_water(v):
    c = Canvas(T, T)
    lo, mid, hi = shade(rgba(WATER), 0.85), rgba(WATER), shade(rgba(WATER), 1.18)
    for y in range(T):
        for x in range(T):
            n = cluster(x, y, salt=v * 47 + 9)
            c.px(x, y, lo if n < 2 else mid)
    # wave crests
    for i in range(3):
        wy = (i * 5 + v * 3 + 2) % (T - 2) + 1
        wx = (i * 7 + v * 5 + 1) % (T - 6) + 1
        for k in range(4):
            c.px(wx + k, wy + (1 if k in (0, 3) else 0), hi)
    if v == 2:  # sparkle variant
        c.px(4, 4, (210, 230, 250, 255))
        c.px(11, 10, (210, 230, 250, 255))
    return c


def tile_road(v):
    c = Canvas(T, T)
    ground(c, ROAD, v)
    # wheel ruts
    for ry in (5, 10):
        for x in range(T):
            if (x + v) % 5 != 0:
                c.px(x, ry, shade(rgba(ROAD), 0.88))
    # pebbles
    for i in range(3):
        bx = (v * 9 + i * 6 + 2) % (T - 2) + 1
        by = (v * 3 + i * 9 + 3) % (T - 2) + 1
        c.px(bx, by, shade(rgba(ROAD), 0.7))
        c.px(bx + 1, by, shade(rgba(ROAD), 1.12))
    return c


def tile_cobble(v):
    c = Canvas(T, T)
    lo, mid, hi = shade(rgba(COBBLE), 0.8), rgba(COBBLE), shade(rgba(COBBLE), 1.14)
    for y in range(T):
        for x in range(T):
            c.px(x, y, mid if cluster(x, y, v * 3) > 1 else hi)
    # coursing: two rows of stones, offset per variant
    for row, y0 in enumerate((0, 8)):
        off = (row * 4 + v * 4) % 8
        for y in (y0 + 7, ):
            for x in range(T):
                c.px(x, y % T, lo)
        for x in range(off, T + 8, 8):
            for y in range(y0, y0 + 7):
                c.px(x % T, y, lo)
        for x in range(off + 1, T, 8):  # stone top highlight
            c.px(x % T, y0, hi)
    return c


def tile_wall():
    c = Canvas(T, T)
    lo, mid, hi = shade(rgba(WALLC), 0.62), rgba(WALLC), shade(rgba(WALLC), 1.22)
    for y in range(T):
        for x in range(T):
            c.px(x, y, mid if cluster(x, y, 5) > 1 else shade(rgba(WALLC), 1.08))
    for y in (0, 5, 10, 15):
        for x in range(T):
            c.px(x, y, lo)
    for row, off in ((1, 4), (6, 9), (11, 4)):
        for x in (off, (off + 8) % T):
            for y in range(row, min(row + 4, T)):
                c.px(x, y, lo)
    for x in range(T):
        c.px(x, 1, hi)  # top course catches light
    return c


def tile_mine(v):
    c = Canvas(T, T)
    ground(c, MINEF, v, litter=shade(rgba(MINEF), 0.7))
    # support plank shadow
    if v == 1:
        for x in range(T):
            c.px(x, 13, shade(rgba(MINEF), 0.78))
    return c


def tile_rock(v):
    c = Canvas(T, T)
    lo, mid, hi = shade(rgba(ROCKC), 0.7), rgba(ROCKC), shade(rgba(ROCKC), 1.25)
    for y in range(T):
        for x in range(T):
            n = cluster(x, y, v * 13 + 3)
            c.px(x, y, lo if n < 2 else mid)
    # facets: light top-left edges of blobs
    for i in range(4):
        fx = (v * 7 + i * 5 + 1) % (T - 4) + 1
        fy = (v * 5 + i * 7 + 2) % (T - 4) + 1
        c.px(fx, fy, hi)
        c.px(fx + 1, fy, hi)
        c.px(fx, fy + 1, mid)
        c.px(fx + 2, fy + 2, lo)
    return c


def tile_tree():
    c = tile_grass(0)
    trunk = (98, 68, 42, 255)
    canopy = (44, 104, 54, 255)
    c.rect(7, 11, 8, 14, trunk)
    c.px(7, 11, shade(trunk, 1.2))
    for y in range(1, 11):
        half = 6 - abs(y - 5) // 1 if y < 9 else 3
        half = min(6, half)
        c.rect(8 - half, y, 7 + half, y, canopy)
    for xx, yy in ((4, 3), (5, 2), (6, 4), (9, 3)):
        c.px(xx, yy, shade(canopy, 1.35))
    for xx, yy in ((10, 7), (11, 8), (5, 8), (9, 9)):
        c.px(xx, yy, shade(canopy, 0.72))
    return c


def tile_crystal():
    c = tile_cobble(0)
    cry = (110, 225, 235, 255)
    for y in range(2, 13):
        half = (5 - abs(y - 7)) // 2 + 1
        c.rect(8 - half, y, 7 + half, y, cry if y % 3 else shade(cry, 1.2))
    c.px(6, 4, (245, 255, 255, 255))
    c.px(8, 7, shade(cry, 0.8))
    c.rect(5, 13, 10, 14, shade(rgba(COBBLE), 0.7))
    return c


def tile_flowers(v):
    c = tile_grass(v)
    petals = (((236, 96, 116), (244, 224, 96)), ((160, 132, 244), (240, 240, 240)))[v]
    for i, col in enumerate(petals * 2):
        bx = (v * 3 + i * 5 + 2) % (T - 3) + 1
        by = (v * 7 + i * 7 + 3) % (T - 3) + 1
        c.px(bx, by, rgba(col))
        c.px(bx + 1, by, rgba(col))
        c.px(bx, by + 1, shade(rgba(col), 0.78))
        c.px(bx + 1, by - 1, shade(rgba(col), 1.1))
    return c


def tile_forge():
    c = tile_cobble(1)
    iron = (70, 70, 80, 255)
    # furnace block with glowing mouth + anvil horn
    c.rect(3, 4, 12, 12, iron)
    c.rect(3, 4, 12, 5, shade(iron, 1.3))
    c.rect(5, 8, 10, 11, (52, 42, 46, 255))
    c.rect(6, 9, 9, 10, (240, 140, 50, 255))
    c.px(7, 9, (255, 210, 90, 255))
    c.rect(2, 13, 13, 14, shade(iron, 0.75))
    c.px(1, 13, shade(iron, 0.75))  # anvil horn
    return c


def tile_foam(mask):
    """Transparent overlay: foam scallops on edges facing land (bit set)."""
    c = Canvas(T, T)
    foam = (222, 238, 250, 255)
    lite = (150, 190, 235, 255)
    if mask & 1:   # N
        for x in range(T):
            c.px(x, 0, foam)
            if x % 3 != 2:
                c.px(x, 1, lite)
    if mask & 4:   # S
        for x in range(T):
            c.px(x, 15, foam)
            if x % 3 != 1:
                c.px(x, 14, lite)
    if mask & 2:   # E
        for y in range(T):
            c.px(15, y, foam)
            if y % 3 != 2:
                c.px(14, y, lite)
    if mask & 8:   # W
        for y in range(T):
            c.px(0, y, foam)
            if y % 3 != 1:
                c.px(1, y, lite)
    return c


def tile_fringe(mask):
    """Transparent overlay: grass tufts hanging over a non-grass tile's edges."""
    c = Canvas(T, T)
    g1, g2 = rgba(GRASS), shade(rgba(GRASS), 0.8)
    if mask & 1:
        for x in range(T):
            c.px(x, 0, g1)
            if x % 3 == 1:
                c.px(x, 1, g2)
    if mask & 4:
        for x in range(T):
            c.px(x, 15, g1)
            if x % 3 == 0:
                c.px(x, 14, g2)
    if mask & 2:
        for y in range(T):
            c.px(15, y, g1)
            if y % 3 == 1:
                c.px(14, y, g2)
    if mask & 8:
        for y in range(T):
            c.px(0, y, g1)
            if y % 3 == 0:
                c.px(1, y, g2)
    return c


def gen_tiles():
    cols, rows = 16, 4
    atlas = Canvas(T * cols, T * rows)

    def put(col, row, tc):
        for y in range(T):
            for x in range(T):
                atlas.px(col * T + x, row * T + y, tc.p[y][x])

    row0 = [tile_grass(0), tile_grass(1), tile_grass(2), tile_tree(),
            tile_water(0), tile_water(1), tile_water(2),
            tile_road(0), tile_road(1), tile_road(2),
            tile_cobble(0), tile_cobble(1), tile_wall(),
            tile_forest(0), tile_forest(1), tile_forest(2)]
    row1 = [tile_mine(0), tile_mine(1), tile_mine(2), tile_rock(0), tile_rock(1),
            tile_crystal(), tile_sand(0), tile_sand(1), tile_sand(2),
            tile_flowers(0), tile_flowers(1), tile_forge()]
    for i, tc in enumerate(row0):
        put(i, 0, tc)
    for i, tc in enumerate(row1):
        put(i, 1, tc)
    for m in range(16):
        put(m, 2, tile_foam(m))
        put(m, 3, tile_fringe(m))
    atlas.save(ROOT / "tiles.png")


# ---------------------------------------------------------------- resource nodes

def gen_oak():
    c = Canvas(FW * 3, FH * 4)
    trunk = (104, 72, 44, 255)
    canopy = (52, 118, 62, 255)
    for direction in range(4):
        for frame in range(3):
            ox, oy = frame * FW, direction * FH
            sway = (-1, 0, 1)[frame] if frame != 1 else 0
            c.rect(ox + 6, oy + 15, ox + 9, oy + 22, trunk)
            c.px(ox + 6, oy + 15, shade(trunk, 1.25))
            c.px(ox + 9, oy + 21, shade(trunk, 0.75))
            c.rect(ox + 4, oy + 20, ox + 5, oy + 21, shade(trunk, 0.8))  # root
            for y in range(2, 15):
                half = 7 - abs(y - 8) * 2 // 3
                c.rect(ox + 8 - half + sway, oy + y, ox + 7 + half + sway, oy + y, canopy)
            for xx, yy in ((5, 4), (7, 3), (10, 5), (4, 7)):
                c.px(ox + xx + sway, oy + yy, shade(canopy, 1.35))
            for xx, yy in ((10, 11), (6, 12), (11, 9)):
                c.px(ox + xx + sway, oy + yy, shade(canopy, 0.7))
    auto_outline(c)
    c.save(ROOT / "units" / "node_oak.png")


def gen_ore_vein():
    c = Canvas(FW * 3, FH * 4)
    rock = (96, 90, 86, 255)
    ore = (216, 168, 74, 255)
    for direction in range(4):
        for frame in range(3):
            ox, oy = frame * FW, direction * FH
            glint = frame == 1
            c.rect(ox + 3, oy + 12, ox + 12, oy + 21, rock)
            c.rect(ox + 4, oy + 10, ox + 11, oy + 11, rock)
            c.rect(ox + 4, oy + 10, ox + 11, oy + 10, shade(rock, 1.25))
            c.px(ox + 3, oy + 12, shade(rock, 1.2))
            c.rect(ox + 3, oy + 21, ox + 12, oy + 21, shade(rock, 0.72))
            for xx, yy in ((5, 14), (8, 16), (10, 13), (6, 19)):
                c.px(ox + xx, oy + yy, ore)
                c.px(ox + xx + 1, oy + yy, shade(ore, 0.8))
                if glint:
                    c.px(ox + xx, oy + yy - 1, (255, 240, 170, 255))
    auto_outline(c)
    c.save(ROOT / "units" / "node_ore.png")


def gen_berry_bush():
    c = Canvas(FW * 3, FH * 4)
    leaf = (58, 128, 64, 255)
    berry = (208, 68, 92, 255)
    for direction in range(4):
        for frame in range(3):
            ox, oy = frame * FW, direction * FH
            b = (0, 1, 0)[frame]
            for y in range(13, 22):
                half = 6 - abs(y - 17) // 2
                c.rect(ox + 8 - half, oy + y, ox + 7 + half, oy + y, leaf)
            c.px(ox + 5, oy + 14, shade(leaf, 1.3))
            c.px(ox + 9, oy + 13, shade(leaf, 1.3))
            c.px(ox + 10, oy + 20, shade(leaf, 0.7))
            for i, (xx, yy) in enumerate(((5, 16), (8, 15), (10, 18), (6, 19))):
                c.px(ox + xx, oy + yy + (b if i % 2 else 0), berry)
                c.px(ox + xx, oy + yy - 1 + (b if i % 2 else 0), shade(berry, 1.25))
    auto_outline(c)
    c.save(ROOT / "units" / "node_berry.png")




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



# ---------------------------------------------------------------- outlined monsters

def outlined(gen):
    """Wraps a gen_* fn: no-op marker (each gen applies auto_outline itself now)."""
    return gen


if __name__ == "__main__":
    gen_tiles()
    for gen in (gen_wisp, gen_wolf, gen_golem, gen_bat, gen_slime, gen_warden):
        gen()
    gen_oak()
    gen_ore_vein()
    gen_berry_bush()
    gen_panel()
