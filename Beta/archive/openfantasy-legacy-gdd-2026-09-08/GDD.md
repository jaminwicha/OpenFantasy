# OpenFantasy — Game Design Document

**Version:** 0.1 (vertical-slice scope) · **Date:** 2026-07-06
**Engine:** Godot 4.4 (.NET/C#) · **Platform:** Desktop (macOS/Windows/Linux)

---

## 1. High Concept

OpenFantasy is a **2D open-world MMORPG** rendered in the visual and tonal
language of 16-bit golden-age JRPGs — specifically inspired by *Final Fantasy VI*:
moody steampunk-fantasy, painterly pixel skies, expressive small-sprite
characters, and an operatic melancholy under the adventure.

Players share one persistent world. They explore a seamless overworld, fight
monsters in **real-time ATB combat** (abilities charge on an Active-Time bar
rather than firing instantly), form parties, chat, quest, and grow characters
that persist between sessions.

> **Legal note:** All assets are original. FF6 is an aesthetic *reference*
> (palette mood, proportions, UI flavor), never a source of copied sprites,
> music, names, or text.

## 2. Pillars

1. **A world, not a lobby** — one shared, seamless, persistent map. No
   instanced "stages." Other players are visibly present everywhere.
2. **16-bit soul** — 16×24-ish character sprites, 16×16 tiles, limited
   palettes, chunky windowed UI with gradient-blue panels and white borders.
3. **ATB, adapted for MMO** — combat is real-time but *deliberate*: every
   action charges on an ATB gauge. Positioning + timing, not APM.
4. **Deterministic, server-authoritative truth** — the server simulation is
   the only authority; clients present and predict, never decide.

## 3. Setting & Tone

The world of **Aldermere**: a continent recovering from a war in which
**Aethertech** — machines that burn crystallized magic — shattered the old
kingdoms. Steam-and-brass towns cling to green frontier; ruins hum with
residual magic; the wilderness between settlements is genuinely dangerous.

Tone: hopeful melancholy. Small people rebuilding after a big war.

## 4. Core Gameplay Loops

- **Minute:** move through the world → engage monsters → time ATB abilities →
  loot XP/gil.
- **Session:** accept quests in town → travel to a wilderness/dungeon
  objective → return, level up, buy gear, chat.
- **Long-term:** level classes, unlock abilities, explore farther regions,
  party for harder content.

## 5. Systems

### 5.1 Characters & Classes (slice: 3)
| Class | Fantasy | Role | Signature |
|---|---|---|---|
| **Squire** | ex-imperial soldier | melee bruiser | *Cleave* (short-range arc) |
| **Mage** | academy dropout | ranged burst | *Fire* (projectile), *Cura* (heal) |
| **Scout** | frontier hunter | fast skirmisher | *Aimed Shot* (long range), high move speed |

Stats: HP, MP, Attack, Magic, Defense, Speed. Level 1–10 in slice; XP curve
`xp(level) = 100 · level^1.5`. Level-ups raise stats per class growth table
(data-driven).

### 5.2 Combat (real-time ATB)
- Each character has an **ATB gauge** that fills at a rate scaled by Speed.
- Using an ability requires (a) full-enough gauge for that ability's **ATB
  cost** and (b) resource (MP) and range. Using it drains the gauge.
- Damage: `max(1, Attack·power − Defense/2)` (physical) or
  `max(1, Magic·power − Defense/4)` (magical), ±10% deterministic variance.
- Death: players respawn at the town crystal with full HP; monsters respawn
  on a timer at their spawn region.
- Aggro: monsters aggro by proximity, leash back to spawn.

### 5.3 World (slice region: **Verdance March**)
- One seamless map ~128×128 tiles: **Brasshollow** (town, safe zone, respawn
  crystal, NPCs) → meadows (low-level mobs) → dark forest (mid) → **Hollow
  Mine** cave mouth (elite mob).
- Collision from tile data. Safe zone suppresses aggro and PvE damage.

### 5.4 Monsters (slice: 3)
| Monster | Habitat | Behavior |
|---|---|---|
| **Leaf Wisp** | meadows | timid, weak melee |
| **Bramble Wolf** | forest | packs, fast, melee |
| **Rust Golem** | mine mouth | slow elite, heavy hits, high HP |

### 5.5 Multiplayer & Social
- Dozens of players per world server (slice target: 16 concurrent).
- **Chat**: zone-wide text chat with system messages (joins, level-ups, deaths).
- **Parties** (post-slice): shared XP within range. Slice ships open-tag XP:
  everyone who damaged a monster gets full XP (generous, simple, social).
- Character persistence server-side per account name.

### 5.6 Progression & Economy (slice-minimal)
- XP → levels → stats + new ability unlock at levels 3 and 5.
- **Gil** drops from monsters; slice has no shops yet (economy is post-slice).

## 6. Presentation

### 6.1 Art
- 16×16 terrain tiles; character sprites 16×24 with 3-frame walk cycles,
  4 directions. Limited FF6-ish palette: deep blue-greens, warm brass/amber
  accents, dusty parchment UI.
- All slice art is **original programmatic placeholder pixel art** shipped in
  the repo, structured so hand-drawn art can replace it file-for-file.

### 6.2 UI
- FF6-flavored windowing: dark blue vertical-gradient panels, 2px white
  border, thin drop shadow, white serif-adjacent bitmap-look font.
- HUD: HP/MP/ATB bars bottom-left, ability hotbar (1–4), chat bottom-left
  above bars, target frame top-center, level/XP top-left.

### 6.3 Audio (post-slice)
- Slice ships silent or with generated square-wave stingers only. Original
  chiptune-orchestral score is a post-slice track.

## 7. Controls
- **WASD / arrows** move (8-way input, 4-way facing).
- **1–4** abilities · **Tab** cycle target · **Enter** chat · **Esc** menu.

## 8. Out of Scope for the Slice (explicit)
Trading, guilds, instanced dungeons, PvP, mounts, crafting, shops, quests
beyond one kill-task, audio score, controller support, mobile.

## 9. Risks & Open Questions
- **R1:** Real-time ATB may read as "cooldown MMO" — mitigate with visible
  gauge and charge-then-act feel. Playtest gate.
- **R2:** MMO netcode scope creep — slice caps at 16 players, one zone,
  zone-wide relevance (no interest management yet).
- **Q1:** Party system design (shared XP radius vs. tagging) — decide after
  slice playtest.
- **Q2:** Overworld scale/travel time tuning needs playtest data.
