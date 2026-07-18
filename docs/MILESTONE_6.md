# OpenFantasy — Milestone 6: *The Bestiary & The Hoard*

**Version:** 1.0 · **Date:** 2026-07-18 · **Builds on:** M5 (`4aa0b5a`)

## Theme

Pure content expansion on the systems already proven: roughly **double
everything**. No new mechanics — more of the world to fight, earn, craft,
meet, and do.

## Targets

| Axis | Before | After (target) |
|---|---|---|
| Monsters | 16 | **≥ 35** (biome bands filled, wilderness elites, dungeon minibosses) |
| Items | 29 | **≥ 65** (weapon/armor tiers to mythril & crystal, consumable tiers, trophy materials) |
| Abilities | 9 (3/class) | **12** (a 4th per class, unlocked at level 7) |
| Recipes | 9 | **≥ 22** (steel/mythril smithing, cooking, brewing) |
| Quests | 20 | **≥ 42** (side chains per town, POI explorations, hunts) |
| NPCs | 87 | **≥ 125** (gate guards, tavern visitors in every town, denser streets/farms) |
| Spawner regions | 17 | **≥ 32** (no empty biome) |
| Node regions | 8 | **≥ 14** |
| Wilderness POIs | 0 | **3** (ruined watchtower, standing stones, bandit camp) |

## Content Notes

- **Monsters**: variants ride the parameterized sprite generators
  (wisp/wolf/golem/bat/slime/scorpion frames + palettes). Every biome gets
  a low/mid/high band; each dungeon gains a miniboss; wilderness elites
  guard the POIs. Drops feed the new trophy materials.
- **Trophy materials** (fang, venom sac, ice shard, storm core, ancient
  coin…) sell well and feed recipes — the hunt-to-craft loop deepens.
- **Weapon flavor by class**: blades (Squire), rods (Mage), bows (Scout)
  at bronze → iron → steel → mythril → crystal tiers; armor tiers to match.
- **POIs**: small authored ruins in the wilderness with elite spawners and
  explore quests pointing at them — reasons to cross the empty quarters.
- **Quests**: each town gets a 2–3 step side chain from its named cast,
  plus hunts for the new elites and fetches for the new materials.

## Acceptance
- [ ] Selftest green, including new content-scale assertions (counts above)
      and full cross-reference validation (drops/recipes/quests/shops).
- [ ] Bots green; tick budget ≤ 2 ms with expanded spawns.
- [ ] Determinism holds with the expanded world.
