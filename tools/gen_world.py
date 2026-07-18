#!/usr/bin/env python3
"""Deterministically authors data/world.json — all of Aldermere (M4).

512x512 tiles, four continents in an ocean:
  Verdance (SW): grass frontier — Brasshollow, Castle Aldermere, Hollow Mine
  Kharzum (NE):  snow + mountains — Gearford, Emberdeep
  Sylvas (SE):   forest + swamp — Sylvanglade, Sunken Crypt
  Miradel (NW):  desert — Willowick, Storm Spire

Run: python3 tools/gen_world.py
"""
import json
import math
import random
from pathlib import Path

W, H = 512, 512
rng = random.Random(60407)  # fixed seed: layout is authored content

(GRASS, TREE, SHALLOW, ROAD, COBBLE, WALL, FOREST, MINEFLOOR, ROCK, CRYSTAL,
 SAND, FLOWERS, FORGE, OCEAN, MOUNTAIN, HILLS, SNOW, DESERT, SWAMP, BRIDGE,
 CASTLE, TOWERF, DOCK, DEADTREE, PLANK, COUNTER, FENCE, FARMLAND) = range(28)

TILESET = [
    {"id": GRASS, "name": "grass", "walkable": True},
    {"id": TREE, "name": "tree", "walkable": False},
    {"id": SHALLOW, "name": "shallow", "walkable": False},
    {"id": ROAD, "name": "road", "walkable": True},
    {"id": COBBLE, "name": "town_floor", "walkable": True},
    {"id": WALL, "name": "wall", "walkable": False},
    {"id": FOREST, "name": "forest_grass", "walkable": True},
    {"id": MINEFLOOR, "name": "mine_floor", "walkable": True},
    {"id": ROCK, "name": "rock", "walkable": False},
    {"id": CRYSTAL, "name": "crystal", "walkable": False},
    {"id": SAND, "name": "sand", "walkable": True},
    {"id": FLOWERS, "name": "flowers", "walkable": True},
    {"id": FORGE, "name": "forge", "walkable": False},
    {"id": OCEAN, "name": "ocean", "walkable": False},
    {"id": MOUNTAIN, "name": "mountain", "walkable": False},
    {"id": HILLS, "name": "hills", "walkable": False},
    {"id": SNOW, "name": "snow", "walkable": True},
    {"id": DESERT, "name": "desert", "walkable": True},
    {"id": SWAMP, "name": "swamp", "walkable": True},
    {"id": BRIDGE, "name": "bridge", "walkable": True},
    {"id": CASTLE, "name": "castle_floor", "walkable": True},
    {"id": TOWERF, "name": "tower_floor", "walkable": True},
    {"id": DOCK, "name": "dock", "walkable": True},
    {"id": DEADTREE, "name": "dead_tree", "walkable": False},
    {"id": PLANK, "name": "plank_floor", "walkable": True},
    {"id": COUNTER, "name": "counter", "walkable": False},
    {"id": FENCE, "name": "fence", "walkable": False},
    {"id": FARMLAND, "name": "farmland", "walkable": True},
]

WALKABLE = {t["id"] for t in TILESET if t["walkable"]}
GROUND_OPEN = {GRASS, FOREST, FLOWERS, SNOW, DESERT, SWAMP, SAND}

grid = [[OCEAN] * W for _ in range(H)]


def fill(x0, y0, x1, y1, t):
    for y in range(max(0, y0), min(H, y1 + 1)):
        for x in range(max(0, x0), min(W, x1 + 1)):
            grid[y][x] = t


def wobble(cx, cy, x, y, seed):
    ang = math.atan2(y - cy, x - cx)
    return (math.sin(ang * 3 + seed) + math.sin(ang * 7 + seed * 2.3)
            + math.sin(ang * 11 + seed * 4.1)) / 3.0


# ------------------------------------------------------------------ continents

CONTINENTS = {
    "verdance": {"cx": 145, "cy": 360, "rx": 118, "ry": 105, "seed": 1.7, "base": GRASS},
    "kharzum": {"cx": 368, "cy": 138, "rx": 112, "ry": 100, "seed": 3.9, "base": SNOW},
    "sylvas": {"cx": 382, "cy": 382, "rx": 100, "ry": 92, "seed": 6.1, "base": FOREST},
    "miradel": {"cx": 130, "cy": 130, "rx": 100, "ry": 88, "seed": 8.3, "base": DESERT},
}

for name, c in CONTINENTS.items():
    for y in range(H):
        for x in range(W):
            dx = (x - c["cx"]) / c["rx"]
            dy = (y - c["cy"]) / c["ry"]
            d = math.sqrt(dx * dx + dy * dy)
            if d < 0.92 + 0.14 * wobble(c["cx"], c["cy"], x, y, c["seed"]):
                grid[y][x] = c["base"]

# beaches + shallow ring
land = [[grid[y][x] != OCEAN for x in range(W)] for y in range(H)]
for y in range(H):
    for x in range(W):
        if land[y][x]:
            edge = any(0 <= x + dx < W and 0 <= y + dy < H and not land[y + dy][x + dx]
                       for dx in (-2, -1, 0, 1, 2) for dy in (-2, -1, 0, 1, 2))
            if edge:
                grid[y][x] = SAND
        else:
            near = any(0 <= x + dx < W and 0 <= y + dy < H and land[y + dy][x + dx]
                       for dx in range(-4, 5) for dy in range(-4, 5))
            if near:
                grid[y][x] = SHALLOW

# ------------------------------------------------------------------ biome interiors


def biome_scatter(x0, y0, x1, y1, base, feature, chance, only=None):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if 0 <= x < W and 0 <= y < H and grid[y][x] == (only or base) and rng.random() < chance:
                grid[y][x] = feature


# Verdance: forest belts + flowers + hills east + rock knot for the mine
fill(175, 330, 235, 420, FOREST)
for y in range(300, 420):
    for x in range(80, 235):
        if grid[y][x] == GRASS and rng.random() < 0.05:
            grid[y][x] = TREE
        elif grid[y][x] == GRASS and rng.random() < 0.03:
            grid[y][x] = FLOWERS
        elif grid[y][x] == FOREST and rng.random() < 0.2:
            grid[y][x] = TREE
fill(196, 378, 226, 404, HILLS)
fill(200, 382, 222, 400, ROCK)

# Kharzum: mountain core, hills ring, snow plains, scattered rocks
kc = CONTINENTS["kharzum"]
for y in range(H):
    for x in range(W):
        if grid[y][x] == SNOW:
            dx = (x - kc["cx"]) / kc["rx"]
            dy = (y - kc["cy"]) / kc["ry"]
            d = math.sqrt(dx * dx + dy * dy)
            if d < 0.34:
                grid[y][x] = MOUNTAIN
            elif d < 0.48:
                grid[y][x] = HILLS
            elif rng.random() < 0.02:
                grid[y][x] = ROCK
            elif rng.random() < 0.02:
                grid[y][x] = TREE

# Sylvas: dense trees + south swamp with dead trees
sc = CONTINENTS["sylvas"]
for y in range(H):
    for x in range(W):
        if grid[y][x] == FOREST and abs(x - sc["cx"]) < 110 and abs(y - sc["cy"]) < 100:
            if y > sc["cy"] + 30:
                grid[y][x] = SWAMP
            elif rng.random() < 0.22:
                grid[y][x] = TREE
for y in range(H):
    for x in range(W):
        if grid[y][x] == SWAMP and rng.random() < 0.06:
            grid[y][x] = DEADTREE

# Miradel: dune hills + oasis greenery + rocks
mc = CONTINENTS["miradel"]
for y in range(H):
    for x in range(W):
        if grid[y][x] == DESERT:
            if rng.random() < 0.025:
                grid[y][x] = HILLS
            elif rng.random() < 0.012:
                grid[y][x] = ROCK
fill(112, 122, 136, 148, GRASS)  # oasis heart (town goes on top)
biome_scatter(108, 118, 142, 152, GRASS, FLOWERS, 0.06)

# ------------------------------------------------------------------ rivers


def carve_river(x0, y0, x1, y1, wobble_seed):
    steps = max(abs(x1 - x0), abs(y1 - y0)) * 2
    for i in range(steps + 1):
        t = i / steps
        x = x0 + (x1 - x0) * t + 6 * math.sin(t * 9 + wobble_seed)
        y = y0 + (y1 - y0) * t + 6 * math.cos(t * 7 + wobble_seed)
        for dy in (0, 1):
            for dx in (0, 1):
                xi, yi = int(x) + dx, int(y) + dy
                if 0 <= xi < W and 0 <= yi < H and grid[yi][xi] not in (OCEAN, MOUNTAIN, WALL):
                    grid[yi][xi] = SHALLOW


carve_river(150, 300, 92, 262, 1.0)    # Verdance river to the north coast
carve_river(352, 200, 332, 236, 2.5)   # Kharzum meltwater south
carve_river(350, 340, 300, 386, 4.2)   # Sylvas river to the west coast

# ------------------------------------------------------------------ towns / compounds

TOWNS = {
    # name: (cx, cy, half_w, half_h, floor)
    "brasshollow": (110, 352, 11, 9, COBBLE),
    "gearford": (360, 140, 11, 9, COBBLE),
    "sylvanglade": (372, 356, 11, 9, FOREST),
    "willowick": (122, 136, 10, 8, SAND),
}

CRYSTALS = []
SAFE_ZONES = []
REGIONS = {}


def build_walled(cx, cy, hw, hh, floor, gates=("n", "s", "e", "w"), wall=WALL):
    x0, y0, x1, y1 = cx - hw, cy - hh, cx + hw, cy + hh
    fill(x0, y0, x1, y1, floor)
    for x in range(x0, x1 + 1):
        grid[y0][x] = wall
        grid[y1][x] = wall
    for y in range(y0, y1 + 1):
        grid[y][x0] = wall
        grid[y][x1] = wall
    if "n" in gates:
        for gx in (cx - 1, cx, cx + 1):
            grid[y0][gx] = floor
    if "s" in gates:
        for gx in (cx - 1, cx, cx + 1):
            grid[y1][gx] = floor
    if "e" in gates:
        for gy in (cy - 1, cy, cy + 1):
            grid[gy][x1] = floor
    if "w" in gates:
        for gy in (cy - 1, cy, cy + 1):
            grid[gy][x0] = floor
    return x0, y0, x1, y1


META = {"towns": {}, "farms": [], "homeless": []}


def build_building(town, kind, x0, y0, x1, y1, floor, door_side="s"):
    """Hollow building: wall shell, interior floor, one door gap. Returns meta record."""
    fill(x0, y0, x1, y1, floor)
    for x in range(x0, x1 + 1):
        grid[y0][x] = WALL
        grid[y1][x] = WALL
    for y in range(y0, y1 + 1):
        grid[y][x0] = WALL
        grid[y][x1] = WALL
    mx, my = (x0 + x1) // 2, (y0 + y1) // 2
    if door_side == "s":
        door = (mx, y1)
    elif door_side == "n":
        door = (mx, y0)
    elif door_side == "e":
        door = (x1, my)
    else:
        door = (x0, my)
    grid[door[1]][door[0]] = floor
    return {"kind": kind, "rect": [x0, y0, x1, y1], "door": list(door), "anchor": [mx, my], "floor": floor}


def build_town(name, cx, cy, floor):
    """Town v3: 33x27 walls, plaza, tavern, shops, houses by wealth."""
    hw, hh = 16, 13
    x0, y0, x1, y1 = build_walled(cx, cy, hw, hh, floor)
    buildings = []

    # Tavern (NW, plank floor, counter along its north wall pocket)
    tav = build_building(name, "tavern", cx - 15, cy - 12, cx - 6, cy - 6, PLANK, "s")
    for bx in range(cx - 13, cx - 8):
        grid[cy - 10][bx] = COUNTER            # the bar
    tav["bar"] = [cx - 11, cy - 11]            # bartender pocket behind the counter
    buildings.append(tav)

    # Shops (NE + W): weapon/armor row and goods row
    buildings.append(build_building(name, "shop_arms", cx + 6, cy - 12, cx + 11, cy - 8, PLANK, "s"))
    buildings.append(build_building(name, "shop_goods", cx - 15, cy - 3, cx - 10, cy + 1, PLANK, "e"))
    grid[cy - 11][cx + 8] = COUNTER
    grid[cy - 1][cx - 13] = COUNTER

    # Houses: rich (SE, stone), mid x2, poor shacks x2 (dirt floors)
    buildings.append(build_building(name, "house_rich", cx + 9, cy + 6, cx + 15, cy + 11, CASTLE, "n"))
    buildings.append(build_building(name, "house_mid", cx - 15, cy + 6, cx - 11, cy + 10, PLANK, "n"))
    buildings.append(build_building(name, "house_mid", cx - 9, cy + 6, cx - 5, cy + 10, PLANK, "n"))
    buildings.append(build_building(name, "house_poor", cx - 2, cy + 6, cx + 1, cy + 9, ROAD, "n"))
    buildings.append(build_building(name, "house_poor", cx + 3, cy + 6, cx + 6, cy + 9, ROAD, "n"))

    # Plaza fixtures
    grid[cy - 4][cx] = CRYSTAL
    grid[cy + 4][cx + 7] = FORGE
    CRYSTALS.append({"x": cx, "y": cy - 3})
    SAFE_ZONES.append({"x": x0 - 1, "y": y0 - 1, "w": (x1 - x0) + 3, "h": (y1 - y0) + 3})
    REGIONS[name] = {"x": x0, "y": y0, "w": x1 - x0 + 1, "h": y1 - y0 + 1}
    tav_rect = tav["rect"]
    REGIONS[f"tavern_{name}"] = {"x": tav_rect[0], "y": tav_rect[1],
                                 "w": tav_rect[2] - tav_rect[0] + 1, "h": tav_rect[3] - tav_rect[1] + 1}
    META["towns"][name] = {"center": [cx, cy], "floor": floor, "buildings": buildings}
    grid[cy + 14][cx + 2] = ROAD                # a patch of packed dirt by the south gate
    META["homeless"].append({"town": name, "pos": [cx + 2, cy + 14]})


for name, (cx, cy, hw, hh, floor) in TOWNS.items():
    build_town(name, cx, cy, floor)


def build_farm(name, town, x0, y0, w, h):
    """Fenced field with tilled rows and a farmhouse in the NW corner."""
    x1, y1 = x0 + w - 1, y0 + h - 1
    fill(x0, y0, x1, y1, GRASS)
    for x in range(x0, x1 + 1):
        grid[y0][x] = FENCE
        grid[y1][x] = FENCE
    for y in range(y0, y1 + 1):
        grid[y][x0] = FENCE
        grid[y][x1] = FENCE
    grid[y1][(x0 + x1) // 2] = GRASS            # gate south
    grid[y1][(x0 + x1) // 2 + 1] = GRASS
    for y in range(y0 + 2, y1 - 1):
        if (y - y0) % 3 != 0:
            for x in range(x0 + 6, x1 - 1):
                grid[y][x] = FARMLAND
    house = build_building(town, "farmhouse", x0 + 1, y0 + 1, x0 + 5, y0 + 4, PLANK, "e")
    size = "large" if w >= 15 else "medium" if w >= 11 else "small"
    REGIONS[name] = {"x": x0, "y": y0, "w": w, "h": h}
    META["farms"].append({"id": name, "town": town, "size": size,
                          "rect": [x0, y0, x1, y1], "house": house,
                          "field": [x0 + 6, y0 + 2, x1 - 2, y1 - 2]})

PLAYER_SPAWN = {"x": TOWNS["brasshollow"][0], "y": TOWNS["brasshollow"][1] + 2}

# Castle Aldermere (Verdance): grand walls, throne walk
ccx, ccy = 180, 322
x0, y0, x1, y1 = build_walled(ccx, ccy, 14, 11, CASTLE, gates=("s",))
fill(ccx - 1, y0 + 2, ccx + 1, y0 + 6, CASTLE)      # throne dais approach
for tx, ty in ((x0 + 3, y0 + 3), (x1 - 3, y0 + 3), (x0 + 3, y1 - 3), (x1 - 3, y1 - 3)):
    grid[ty][tx] = WALL                              # corner turrets
SAFE_ZONES.append({"x": x0 - 1, "y": y0 - 1, "w": (x1 - x0) + 3, "h": (y1 - y0) + 3})
REGIONS["castle_aldermere"] = {"x": x0, "y": y0, "w": x1 - x0 + 1, "h": y1 - y0 + 1}

# Storm Spire (Miradel): concentric tower compound
scx, scy = 86, 96
x0, y0, x1, y1 = build_walled(scx, scy, 9, 9, TOWERF, gates=("s",))
bx0, by0, bx1, by1 = build_walled(scx, scy, 5, 5, TOWERF, gates=("s",))
REGIONS["storm_spire"] = {"x": x0, "y": y0, "w": x1 - x0 + 1, "h": y1 - y0 + 1}
REGIONS["spire_top"] = {"x": scx - 2, "y": scy - 2, "w": 5, "h": 5}

# Hollow Mine (Verdance rock knot): carve interior
fill(208, 386, 214, 396, MINEFLOOR)
fill(210, 380, 212, 396, MINEFLOOR)                  # shaft north
fill(206, 380, 216, 384, MINEFLOOR)                  # chamber
fill(210, 396, 212, 404, MINEFLOOR)                  # entrance south
REGIONS["hollow_mine"] = {"x": 204, "y": 378, "w": 16, "h": 12}

# Emberdeep (Kharzum mountains): long tunnel south through mountain+hill rings
for yy in range(108, 186):
    fill(388, yy, 390, yy, MINEFLOOR)
fill(382, 100, 396, 108, MINEFLOOR)
REGIONS["emberdeep"] = {"x": 382, "y": 100, "w": 15, "h": 10}

# Sunken Crypt (Sylvas swamp): walled crypt, wight chamber
x0, y0, x1, y1 = build_walled(402, 414, 8, 6, MINEFLOOR, gates=("n",))
REGIONS["sunken_crypt"] = {"x": x0, "y": y0, "w": x1 - x0 + 1, "h": y1 - y0 + 1}

# ------------------------------------------------------------------ wilderness POIs

# Ruined watchtower (Verdance north meadow): broken wall square, elite den
for x in range(148, 157):
    if x % 3 != 2:
        grid[306][x] = WALL
        grid[313][x] = WALL
for y in range(306, 314):
    if y % 3 != 1:
        grid[y][148] = WALL
        grid[y][156] = WALL
fill(150, 308, 154, 311, COBBLE)
REGIONS["ruined_watchtower"] = {"x": 147, "y": 305, "w": 11, "h": 10}

# Standing stones (Kharzum snowfield): ring of monoliths on old tower floor
for ang_i in range(8):
    import math as _m
    sx = int(316 + 7 * _m.cos(ang_i * _m.tau / 8))
    sy = int(120 + 6 * _m.sin(ang_i * _m.tau / 8))
    grid[sy][sx] = ROCK
fill(314, 118, 318, 122, TOWERF)
REGIONS["standing_stones"] = {"x": 306, "y": 111, "w": 20, "h": 19}

# Bandit camp (Sylvas north forest): broken fence ring, trampled ground
for x in range(346, 359):
    if x % 4 != 3:
        grid[306][x] = FENCE
        grid[315][x] = FENCE
for y in range(306, 316):
    if y % 4 != 1:
        grid[y][346] = FENCE
        grid[y][358] = FENCE
fill(349, 309, 355, 313, ROAD)
REGIONS["bandit_camp"] = {"x": 345, "y": 305, "w": 15, "h": 12}

# ------------------------------------------------------------------ farms

build_farm("farm_verdance_large", "brasshollow", 128, 368, 16, 12)
build_farm("farm_verdance_small", "brasshollow", 100, 320, 9, 7)
build_farm("farm_sylvas_medium", "sylvanglade", 338, 330, 12, 9)
build_farm("farm_miradel_small", "willowick", 110, 152, 9, 7)

# ------------------------------------------------------------------ roads


def carve_road(points, tile=ROAD):
    for (ax, ay), (bx, by) in zip(points, points[1:]):
        steps = max(abs(bx - ax), abs(by - ay))
        for i in range(steps + 1):
            t = i / max(1, steps)
            x, y = int(ax + (bx - ax) * t), int(ay + (by - ay) * t)
            for dx in (0, 1):
                for dy in (0, 1):
                    xi, yi = x + dx, y + dy
                    if 0 <= xi < W and 0 <= yi < H:
                        cur = grid[yi][xi]
                        if cur in (SHALLOW,):
                            grid[yi][xi] = BRIDGE
                        elif cur in GROUND_OPEN or cur in (TREE, HILLS, ROCK, DEADTREE, FLOWERS, MOUNTAIN):
                            grid[yi][xi] = tile


# Verdance: Brasshollow -> castle -> mine; Brasshollow -> west dock
carve_road([(126, 352), (152, 342), (178, 334)])
carve_road([(180, 333), (196, 360), (210, 398)])
carve_road([(94, 352), (78, 352)])
# Kharzum: Gearford -> Emberdeep tunnel mouth; Gearford -> south dock
carve_road([(360, 153), (376, 168), (389, 185)])
carve_road([(360, 153), (348, 190), (334, 224)])
# Sylvas: Sylvanglade -> crypt; -> west dock
carve_road([(372, 369), (390, 392), (402, 408)])
carve_road([(356, 356), (322, 360), (292, 372)])
# Miradel: Willowick -> Spire; -> south dock
carve_road([(122, 123), (104, 112), (86, 105)])
carve_road([(122, 149), (122, 180), (118, 208)])

# ------------------------------------------------------------------ docks (ports)

# Anchors are road ends on land; docks auto-extend to the actual shoreline.
PORT_ANCHORS = {
    "brasshollow": (78, 352, -1, 0),   # march west
    "gearford": (334, 222, 0, 1),      # march south
    "sylvanglade": (292, 372, -1, 0),  # march west
    "willowick": (118, 208, 0, 1),     # march south
}
PORTS = {}
for name, (ax, ay, dx, dy) in PORT_ANCHORS.items():
    x, y = ax, ay
    for _ in range(64):
        nx, ny = x + dx, y + dy
        if grid[ny][nx] in (SHALLOW, OCEAN):
            break
        x, y = nx, ny
    else:
        raise AssertionError(f"port {name}: no shoreline within 64 tiles of anchor")
    # walkway from the anchor to the waterline, then a pier 6 tiles into the water
    px, py = x, y
    cx, cy = ax, ay
    while (cx, cy) != (px, py):
        grid[cy][cx] = ROAD
        grid[cy + (1 if dx != 0 else 0)][cx + (1 if dy != 0 else 0)] = ROAD
        cx += dx
        cy += dy
    for i in range(0, 7):
        tx, ty = px + dx * i, py + dy * i
        grid[ty][tx] = DOCK
        grid[ty + (1 if dx != 0 else 0)][tx + (1 if dy != 0 else 0)] = DOCK
    PORTS[name] = (px + dx * 3, py + dy * 3)
    REGIONS[f"port_{name}"] = {"x": px - 4, "y": py - 4, "w": 10 + 4 * abs(dx), "h": 10 + 4 * abs(dy)}

# ------------------------------------------------------------------ spawners & nodes

SPAWNERS = [
    # Verdance (levels 1-6)
    {"id": 1, "monster": "leaf_wisp", "region": {"x": 90, "y": 310, "w": 40, "h": 30}, "count": 8, "respawnTicks": 300},
    {"id": 2, "monster": "leaf_wisp", "region": {"x": 120, "y": 370, "w": 40, "h": 30}, "count": 8, "respawnTicks": 300},
    {"id": 3, "monster": "bramble_wolf", "region": {"x": 175, "y": 335, "w": 30, "h": 35}, "count": 6, "respawnTicks": 400},
    {"id": 4, "monster": "bramble_wolf", "region": {"x": 150, "y": 390, "w": 40, "h": 24}, "count": 6, "respawnTicks": 400},
    {"id": 5, "monster": "rust_golem", "region": {"x": 204, "y": 378, "w": 16, "h": 12}, "count": 2, "respawnTicks": 1200},
    {"id": 6, "monster": "cave_bat", "region": {"x": 206, "y": 380, "w": 12, "h": 8}, "count": 5, "respawnTicks": 350},
    # Kharzum (5-8)
    {"id": 7, "monster": "snow_wolf", "region": {"x": 320, "y": 90, "w": 50, "h": 34}, "count": 8, "respawnTicks": 420},
    {"id": 8, "monster": "hill_brute", "region": {"x": 330, "y": 170, "w": 44, "h": 30}, "count": 5, "respawnTicks": 700},
    {"id": 9, "monster": "ember_wisp", "region": {"x": 382, "y": 100, "w": 15, "h": 10}, "count": 5, "respawnTicks": 500},
    {"id": 10, "monster": "ore_slime", "region": {"x": 382, "y": 100, "w": 15, "h": 10}, "count": 3, "respawnTicks": 600},
    # Sylvas (4-8)
    {"id": 11, "monster": "marsh_wisp", "region": {"x": 350, "y": 405, "w": 60, "h": 30}, "count": 8, "respawnTicks": 400},
    {"id": 12, "monster": "bramble_wolf", "region": {"x": 340, "y": 320, "w": 50, "h": 28}, "count": 7, "respawnTicks": 400},
    {"id": 13, "monster": "crypt_wight", "region": {"x": 396, "y": 410, "w": 14, "h": 9}, "count": 3, "respawnTicks": 900},
    # Miradel (7-10)
    {"id": 14, "monster": "dune_scorpion", "region": {"x": 100, "y": 160, "w": 60, "h": 34}, "count": 8, "respawnTicks": 450},
    {"id": 15, "monster": "storm_elemental", "region": {"x": 78, "y": 88, "w": 18, "h": 18}, "count": 4, "respawnTicks": 700},
    {"id": 16, "monster": "storm_warden", "region": {"x": 84, "y": 94, "w": 5, "h": 5}, "count": 1, "respawnTicks": 3600},
    # Old friends guarding the old mine approach
    {"id": 17, "monster": "magitek_warden", "region": {"x": 206, "y": 380, "w": 12, "h": 6}, "count": 1, "respawnTicks": 3600},
    # --- M6 band fill: Verdance ---
    {"id": 18, "monster": "blood_wisp", "region": {"x": 160, "y": 300, "w": 40, "h": 26}, "count": 7, "respawnTicks": 320},
    {"id": 19, "monster": "dire_wolf", "region": {"x": 147, "y": 305, "w": 11, "h": 10}, "count": 3, "respawnTicks": 700},
    {"id": 20, "monster": "ash_hound", "region": {"x": 147, "y": 305, "w": 11, "h": 10}, "count": 2, "respawnTicks": 700},
    {"id": 21, "monster": "clay_golem", "region": {"x": 90, "y": 390, "w": 60, "h": 24}, "count": 4, "respawnTicks": 800},
    {"id": 22, "monster": "mine_foreman", "region": {"x": 206, "y": 380, "w": 12, "h": 8}, "count": 1, "respawnTicks": 4200},
    # --- Kharzum ---
    {"id": 23, "monster": "frost_wisp", "region": {"x": 300, "y": 80, "w": 60, "h": 30}, "count": 8, "respawnTicks": 380},
    {"id": 24, "monster": "snow_bat", "region": {"x": 380, "y": 130, "w": 46, "h": 40}, "count": 7, "respawnTicks": 380},
    {"id": 25, "monster": "frost_crab", "region": {"x": 320, "y": 190, "w": 60, "h": 30}, "count": 6, "respawnTicks": 480},
    {"id": 26, "monster": "frost_golem", "region": {"x": 306, "y": 111, "w": 20, "h": 19}, "count": 2, "respawnTicks": 1400},
    {"id": 27, "monster": "obsidian_golem", "region": {"x": 306, "y": 111, "w": 20, "h": 19}, "count": 1, "respawnTicks": 2400},
    # --- Sylvas ---
    {"id": 28, "monster": "gloom_wisp", "region": {"x": 320, "y": 330, "w": 44, "h": 40}, "count": 8, "respawnTicks": 360},
    {"id": 29, "monster": "bog_slime", "region": {"x": 360, "y": 400, "w": 60, "h": 34}, "count": 6, "respawnTicks": 480},
    {"id": 30, "monster": "marsh_stalker", "region": {"x": 396, "y": 330, "w": 40, "h": 46}, "count": 6, "respawnTicks": 480},
    {"id": 31, "monster": "venom_scorpion", "region": {"x": 340, "y": 420, "w": 60, "h": 26}, "count": 5, "respawnTicks": 560},
    {"id": 32, "monster": "crypt_king", "region": {"x": 396, "y": 410, "w": 14, "h": 9}, "count": 1, "respawnTicks": 4800},
    {"id": 33, "monster": "ash_hound", "region": {"x": 345, "y": 305, "w": 15, "h": 12}, "count": 4, "respawnTicks": 600},
    {"id": 34, "monster": "bandit_chief", "region": {"x": 345, "y": 305, "w": 15, "h": 12}, "count": 1, "respawnTicks": 3600},
    # --- Miradel ---
    {"id": 35, "monster": "rock_scuttler", "region": {"x": 70, "y": 130, "w": 40, "h": 40}, "count": 7, "respawnTicks": 420},
    {"id": 36, "monster": "dune_jackal", "region": {"x": 140, "y": 100, "w": 50, "h": 50}, "count": 7, "respawnTicks": 420},
    {"id": 37, "monster": "sun_wisp", "region": {"x": 150, "y": 60, "w": 46, "h": 40}, "count": 6, "respawnTicks": 520},
    {"id": 38, "monster": "storm_bat", "region": {"x": 70, "y": 70, "w": 40, "h": 30}, "count": 5, "respawnTicks": 560},
    {"id": 39, "monster": "gold_slime", "region": {"x": 150, "y": 60, "w": 60, "h": 60}, "count": 1, "respawnTicks": 6000},
]

NODES = [
    {"id": 101, "type": "oak", "region": {"x": 95, "y": 315, "w": 60, "h": 60}, "count": 12, "respawnTicks": 900},
    {"id": 102, "type": "oak", "region": {"x": 330, "y": 320, "w": 70, "h": 50}, "count": 12, "respawnTicks": 900},
    {"id": 103, "type": "berry", "region": {"x": 100, "y": 330, "w": 50, "h": 50}, "count": 8, "respawnTicks": 700},
    {"id": 104, "type": "berry", "region": {"x": 350, "y": 400, "w": 50, "h": 30}, "count": 8, "respawnTicks": 700},
    {"id": 105, "type": "ore", "region": {"x": 204, "y": 378, "w": 16, "h": 12}, "count": 5, "respawnTicks": 1100},
    {"id": 106, "type": "ore", "region": {"x": 382, "y": 100, "w": 15, "h": 10}, "count": 6, "respawnTicks": 1100},
    {"id": 107, "type": "ore", "region": {"x": 316, "y": 160, "w": 50, "h": 40}, "count": 6, "respawnTicks": 1100},
    {"id": 108, "type": "berry", "region": {"x": 108, "y": 120, "w": 36, "h": 32}, "count": 6, "respawnTicks": 700},
    {"id": 109, "type": "oak", "region": {"x": 150, "y": 296, "w": 60, "h": 30}, "count": 10, "respawnTicks": 900},
    {"id": 110, "type": "oak", "region": {"x": 396, "y": 330, "w": 40, "h": 40}, "count": 10, "respawnTicks": 900},
    {"id": 111, "type": "ore", "region": {"x": 300, "y": 180, "w": 70, "h": 40}, "count": 7, "respawnTicks": 1100},
    {"id": 112, "type": "ore", "region": {"x": 70, "y": 90, "w": 40, "h": 50}, "count": 6, "respawnTicks": 1100},
    {"id": 113, "type": "berry", "region": {"x": 320, "y": 340, "w": 40, "h": 30}, "count": 7, "respawnTicks": 700},
    {"id": 114, "type": "berry", "region": {"x": 96, "y": 380, "w": 50, "h": 30}, "count": 7, "respawnTicks": 700},
]

# ------------------------------------------------------------------ checks


def flood(start, passable):
    seen = [[False] * W for _ in range(H)]
    stack = [start]
    if not passable(grid[start[1]][start[0]]):
        return seen
    seen[start[1]][start[0]] = True
    while stack:
        x, y = stack.pop()
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < W and 0 <= ny < H and not seen[ny][nx] and passable(grid[ny][nx]):
                seen[ny][nx] = True
                stack.append((nx, ny))
    return seen


def region_reached(seen, r):
    return any(seen[y][x]
               for y in range(r["y"], r["y"] + r["h"])
               for x in range(r["x"], r["x"] + r["w"])
               if 0 <= x < W and 0 <= y < H)


assert grid[PLAYER_SPAWN["y"]][PLAYER_SPAWN["x"]] in WALKABLE, "spawn not walkable"

# Foot reachability within each continent (town -> its locations)
foot = lambda t: t in WALKABLE
seen_foot = flood((PLAYER_SPAWN["x"], PLAYER_SPAWN["y"]), foot)
for rid in ("castle_aldermere", "hollow_mine", "port_brasshollow"):
    assert region_reached(seen_foot, REGIONS[rid]), f"{rid} not foot-reachable from Brasshollow"

for town, rids in (("gearford", ("emberdeep", "port_gearford")),
                   ("sylvanglade", ("sunken_crypt", "port_sylvanglade")),
                   ("willowick", ("storm_spire", "port_willowick"))):
    cx, cy = TOWNS[town][0], TOWNS[town][1]
    seen_t = flood((cx, cy), foot)
    for rid in rids:
        assert region_reached(seen_t, REGIONS[rid]), f"{rid} not foot-reachable from {town}"

# Ship reachability: all ports connected over water (shallow+ocean+dock edge)
water = lambda t: t in (SHALLOW, OCEAN)
port_names = list(PORTS)
px, py = PORTS[port_names[0]]
# start from water tile adjacent to the first dock
start_water = None
for dy in range(-3, 4):
    for dx in range(-8, 9):
        x, y = px + dx, py + dy
        if 0 <= x < W and 0 <= y < H and water(grid[y][x]):
            start_water = (x, y)
            break
    if start_water:
        break
assert start_water, "no water near first port"
seen_ship = flood(start_water, water)
for name in port_names[1:]:
    qx, qy = PORTS[name]
    ok = any(seen_ship[qy + dy][qx + dx]
             for dy in range(-4, 5) for dx in range(-9, 10)
             if 0 <= qx + dx < W and 0 <= qy + dy < H)
    assert ok, f"port {name} not ship-reachable"

# Spawner/node regions contain walkable ground
for coll, label in ((SPAWNERS, "spawner"), (NODES, "node")):
    for s in coll:
        r = s["region"]
        assert any(grid[y][x] in WALKABLE
                   for y in range(r["y"], r["y"] + r["h"])
                   for x in range(r["x"], r["x"] + r["w"])), f"{label} {s['id']} has no walkable tile"

# ------------------------------------------------------------------ write

rows = []
for y in range(H):
    row, run_tile, run_len = [], grid[y][0], 1
    for x in range(1, W):
        if grid[y][x] == run_tile:
            run_len += 1
        else:
            row += [run_tile, run_len]
            run_tile, run_len = grid[y][x], 1
    row += [run_tile, run_len]
    rows.append(row)

out = {
    "width": W,
    "height": H,
    "tileset": TILESET,
    "rows": rows,
    "playerSpawn": PLAYER_SPAWN,
    "safeZones": SAFE_ZONES,
    "crystals": CRYSTALS,
    "regions": [{"id": k, **v} for k, v in REGIONS.items()],
    "spawners": SPAWNERS,
    "nodes": NODES,
}

dest = Path(__file__).resolve().parent.parent / "data" / "world.json"
dest.write_text(json.dumps(out, separators=(",", ":")) + "\n")
meta_dest = Path(__file__).resolve().parent.parent / "data" / "world_meta.json"
meta_dest.write_text(json.dumps(META, separators=(",", ":")) + "\n")
print(f"wrote {dest} ({dest.stat().st_size} bytes), regions={len(REGIONS)}, "
      f"spawners={len(SPAWNERS)}, nodes={len(NODES)}, crystals={len(CRYSTALS)}, "
      f"buildings={sum(len(t['buildings']) for t in META['towns'].values())}, farms={len(META['farms'])}")
