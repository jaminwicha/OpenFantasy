# OpenFantasy — Milestone 5: *Hearth & Homestead*

**Version:** 1.0 · **Date:** 2026-07-11 · **Builds on:** M4 (`fb7e7ad`)

## Theme

M4 built the map; M5 fills it with **people**. Towns become places you'd
believe in: enterable buildings, specialized shops, taverns that feel
alive, houses that show who's rich and who sleeps by the gate, farms
working the land outside the walls — and quests coming from ordinary folk,
not just kings and thanes.

## Feature Set

### F1. Enterable buildings (town generator v3)
- Buildings are hollow: wall shell, **plank floor** interior, a door gap
  facing the street. New tiles: plank floor, counter (bar/shop desks),
  fence, farmland.
- Per town: a **tavern** (largest), 2–3 **specialized shops**, houses by
  wealth — a **rich house** (large, stone-floored), **mid houses**, and
  **poor shacks** (small, dirt-floored). Towns grew to fit.
- Building layout, doors, and occupancy are exported to
  `data/world_meta.json` so the NPC generator can place people indoors.

### F2. Shop variety
Specialized keepers replace the single general store per town where it
makes sense: **weaponsmith**, **armorer**, **apothecary**, **general
goods**, **tools**, plus port masters (M4) and one shady **fence** in a
tavern who buys anything and sells odd goods cheap.

### F3. Living taverns
Every town's tavern has:
- a **bartender** — information dialogs (rumors that point at real
  content) and a quest;
- **patrons** — villagers who mill about;
- **visitors** — a rotating cast at the showcase tavern (Brasshollow's
  *Rusted Tankard*): a **mysterious hooded figure** (well-paying, no
  questions), a **rogue/fence**, a **solo adventurer**, and a
  **three-member adventuring party** with matching colors and dialog.

### F4. Villagers, wealth, and the street
- ~40 generated **villagers** across the four towns: named, dressed by
  the appearance system, with dialog pools and homes.
- **Wandering NPCs**: villagers and patrons amble deterministically
  within a home radius (server-side, walkable tiles only); shopkeepers
  and bartenders hold their posts.
- **Homeless** NPCs by the gates with their own voices and a small quest.

### F5. Farms (small / medium / large)
Fenced fields with farmland rows and a farmhouse outside towns:
- **Verdance**: large farm + small farm (the breadbasket)
- **Sylvas**: medium forest farm
- **Miradel**: small oasis farm
Farmers and farmhands live there and offer quests.

### F6. Quests from commoners (Fetch + Kill)
New quest kind **Fetch** — bring N of item X; turn-in consumes the goods.
~8 new quests: the bartender's rumor (slay), the hooded figure's ore
errand (fetch, pays too well), farm quests (wolf culls, fence-wood,
berry baskets), the homeless man's supper, the fence's pelts, the
adventuring party's brute hunt.

## Sim Rules
- NPC wander: same deterministic amble as monster idle (world RNG,
  stagger by id), leashed to home radius, never leaves walkable tiles,
  never blocks interaction (interact range unchanged).
- Fetch: objective met when `Count(item) ≥ n`; turn-in removes the items
  then pays rewards. Validator covers item refs and counts.
- Buildings are static tiles; occupancy is authoring-time data, not sim
  state. Wealth is presentation + flavor (no tax sim… yet).

## Acceptance
- [ ] Selftest green incl.: world v3 (tavern/shop/house/farm regions per
      town), fetch lifecycle (accept → gather → consume → reward),
      wander containment (radius + walkable over 1000 ticks),
      determinism with the full population active.
- [ ] NPC population ≥ 50 total; every town has tavern + ≥2 shops +
      ≥4 houses; farms present at the three specified sites.
- [ ] Bots green; tick budget holds with the larger population.
- [ ] Screenshots: tavern interior with patrons, a farm.

## Out of scope (M6+)
Day/night NPC schedules (home at night), building ownership/economy,
tavern minigames, safehouse/crime systems, NPC pathfinding beyond amble.
