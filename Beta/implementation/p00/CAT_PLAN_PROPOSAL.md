# CAT-PLAN: proposed catalog targets for owner decision

Lead proposal for CURRENT_ROADMAP's CAT-PLAN, the P00 exit item asking for finite typed targets for all 12
families in the [production catalog register](production-catalog-register.json) before bulk content
production. **Nothing here is approved.** Every `shipping_definition_target_count` stays null until the owner
decides; null still means unknown, not zero. No content is cut: every retained record and required family
stays in scope, and a count is never permission to trim one.

## How to read the targets

Four typed units, never added together:

- **Identity:** a distinct being, place or object with continuity, such as a named person or region.
- **Definition:** one admitted rule or content record, such as an item, ability, recipe or species profile.
- **Native kit:** one art, animation or audio production job, such as a body plan's action set or a place's
  music stems.
- **Validated variant:** a combination actually exercised in tests or play, such as species with culture
  clothing on a body plan.

Each family gets two layers:

- **Floor:** derived only from the register's recorded legacy counts and required family sets. Every legacy
  record maps forward, and every required family has at least one complete, reachable definition set. Floors
  involve no judgment; approving them commits only to what the requirements already demand.
- **Recommended target:** lead judgment, shown with its basis. It sets depth: how many definitions per
  required family. These are the numbers the owner should accept, change or defer to the family's
  integration phase.

Procedural instances, such as generated residents, places or variants, are not counted. They are validated by
family, topology, history and boundary partitions under the register's coverage rules.

## Proposal

| ID | Family, phase | Floor | Recommended target | Basis |
|---|---|---|---|---|
| CAT-01 | Classes, abilities, skills, P05 | 3 classes; 12 abilities; 17 discipline definitions, one per required discipline | 3 classes; 21 class abilities, the 12 legacy plus 3 per class; 17 disciplines with 4 graded actions each, 68 actions; a closed table of at most 24 effect operations | RQ-144 asks for actual actions, learning, costs and outcomes per skill; four grades give novice-to-expert progression without a class explosion |
| CAT-02 | Bodies, species, forms, P11 | 35 legacy monster identities; 4 appearance races mapped; 15 named species families, one subspecies each; dragonkind and dragons as separate taxa | 30 subspecies, two per species family; 8 shared body plans that all species and monsters map onto | Body plans bound the art and animation cost in CAT-10; two subspecies per family is the smallest count that shows variation within a species |
| CAT-03 | People, institutions, cultures, P11 | 123 retained NPC identities with every dialogue, shop and quest link; 9 institution and culture families, one definition each | 123 retained plus 40 new authored people for arcs, mysteries and teaching; 6 authored cultures; 12 institutions across the required families | City-scale populations are procedural; authored people carry story and teaching roles, about one per authored adventurelet in CAT-08 |
| CAT-04 | Items, processes, materials, P11 | 63 items; 28 recipes; 3 node archetypes; 6 process families, one each | About 150 item definitions; about 100 process definitions; 12 source archetypes: wood, ore, berries, herbs, clay, stone, sand, fiber, fish, game, sap, crystal | Production chains need materials, intermediate parts and repair inputs; 12 sources feed every required process family |
| CAT-05 | Vehicles, mounts, care, P06 | 5 legacy vehicle identities kept distinct; 16 required entries, one definition each | 11 vehicle definitions with about 6 part definitions each, about 66 parts; 3 mount species; 4 pet species; 4 familiar forms | Parts carry crew, cargo, damage and repair consequences; mounts and companions need body and care profiles shared with CAT-02 |
| CAT-06 | Geology, terrain, fluids, ecology, P10 | 29 terrain kinds; 39 spawners classified as legacy respawn or ecology; 14 resource placements; 12 process families, one model each | About 44 material kinds; 3 liquids, water, lava and one more declared liquid; about 12 phase and reaction rules; 24 plant species with stages; 18 animal species, 8 herbivores, 6 predators and 4 intermediate | ECO-01 needs at least one complete food web; these counts allow distinct ecologies per biome without unbounded species tables |
| CAT-07 | Places, building, grammars, P11 | 25 retained regions; 4 towns; 32 building records; 4 farms; 5 safe zones; 4 crystals; 7 site recipes; 8 grammar families, one each | 25 retained plus 15 new authored places; 10 site grammars; 32 retained plus 20 new building types | Each grammar needs a cultural variant for at least one family; new places host the new arcs and the second-liquid and cave obligations |
| CAT-08 | Narratives, mysteries, teaching, P11 | 45 retained quests with source routes; 8 narrative families, one each | 45 retained; 40 authored adventurelets; 6 encounter decks; 3 contingent grand arcs; 12 mysteries including STORY-01; 20 teaching sequences | A 60–80-hour journey at roughly one adventurelet per one to two hours needs 40 to 60; 40 is the low end, measured later in play |
| CAT-09 | Planes, language, cosmology, lifecycle, P10 | 8 required families, one each | 4 realms: material, dream, ethereal and shadow; 3 portal types; 6 languages, one per culture, plus 2 cants; 6 deities with domains; lifecycle stages per CAT-02 species | Languages follow cultures one to one; realms cover every required plane family |
| CAT-10 | Native art and animation, P11 | 89 legacy asset files; 8 presets; 12 hair styles; 10 outfits; 4 accessories retained; 8 kit families, one job each | 8 body-plan kits, each with 4 directions and about 14 actions, about 112 animated actions; 10 biome tilesets; 6 cultural architecture sets | Actions per kit: walk, run, gather, craft, build, dig, attack, hit, incapacitated, talk, gesture, idle, rest, carry. Shared body plans keep species variety inside a finite job list |
| CAT-11 | Audio and acoustic intent, P11 | 9 required families, one each; every intent explicit, including silence | 8 place motifs with 5 contexts each, 40 stem sets; 12 ambience beds; about 60 action and material cues; about 30 creature voice sets | Contexts match the bound score states: exploration, settlement, danger, encounter, aftermath. Cue counts follow surfaces, tools and vehicles in CAT-04 and CAT-05 |
| CAT-12 | UI, accessibility, localization, packages, P11 | 11 required screen families, one each | About 16 screens; 1 shipping locale with complete string IDs; remap, controller, text scaling, captions and reduced motion; 1 package manifest format | Localization infrastructure is required; additional locales are a later owner decision, not a P11 obligation implied here |

## Capacity and staffing

No authoring throughput has been measured, so the proposal gives counts, not dates. Native art and audio are
unstaffed; CAT-10 and CAT-11 recommendations imply roughly 112 animated actions, 16 tile and architecture sets,
40 stem sets and about 100 cue sets for dedicated creators. The first bulk batch for each family should
record actual definitions per working day, review and rework, and those measurements should revise the
remaining targets. A count never closes a family; P11 requires every definition reachable, mapped and
presented with evidence.

## Decisions requested from the owner

1. **Approve the floors.** They are data-derived and commit only to existing requirements. Approval lets later
   phases plan against a finite minimum.
2. **For each family, choose:** accept the recommended target, set a different number, or defer the depth
   decision to that family's integration phase with the floor binding until then.
3. **Taxonomy:** confirm dragonkind and dragons as separate taxa, and whether giants are a species family or
   a size class across species.
4. **Locales:** confirm one shipping locale for P11.
5. **Staffing:** name or decline dedicated art and audio creators; their absence leaves CAT-10 and CAT-11 at
   the floor regardless of targets.

Approved numbers go into `shipping_definition_target_count` with a dated owner decision. Until then the
register's per-catalog `proposal` objects record this proposal with status `awaiting owner decision`.

## Documentation review

- **Plan/spec:** fulfills CAT-PLAN's lead proposal in CURRENT_ROADMAP and the P00 exit checklist's catalog
  item as a proposal only. P00 stays open: the owner decision and target recording remain. No requirement,
  phase or gate changes.
- **Reference:** no impact. The [content and composition](../../Runtime/docs/architecture/content-and-composition.md)
  reference already describes catalogs, procedural validation and coverage; proposed counts are dated
  planning and belong here.
- **Accuracy/completeness:** floors were computed from the register's `known_legacy_counts` and
  `required_family_set` fields as committed. Recommended targets are lead judgment, with each basis stated.
  STORY-01 is counted in CAT-08 mysteries. No audio legacy count is asserted, matching the register.
- **Checks:** `python3 Beta/tools/check_documentation.py --work-record Beta/implementation/p00/CAT_PLAN_PROPOSAL.md --files`
  with affected pages, JSON validation of the register, and `git diff --check`.
