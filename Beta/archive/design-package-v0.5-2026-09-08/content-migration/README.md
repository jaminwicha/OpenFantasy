# Complete OpenFantasy content migration

8 September 2026. **Owner requirement: all original OpenFantasy concepts and content must be preserved, ported, and incorporated into Beta.** This supersedes earlier wording that made original-game assimilation optional. RogueFiction remains a source for deeper selected contributions. Implementation reuse can remain selective; content preservation cannot.

The original deterministic, seeded simulation philosophy is also established direction. A new persistence/state contract should strengthen that foundation, retaining reusable pure simulation and generator algorithms with their tests. A replacement implementation is not permission to discard a class, story, NPC, asset, mechanic, or authored place.

## What is preserved now

The [inventory](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/content-migration/original-content-inventory-2026-09-08.json) and [source snapshot](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/content-migration/original-source-2026-09-08.zip) capture the current working tree, including staged milestone 8, without modifying the original files. The archive preserves exact bytes for 201 files across data, assets, documentation, simulation/client/network source, tools, scenes, and project configuration. Every archived file was checked against its recorded SHA-256 hash. This is preservation and migration planning; **nothing has yet been imported into a running Beta game**.

| Inventoried content | Count | Integration obligation |
|---|---:|---|
| Classes / abilities | 3 / 12 | Preserve Squire, Mage, Scout, their identities, all named abilities and progression roles |
| Monsters / items | 35 / 63 | Preserve every definition, appearance reference, habitat, drop, equipment/tool/vehicle function |
| Quests / recipes | 45 / 28 | Preserve every narrative, objective, prerequisite, reward, recipe input/output and station requirement |
| NPCs | 123 | Preserve named identities, dialogue, looks, social roles, shops and quest associations |
| Terrain kinds / authored regions | 29 / 25 | Preserve terrain semantics, named destinations and their meaningful relationships |
| Monster spawners / resource placements | 39 / 14 | Preserve encounter/resource distributions as legacy evidence and adapt into Beta ecology |
| Towns / building records / farms | 4 / 32 / 4 | Preserve distinct places and the lives, work and services associated with them |
| Safe zones / crystals | 5 / 4 | Preserve refuge, travel and recovery capabilities with explicit Beta rules |
| Hardcoded site recipes / resource archetypes | 7 / 3 | Move content out of hardcoded registries without losing a family or behavior |
| Asset files | 89 | Preserve source bytes and map every terrain, unit, vehicle, UI and appearance-layer asset into Beta |
| Appearance options | 4 races, 8 presets, 12 hair styles, 10 outfits, 4 accessory indices | Preserve customization meaning and visual identity through accepted-art replacements |

The manifest contains 532 structured data records, 10 separately indexed code-defined content entries, and 187 documentation headings with section hashes. Some records nest inside others; these counts are **not** 729 independently verified concepts. JSON pointers identify anonymous objects without inventing permanent game IDs. Raw JSON and prose remain intact in the snapshot. All migration target IDs and playable evidence are currently empty.

File coverage is complete for the declared snapshot scope. Semantic coverage still requires review of prose, code-defined behavior, generated families and superseded designs. The inventory deliberately labels this unfinished work instead of claiming an automatic file scan understands every concept. Build/import caches, Python bytecode, Git internals, Beta itself and external player saves are excluded. Future original-game changes require a new snapshot and a reconciled delta; this snapshot is not permission to stop tracking development.

A separate [source reference check](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/content-migration/source-reference-check.json) resolved 409 class/ability, monster/drop, recipe/item, NPC/shop/quest and quest/giver/target/prerequisite/reward references with zero missing targets. This checks the selected source-data relationships only; it does not prove every quest is playable or that any Beta import has succeeded.

## Two obligations for every migrated element

**Preserve the source and identity.** Keep original payload, asset bytes, provenance, legacy ID and source version. Allocate a namespaced Beta definition ID and maintain an explicit mapping. If several old records consolidate, retain aliases and distinct narrative/visual variations; if one splits, record all targets. Never silently map an unknown class or item to a default. No unresolved record can be marked complete.

**Make the element meaningful in play.** A definition in a database, an unreachable scene, a disconnected museum map, or a PNG in an archive does not satisfy incorporation. Record where the player meets it, how it participates in progression/economy/social life, which dependencies it needs, and a playable verification scenario. The faithful source is retained even when Beta receives expanded or rebalanced versions.

For assets, preserve every original file and map it to an engine-usable Beta representation. Keep the legacy appearance available as a reference/compatibility art set; artist-authored replacements can become the default after style acceptance. Repainting must not erase the identity of a monster, outfit, vehicle or place. Preserve original portraits, audio and other assets if discovered in subsequent source deltas; none should be invented as already existing. The archive includes all current assets, but archive preservation alone is not completed asset import.

For concepts that appeared only in plans, preserve their status accurately: **implemented**, **intended but not implemented**, **superseded**, or **conflicting and awaiting reconciliation**. All remain accounted for. Superseded engineering constraints need not be reintroduced as contradictory gameplay. For example, the early GDD excludes instanced dungeons while later milestones implement them. Preserve the seamless shared-world intent and later site content; do not require two mutually exclusive universal rules. Resolve creative conflicts explicitly without silently deleting their underlying concepts.

## Incorporation into Beta's fabric

Use an **Aldermere content package** with named people, cultures, places, plots, recipes, ecology, art mappings and generation constraints. This package is a first-party part of Beta's intended content coverage. It is not a second authoritative runtime.

Preserve the authored 512×512 map and its placements as a faithful reference and compatibility realization. In the main generated-world realization, retain Brasshollow, Gearford, Sylvanglade, Willowick, the castle, farms, ports and all dungeon identities as concrete places connected by travel and story dependencies. Their global coordinates can vary by seed; their existence and required relationships cannot be left to a chance that silently removes them. A generated world may place the Aldermere homeland within a broader geography rather than stamp the original rectangle into every world.

A paired realization is a proposed implementation strategy: one preserved authored-map realization for fidelity checks and one generated, fully integrated realization for Beta play. The no-loss requirement is established; the precise geography mechanism remains a technical design decision. A content profile being optional to load is not a reason to leave its import unfinished.

Retain the complete main quest graph, including the king's writ, regional travel, Storm Spire and the Storm Warden, culminating in Brasswing. Enrich its causes and consequences using the larger world: Aethertech extraction affects resources and communities, workshops need actual inputs, transport opens meaningful routes, and the final vehicle changes expedition possibilities. Preserve every side quest and ordinary person's voice. Farmers, tavern patrons, beggars, guards, craftspeople and royalty should all remain identifiable agents in the same society.

Keep all five vehicle capabilities—boat, canoe, ship, tank, airship—with their progression roles. The first slice can demonstrate one or two; staging their delivery does not delete the others. Preserve all class and ability identities while translating timing and balance through an explicit compatibility table if the final combat mode changes. Preserve quests' original objective/reward paths before adding systemic alternatives; depth should add solutions and consequences rather than erase the original authored route.

Carry frontier generation as a **generative capability**, including names, settlement layouts, resident roles, shops, rumors, bounties, site families, secrets, locks, chasms and pacing motifs. Infinite possible generated instances cannot be exhaustively enumerated. Preserve source/version identity and select deterministic fixtures spanning each family, topology style, biome and boundary case. Future import must materialize accepted generated quests and their bindings durably, fixing the original loss on fresh reconstruction.

## Migration record and sequence

Extend each inventory row with target package/ID, original and Beta behavior, field-level transforms, dependencies, art mapping, narrative role, placement rule, save migration needs, status, reviewer evidence and playable scenario. Namespaced IDs proposed for this package can use `openfantasy.original:<kind>/<legacy-id>`; do not confuse these immutable definition IDs with live entity or branch IDs.

1. **Freeze and reconcile the source.** Already captured initial bytes. Next, classify all 187 documentation sections, enumerate remaining code-only concepts and identify current-versus-planned behavior. Compare future source deltas before claiming completion.
2. **Prove the contracts on a connected sample.** Port one class/ability, NPC, quest, recipe, monster, item, location and representative art mapping together. Demonstrate quest completion, reward ownership, save/restart and deterministic replay. This validates the importer design before bulk conversion.
3. **Import all finite definitions and narrative.** Preserve full payloads, IDs, relationship graphs, descriptions and art references. Reject unresolved references with a repair report; do not truncate lists, skip unknown fields silently, or create empty fallback objects.
4. **Integrate place, progression and generative content.** Resolve quest reachability, required resources, travel domains and all site/generator families. Add required-landmark placement constraints and stable identities for generated residents and obligations.
5. **Recreate and validate presentation.** Artist agents own all legacy-to-accepted-style mappings and expressive motion. Engine builders verify display/import behavior. Retain originals and record the user's accepted style; the restrained v06 direction and five scenes are positively received, with repetition refinement and native/motion validation still required.
6. **Reconcile saved progress and cutover.** If existing player saves are brought forward, use an opt-in, non-destructive migration report for inventory, appearance, class, XP, quests, chest claims and known coordinates. Never overwrite source saves. No world mutations absent from the original save can be reconstructed honestly. Import them only from actual available evidence, and state any unknowns.

## Completion gates

| Gate | Required evidence |
|---|---|
| MIG-01 Source coverage | Every source file/hash and new delta accounted for; every semantic concept classified and mapped; no unreviewed section treated as complete |
| MIG-02 Data integrity | Every legacy definition resolves to Beta target(s); every field is preserved or has a documented transform; zero silent omissions |
| MIG-03 Dependency integrity | All class abilities, drops, recipes, shops, quest prerequisites/targets/rewards, sprites and destinations resolve; quest graphs have valid starts and outcomes |
| MIG-04 Playable incorporation | Every content row has a reachable use/encounter/story path in supported generated-world configurations; validate required seeds/profiles and generation constraints |
| MIG-05 Behavioral preservation | All original mechanics have a Beta equivalent or explicit reconciled evolution; original routes/choices remain represented; every planned concept remains tracked |
| MIG-06 Durability and determinism | Repeat ordered inputs from the same checkpoint/rules; restart midway through every quest kind, travel and crafting; no loss or duplicate rewards; generated bindings survive |
| MIG-07 Art completeness | Every original asset has a preserved source and an integrated mapping; replacement assets meet accepted style and native/motion checks |
| MIG-08 Narrative continuity | NPC identity, voice, motive, relationships and callbacks survive; additions remain causally consistent with actions and the branch |

The import is complete only when these gates pass. The current result is a verified preservation snapshot, a detailed inventory and this plan. The playable import is still unimplemented. Full migration adds a real content, art and verification obligation to Beta; it strengthens the case for a capable importer and authoring tools before further content expansion.
