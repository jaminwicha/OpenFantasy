#!/usr/bin/env python3
"""Deterministically authors data/world.json — the Verdance March region.

Layout (128x128 tiles): walled town of Brasshollow (safe zone, respawn
crystal) in the west, meadows around it, a dark forest band in the east,
and the Hollow Mine mouth in the northeast. A road links town to mine.

Run: python3 tools/gen_world.py
"""
import json
import random
from pathlib import Path

W, H = 128, 128
rng = random.Random(60406)  # fixed seed: world layout is content, not runtime randomness

GRASS, TREE, WATER, ROAD, COBBLE, WALL, FOREST, MINEFLOOR, ROCK, CRYSTAL, SAND, FLOWERS, FORGE = range(13)

TILESET = [
    {"id": GRASS, "name": "grass", "walkable": True},
    {"id": TREE, "name": "tree", "walkable": False},
    {"id": WATER, "name": "water", "walkable": False},
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
]

grid = [[GRASS] * W for _ in range(H)]


def fill(x0, y0, x1, y1, t):
    for y in range(max(0, y0), min(H, y1 + 1)):
        for x in range(max(0, x0), min(W, x1 + 1)):
            grid[y][x] = t


# --- water border with wobble, sand shore inside it -------------------------
for y in range(H):
    for x in range(W):
        d = min(x, y, W - 1 - x, H - 1 - y)
        wobble = (x * 7 + y * 13) % 3
        if d < 2 + wobble // 2:
            grid[y][x] = WATER
        elif d < 4 + wobble // 2:
            grid[y][x] = SAND

# --- forest band (east) ------------------------------------------------------
fill(64, 24, 104, 104, FOREST)

# --- mine area (northeast): rock massif with a carved clearing ---------------
fill(98, 8, 124, 44, ROCK)
CLEAR_CX, CLEAR_CY, CLEAR_R = 110, 26, 8
for y in range(CLEAR_CY - CLEAR_R, CLEAR_CY + CLEAR_R + 1):
    for x in range(CLEAR_CX - CLEAR_R, CLEAR_CX + CLEAR_R + 1):
        if (x - CLEAR_CX) ** 2 + (y - CLEAR_CY) ** 2 <= CLEAR_R * CLEAR_R:
            grid[y][x] = MINEFLOOR
# entrance corridor south from the clearing down to the road line
fill(108, 26, 111, 60, MINEFLOOR)

# --- mine interior (M2): tunnels + three chambers carved through the massif --
def carve_circle(cx, cy, r):
    for y in range(cy - r, cy + r + 1):
        for x in range(cx - r, cx + r + 1):
            if (x - cx) ** 2 + (y - cy) ** 2 <= r * r and grid[y][x] == ROCK:
                grid[y][x] = MINEFLOOR

fill(109, 12, 110, 26, MINEFLOOR)   # tunnel north from the clearing
carve_circle(104, 13, 4)            # chamber A — cave bats
fill(104, 12, 119, 13, MINEFLOOR)   # tunnel east
carve_circle(119, 13, 4)            # chamber B — ore slimes
fill(118, 13, 120, 37, MINEFLOOR)   # tunnel south, into the deep
carve_circle(118, 37, 5)            # boss chamber — the Magitek Warden

# --- town of Brasshollow ------------------------------------------------------
TX0, TY0, TX1, TY1 = 16, 52, 36, 72
fill(TX0, TY0, TX1, TY1, COBBLE)
for x in range(TX0, TX1 + 1):
    grid[TY0][x] = WALL
    grid[TY1][x] = WALL
for y in range(TY0, TY1 + 1):
    grid[y][TX0] = WALL
    grid[y][TX1] = WALL
# gates: east (to the road) and north (to the meadows)
for gy in (61, 62, 63):
    grid[gy][TX1] = COBBLE
for gx in (25, 26, 27):
    grid[TY0][gx] = COBBLE
# houses (solid wall blocks) for silhouette
for hx, hy in ((19, 55), (30, 55), (19, 66), (30, 66)):
    fill(hx, hy, hx + 3, hy + 2, WALL)
# respawn crystal north of the plaza; players spawn just south of it
grid[58][26] = CRYSTAL
PLAYER_SPAWN = {"x": 26, "y": 62}
# forge (crafting station) in the southeast quarter of town
grid[68][33] = FORGE

# --- road: east gate -> through forest -> mine corridor ----------------------
for x in range(TX1 + 1, 109):
    for y in (61, 62, 63):
        grid[y][x] = ROAD
# north path from town's north gate into the meadow
for y in range(30, TY0):
    for x in (25, 26, 27):
        grid[y][x] = GRASS if grid[y][x] == TREE else grid[y][x]
        grid[y][x] = ROAD if grid[y][x] in (GRASS, FOREST, FLOWERS) else grid[y][x]

# --- scatter: trees in meadow, dense trees in forest, flowers ----------------
for y in range(4, H - 4):
    for x in range(4, W - 4):
        t = grid[y][x]
        if t == GRASS:
            r = rng.random()
            if r < 0.045:
                grid[y][x] = TREE
            elif r < 0.075:
                grid[y][x] = FLOWERS
        elif t == FOREST:
            if rng.random() < 0.24:
                grid[y][x] = TREE

# re-clear the road/corridor after scatter (scatter never wrote over ROAD, but be safe)
for x in range(TX1 + 1, 109):
    for y in (61, 62, 63):
        grid[y][x] = ROAD

# --- spawner regions ----------------------------------------------------------
SPAWNERS = [
    {"id": 1, "monster": "leaf_wisp", "region": {"x": 14, "y": 22, "w": 32, "h": 24}, "count": 8, "respawnTicks": 300},
    {"id": 2, "monster": "leaf_wisp", "region": {"x": 40, "y": 66, "w": 20, "h": 30}, "count": 8, "respawnTicks": 300},
    {"id": 3, "monster": "bramble_wolf", "region": {"x": 66, "y": 30, "w": 34, "h": 26}, "count": 6, "respawnTicks": 400},
    {"id": 4, "monster": "bramble_wolf", "region": {"x": 66, "y": 68, "w": 34, "h": 30}, "count": 6, "respawnTicks": 400},
    {"id": 5, "monster": "rust_golem", "region": {"x": 104, "y": 20, "w": 13, "h": 13}, "count": 1, "respawnTicks": 1200},
    {"id": 6, "monster": "cave_bat", "region": {"x": 100, "y": 9, "w": 9, "h": 9}, "count": 6, "respawnTicks": 350},
    {"id": 7, "monster": "ore_slime", "region": {"x": 115, "y": 9, "w": 9, "h": 9}, "count": 4, "respawnTicks": 500},
    {"id": 8, "monster": "magitek_warden", "region": {"x": 113, "y": 32, "w": 10, "h": 10}, "count": 1, "respawnTicks": 3600},
]

SAFE_ZONE = {"x": TX0 - 1, "y": TY0 - 1, "w": (TX1 - TX0) + 3, "h": (TY1 - TY0) + 3}

# --- resource node regions (M3: gathering) -----------------------------------
NODES = [
    {"id": 101, "type": "oak", "region": {"x": 14, "y": 22, "w": 34, "h": 26}, "count": 10, "respawnTicks": 900},
    {"id": 102, "type": "oak", "region": {"x": 64, "y": 28, "w": 38, "h": 70}, "count": 14, "respawnTicks": 900},
    {"id": 103, "type": "berry", "region": {"x": 40, "y": 64, "w": 22, "h": 32}, "count": 8, "respawnTicks": 700},
    {"id": 104, "type": "berry", "region": {"x": 12, "y": 30, "w": 24, "h": 18}, "count": 6, "respawnTicks": 700},
    {"id": 105, "type": "ore", "region": {"x": 100, "y": 10, "w": 20, "h": 20}, "count": 8, "respawnTicks": 1100},
    {"id": 106, "type": "ore", "region": {"x": 104, "y": 20, "w": 13, "h": 13}, "count": 4, "respawnTicks": 1100},
]

# --- sanity checks before writing ---------------------------------------------
walkable = {t["id"] for t in TILESET if t["walkable"]}
assert grid[PLAYER_SPAWN["y"]][PLAYER_SPAWN["x"]] in walkable, "player spawn not walkable"
for s in SPAWNERS:
    r = s["region"]
    assert any(
        grid[y][x] in walkable
        for y in range(r["y"], r["y"] + r["h"])
        for x in range(r["x"], r["x"] + r["w"])
    ), f"spawner {s['id']} region has no walkable tile"

# reachability sweep from spawn (4-way flood fill) — every spawner must be reachable
seen = [[False] * W for _ in range(H)]
stack = [(PLAYER_SPAWN["x"], PLAYER_SPAWN["y"])]
seen[PLAYER_SPAWN["y"]][PLAYER_SPAWN["x"]] = True
while stack:
    x, y = stack.pop()
    for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
        if 0 <= nx < W and 0 <= ny < H and not seen[ny][nx] and grid[ny][nx] in walkable:
            seen[ny][nx] = True
            stack.append((nx, ny))
for s in SPAWNERS:
    r = s["region"]
    assert any(
        seen[y][x]
        for y in range(r["y"], r["y"] + r["h"])
        for x in range(r["x"], r["x"] + r["w"])
    ), f"spawner {s['id']} region unreachable from town"
for n in NODES:
    r = n["region"]
    assert any(
        seen[y][x]
        for y in range(r["y"], r["y"] + r["h"])
        for x in range(r["x"], r["x"] + r["w"])
    ), f"node region {n['id']} unreachable from town"
# forge must be adjacent to a reachable tile
assert any(seen[68 + dy][33 + dx] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))), "forge unreachable"

# --- RLE encode + write --------------------------------------------------------
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
    "safeZone": SAFE_ZONE,
    "spawners": SPAWNERS,
    "nodes": NODES,
}

dest = Path(__file__).resolve().parent.parent / "data" / "world.json"
dest.write_text(json.dumps(out, separators=(",", ":")) + "\n")
print(f"wrote {dest} ({dest.stat().st_size} bytes)")
