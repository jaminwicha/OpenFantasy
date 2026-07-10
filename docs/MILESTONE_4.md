# OpenFantasy — Milestone 4: *The Wide World*

**Version:** 1.0 · **Date:** 2026-07-10 · **Builds on:** M3 (`846f725`)

## Theme

Verdance March becomes one corner of **Aldermere entire**: four continents,
four towns, a castle, a mage tower, three dungeons, wild biomes between —
and the vehicles, races, and quest structure to make crossing it matter.

## Feature Set

### F1. The vast world (512×512, four continents)
| Continent | Character | Locations |
|---|---|---|
| **Verdance** (SW) | green frontier | Brasshollow (human town), Hollow Mine, Castle Aldermere |
| **Kharzum** (NE) | mountains, tundra | Gearford (dwarven town), Emberdeep (dungeon) |
| **Sylvas** (SE) | deep forest, swamp | Sylvanglade (elven town), Sunken Crypt (swamp dungeon) |
| **Miradel** (NW) | desert, dunes | Willowick (gnomish oasis town), Storm Spire (mage tower) |

- New biomes/tiles: ocean vs **shallow** water, rivers, beach, desert,
  snow, swamp, hills (tank-passable), mountains (airship-only), bridges,
  castle stone, tower floor, docks.
- Each town: walls, NPCs, respawn crystal, forge (Gearford's is the grand
  forge), port where coastal. Castle and tower are walled compounds with
  interiors, guards, and quest NPCs.
- Every biome gets spawner + resource-node coverage tuned to its level
  band (Verdance 1–6 → Kharzum/Sylvas 5–9 → Miradel/Spire 8–10).

### F2. Races
- Appearance gains **race**: Human, Elf, Dwarf, Gnome (byte 8, in the
  creator as the first row, on the wire, in saves and the state hash).
- Visuals via overlay layers: elf ears, dwarf beard (hair-tinted), gnome
  ears + button nose. Same 16×24 frame; silhouette reads at a glance.
- Stat identity (applied in RecomputeStats):
  Human balanced · Elf +Magic/+Speed/−HP · Dwarf +HP/+Def/−Speed ·
  Gnome +MP/+Speed/−Attack.

### F3. Vehicles
Key items that change your movement domain (press **V** to embark/disembark;
you appear as the vehicle while aboard; monsters ignore embarked players):

| Vehicle | Domain | Speed | How acquired |
|---|---|---|---|
| **Canoe** | shallow water + rivers | 1.2× | crafted (8 wood) |
| **Sailboat** | shallow + coastal ocean | 1.5× | bought at any port |
| **Ship** | all water | 1.8× | bought (expensive) at Brasshollow port |
| **Dwarven Tank** | land + hills + swamp, crushes through forest | 1.6× | Gearford side quest |
| **Airship** | everywhere (lands on grass/desert/snow) | 2.5× | main quest finale |

Embark rules are server-authoritative: you must stand adjacent to a legal
tile for the domain; disembark requires adjacent walkable land.

### F4. Quests v2
- Quest kinds: **Kill** (existing), **Explore** (reach a named region),
  **Talk** (deliver to another NPC).
- **Prerequisites** chain quests; **main** vs **side** classification and
  a two-section quest log.
- **Main chain (6 steps):** Elder Bran's Cull (kill) → An Audience
  (explore Castle Aldermere) → The King's Writ (talk: Thane Bruni,
  Gearford) → The Sunken Seal (kill: Crypt Wight, Sunken Crypt) →
  The Spire Key (explore: Storm Spire top) → **The Warden of Storms**
  (kill: Storm Warden) → reward: **the airship *Brasswing***.
- Side quests in every town (tank quest in Gearford among them).

### F5. Richer NPCs
- `dialogs` list per NPC (cycles per interaction), quest-aware greeting
  (turn-in text when a quest is ready), ~12 NPCs total: guards, a king,
  thane, archdruid, oasis matriarch, port masters, flavor townsfolk.

### F6. New monsters (biome spread)
Palette/behaviour variants of proven archetypes + new stats/drops:
snow wolf, dune scorpion (wolf frame), ember wisp, marsh wisp,
crypt wight (golem frame), storm elemental (wisp frame), hill brute
(golem), Storm Warden (boss). Level bands per continent.

## Protocol v4
- Appearance = 8 bytes (+race). Snapshot entities carry a vehicle byte.
- New ops: none beyond M3 (embark rides ItemOp.Use on the vehicle item).

## Acceptance
- [ ] Selftest green including: race stat modifiers, appearance v2
      round-trip, vehicle domain rules (canoe shallow-only, ship ocean,
      tank hills, airship anywhere, disembark rules), quest prereq gating,
      explore + talk quest lifecycles, world v2 validation (all towns/
      dungeons reachable — by vehicle domain where needed), determinism.
- [ ] Main chain completable in principle (selftest walks the chain
      through sim APIs).
- [ ] Bots pass; tick budget holds on the 512×512 world.
- [ ] Screenshots: world variety (second town or biome), vehicle at sea.

## Out of scope (M5+)
Multi-floor interiors, fast travel network, weather, naval combat,
vehicle-vs-vehicle, race-specific quest lines, mounts beyond vehicles.
