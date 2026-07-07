# OpenFantasy

A 2D open-world MMORPG vertical slice in **Godot 4.4 (.NET/C#)**, styled after
16-bit golden-age JRPGs (FF6-inspired mood; all assets original). One shared
persistent region — *Verdance March* — with real-time ATB combat, monster AI,
XP/levels, chat, and server-side character persistence.

## Documents

- [`docs/GDD.md`](docs/GDD.md) — game design
- [`docs/TECH_DESIGN.md`](docs/TECH_DESIGN.md) — architecture (deterministic sim / net / thin client)
- [`docs/VERTICAL_SLICE.md`](docs/VERTICAL_SLICE.md) — slice scope, milestones, acceptance

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
python3 tools/gen_world.py    # data/world.json (Verdance March layout)
python3 tools/gen_assets.py   # assets/ placeholder pixel art
```

## Controls

WASD/arrows move · **1–4** abilities · **Tab** cycle target · **Enter** chat

## Layout

```
src/Sim/     pure deterministic simulation (no Godot types)
src/Net/     binary protocol + ENet server/client nodes
src/Client/  thin presentation: menu, world renderer, entity views, HUD
data/        schema-validated content JSON
assets/      generated placeholder art (replace file-for-file with real art)
tools/       content + asset generators
```
