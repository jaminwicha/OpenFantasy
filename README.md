# OpenFantasy

A 2D open-world MMORPG in **Godot 4.4 (.NET/C#)**, styled after 16-bit
golden-age JRPGs (FF6-inspired look; all assets original). One shared
persistent world — **Aldermere**, 512×512 tiles across **four continents**
(green Verdance, mountain-locked Kharzum, forest-and-swamp Sylvas, desert
Miradel) with four towns, Castle Aldermere, the Storm Spire tower, and three
dungeons. Real-time ATB combat, 35+ monster types in level bands, XP/levels,
**four playable races** (human/elf/dwarf/gnome with stat identities),
items/equipment, a **main quest chain** (kill/explore/deliver/fetch) plus
side quests from kings, farmers, bartenders and beggars alike, parties,
gathering and crafting, **vehicles** (canoe, sailboat, ship, dwarven tank,
and the airship *Brasswing*), chat, day/night, and server-side persistence.

Towns are **alive**: ~87 NPCs across enterable buildings — taverns with
bartenders, patrons, a hooded figure, a fence, and an adventuring party;
specialized shops (smith, apothecary, toolwright, general goods, ports);
houses from stone-floored gentry manors to dirt-floored shacks, with
homeless folk by the gates; and small/medium/large **farms** outside the
walls, worked by farmers who post their own quests. Villagers wander their
streets deterministically.

Every player is built in the **character creator**: a paper-doll sprite
compositor (8 skins × 12 hair styles × 16 hair colors × 10 outfits × 16×16
outfit colors × 4 accessories ≈ 25M combinations) in authentic 16×24
FF6-era sprite craft — outlines, 3-tone ramps, hair shine, 3-frame walks.
A preset gallery of original heroes in famous silhouettes shows the range;
layer sheets regenerate entirely from `tools/gen_layers.py`.

## Documents

- [`docs/GDD.md`](docs/GDD.md) — game design
- [`docs/TECH_DESIGN.md`](docs/TECH_DESIGN.md) — architecture (deterministic sim / net / thin client)
- [`docs/VERTICAL_SLICE.md`](docs/VERTICAL_SLICE.md) — milestone 1 scope and acceptance
- [`docs/MILESTONE_2.md`](docs/MILESTONE_2.md) — milestone 2: items, shops, quests, parties, dungeon
- [`docs/MILESTONE_3.md`](docs/MILESTONE_3.md) — milestone 3: sprite fidelity, character creator, gathering/crafting
- [`docs/MILESTONE_4.md`](docs/MILESTONE_4.md) — milestone 4: four continents, races, vehicles, quest chains
- [`docs/MILESTONE_5.md`](docs/MILESTONE_5.md) — milestone 5: living towns, taverns, farms, fetch quests
- [`docs/MILESTONE_6.md`](docs/MILESTONE_6.md) — milestone 6: content expansion (bestiary, hoard, quests, POIs)
- [`docs/EXPANSION_ROADMAP.md`](docs/EXPANSION_ROADMAP.md) — project review & content expansion roadmap
- [`docs/RUNTIME_SEED_DESIGN.md`](docs/RUNTIME_SEED_DESIGN.md) — runtime seed procedural world design ("Infinite Aldermere")
- [`docs/ROGUEFICTION_PORTING_BRIEF.md`](docs/ROGUEFICTION_PORTING_BRIEF.md) — porting RogueFiction's proven xandering topology research into Milestone 7B
- [`docs/MILESTONE_7.md`](docs/MILESTONE_7.md) — milestone 7: runtime seed foundations + Jaquayed multi-floor delves

## Requirements

- Godot **4.4.x .NET (mono)** editor/runtime
- .NET 8 SDK
- Python 3 (content/asset generators only)

## Run

```sh
dotnet build

# Play (menu: name, class, then Play Solo or Join World)
godot --path .            # use the mono build of Godot

# Dedicated server
godot --headless --path . -- --server --port 24565

# Join a LAN server: run the game, enter host:port, press Join World
```

## Verify

```sh
# Full selftest: data validation, xp curve, combat math, RNG stability,
# 1000-tick determinism, movement collision, scripted combat integration
godot --headless --path . -- --selftest

# Two-bot multiplayer smoke test (see also tools/)
godot --headless --path . -- --server --port 24571 &
godot --headless --path . -- --bot Aria  --port 24571   # exit 0 = saw 2 players + chat
godot --headless --path . -- --bot Brand --port 24571

# Auto-play 10 s solo session, saves user://auto_screenshot.png
godot --path . -- --auto Terra
```

## Regenerate content

```sh
python3 tools/gen_world.py       # data/world.json + world_meta.json (Aldermere)
python3 tools/gen_npcs.py        # data/npcs.json (population; needs world_meta)
python3 tools/gen_assets.py      # assets/ tiles, monsters, nodes, UI
python3 tools/gen_layers.py      # assets/layers/ character sprite layers + appearance.json
python3 tools/preview_sprites.py # docs/screenshots/preset-gallery.png (offline compositor)
```

## Controls

WASD/arrows move · **1–4** abilities · **Tab** cycle target · **Enter** chat ·
**E** talk / gather / use surroundings (delve entrances, stairs, chests,
suspicious walls; tools required: axe→oak, pickaxe→ore, sickle→berries) ·
**V** embark/disembark vehicle · **I** inventory · **J** quest log ·
**C** crafting · **M** auto-map · **Esc** close window

Chat commands: `/party invite <name>` · `/party accept` · `/party leave` · `/p <msg>`

## Layout

```
src/Sim/     pure deterministic simulation (no Godot types)
src/Net/     binary protocol + ENet server/client nodes
src/Client/  thin presentation: menu, world renderer, entity views, HUD
data/        schema-validated content JSON
assets/      generated placeholder art (replace file-for-file with real art)
tools/       content + asset generators
```
