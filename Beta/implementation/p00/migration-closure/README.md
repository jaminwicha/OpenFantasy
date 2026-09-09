# Retained Aldermere source closure

This is an executed P00 dependency-preservation step, not a playable migration. The original 31 selected roots now resolve to **49 structured records, two code-defined node archetypes, 171 typed edges and 29 preserved asset files**. The packet retains 48 original source/asset/support files. The source inventory still contains **532 structured records, 10 indexed code-defined entries, 201 files and 187 documentation headings**; it has not been edited or relabeled complete.

The packet uses `openfantasy.aldermere:<family>/<legacy-id>` stable source keys. These allocate retained definition identities, not live actors, branch instances or gameplay implementations. The generic package contract may hash and retain this packet, but accepting its transport/identity structure does not close the migration's gameplay gates.

## Reproduce and inspect

From the repository root:

```sh
python3 Beta/implementation/p00/migration-closure/build_closure.py --check
python3 Beta/implementation/p00/migration-closure/test_closure.py
```

To regenerate after a deliberately reviewed source delta, run the builder without `--check`. Existing snapshot hashes reject unnoticed changes; do not revise the old inventory or selection merely to make an unexpected source change pass. The builder computes all bytes in memory before writing. It sorts identities, mappings, source dependencies and edges explicitly. No generation timestamp, filesystem order, hash-map iteration order or global random input enters the packet.

[The runtime packet](../../../Runtime/content/legacy/aldermere-first-loop/closure-manifest.json) includes:

- `records.json`: every selected/transitive structured payload unchanged, original pointer/inventory identity and canonical payload SHA-256; exact retained C# constructor text for the two node archetypes, plus explicitly labeled extracted fields.
- `references.json`: typed source-to-target edges. Duplicate identical edges are collapsed; source arrays and their order remain unchanged in the payload.
- `source-key-map.json`: one-to-one source-to-stable-key mappings, with empty transformation lists because no gameplay field transformations have occurred. The original code inventory has no unique ID column; code source IDs are explicit `path#node_archetype/<id>` composites of its original path/kind/legacy ID.
- `assets/`: original asset bytes, including complete appearance layers needed by the retained NPCs and additional selected calibration references.
- `source-support/`: exact original data and inspected code context. C# context is stored as `.cs.txt` and is not another compiled authority.
- `closure-manifest.json`: package/schema identity, root list, source inventory/selection hashes, source file fingerprints, output hashes, counts, declared reference rules and pending transformations.

## What closure means here

| Inspected source semantics | Closure treatment |
|---|---|
| Class ability list; NPC shops and quest lists | Resolve all entries transitively without shortening any list |
| Quest giver, prerequisites, monster/region/item/talk target, reward item | Resolve the typed family even if another family happens to contain the same ID; NPC–quest cycles terminate without deleting either side |
| Monster drops and recipe inputs/output | Resolve exact item definitions; preserve counts, chances and ordering |
| Node placement `type` | Resolve the exact `NodeArchetype` constructor in `World.Gathering.cs`, then its yield item and sheet |
| Vehicle item enum | Preserve source code semantics, resolve the relevant named terrain definitions and exact vehicle sheet; no assembly/custody conversion yet |
| NPC appearance | Retain complete palette/count manifest and exact body, hair, outfit, species and accessory layers selected by source indices. Inspected absent-index defaults affect only reference discovery; raw payloads remain unchanged. An out-of-range index is rejected instead of silently applying the source sanitizer |
| Monster identity | Resolve its original unit sheet through the inspected `EntityView`/spawn convention |
| Original selected Squire/resource/terrain assets | Preserve as calibration inputs; a class sheet does not mean the current source player bypasses its composite appearance |
| Literal prose, names, coordinates, rectangles, enums and balance values | Preserve verbatim values; these are not guessed string-ID references. Narrative meaning and geographical reachability remain separate review obligations |
| Whole source data/code files | Retain exact context; unselected records inside a copied source file are not promoted to migrated content merely by file presence |

Unknown fields (including nested object fields), unknown enum meanings, duplicate typed identities, missing or wrong-family definitions, unresolved node-constructor syntax, missing asset files and inventory/source drift stop generation. Forge recipes are currently outside the selected closure: adding one rejects until its source station/terrain witness is implemented. That explicit rejection is preferable to pretending an enum label alone establishes a working station. Likewise, if a future node yield introduces a new vehicle or node record after the code-semantic expansion pass, generation rejects until a further semantic pass is implemented; it cannot emit a closed packet missing that new record's terrain/asset dependencies. Current wood/berry yields do not require this extra pass.

This is a **closure of the enumerated, source-inspected reference semantics**. It is not a claim that arbitrary source code behavior or implicit references in prose have been statically understood. Code context preserves those remaining semantics for compatible transformation; it is not imported execution. No mutable game state, resource accounting, quest completion, reward grant, world generation, native animation or art acceptance occurs in this step.

## Executed checks and remaining decisions

Fifteen retained tests passed on this packet. They exercise exact root/payload preservation; actual added shop/quest dependencies; all emitted endpoints; missing target; wrong family; duplicate identity; unknown top-level and nested fields; unknown enums; cyclic quest/NPC references; eight deterministic ordering permutations; byte-identical rebuilding; exact file hashes/source bytes; actual source-byte drift rejection; exact code archetype extraction; and rejection of a late-discovered code-dependent yield before publishing a closed claim. The separate `--check` rebuilt all packet files in memory and matched the retained output byte for byte.

Next content/rules work must decide explicit source-to-current transforms for attributes, damage/range units, ATB/ticks, currency, stack/mass/volume, resource periodic respawn, stock/pricing, vehicle custody/embark behavior and appearance indexing. Retained world coordinates and map bytes do not prove a playable two-town route. Quest availability, changed/dead giver continuation, duplicate turn-in, interrupted work and accepted native presentation still require later executable scenarios. The rest of the full source inventory and its semantic/generative content remain mandatory migration scope.
